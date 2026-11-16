using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Recursos de la vista 2.5D: carga de los modelos de los kits Kenney Tower Defense
    /// (Resources/KenneyTD) y KayKit Dungeon (Resources/KayKit), materiales con el sombreado
    /// propio "TD/Lambert25D" y primitivas de Unity.
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

        /// <summary>Prefijos de carpeta de cada kit dentro de Resources.</summary>
        public const string TD = "KenneyTD/";
        public const string KK = "KayKit/";

        /// <summary>
        /// Estilo sombrío y frío: paleta del kit editada (colormap_sombrio.png), colores de KayKit
        /// apagados con la misma fórmula, terreno nevado, luz azulada y neblina.
        /// En false se usan los colores originales de los kits.
        /// </summary>
        public static bool TemaSombrio = true;

        // Luz del tema (valores lineales). Luz directa azulada y ambiente frío y bajo.
        private static readonly Vector4 luzSombria = new Vector4(0.4f, 0.45f, 0.58f, 1f);
        private static readonly Vector4 ambienteSombrio = new Vector4(0.2f, 0.24f, 0.33f, 1f);
        private static readonly Vector4 luzClasica = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Vector4 ambienteClasico = new Vector4(0.5f, 0.5f, 0.5f, 1f);

        private static Shader shader;
        private static bool shaderBuscado;
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Material> materialesModelo = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> materialesColor = new Dictionary<string, Material>();
        private static readonly Dictionary<string, float> factorKit = new Dictionary<string, float>();
        private static Texture2D texColormap;
        private static bool colormapBuscado;
        private static MaterialPropertyBlock bloque;

        private static int idTinte = -1;

        // Paleta de los materiales del kit KayKit (no usa texturas: cada material es un color plano).
        // Valores tomados de los archivos del kit y pasados a espacio sRGB.
        private static readonly Dictionary<string, Color> coloresKayKit = new Dictionary<string, Color>
        {
            { "beige", new Color(0.856f, 0.684f, 0.492f) },
            { "black", new Color(0.151f, 0.197f, 0.213f) },
            { "blue", new Color(0.391f, 0.708f, 1f) },
            { "bluedark", new Color(0.397f, 0.54f, 0.671f) },
            { "bluelight", new Color(0.635f, 0.754f, 0.864f) },
            { "brown", new Color(0.784f, 0.52f, 0.374f) },
            { "browndark", new Color(0.607f, 0.352f, 0.27f) },
            { "glass", new Color(0.906f, 0.906f, 0.906f) },
            { "gold", new Color(0.733f, 0.613f, 0.359f) },
            { "green", new Color(0.283f, 0.736f, 0.552f) },
            { "greendark", new Color(0.158f, 0.544f, 0.553f) },
            { "metal", new Color(0.666f, 0.721f, 0.747f) },
            { "mud", new Color(0.593f, 0.562f, 0.526f) },
            { "pinklight", new Color(0.965f, 0.753f, 0.612f) },
            { "purple", new Color(0.547f, 0.253f, 0.786f) },
            { "purpledark", new Color(0.282f, 0.267f, 0.443f) },
            { "red", new Color(1f, 0.172f, 0.378f) },
            { "stone", new Color(0.476f, 0.515f, 0.533f) },
            { "stone2", new Color(0.351f, 0.378f, 0.393f) },
            { "stonedark", new Color(0.351f, 0.378f, 0.393f) },
            { "white", new Color(0.906f, 0.906f, 0.906f) },
            { "wooddark", new Color(0.429f, 0.376f, 0.359f) }
        };

        /// <summary>
        /// Fija las variables globales del sombreado: color de la luz, luz ambiente y neblina.
        /// La neblina oscurece lo que está lejos de la cámara (de <paramref name="inicio"/> a <paramref name="fin"/>).
        /// </summary>
        public static void AplicarAmbiente(Color colorNiebla, float inicio, float fin)
        {
            Shader.SetGlobalVector("_TD_Luz", TemaSombrio ? luzSombria : luzClasica);
            Shader.SetGlobalVector("_TD_Ambiente", TemaSombrio ? ambienteSombrio : ambienteClasico);
            Color n = colorNiebla;
            n.a = TemaSombrio ? 0.6f : 0f;
            Shader.SetGlobalColor("_TD_Niebla", n);
            Shader.SetGlobalVector("_TD_NieblaRango", new Vector4(inicio, Mathf.Max(inicio + 0.1f, fin), 0f, 0f));
        }

        /// <summary>
        /// Versión sombría y fría de un color sRGB: menos saturación, tono azulado y más oscuro.
        /// Es la misma fórmula con la que se generó colormap_sombrio.png a partir de la paleta de Kenney.
        /// </summary>
        public static Color Sombrio(Color c)
        {
            float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float r = Mathf.Lerp(l, c.r, 0.55f) * 0.86f * 0.82f;
            float g = Mathf.Lerp(l, c.g, 0.55f) * 0.94f * 0.82f;
            float b = Mathf.Lerp(l, c.b, 0.55f) * 1.08f * 0.82f;
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), c.a);
        }

        /// <summary>Variante nevada de una pieza del kit Kenney ("tile" → "snow-tile") si el tema sombrío está activo y existe.</summary>
        public static string Nieve(string nombre)
        {
            if (!TemaSombrio) return TD + nombre;
            string nevado = TD + "snow-" + nombre;
            return Resources.Load<GameObject>(nevado) != null ? nevado : TD + nombre;
        }

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
                    // Valores por defecto de las globales (la neblina la ajusta Vista25D al encuadrar)
                    AplicarAmbiente(Vista25D.COLOR_FONDO, 1000f, 1001f);
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

        /// <summary>Textura de colores del kit Kenney Tower Defense (todas sus piezas la comparten).</summary>
        private static Texture2D Colormap()
        {
            if (colormapBuscado) return texColormap;
            colormapBuscado = true;
            if (TemaSombrio) texColormap = Resources.Load<Texture2D>(TD + "Textures/colormap_sombrio");
            if (texColormap == null) texColormap = Resources.Load<Texture2D>(TD + "Textures/colormap");
            if (texColormap != null)
            {
                texColormap.filterMode = FilterMode.Point;      // cada pieza toma colores planos de la paleta
                texColormap.wrapMode = TextureWrapMode.Clamp;
            }
            return texColormap;
        }

        /// <summary>Nombre del material sin sufijos de Unity ("Stone (Instance)", "colormap.001").</summary>
        private static string NombreBase(Material m)
        {
            if (m == null) return "default";
            string n = m.name.Replace(" (Instance)", "").Trim();
            int i = n.IndexOfAny(new[] { ' ', '.' });
            return (i > 0 ? n.Substring(0, i) : n).ToLowerInvariant();
        }

        /// <summary>Convierte el material importado del FBX al sombreado propio conservando su aspecto.</summary>
        private static Material MaterialDeModelo(Material original)
        {
            string clave = NombreBase(original);
            if (materialesModelo.TryGetValue(clave, out Material m) && m != null) return m;

            Texture tex = null;
            Color color = Color.white;
            if (clave == "colormap")
            {
                tex = Colormap();
            }
            else if (coloresKayKit.TryGetValue(clave, out Color c))
            {
                color = TemaSombrio ? Sombrio(c) : c;
            }

            // Respaldo: lo que traiga el material importado
            if (tex == null && clave == "colormap" && original != null)
            {
                if (original.HasProperty("_BaseMap")) tex = original.GetTexture("_BaseMap");
                if (tex == null && original.HasProperty("_MainTex")) tex = original.GetTexture("_MainTex");
            }
            if (clave != "colormap" && !coloresKayKit.ContainsKey(clave) && original != null)
            {
                if (original.HasProperty("_BaseColor")) color = original.GetColor("_BaseColor");
                else if (original.HasProperty("_Color")) color = original.GetColor("_Color");
                if (original.HasProperty("_BaseMap")) tex = original.GetTexture("_BaseMap");
                if (tex == null && original.HasProperty("_MainTex")) tex = original.GetTexture("_MainTex");
            }

            // Doble cara como en los archivos originales de los kits (el sombreado corrige la normal de la cara trasera)
            m = NuevoMaterial(tex != null ? tex : Texture2D.whiteTexture, color, Color.clear, false, 0f, true);
            m.name = "TD_Modelo_" + clave;
            materialesModelo[clave] = m;
            return m;
        }

        #endregion

        #region Modelos y Primitivas

        /// <summary>Carga un modelo por su ruta dentro de Resources (p. ej. "KenneyTD/tile").</summary>
        public static GameObject CargarPrefab(string ruta)
        {
            if (prefabs.TryGetValue(ruta, out GameObject p)) return p;
            p = Resources.Load<GameObject>(ruta);
            if (p == null) Debug.LogWarning($"[Vista 2.5D] No se encontró el modelo Resources/{ruta}; se usará una figura simple.");
            prefabs[ruta] = p;
            return p;
        }

        /// <summary>True si el modelo existe en Resources.</summary>
        public static bool Existe(string ruta) => CargarPrefab(ruta) != null;

        /// <summary>Límites de todas las mallas de <paramref name="objeto"/> expresados en el espacio local de <paramref name="espacio"/>.</summary>
        public static bool Limites(Transform objeto, Transform espacio, out Bounds b)
        {
            Matrix4x4 aLocal = espacio.worldToLocalMatrix;
            bool hay = false;
            b = new Bounds();
            MeshFilter[] filtros = objeto.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filtros.Length; i++)
            {
                if (filtros[i].sharedMesh == null) continue;
                Bounds mb = filtros[i].sharedMesh.bounds;
                Matrix4x4 m = aLocal * filtros[i].transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 esquina = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 q = m.MultiplyPoint3x4(esquina);
                    if (!hay) { b = new Bounds(q, Vector3.zero); hay = true; }
                    else b.Encapsulate(q);
                }
            }
            return hay;
        }

        /// <summary>
        /// Corrige la escala de importación de cada kit (por si el FBX llega en centímetros):
        /// mide una pieza de referencia de tamaño conocido y redondea a la potencia de 10 más cercana.
        /// </summary>
        private static float FactorKit(string ruta)
        {
            string kit = ruta.StartsWith(TD) ? TD : (ruta.StartsWith(KK) ? KK : "");
            if (kit.Length == 0) return 1f;
            if (factorKit.TryGetValue(kit, out float f)) return f;

            f = 1f;
            string referencia = kit == TD ? TD + "tile" : KK + "barrel";
            float esperado = 1f; // la casilla de Kenney mide 1 de ancho; el barril de KayKit, 1 de alto
            GameObject prefab = CargarPrefab(referencia);
            if (prefab != null)
            {
                // Se mide igual que en CrearModelo: dentro de un contenedor neutro en el origen
                GameObject tmp = new GameObject("MedidaKit");
                Object.Instantiate(prefab, tmp.transform, false);
                if (Limites(tmp.transform, tmp.transform, out Bounds b))
                {
                    float medido = kit == TD ? b.size.x : b.size.y;
                    if (medido > 0.00001f)
                    {
                        float lg = Mathf.Log10(esperado / medido);
                        if (Mathf.Abs(lg) > 0.5f) f = Mathf.Pow(10f, Mathf.Round(lg));
                    }
                }
                Object.Destroy(tmp);
            }
            if (!Mathf.Approximately(f, 1f)) Debug.Log($"[Vista 2.5D] Escala de importación corregida para {kit}: x{f}");
            factorKit[kit] = f;
            return f;
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
        /// Instancia un modelo de un kit bajo una raíz de espacio de modelo. La base del modelo queda
        /// en y = 0 y su centro en x/z = 0, sin importar el pivote o la escala de importación del FBX.
        /// <paramref name="ruta"/> incluye la carpeta del kit (Recursos25D.TD + "tile").
        /// Devuelve null si el modelo no existe.
        /// </summary>
        public static Transform CrearModelo(string ruta, Transform padre, Vector3 posicion, Vector3 escala, float giroY = 0f)
        {
            GameObject prefab = CargarPrefab(ruta);
            if (prefab == null) return null;
            float factor = FactorKit(ruta);

            GameObject cont = new GameObject(ruta.Substring(ruta.LastIndexOf('/') + 1));
            cont.transform.SetParent(padre, false);
            cont.transform.localPosition = posicion;
            cont.transform.localRotation = Quaternion.Euler(0f, giroY, 0f);
            cont.transform.localScale = escala;

            GameObject inst = Object.Instantiate(prefab, cont.transform, false);
            if (!Mathf.Approximately(factor, 1f)) inst.transform.localScale *= factor;

            // Centrar y apoyar sobre el suelo usando los límites en el espacio del contenedor
            if (Limites(inst.transform, cont.transform, out Bounds b))
                inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

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
            Animator[] anims = inst.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++) Object.Destroy(anims[i]);
            return cont.transform;
        }

        private static readonly Dictionary<string, Material> materialesTransparentes = new Dictionary<string, Material>();

        /// <summary>Vuelve semitransparente un modelo (p. ej. el enemigo furtivo) con copias cacheadas de sus materiales.</summary>
        public static void HacerTransparente(Transform modelo, float alfa)
        {
            Renderer[] rs = modelo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Material[] mats = rs[i].sharedMaterials;
                for (int k = 0; k < mats.Length; k++)
                {
                    if (mats[k] == null) continue;
                    string clave = mats[k].name + "_" + alfa.ToString("0.00");
                    if (!materialesTransparentes.TryGetValue(clave, out Material t) || t == null)
                    {
                        t = new Material(mats[k]) { name = clave };
                        Color c = t.HasProperty("_Color") ? t.GetColor("_Color") : Color.white;
                        c.a = alfa;
                        t.SetColor("_Color", c);
                        t.SetColor("_BaseColor", c);
                        t.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        t.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        t.SetFloat("_ZWrite", 0f);
                        t.renderQueue = (int)RenderQueue.Transparent;
                        materialesTransparentes[clave] = t;
                    }
                    mats[k] = t;
                }
                rs[i].sharedMaterials = mats;
            }
        }

        /// <summary>Busca el primer descendiente cuyo nombre contenga todos los fragmentos (sin distinguir mayúsculas).</summary>
        public static Transform BuscarHijo(Transform raiz, params string[] fragmentos)
        {
            if (raiz == null) return null;
            Transform[] todos = raiz.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < todos.Length; i++)
            {
                string n = todos[i].name.ToLowerInvariant();
                bool ok = true;
                for (int k = 0; ok && k < fragmentos.Length; k++) ok = n.Contains(fragmentos[k].ToLowerInvariant());
                if (ok && todos[i] != raiz) return todos[i];
            }
            return null;
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
