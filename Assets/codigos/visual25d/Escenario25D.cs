using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Escenario 3D de la vista 2.5D con los kits Kenney Tower Defense y KayKit Dungeon:
    /// una isla de casillas de pasto (reemplaza la imagen de fondo), caminos de tierra sobre las
    /// aristas del grafo (tierra = ruta principal, arena = sendero, agua = pantano), barricadas de
    /// cajas sobre aristas bloqueadas, castillo aliado en la base, puerta-portal en el punto de
    /// aparición y decoración (árboles, rocas, cristales) en las zonas libres.
    /// </summary>
    public class Escenario25D : MonoBehaviour
    {
        public static Escenario25D Instance { get; private set; }

        private const float CELDA = 1f;            // lado de cada casilla de pasto de la isla
        private const float ANCHO_CAMINO = 0.82f;
        private const float ANCHO_SENDERO = 0.62f;
        private const float ANCHO_PANTANO = 0.78f;

        private static string TD(string n) => Recursos25D.TD + n;
        private static string KK(string n) => Recursos25D.KK + n;

        private Transform contenido;   // se oculta en la vista 2D
        private Transform raizModelo;  // espacio de modelo (RotModelo)
        private Transform capaTerreno, capaCaminos, capaBarricadas, capaDecor, castillo, portal;
        private Transform discoPortal, nucleo;
        private Renderer[] rNucleo;
        private Renderer rDomo;
        private LineRenderer anilloVida;
        private float alturaNucleo = 1.4f;

        private PathGraph suscrito;
        private int versionVista = -1;
        private int firmaTerreno = -1;
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
            capaTerreno = Hijo("Terreno");
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
                if (firma != firmaTerreno)
                {
                    firmaTerreno = firma;
                    ReconstruirTerreno(g);
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

            for (int s = 0; s < segs.Count; s++)
            {
                Segmento seg = segs[s];
                CrearTramo(PathNode.TerrenoDeMultiplicador(seg.mult), seg.a, seg.b);
                if (seg.bloqueado) CrearBarricada(seg.a, seg.b);
            }
        }

        // Tinte arenoso para los senderos (alternativas más lentas)
        private static readonly Color tinteSendero = new Color(0.93f, 0.8f, 0.52f, 0.5f);

        /// <summary>
        /// Losa de tierra a lo largo de la arista, alargada medio ancho en cada extremo para que las
        /// esquinas queden cerradas. La ruta principal queda un poco más alta que los senderos y el
        /// pantano, así en los cruces siempre se ve encima.
        /// </summary>
        private void CrearTramo(TipoTerreno terreno, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float largo = d.magnitude;
            if (largo < 0.01f) return;
            Vector2 dir = d / largo;
            float giro = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            Vector3 centro = Recursos25D.AModelo((a + b) * 0.5f);
            float h = Vista25D.ALTURA_CAMINO;

            switch (terreno)
            {
                case TipoTerreno.Pantano:
                {
                    // Orilla de tierra y agua un poco hundida
                    Losa(centro, ANCHO_PANTANO + 0.12f, largo, h - 0.008f, giro, null);
                    Material agua = Recursos25D.MaterialColor(new Color(0.42f, 0.74f, 0.93f), new Color(0.04f, 0.1f, 0.16f));
                    Recursos25D.Primitiva(PrimitiveType.Cube, capaCaminos, centro + Vector3.up * (h - 0.006f) * 0.5f,
                        new Vector3(ANCHO_PANTANO - 0.06f, h - 0.006f, largo + ANCHO_PANTANO - 0.06f), agua, new Vector3(0f, giro, 0f));
                    break;
                }
                case TipoTerreno.Sendero:
                    Losa(centro, ANCHO_SENDERO, largo, h - 0.004f, giro, tinteSendero);
                    break;
                default:
                    Losa(centro, ANCHO_CAMINO, largo, h, giro, null);
                    break;
            }
        }

        private void Losa(Vector3 centro, float ancho, float largo, float alto, float giro, Color? tinte)
        {
            // tile-dirt mide 1 x 0.1 x 1: se estira a lo largo del tramo
            Transform t = Recursos25D.CrearModelo(TD("tile-dirt"), capaCaminos, centro, new Vector3(ancho, alto / 0.1f, largo + ancho), giro);
            if (t == null)
            {
                t = Recursos25D.Primitiva(PrimitiveType.Cube, capaCaminos, centro + Vector3.up * alto * 0.5f, new Vector3(ancho, alto, largo + ancho),
                    Recursos25D.MaterialColor(new Color(0.86f, 0.52f, 0.34f)), new Vector3(0f, giro, 0f));
            }
            if (tinte.HasValue) Recursos25D.Tenir(t.GetComponentsInChildren<Renderer>(true), tinte.Value);
        }

        /// <summary>Cajas y barriles atravesados en el tramo bloqueado por la habilidad Barricada.</summary>
        private void CrearBarricada(Vector2 a, Vector2 b)
        {
            Vector2 medio = (a + b) * 0.5f;
            Vector2 dir = (b - a).normalized;
            Vector2 lat = new Vector2(-dir.y, dir.x);
            float giro = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            float h = Vista25D.ALTURA_CAMINO;

            Transform c1 = Recursos25D.CrearModelo(KK("crate"), capaBarricadas, Recursos25D.AModelo(medio + lat * 0.24f, h), Vector3.one * 0.3f, giro + 8f);
            Transform c2 = Recursos25D.CrearModelo(KK("crateDark"), capaBarricadas, Recursos25D.AModelo(medio - lat * 0.24f, h), Vector3.one * 0.3f, giro - 6f);
            Recursos25D.CrearModelo(KK("barrel"), capaBarricadas, Recursos25D.AModelo(medio + dir * 0.2f, h), Vector3.one * 0.28f, giro);
            Recursos25D.CrearModelo(KK("crate"), capaBarricadas, Recursos25D.AModelo(medio + lat * 0.05f - dir * 0.05f, h + 0.3f), Vector3.one * 0.24f, giro + 30f);
            if (c1 == null && c2 == null)
            {
                Recursos25D.Primitiva(PrimitiveType.Cube, capaBarricadas, Recursos25D.AModelo(medio, h + 0.18f), new Vector3(0.8f, 0.35f, 0.2f),
                    Recursos25D.MaterialColor(new Color(0.55f, 0.35f, 0.18f)), new Vector3(0f, giro + 90f, 0f));
            }
        }

        #endregion

        #region Castillo (base aliada) y Portal

        /// <summary>Apila piezas del kit una sobre otra midiendo su altura real. Devuelve la altura final.</summary>
        public static float Apilar(Transform padre, Vector3 pos, float escala, float alturaInicial, params string[] piezas)
        {
            float y = alturaInicial;
            for (int i = 0; i < piezas.Length; i++)
            {
                Transform t = Recursos25D.CrearModelo(piezas[i], padre, pos + Vector3.up * y, Vector3.one * escala);
                if (t == null) continue;
                if (Recursos25D.Limites(t, padre, out Bounds b)) y = b.max.y - pos.y;
            }
            return y;
        }

        private void ConstruirCastillo()
        {
            // Torreón central (Kenney) con los cristales del núcleo encima
            float alto = Apilar(castillo, Vector3.zero, 0.9f, 0f, TD("tower-square-bottom-b"), TD("tower-square-middle-b"), TD("tower-square-top-b"));
            if (alto < 0.1f)
            {
                Recursos25D.Primitiva(PrimitiveType.Cube, castillo, new Vector3(0f, 0.65f, 0f), new Vector3(0.9f, 1.3f, 0.9f), Recursos25D.MaterialColor(new Color(0.55f, 0.55f, 0.6f)));
                alto = 1.3f;
            }

            // Torres redondas detrás del torreón (detrás = +Z en modelo = más lejos de la cámara)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(0.62f * s, 0f, 0.38f);
                Apilar(castillo, p, 0.55f, 0f, TD("tower-round-bottom-a"), TD("tower-round-top-a"), TD("tower-round-roof-a"));
            }

            // Estandarte al frente (cara -Z, la que mira a la cámara)
            Recursos25D.CrearModelo(KK("banner"), castillo, new Vector3(0f, alto * 0.42f, -0.43f), Vector3.one * 0.2f, 180f);

            // Tesoro y antorchas al pie del torreón
            Recursos25D.CrearModelo(KK("chest_rare"), castillo, new Vector3(0.36f, 0f, -0.68f), Vector3.one * 0.2f, 160f);
            Recursos25D.CrearModelo(KK("coinsLarge"), castillo, new Vector3(-0.32f, 0f, -0.7f), Vector3.one * 0.2f, 20f);
            Recursos25D.CrearModelo(KK("torch"), castillo, new Vector3(-0.7f, 0f, -0.55f), Vector3.one * 0.3f);
            Recursos25D.CrearModelo(KK("torch"), castillo, new Vector3(0.72f, 0f, -0.5f), Vector3.one * 0.3f);

            // Núcleo: cristales que cambian de color con la vida de la base
            Transform cristales = Recursos25D.CrearModelo(TD("tower-round-crystals"), castillo, new Vector3(0f, alto - 0.12f, 0f), Vector3.one * 0.7f);
            if (cristales != null)
            {
                nucleo = cristales;
                alturaNucleo = alto - 0.12f;
            }
            else
            {
                Material mNucleo = Recursos25D.MaterialColor(new Color(0.3f, 0.85f, 1f), new Color(0.2f, 0.6f, 0.8f));
                nucleo = Recursos25D.Primitiva(PrimitiveType.Sphere, castillo, new Vector3(0f, alto + 0.2f, 0f), Vector3.one * 0.3f, mNucleo);
                alturaNucleo = alto + 0.2f;
            }
            rNucleo = nucleo.GetComponentsInChildren<Renderer>();

            Material mDomo = Recursos25D.MaterialColor(new Color(0.3f, 0.9f, 1f, 0.16f), new Color(0.05f, 0.2f, 0.3f, 0f), true);
            rDomo = Recursos25D.Primitiva(PrimitiveType.Sphere, castillo, new Vector3(0f, 0.55f, 0.05f), Vector3.one * 2.2f, mDomo).GetComponent<Renderer>();
            rDomo.sortingOrder = 6;
        }

        private void ConstruirPortal()
        {
            // Arco de mazmorra (KayKit): su paso va a lo largo del eje Z del modelo
            if (Recursos25D.CrearModelo(KK("wall_gate"), portal, Vector3.zero, Vector3.one * 0.3f) == null)
            {
                Recursos25D.Primitiva(PrimitiveType.Cube, portal, new Vector3(0f, 0.6f, 0f), new Vector3(1.1f, 1.2f, 0.3f), Recursos25D.MaterialColor(new Color(0.45f, 0.45f, 0.5f)));
            }
            Recursos25D.CrearModelo(KK("torch"), portal, new Vector3(-0.72f, 0f, 0.3f), Vector3.one * 0.28f);
            Recursos25D.CrearModelo(KK("torch"), portal, new Vector3(0.72f, 0f, 0.3f), Vector3.one * 0.28f);

            // Plataforma de aparición del kit Kenney delante de la puerta
            Transform pad = Recursos25D.CrearModelo(TD("spawn-round"), portal, new Vector3(0f, 0f, 0.62f), Vector3.one * 1.3f);
            if (pad != null) Recursos25D.Tenir(pad.GetComponentsInChildren<Renderer>(true), new Color(0.6f, 0.3f, 0.95f, 0.35f));

            Material mDisco = Recursos25D.MaterialColor(new Color(0.75f, 0.35f, 1f, 0.75f), new Color(0.45f, 0.15f, 0.7f), true);
            // Cilindro aplanado y girado 90° para quedar de frente en la boca del arco (cara +Z, hacia el camino)
            discoPortal = Recursos25D.Primitiva(PrimitiveType.Cylinder, portal, new Vector3(0f, 0.42f, 0.26f), new Vector3(0.5f, 0.02f, 0.66f), mDisco, new Vector3(90f, 0f, 0f));
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

        #region Terreno (isla) y Decoración

        private static readonly string[] casillasBorde = { "tile-tree", "tile-tree", "tile-tree-double", "tile-tree-quad", "tile-rock", "tile-hill", "tile-crystal" };
        private static readonly string[] casillasInterior = { "tile-tree", "tile-rock", "tile-hill" };
        private static readonly string[] detalles = { "detail-tree", "detail-tree", "detail-tree-large", "detail-rocks", "detail-rocks-large", "detail-crystal", "detail-dirt", "detail-dirt-large" };

        /// <summary>
        /// Isla de casillas de pasto que cubre el tablero (reemplaza la imagen de fondo), con
        /// casillas especiales (bosque, rocas, colinas, cristales) en las zonas libres y en el borde.
        /// Semilla fija: el mismo mapa da siempre la misma isla.
        /// </summary>
        private void ReconstruirTerreno(PathGraph g)
        {
            Vaciar(capaTerreno);
            Vaciar(capaDecor);

            Rect tablero = Vista25D.Instance != null ? Vista25D.Instance.LimitesTablero() : new Rect(-8f, -6f, 16f, 12f);
            List<Segmento> segs = Segmentos(g);
            PlotTorre[] plots = FindObjectsByType<PlotTorre>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameManager gm = GameManager.Instance;
            Vector2 baseP = gm != null ? (Vector2)gm.PosicionBase : new Vector2(7f, -4.2f);
            Vector2 spawnP = new Vector2(portal.localPosition.x, portal.localPosition.z);

            System.Random rng = new System.Random(2710);

            // Distancia libre de un punto a caminos, casillas, base y portal
            float Libre(Vector2 p)
            {
                float m = Mathf.Min(Vector2.Distance(p, baseP) - 0.9f, Vector2.Distance(p, spawnP) - 0.6f);
                for (int i = 0; i < segs.Count; i++) m = Mathf.Min(m, PathGraph.DistanciaPuntoSegmento(p, segs[i].a, segs[i].b) - ANCHO_CAMINO * 0.5f);
                for (int i = 0; i < plots.Length; i++) m = Mathf.Min(m, Vector2.Distance(p, plots[i].transform.position) - 0.42f);
                return m;
            }

            // ── Isla: rejilla de casillas con un anillo exterior irregular ──
            const int anillo = 1;
            int nx = Mathf.CeilToInt(tablero.width / CELDA) + anillo * 2;
            int ny = Mathf.CeilToInt(tablero.height / CELDA) + anillo * 2;
            Vector2 origen = tablero.center - new Vector2(nx * CELDA, ny * CELDA) * 0.5f + Vector2.one * (CELDA * 0.5f);
            Vector3 escala = new Vector3(CELDA, 1f, CELDA);

            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    Vector2 p = origen + new Vector2(ix * CELDA, iy * CELDA);
                    bool borde = ix < anillo || iy < anillo || ix >= nx - anillo || iy >= ny - anillo;
                    double azar = rng.NextDouble();
                    float libre = Libre(p);
                    if (borde && libre > 0.2f && azar < 0.22) continue; // muescas en la orilla

                    string pieza = "tile";
                    double azar2 = rng.NextDouble();
                    if (libre > 0.55f)
                    {
                        if (borde && azar2 < 0.5) pieza = casillasBorde[rng.Next(casillasBorde.Length)];
                        else if (!borde && azar2 < 0.09) pieza = casillasInterior[rng.Next(casillasInterior.Length)];
                    }
                    float giro = rng.Next(4) * 90f;

                    // La cara superior del pasto queda en y = 0 (el plano del juego)
                    Transform t = Recursos25D.CrearModelo(TD(pieza), capaTerreno, Recursos25D.AModelo(p, -0.2f), escala, giro);
                    if (t == null && pieza != "tile") t = Recursos25D.CrearModelo(TD("tile"), capaTerreno, Recursos25D.AModelo(p, -0.2f), escala, giro);
                    if (t == null)
                    {
                        Recursos25D.Primitiva(PrimitiveType.Cube, capaTerreno, Recursos25D.AModelo(p, -0.1f), new Vector3(CELDA, 0.2f, CELDA),
                            Recursos25D.MaterialColor(new Color(0.36f, 0.72f, 0.45f)));
                    }
                }
            }

            // ── Detalles sueltos: árboles pequeños, rocas, cristales y montículos ──
            List<Vector2> puestos = new List<Vector2>();
            for (int intento = 0; intento < 900 && puestos.Count < 46; intento++)
            {
                Vector2 p = new Vector2(
                    Mathf.Lerp(tablero.xMin + 0.15f, tablero.xMax - 0.15f, (float)rng.NextDouble()),
                    Mathf.Lerp(tablero.yMin + 0.15f, tablero.yMax - 0.15f, (float)rng.NextDouble()));
                if (Libre(p) < 0.12f) continue;
                bool cerca = false;
                for (int i = 0; !cerca && i < puestos.Count; i++) cerca = Vector2.Distance(p, puestos[i]) < 0.45f;
                if (cerca) continue;

                puestos.Add(p);
                string nombre = detalles[rng.Next(detalles.Length)];
                float esc = Mathf.Lerp(0.75f, 1.15f, (float)rng.NextDouble());
                float giro = (float)rng.NextDouble() * 360f;
                if (Recursos25D.CrearModelo(TD(nombre), capaDecor, Recursos25D.AModelo(p), Vector3.one * esc, giro) == null && nombre.Contains("tree"))
                {
                    Recursos25D.Primitiva(PrimitiveType.Capsule, capaDecor, Recursos25D.AModelo(p, 0.25f * esc),
                        new Vector3(0.3f, 0.25f, 0.3f) * esc, Recursos25D.MaterialColor(new Color(0.2f, 0.6f, 0.35f)));
                }
            }

            // Un par de barriles y vasijas cerca de la base para darle vida
            for (int i = 0; i < 3; i++)
            {
                float a = (200f + i * 50f) * Mathf.Deg2Rad;
                Vector2 p = baseP + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.25f;
                if (Libre(p) < 0f) continue;
                string[] props = { "barrel", "pots", "lootSackA" };
                Recursos25D.CrearModelo(KK(props[i]), capaDecor, Recursos25D.AModelo(p), Vector3.one * 0.22f, i * 70f);
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
                discoPortal.localScale = new Vector3(0.5f * pulso, 0.02f, 0.66f * pulso);
            }

            if (gm == null) return;
            float pct = Mathf.Clamp01(gm.vidaBaseActual / Mathf.Max(1f, gm.vidaBaseMaxima));

            if (nucleo != null)
            {
                nucleo.localPosition = new Vector3(0f, alturaNucleo + 0.05f * Mathf.Sin(t * 2.5f), 0f);
                nucleo.localRotation = Quaternion.Euler(0f, t * 25f, 0f);
                Color c = Color.Lerp(Paleta.Negativo, new Color(0.3f, 0.85f, 1f), pct);
                c.a = 0.4f + 0.2f * Mathf.Sin(t * (pct < 0.35f ? 12f : 3f));
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
