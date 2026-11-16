using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Embellece automáticamente el escenario: cuadrícula táctica sobre el mapa, calzadas con
    /// bordes luminosos a lo largo de las aristas del grafo, portal de aparición animado y
    /// base aliada con reactor hexagonal y domo de escudo. Todo generado por código.
    /// </summary>
    public class AmbientacionMapa2D : MonoBehaviour
    {
        public static AmbientacionMapa2D Instance { get; private set; }

        private Transform capaCaminos;
        private Transform portal;
        private readonly List<Transform> anillosPortal = new List<Transform>();
        private Transform baseAliada;
        private Transform reactor;
        private SpriteRenderer srNucleo, srDomo, srVidaBase;
        private LineRenderer anilloVida;
        private PathGraph suscrito;
        private int versionVista = -1;

        public static AmbientacionMapa2D Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("AmbientacionMapa2D");
            return go.AddComponent<AmbientacionMapa2D>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            CrearFondoTactico();
            capaCaminos = new GameObject("Caminos").transform;
            capaCaminos.SetParent(transform, false);
            CrearPortal();
            CrearBase();
        }

        private void OnDestroy()
        {
            if (suscrito != null) suscrito.OnGrafoCambiado -= ReconstruirCaminos;
            if (Instance == this) Instance = null;
        }

        private void CrearFondoTactico()
        {
            Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            float alto = cam != null && cam.orthographic ? cam.orthographicSize * 2f + 2f : 14f;
            // Más grande que la vista 2D para cubrir también el horizonte de la vista 2.5D
            alto = Mathf.Max(alto, 34f);
            float ancho = Mathf.Max(alto * 2.2f, 70f);
            if (cam != null) cam.backgroundColor = new Color(0.03f, 0.05f, 0.09f);

            // El arte original del mapa queda al fondo y se tiñe con un velo oscuro azulado
            SpriteRenderer[] todos = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < todos.Length; i++)
            {
                SpriteRenderer sr = todos[i];
                if (sr.sortingOrder == 0 && sr.GetComponent<PlotTorre>() == null && sr.bounds.size.x > 8f) sr.sortingOrder = -20;
            }

            SpriteRenderer velo = GraficosProc.CrearSprite(transform, "VeloTactico", GraficosProc.Cuadrado, new Color(0.02f, 0.06f, 0.14f, 0.42f), -15);
            velo.transform.localScale = new Vector3(ancho, alto, 1f);

            // Cuadrícula táctica (una textura repetida)
            const int px = 64;
            Texture2D tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    bool linea = x == 0 || y == 0;
                    bool cruz = (x < 5 && y == 0) || (y < 5 && x == 0) || (x > px - 5 && y == 0) || (y > px - 5 && x == 0);
                    tex.SetPixel(x, y, cruz ? new Color(0.4f, 0.9f, 1f, 0.5f) : (linea ? new Color(0.3f, 0.75f, 1f, 0.16f) : Color.clear));
                }
            }
            tex.Apply();
            GameObject rejilla = new GameObject("CuadriculaTactica");
            rejilla.transform.SetParent(transform, false);
            SpriteRenderer srG = rejilla.AddComponent<SpriteRenderer>();
            srG.sprite = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px / 1.18f, 0, SpriteMeshType.FullRect);
            srG.drawMode = SpriteDrawMode.Tiled;
            srG.size = new Vector2(ancho, alto);
            srG.sortingOrder = -14;
        }

        private void ReconstruirCaminos()
        {
            for (int i = capaCaminos.childCount - 1; i >= 0; i--) Destroy(capaCaminos.GetChild(i).gameObject);
            PathGraph g = PathGraph.main;
            if (g == null) return;

            List<KeyValuePair<PathNode, PathNode>> aristas = g.ObtenerAristas();
            for (int i = 0; i < aristas.Count; i++)
            {
                Vector3 a = aristas[i].Key.transform.position, b = aristas[i].Value.transform.position;
                float mult = aristas[i].Key.MultiplicadorHacia(aristas[i].Value);
                Color c = GrafosEST2.VisualizadorGrafoRutas.ColorTerreno(mult);
                bool principal = mult <= 1.05f;

                // Calzada translúcida + dos bordes luminosos
                Color calzada = principal ? new Color(0.05f, 0.12f, 0.2f, 0.45f) : new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.2f, 0.4f);
                LineRenderer centro = GraficosProc.CrearLinea(capaCaminos, "Calzada", calzada, calzada, 0.78f, -10);
                centro.numCapVertices = 6;
                centro.SetPosition(0, a);
                centro.SetPosition(1, b);

                Vector3 dir = (b - a).normalized;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * 0.41f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Color cb = c;
                    cb.a = principal ? 0.5f : 0.38f;
                    LineRenderer borde = GraficosProc.CrearLinea(capaCaminos, "Borde", cb, cb, 0.045f, -9);
                    borde.SetPosition(0, a + perp * s);
                    borde.SetPosition(1, b + perp * s);
                }
            }

            ColocarPortalYBase();
        }

        private void CrearPortal()
        {
            portal = new GameObject("PortalSpawn").transform;
            portal.SetParent(transform, false);
            GraficosProc.CrearSprite(portal, "Halo", GraficosProc.Brillo, new Color(0.7f, 0.3f, 1f, 0.55f), -6, 2.6f);
            Color[] cols = { new Color(0.75f, 0.35f, 1f), new Color(0.3f, 0.9f, 1f), new Color(1f, 0.4f, 0.8f) };
            for (int i = 0; i < 3; i++)
            {
                // Anillo aplastado: al girar produce el efecto de portal
                SpriteRenderer an = GraficosProc.CrearSprite(portal, "Anillo" + i, GraficosProc.Anillo, cols[i], -5 + i);
                an.transform.localScale = new Vector3(1.5f - i * 0.32f, (1.5f - i * 0.32f) * 0.72f, 1f);
                anillosPortal.Add(an.transform);
            }
            GraficosProc.CrearSprite(portal, "Nucleo", GraficosProc.Circulo, new Color(0.1f, 0.02f, 0.2f, 0.95f), -2, 0.55f);
        }

        private void CrearBase()
        {
            baseAliada = new GameObject("BaseAliada").transform;
            baseAliada.SetParent(transform, false);
            GraficosProc.CrearSprite(baseAliada, "Halo", GraficosProc.Brillo, new Color(0.2f, 0.8f, 1f, 0.45f), -6, 3f);
            GraficosProc.CrearSprite(baseAliada, "Plataforma", GraficosProc.Hexagono, new Color(0.08f, 0.14f, 0.22f), -4, 1.5f);

            SpriteRenderer hex = GraficosProc.CrearSprite(baseAliada, "Reactor", GraficosProc.Hexagono, new Color(0.2f, 0.7f, 0.95f), -3, 1.05f);
            reactor = hex.transform;
            srVidaBase = hex;
            srNucleo = GraficosProc.CrearSprite(baseAliada, "Nucleo", GraficosProc.Circulo, Color.white, -2, 0.42f);

            srDomo = GraficosProc.CrearSprite(baseAliada, "DomoEscudo", GraficosProc.Circulo, new Color(0.3f, 0.9f, 1f, 0.18f), 3, 2.3f);
            anilloVida = GraficosProc.CrearLinea(baseAliada, "AnilloVida", Paleta.Positivo, Paleta.Positivo, 0.07f, 4);
        }

        private void ColocarPortalYBase()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            baseAliada.position = gm.PosicionBase;

            // El punto de aparición puede quedar fuera de cámara: el portal se dibuja en el
            // primer punto visible del tramo inicial de la ruta.
            Vector3 pos = gm.PosicionSpawn;
            Camera cam = Camera.main;
            PathGraph g = PathGraph.main;
            if (cam != null && cam.orthographic && g != null)
            {
                List<PathNode> ruta = g.ObtenerRutaActual();
                float techo = cam.transform.position.y + cam.orthographicSize - 1.75f;
                if (ruta != null && ruta.Count > 1 && pos.y > techo)
                {
                    Vector3 a = ruta[0].transform.position, b = ruta[1].transform.position;
                    if (Mathf.Abs(a.y - b.y) > 0.01f)
                    {
                        float k = Mathf.Clamp01((a.y - techo) / (a.y - b.y));
                        pos = Vector3.Lerp(a, b, k);
                    }
                }
            }
            portal.position = pos;
        }

        private void Update()
        {
            PathGraph g = PathGraph.main;
            if (g != suscrito)
            {
                if (suscrito != null) suscrito.OnGrafoCambiado -= ReconstruirCaminos;
                suscrito = g;
                if (g != null) g.OnGrafoCambiado += ReconstruirCaminos;
                ReconstruirCaminos();
            }

            // En 2.5D los caminos, el portal y la base 2D los reemplaza Escenario25D
            if (versionVista != Vista25D.Version)
            {
                versionVista = Vista25D.Version;
                bool v2 = !Vista25D.Activa;
                capaCaminos.gameObject.SetActive(v2);
                portal.gameObject.SetActive(v2);
                baseAliada.gameObject.SetActive(v2);
                if (v2) ColocarPortalYBase();
            }
            if (Vista25D.Activa) return;

            float t = Time.unscaledTime;
            for (int i = 0; i < anillosPortal.Count; i++)
                anillosPortal[i].Rotate(0, 0, (i % 2 == 0 ? 70f : -110f) * Time.unscaledDeltaTime * (1f + i * 0.3f));

            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            bool generando = gm.estado == EstadoJuego.GenerandoOleada;
            portal.localScale = Vector3.one * (1f + (generando ? 0.14f : 0.05f) * Mathf.Sin(t * (generando ? 9f : 2.5f)));

            // Base: el reactor refleja la vida y el domo aparece mientras haya escudo
            float pct = Mathf.Clamp01(gm.vidaBaseActual / Mathf.Max(1f, gm.vidaBaseMaxima));
            reactor.Rotate(0, 0, 25f * Time.unscaledDeltaTime);
            Color cv = Color.Lerp(Paleta.Negativo, new Color(0.2f, 0.75f, 1f), pct);
            srVidaBase.color = cv;
            srNucleo.color = Color.Lerp(cv, Color.white, 0.5f + 0.5f * Mathf.Sin(t * (pct < 0.35f ? 12f : 3f)));
            srNucleo.transform.localScale = Vector3.one * (0.38f + 0.06f * Mathf.Sin(t * 4f));

            bool escudo = gm.escudoBaseActual > 0f;
            srDomo.enabled = escudo;
            if (escudo) srDomo.color = new Color(0.3f, 0.9f, 1f, 0.14f + 0.07f * Mathf.Sin(t * 3f));

            // Anillo de vida: arco proporcional a la vida restante
            int seg = Mathf.Max(2, Mathf.RoundToInt(48 * pct));
            anilloVida.loop = false;
            anilloVida.positionCount = seg + 1;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 0.5f - i * (Mathf.PI * 2f * pct) / seg;
                anilloVida.SetPosition(i, baseAliada.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.95f);
            }
            Color ca = Color.Lerp(Paleta.Negativo, Paleta.Positivo, pct);
            anilloVida.startColor = ca;
            anilloVida.endColor = ca;
        }
    }
}
