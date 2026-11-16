using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Escenario 3D de la vista 2.5D construido con el kit Kenney Retro Fantasy:
    /// baldosas a lo largo de las aristas del grafo de caminos (piedra = ruta principal,
    /// madera = sendero, agua = pantano), barricadas sobre aristas bloqueadas, castillo aliado
    /// en la base, puerta-portal en el punto de aparición y decoración en las zonas libres.
    /// </summary>
    public class Escenario25D : MonoBehaviour
    {
        public static Escenario25D Instance { get; private set; }

        private const float ESCALA_BALDOSA = 0.8f;
        private const float PASO_BALDOSA = 0.72f;

        private Transform contenido;   // se oculta en la vista 2D
        private Transform raizModelo;  // espacio de modelo (RotModelo)
        private Transform capaCaminos, capaBarricadas, capaDecor, castillo, portal;
        private Transform discoPortal, nucleo;
        private Renderer[] rNucleo;
        private Renderer rDomo;
        private LineRenderer anilloVida;

        private PathGraph suscrito;
        private int versionVista = -1;
        private int firmaDecor = -1;
        private bool caminosPendientes = true;

        public static Escenario25D Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("Escenario25D");
            return go.AddComponent<Escenario25D>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            contenido = new GameObject("Contenido3D").transform;
            contenido.SetParent(transform, false);
            raizModelo = Recursos25D.CrearRaizModelo(contenido, "EspacioModelo");
            capaCaminos = Hijo("Caminos");
            capaBarricadas = Hijo("Barricadas");
            capaDecor = Hijo("Decoracion");
            castillo = Hijo("Castillo");
            portal = Hijo("Portal");

            anilloVida = GraficosProc.CrearLinea(contenido, "AnilloVidaBase", Paleta.Positivo, Paleta.Positivo, 0.07f, 4);
            ConstruirCastillo();
            ConstruirPortal();
        }

        private Transform Hijo(string nombre)
        {
            Transform t = new GameObject(nombre).transform;
            t.SetParent(raizModelo, false);
            return t;
        }

        private void OnDestroy()
        {
            if (suscrito != null) suscrito.OnGrafoCambiado -= MarcarCaminos;
            if (Instance == this) Instance = null;
        }

        private void MarcarCaminos() => caminosPendientes = true;

        private void Update()
        {
            PathGraph g = PathGraph.main;
            if (g != suscrito)
            {
                if (suscrito != null) suscrito.OnGrafoCambiado -= MarcarCaminos;
                suscrito = g;
                if (g != null) g.OnGrafoCambiado += MarcarCaminos;
                caminosPendientes = true;
            }

            if (versionVista != Vista25D.Version)
            {
                versionVista = Vista25D.Version;
                contenido.gameObject.SetActive(Vista25D.Activa);
                caminosPendientes = true; // el encuadre cambió: recolocar el portal
            }

            if (!Vista25D.Activa) return;

            if (caminosPendientes && g != null && GameManager.Instance != null && g.nodos.Count > 1)
            {
                caminosPendientes = false;
                ReconstruirCaminos(g);
                ColocarCastilloYPortal();
                int firma = g.TopologiaActual * 1000 + g.nodos.Count;
                if (firma != firmaDecor)
                {
                    firmaDecor = firma;
                    ReconstruirDecoracion(g);
                }
            }

            Animar();
        }

        #region Caminos y Barricadas

        private static void Vaciar(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        private struct Segmento
        {
            public Vector2 a, b;
            public float mult;
            public bool bloqueado;
        }

        private static List<Segmento> Segmentos(PathGraph g)
        {
            List<Segmento> lista = new List<Segmento>();
            List<KeyValuePair<PathNode, PathNode>> aristas = g.ObtenerAristas();
            for (int i = 0; i < aristas.Count; i++)
            {
                lista.Add(new Segmento
                {
                    a = aristas[i].Key.transform.position,
                    b = aristas[i].Value.transform.position,
                    mult = aristas[i].Key.MultiplicadorHacia(aristas[i].Value)
                });
            }
            for (int i = 0; i < g.bloqueos.Count; i++)
            {
                PathGraph.Bloqueo bl = g.bloqueos[i];
                if (bl.a == null || bl.b == null) continue;
                lista.Add(new Segmento { a = bl.a.transform.position, b = bl.b.transform.position, mult = bl.multiplicador, bloqueado = true });
            }
            return lista;
        }

        private void ReconstruirCaminos(PathGraph g)
        {
            Vaciar(capaCaminos);
            Vaciar(capaBarricadas);
            List<Segmento> segs = Segmentos(g);
            List<Vector2> colocadas = new List<Vector2>();

            // Primero la ruta principal para que tenga prioridad en los cruces
            segs.Sort((x, y) => x.mult.CompareTo(y.mult));

            for (int s = 0; s < segs.Count; s++)
            {
                Segmento seg = segs[s];
                TipoTerreno terreno = PathNode.TerrenoDeMultiplicador(seg.mult);
                float largo = Vector2.Distance(seg.a, seg.b);
                int pasos = Mathf.Max(1, Mathf.CeilToInt(largo / PASO_BALDOSA));

                for (int k = 0; k <= pasos; k++)
                {
                    Vector2 p = Vector2.Lerp(seg.a, seg.b, k / (float)pasos);
                    bool repetida = false;
                    for (int c = 0; c < colocadas.Count; c++)
                    {
                        if ((colocadas[c] - p).sqrMagnitude < 0.45f * 0.45f) { repetida = true; break; }
                    }
                    if (repetida) continue;
                    colocadas.Add(p);
                    CrearBaldosa(terreno, p);
                }

                if (seg.bloqueado) CrearBarricada(seg.a, seg.b);
            }
        }

        // Tinte terroso para que la ruta principal no se confunda con las plataformas de construcción
        private static readonly Color tinteCamino = new Color(0.36f, 0.25f, 0.14f, 0.45f);

        private void CrearBaldosa(TipoTerreno terreno, Vector2 p)
        {
            Vector3 pos = Recursos25D.AModelo(p);
            float e = ESCALA_BALDOSA;
            Transform t;
            switch (terreno)
            {
                case TipoTerreno.Sendero:
                    // wood-floor mide 0.12 de alto: se aplana para que coincida con ALTURA_CAMINO
                    if (Recursos25D.CrearModelo("wood-floor", capaCaminos, pos, new Vector3(e, Vista25D.ALTURA_CAMINO / 0.12f, e)) == null)
                        Recursos25D.Primitiva(PrimitiveType.Cube, capaCaminos, pos + Vector3.up * 0.02f, new Vector3(e, 0.04f, e), Recursos25D.MaterialColor(new Color(0.55f, 0.4f, 0.25f)));
                    break;
                case TipoTerreno.Pantano:
                    if (Recursos25D.CrearModelo("water", capaCaminos, pos, new Vector3(e, Vista25D.ALTURA_CAMINO / 0.2f, e)) == null)
                        Recursos25D.Primitiva(PrimitiveType.Cube, capaCaminos, pos + Vector3.up * 0.02f, new Vector3(e, 0.04f, e), Recursos25D.MaterialColor(new Color(0.25f, 0.5f, 0.45f)));
                    break;
                default:
                    t = Recursos25D.CrearModelo("floor", capaCaminos, pos, new Vector3(e, Vista25D.ALTURA_CAMINO / 0.05f, e));
                    if (t == null)
                        t = Recursos25D.Primitiva(PrimitiveType.Cube, capaCaminos, pos + Vector3.up * 0.02f, new Vector3(e, 0.04f, e), Recursos25D.MaterialColor(new Color(0.42f, 0.33f, 0.24f)));
                    Recursos25D.Tenir(t.GetComponentsInChildren<Renderer>(true), tinteCamino);
                    break;
            }
        }

        /// <summary>Valla de madera atravesada en el tramo bloqueado por la habilidad Barricada.</summary>
        private void CrearBarricada(Vector2 a, Vector2 b)
        {
            Vector2 medio = (a + b) * 0.5f;
            Vector2 dir = (b - a).normalized;
            // Rotar el eje X del modelo hasta la perpendicular del tramo
            float giro = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;
            Vector3 pos = Recursos25D.AModelo(medio, Vista25D.ALTURA_CAMINO);
            Vector3 lateral = Recursos25D.AModelo(dir) * 0.18f;

            Transform v1 = Recursos25D.CrearModelo("fence-wood", capaBarricadas, pos - lateral, new Vector3(0.9f, 1.8f, 1f), giro);
            Transform v2 = Recursos25D.CrearModelo("fence-wood", capaBarricadas, pos + lateral, new Vector3(0.9f, 1.8f, 1f), giro);
            if (v1 == null && v2 == null)
            {
                Recursos25D.Primitiva(PrimitiveType.Cube, capaBarricadas, pos + Vector3.up * 0.2f, new Vector3(0.85f, 0.35f, 0.12f),
                    Recursos25D.MaterialColor(new Color(0.55f, 0.35f, 0.18f)), new Vector3(0f, giro, 0f));
            }
            Recursos25D.CrearModelo("detail-crate", capaBarricadas, pos + Recursos25D.AModelo(new Vector2(-dir.y, dir.x)) * 0.42f, Vector3.one * 1.1f, giro + 20f);
        }

        #endregion

        #region Castillo (base aliada) y Portal

        private void ConstruirCastillo()
        {
            Color piedra = new Color(0.55f, 0.55f, 0.58f);
            Recursos25D.ModeloOCilindro("tower-paint-base", castillo, Vector3.zero, new Vector3(0.78f, 0.95f, 0.78f), 0.8f, 0.95f, piedra);
            Recursos25D.CrearModelo("tower-top", castillo, new Vector3(0f, 0.95f, 0f), Vector3.one * 0.78f);

            // Torres laterales detrás del torreón (detrás = +Z en modelo = más lejos de la cámara)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(0.58f * s, 0f, 0.42f);
                Recursos25D.ModeloOCilindro("tower-base", castillo, p, new Vector3(0.42f, 0.7f, 0.42f), 0.45f, 0.7f, piedra);
                Recursos25D.CrearModelo("tower-edge", castillo, p + new Vector3(0f, 0.7f, 0f), Vector3.one * 0.42f);
            }

            Material mNucleo = Recursos25D.MaterialColor(new Color(0.3f, 0.85f, 1f), new Color(0.2f, 0.6f, 0.8f));
            nucleo = Recursos25D.Primitiva(PrimitiveType.Sphere, castillo, new Vector3(0f, 1.38f, 0f), Vector3.one * 0.26f, mNucleo);
            rNucleo = nucleo.GetComponentsInChildren<Renderer>();

            Material mDomo = Recursos25D.MaterialColor(new Color(0.3f, 0.9f, 1f, 0.16f), new Color(0.05f, 0.2f, 0.3f, 0f), true);
            rDomo = Recursos25D.Primitiva(PrimitiveType.Sphere, castillo, new Vector3(0f, 0.55f, 0.1f), Vector3.one * 2.1f, mDomo).GetComponent<Renderer>();
            rDomo.sortingOrder = 6;
        }

        private void ConstruirPortal()
        {
            if (Recursos25D.CrearModelo("wall-fortified-gate", portal, Vector3.zero, Vector3.one * 1.15f) == null)
            {
                Recursos25D.Primitiva(PrimitiveType.Cube, portal, new Vector3(0f, 0.6f, 0f), new Vector3(1.1f, 1.2f, 0.3f), Recursos25D.MaterialColor(new Color(0.45f, 0.45f, 0.5f)));
            }
            Material mDisco = Recursos25D.MaterialColor(new Color(0.75f, 0.35f, 1f, 0.75f), new Color(0.45f, 0.15f, 0.7f), true);
            // Cilindro aplanado y girado 90° para quedar de frente dentro del arco
            // En la boca del arco (cara +Z del modelo, la que mira hacia el camino)
            discoPortal = Recursos25D.Primitiva(PrimitiveType.Cylinder, portal, new Vector3(0f, 0.32f, 0.6f), new Vector3(0.44f, 0.02f, 0.58f), mDisco, new Vector3(90f, 0f, 0f));
            discoPortal.GetComponent<Renderer>().sortingOrder = 6;
        }

        private void ColocarCastilloYPortal()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            castillo.localPosition = Recursos25D.AModelo(gm.PosicionBase);

            // El punto de aparición puede quedar fuera de cámara: el portal se ubica en el primer
            // punto visible del primer tramo de la ruta óptima.
            Vector2 pos = gm.PosicionSpawn;
            Vector2 dir = Vector2.down;
            List<PathNode> ruta = PathGraph.main != null ? PathGraph.main.ObtenerRutaActual() : null;
            if (ruta != null && ruta.Count > 1)
            {
                Vector2 a = ruta[0].transform.position, b = ruta[1].transform.position;
                dir = (b - a).normalized;
                Camera cam = Vista25D.Camara;
                if (cam != null)
                {
                    for (int k = 0; k <= 40; k++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, k / 40f);
                        Vector3 vp = cam.WorldToViewportPoint(new Vector3(p.x, p.y, -1.2f));
                        pos = p;
                        if (vp.z > 0f && vp.y < 0.9f) break;
                    }
                }
            }
            portal.localPosition = Recursos25D.AModelo(pos);
            // El paso de la puerta (eje Z del modelo) se alinea con el tramo
            portal.localRotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f);
        }

        #endregion

        #region Decoración

        /// <summary>
        /// Árboles, arbustos, barriles y cajas en zonas libres: lejos de los caminos, de las
        /// casillas de construcción, de la base y del portal. Semilla fija = mismo resultado siempre.
        /// </summary>
        private void ReconstruirDecoracion(PathGraph g)
        {
            Vaciar(capaDecor);
            Rect r = Vista25D.Instance != null ? Vista25D.Instance.LimitesTablero() : new Rect(-8f, -6f, 16f, 12f);
            List<Segmento> segs = Segmentos(g);
            PlotTorre[] plots = FindObjectsByType<PlotTorre>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameManager gm = GameManager.Instance;
            Vector2 baseP = gm != null ? (Vector2)gm.PosicionBase : new Vector2(7f, -4.2f);
            Vector2 spawnP = new Vector2(portal.localPosition.x, portal.localPosition.z);

            System.Random rng = new System.Random(2710);
            List<Vector2> puestos = new List<Vector2>();
            string[] modelos = { "tree-large", "tree-large", "tree-large", "tree-shrub", "tree-shrub", "barrels", "detail-barrel", "detail-crate", "detail-crate-ropes" };

            for (int intento = 0; intento < 900 && puestos.Count < 42; intento++)
            {
                Vector2 p = new Vector2(
                    Mathf.Lerp(r.xMin + 0.2f, r.xMax - 0.2f, (float)rng.NextDouble()),
                    Mathf.Lerp(r.yMin + 0.2f, r.yMax - 0.2f, (float)rng.NextDouble()));

                bool libre = Vector2.Distance(p, baseP) > 1.5f && Vector2.Distance(p, spawnP) > 1.2f;
                for (int i = 0; libre && i < segs.Count; i++)
                    if (PathGraph.DistanciaPuntoSegmento(p, segs[i].a, segs[i].b) < 0.85f) libre = false;
                for (int i = 0; libre && i < plots.Length; i++)
                    if (Vector2.Distance(p, plots[i].transform.position) < 0.7f) libre = false;
                for (int i = 0; libre && i < puestos.Count; i++)
                    if (Vector2.Distance(p, puestos[i]) < 0.6f) libre = false;
                if (!libre) continue;

                puestos.Add(p);
                string nombre = modelos[rng.Next(modelos.Length)];
                bool arbol = nombre.StartsWith("tree");
                float escala = arbol ? Mathf.Lerp(0.55f, 0.9f, (float)rng.NextDouble()) : Mathf.Lerp(0.9f, 1.25f, (float)rng.NextDouble());
                float giro = (float)rng.NextDouble() * 360f;
                if (Recursos25D.CrearModelo(nombre, capaDecor, Recursos25D.AModelo(p), Vector3.one * escala, giro) == null && arbol)
                {
                    Recursos25D.Primitiva(PrimitiveType.Capsule, capaDecor, Recursos25D.AModelo(p, 0.45f * escala),
                        new Vector3(0.5f, 0.45f, 0.5f) * escala, Recursos25D.MaterialColor(new Color(0.2f, 0.45f, 0.25f)));
                }
            }
        }

        #endregion

        #region Animación y Estado de la Base

        private void Animar()
        {
            float t = Time.unscaledTime;
            GameManager gm = GameManager.Instance;

            if (discoPortal != null)
            {
                bool generando = gm != null && gm.estado == EstadoJuego.GenerandoOleada;
                discoPortal.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, t * (generando ? 360f : 90f), 0f);
                float pulso = 1f + (generando ? 0.12f : 0.04f) * Mathf.Sin(t * (generando ? 10f : 3f));
                discoPortal.localScale = new Vector3(0.44f * pulso, 0.02f, 0.58f * pulso);
            }

            if (gm == null) return;
            float pct = Mathf.Clamp01(gm.vidaBaseActual / Mathf.Max(1f, gm.vidaBaseMaxima));

            if (nucleo != null)
            {
                nucleo.localPosition = new Vector3(0f, 1.38f + 0.06f * Mathf.Sin(t * 2.5f), 0f);
                Color c = Color.Lerp(Paleta.Negativo, new Color(0.3f, 0.85f, 1f), pct);
                c.a = 0.55f + 0.25f * Mathf.Sin(t * (pct < 0.35f ? 12f : 3f));
                Recursos25D.Tenir(rNucleo, c);
            }

            if (rDomo != null) rDomo.enabled = gm.escudoBaseActual > 0f;

            if (anilloVida != null)
            {
                Vector3 centro = Vista25D.Elevar(gm.PosicionBase, 0.07f);
                int seg = Mathf.Max(2, Mathf.RoundToInt(48 * pct));
                anilloVida.loop = false;
                anilloVida.positionCount = seg + 1;
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.PI * 0.5f - i * (Mathf.PI * 2f * pct) / seg;
                    anilloVida.SetPosition(i, centro + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.15f);
                }
                Color ca = Color.Lerp(Paleta.Negativo, Paleta.Positivo, pct);
                anilloVida.startColor = ca;
                anilloVida.endColor = ca;
            }
        }

        #endregion
    }
}
