using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Recursos de la vista 2.5D: carga de los modelos del kit Kenney (Resources/Kenney),
    /// materiales con el sombreado propio "TD/Lambert25D" y primitivas de Unity.
    ///
    /// Convención de ejes: la lógica del juego vive en el plano XY (z = 0) y la ALTURA
    /// de los objetos 3D crece hacia -Z (hacia la cámara). Los modelos se construyen en
    /// "espacio de modelo" (Y arriba, Z adelante) bajo una raíz rotada con RotModelo:
    ///     modelo (x, y, z)  →  mundo (x, z, -y)
    /// Así un punto del suelo (x, y) del juego equivale al punto de modelo (x, 0, y).
    /// </summary>
    public static class Recursos25D
    {
        public static readonly Quaternion RotModelo = Quaternion.Euler(-90f, 0f, 0f);

        private static Shader shader;
        private static bool shaderBuscado;
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> materialesModelo = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> materialesColor = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> texturas = new Dictionary<string, Texture2D>();
        private static MaterialPropertyBlock bloque;

        private static int idTinte = -1;

        // Material del modelo FBX → textura del kit (misma correspondencia que los GLB de Kenney)
        private static readonly Dictionary<string, string> texturaDeMaterial = new Dictionary<string, string>
        {
            { "bricks", "cobblestoneAlternative" },
            { "stones", "cobblestone" },
            { "stonesPainted", "cobblestonePainted" },
            { "planks", "planks" },
            { "barrel", "barrel" },
            { "details", "details" },
            { "fence", "fence" },
            { "roof", "roof" },
            { "tree", "tree" },
            { "water", "water" }
        };

        /// <summary>Convierte un punto del suelo del juego a coordenadas de espacio de modelo.</summary>
        public static Vector3 AModelo(Vector2 suelo, float altura = 0f) => new Vector3(suelo.x, altura, suelo.y);

        #region Sombreado y Materiales

        public static Shader Sombreado
        {
            get
            {
                if (!shaderBuscado)
                {
                    shaderBuscado = true;
                    shader = Shader.Find("TD/Lambert25D");
                    if (shader == null) shader = Resources.Load<Shader>("Shaders/TDLambert25D");
                    if (shader == null || !shader.isSupported)
                    {
                        Debug.LogWarning("[Vista 2.5D] No se pudo usar el shader TD/Lambert25D; se usa URP Unlit (sin sombreado).");
                        shader = Shader.Find("Universal Render Pipeline/Unlit");
                    }
                    if (shader == null) shader = Shader.Find("Unlit/Texture");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                }
                return shader;
            }
        }

        private static Material NuevoMaterial(Texture tex, Color color, Color emision, bool transparente, float corte, bool dosCaras)
        {
            Material m = new Material(Sombreado);
            if (tex != null)
            {
                m.SetTexture("_MainTex", tex);
                m.SetTexture("_BaseMap", tex); // por si se usa el respaldo URP Unlit
            }
            m.SetColor("_Color", color);
            m.SetColor("_BaseColor", color);
            m.SetColor("_Emision", emision);
            m.SetColor("_Tinte", new Color(0f, 0f, 0f, 0f));
            m.SetFloat("_Corte", corte);
            m.SetFloat("_Cull", dosCaras ? (float)CullMode.Off : (float)CullMode.Back);

            if (transparente)
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.renderQueue = (int)RenderQueue.Geometry;
            }
            return m;
        }

        /// <summary>Material de color plano (cacheado) para primitivas.</summary>
        public static Material MaterialColor(Color color, Color emision = default, bool transparente = false)
        {
            string clave = $"{ColorUtility.ToHtmlStringRGBA(color)}_{ColorUtility.ToHtmlStringRGBA(emision)}_{transparente}";
            if (materialesColor.TryGetValue(clave, out Material m) && m != null) return m;
            m = NuevoMaterial(Texture2D.whiteTexture, color, emision, transparente, 0f, false);
            m.name = "TD_Color_" + clave;
            materialesColor[clave] = m;
            return m;
        }

        private static Texture2D TexturaKit(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            if (texturas.TryGetValue(nombre, out Texture2D t)) return t;
            t = Resources.Load<Texture2D>("Kenney/Textures/" + nombre);
            if (t != null)
            {
                t.filterMode = FilterMode.Point;        // estética retro pixelada del kit
                t.wrapMode = TextureWrapMode.Repeat;    // los modelos del kit repiten la textura (UV fuera de 0..1)
            }
            texturas[nombre] = t;
            return t;
        }

        private static string NombreBase(Material m)
        {
            if (m == null) return "default";
            string n = m.name.Replace(" (Instance)", "").Trim();
            int i = n.IndexOf(' ');
            return i > 0 ? n.Substring(0, i) : n;
        }

        /// <summary>Convierte el material importado del FBX al sombreado propio conservando su textura.</summary>
        private static Material MaterialDeModelo(Material original)
        {
            string clave = NombreBase(original);
            if (materialesModelo.TryGetValue(clave, out Material m) && m != null) return m;

            Texture tex = null;
            texturaDeMaterial.TryGetValue(clave, out string nombreTex);
            tex = TexturaKit(nombreTex);
            if (tex == null && original != null)
            {
                if (original.HasProperty("_BaseMap")) tex = original.GetTexture("_BaseMap");
                if (tex == null && original.HasProperty("_MainTex")) tex = original.GetTexture("_MainTex");
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Repeat;
                }
            }

            bool planta = clave == "tree";
            bool lamina = planta || clave == "fence";
            m = NuevoMaterial(tex != null ? tex : Texture2D.whiteTexture, Color.white, Color.clear, false, planta ? 0.5f : 0f, lamina);
            m.name = "TD_Kenney_" + clave;
            materialesModelo[clave] = m;
            return m;
        }

        #endregion

        #region Modelos y Primitivas

        public static GameObject CargarPrefab(string nombre)
        {
            if (prefabs.TryGetValue(nombre, out GameObject p)) return p;
            p = Resources.Load<GameObject>("Kenney/" + nombre);
            if (p == null) Debug.LogWarning($"[Vista 2.5D] No se encontró el modelo Resources/Kenney/{nombre}; se usará una figura simple.");
            prefabs[nombre] = p;
            return p;
        }

        /// <summary>Crea una raíz en espacio de modelo (rotada RotModelo) con escala de mundo 1.</summary>
        public static Transform CrearRaizModelo(Transform padre, string nombre)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.rotation = RotModelo;
            if (padre != null)
            {
                float s = padre.lossyScale.x;
                go.transform.localScale = Mathf.Abs(s) > 0.0001f ? Vector3.one / s : Vector3.one;
            }
            return go.transform;
        }

        /// <summary>
        /// Instancia un modelo del kit bajo una raíz de espacio de modelo. La base del modelo queda
        /// en y = 0 y su centro en x/z = 0, sin importar el pivote o la escala de importación del FBX.
        /// Devuelve null si el modelo no existe.
        /// </summary>
        public static Transform CrearModelo(string nombre, Transform padre, Vector3 posicion, Vector3 escala, float giroY = 0f)
        {
            GameObject prefab = CargarPrefab(nombre);
            if (prefab == null) return null;

            GameObject cont = new GameObject(nombre);
            cont.transform.SetParent(padre, false);
            cont.transform.localPosition = posicion;
            cont.transform.localRotation = Quaternion.Euler(0f, giroY, 0f);
            cont.transform.localScale = escala;

            GameObject inst = Object.Instantiate(prefab, cont.transform, false);

            // Límites en el espacio local del contenedor para centrar y apoyar sobre el suelo
            Matrix4x4 aLocal = cont.transform.worldToLocalMatrix;
            bool hay = false;
            Bounds b = new Bounds();
            MeshFilter[] filtros = inst.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filtros.Length; i++)
            {
                if (filtros[i].sharedMesh == null) continue;
                Bounds mb = filtros[i].sharedMesh.bounds;
                Matrix4x4 m = aLocal * filtros[i].transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 esquina = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(esquina);
                    if (!hay) { b = new Bounds(p, Vector3.zero); hay = true; }
                    else b.Encapsulate(p);
                }
            }
            if (hay) inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

            Renderer[] rs = inst.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Material[] orig = rs[i].sharedMaterials;
                Material[] nuevos = new Material[orig.Length];
                for (int k = 0; k < orig.Length; k++) nuevos[k] = MaterialDeModelo(orig[k]);
                rs[i].sharedMaterials = nuevos;
                Preparar(rs[i]);
            }

            Collider[] cols = inst.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) Object.Destroy(cols[i]);
            return cont.transform;
        }

        /// <summary>Primitiva de Unity sin colisionador, con material propio, en espacio de modelo.</summary>
        public static Transform Primitiva(PrimitiveType tipo, Transform padre, Vector3 posicion, Vector3 escala, Material mat, Vector3 rotacion = default)
        {
            GameObject go = GameObject.CreatePrimitive(tipo);
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicion;
            go.transform.localRotation = Quaternion.Euler(rotacion);
            go.transform.localScale = escala;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            Preparar(r);
            return go.transform;
        }

        /// <summary>Modelo del kit o, si no existe, un cilindro de piedra de tamaño equivalente.</summary>
        public static Transform ModeloOCilindro(string nombre, Transform padre, Vector3 pos, Vector3 escala, float ancho, float alto, Color colorRespaldo)
        {
            Transform t = CrearModelo(nombre, padre, pos, escala);
            if (t != null) return t;
            return Primitiva(PrimitiveType.Cylinder, padre, pos + new Vector3(0f, alto * 0.5f, 0f),
                new Vector3(ancho, alto * 0.5f, ancho), MaterialColor(colorRespaldo));
        }

        private static void Preparar(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingOrder = 1;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Aplica un tinte (alfa = intensidad) sin crear materiales nuevos.</summary>
        public static void Tenir(Renderer[] renderers, Color tinte)
        {
            if (renderers == null) return;
            if (bloque == null) bloque = new MaterialPropertyBlock();
            if (idTinte < 0) idTinte = Shader.PropertyToID("_Tinte");
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(bloque);
                bloque.SetColor(idTinte, tinte);
                renderers[i].SetPropertyBlock(bloque);
            }
        }

        #endregion
    }
}
