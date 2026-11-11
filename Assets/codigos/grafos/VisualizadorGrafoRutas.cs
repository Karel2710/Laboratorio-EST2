using System.Collections.Generic;
using UnityEngine;
using TowerDefenseGameplay;

namespace GrafosEST2
{
    /// <summary>
    /// Renderizado en tiempo de ejecución del grafo de caminos (no depende de Gizmos):
    /// aristas coloreadas por terreno con su peso, nodos etiquetados (N0, N1...),
    /// ruta óptima de Dijkstra resaltada con una línea luminosa animada,
    /// barricadas activas y previsualización del tramo a bloquear.
    /// </summary>
    public class VisualizadorGrafoRutas : MonoBehaviour
    {
        public static VisualizadorGrafoRutas Instance { get; private set; }

        public bool Visible { get; private set; }

        private Transform capaGrafo;
        private Transform capaBloqueos;
        private LineRenderer lineaRuta;
        private LineRenderer lineaPrevia;
        private readonly List<Transform> pulsos = new List<Transform>();
        private readonly List<Vector3> puntosRuta = new List<Vector3>();
        private float longitudRuta = 0f;
        private readonly List<LineRenderer> lineasBloqueo = new List<LineRenderer>();
        private readonly List<TextMesh> textosBloqueo = new List<TextMesh>();
        private PathGraph suscrito;

        public static VisualizadorGrafoRutas Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("VisualizadorGrafoRutas");
            return go.AddComponent<VisualizadorGrafoRutas>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            capaGrafo = new GameObject("CapaGrafo").transform;
            capaGrafo.SetParent(transform, false);
            capaBloqueos = new GameObject("CapaBloqueos").transform;
            capaBloqueos.SetParent(transform, false);
            lineaPrevia = GraficosProc.CrearLinea(capaBloqueos, "PreviaBarricada", Paleta.Aviso, Paleta.Aviso, 0.22f, 18);
            lineaPrevia.gameObject.SetActive(false);
            capaGrafo.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (suscrito != null) suscrito.OnGrafoCambiado -= Reconstruir;
            if (Instance == this) Instance = null;
        }

        public void Alternar()
        {
            Visible = !Visible;
            capaGrafo.gameObject.SetActive(Visible);
            if (Visible) Reconstruir();
        }

        public static Color ColorTerreno(float multiplicador)
        {
            switch (PathNode.TerrenoDeMultiplicador(multiplicador))
            {
                case TipoTerreno.Sendero: return new Color(1f, 0.76f, 0.4f);
                case TipoTerreno.Pantano: return new Color(0.72f, 0.88f, 0.48f);
                default: return new Color(0.5f, 0.83f, 1f);
            }
        }

        private void Reconstruir()
        {
            PathGraph g = PathGraph.main;
            if (g == null || !Visible) return;

            for (int i = capaGrafo.childCount - 1; i >= 0; i--) Destroy(capaGrafo.GetChild(i).gameObject);
            pulsos.Clear();

            // Aristas con su peso
            List<KeyValuePair<PathNode, PathNode>> aristas = g.ObtenerAristas();
            for (int i = 0; i < aristas.Count; i++)
            {
                PathNode a = aristas[i].Key, b = aristas[i].Value;
                float mult = a.MultiplicadorHacia(b);
                Color c = ColorTerreno(mult);
                c.a = 0.85f;
                LineRenderer lr = GraficosProc.CrearLinea(capaGrafo, "Arista", c, c, 0.06f, 19);
                lr.SetPosition(0, a.transform.position);
                lr.SetPosition(1, b.transform.position);

                Vector3 medio = (a.transform.position + b.transform.position) * 0.5f;
                Vector3 dir = (b.transform.position - a.transform.position).normalized;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * 0.26f;
                string etiqueta = a.PesoHacia(b).ToString("0.0") + (mult > 1.05f ? $" (x{mult:0.0})" : "");
                TextMesh tm = GraficosProc.CrearTextoMundo(capaGrafo, etiqueta, Color.Lerp(c, Color.white, 0.4f), 0.26f, 22);
                tm.transform.position = medio + perp;
            }

            // Nodos con su identificador
            for (int i = 0; i < g.nodos.Count; i++)
            {
                PathNode n = g.nodos[i];
                if (n == null) continue;
                Color c = n.esInicio ? Paleta.Positivo : (n.esFinal ? Paleta.Negativo : new Color(0.2f, 0.55f, 1f));
                SpriteRenderer borde = GraficosProc.CrearSprite(capaGrafo, "NodoBorde", GraficosProc.Circulo, Color.white, 20, 0.5f);
                borde.transform.position = n.transform.position;
                SpriteRenderer sr = GraficosProc.CrearSprite(capaGrafo, "Nodo", GraficosProc.Circulo, c, 21, 0.42f);
                sr.transform.position = n.transform.position;
                TextMesh tm = GraficosProc.CrearTextoMundo(capaGrafo, "N" + n.nodeId, Color.white, 0.24f, 22);
                tm.transform.position = n.transform.position;
                if (n.esInicio || n.esFinal)
                {
                    TextMesh rot = GraficosProc.CrearTextoMundo(capaGrafo, n.esInicio ? "INICIO" : "BASE", c, 0.22f, 22);
                    rot.transform.position = n.transform.position + new Vector3(0, n.esInicio ? -0.42f : 0.42f, 0);
                }
            }

            // Ruta óptima de Dijkstra
            List<PathNode> ruta = g.ObtenerRutaActual();
            puntosRuta.Clear();
            longitudRuta = 0f;
            if (ruta != null)
            {
                for (int i = 0; i < ruta.Count; i++)
                {
                    if (ruta[i] == null) continue;
                    Vector3 p = ruta[i].transform.position;
                    if (puntosRuta.Count > 0) longitudRuta += Vector3.Distance(puntosRuta[puntosRuta.Count - 1], p);
                    puntosRuta.Add(p);
                }
            }

            lineaRuta = GraficosProc.CrearLinea(capaGrafo, "RutaOptima", Paleta.Dorado, Paleta.Dorado, 0.16f, 18, puntosRuta.Count);
            for (int i = 0; i < puntosRuta.Count; i++) lineaRuta.SetPosition(i, puntosRuta[i]);

            int cantidad = Mathf.Clamp(Mathf.RoundToInt(longitudRuta / 2.5f), 3, 16);
            for (int i = 0; i < cantidad; i++)
            {
                SpriteRenderer p = GraficosProc.CrearSprite(capaGrafo, "Pulso", GraficosProc.Brillo, Color.white, 23, 0.55f);
                pulsos.Add(p.transform);
            }
        }

        private Vector3 PuntoEnRuta(float distancia)
        {
            for (int i = 0; i < puntosRuta.Count - 1; i++)
            {
                float tramo = Vector3.Distance(puntosRuta[i], puntosRuta[i + 1]);
                if (distancia <= tramo) return Vector3.Lerp(puntosRuta[i], puntosRuta[i + 1], tramo > 0.001f ? distancia / tramo : 0f);
                distancia -= tramo;
            }
            return puntosRuta.Count > 0 ? puntosRuta[puntosRuta.Count - 1] : Vector3.zero;
        }

        private void Update()
        {
            PathGraph g = PathGraph.main;
            if (g != suscrito)
            {
                if (suscrito != null) suscrito.OnGrafoCambiado -= Reconstruir;
                suscrito = g;
                if (g != null) g.OnGrafoCambiado += Reconstruir;
                Reconstruir();
            }
            if (g == null) return;

            float t = Time.unscaledTime;

            // Animación de la ruta óptima: brillo pulsante y destellos que la recorren
            if (Visible && lineaRuta != null && puntosRuta.Count > 1)
            {
                float onda = 0.5f + 0.5f * Mathf.Sin(t * 4f);
                Color c = Color.Lerp(Paleta.Dorado, Color.white, onda * 0.5f);
                c.a = 0.55f + 0.35f * onda;
                lineaRuta.startColor = c;
                lineaRuta.endColor = c;
                float ancho = 0.12f + 0.06f * onda;
                lineaRuta.startWidth = ancho;
                lineaRuta.endWidth = ancho;

                for (int i = 0; i < pulsos.Count; i++)
                {
                    if (pulsos[i] == null) continue;
                    float d = Mathf.Repeat(t * 2.2f + i * (longitudRuta / pulsos.Count), Mathf.Max(0.01f, longitudRuta));
                    pulsos[i].position = PuntoEnRuta(d);
                }
            }

            // Barricadas activas (siempre visibles, aunque el grafo esté oculto)
            while (lineasBloqueo.Count < g.bloqueos.Count)
            {
                lineasBloqueo.Add(GraficosProc.CrearLinea(capaBloqueos, "Barricada", Paleta.Negativo, Paleta.Negativo, 0.2f, 17));
                TextMesh tm = GraficosProc.CrearTextoMundo(capaBloqueos, "", Color.white, 0.3f, 24);
                textosBloqueo.Add(tm);
            }
            for (int i = 0; i < lineasBloqueo.Count; i++)
            {
                bool activo = i < g.bloqueos.Count && g.bloqueos[i].a != null && g.bloqueos[i].b != null;
                lineasBloqueo[i].gameObject.SetActive(activo);
                textosBloqueo[i].gameObject.SetActive(activo);
                if (!activo) continue;
                PathGraph.Bloqueo b = g.bloqueos[i];
                Vector3 pa = b.a.transform.position, pb = b.b.transform.position;
                lineasBloqueo[i].SetPosition(0, Vector3.Lerp(pa, pb, 0.12f));
                lineasBloqueo[i].SetPosition(1, Vector3.Lerp(pa, pb, 0.88f));
                Color cb = Color.Lerp(Paleta.Negativo, Paleta.Aviso, 0.5f + 0.5f * Mathf.Sin(t * 8f));
                cb.a = 0.8f;
                lineasBloqueo[i].startColor = cb;
                lineasBloqueo[i].endColor = cb;
                textosBloqueo[i].transform.position = (pa + pb) * 0.5f;
                textosBloqueo[i].text = b.tiempoRestante >= 0f ? $"BLOQUEADO {b.tiempoRestante:0}s" : "BLOQUEADO";
            }

            // Previsualización del tramo a bloquear
            GameManager gm = GameManager.Instance;
            bool previa = gm != null && gm.apuntandoBarricada && gm.BarricadaPreviaA != null && gm.BarricadaPreviaB != null;
            lineaPrevia.gameObject.SetActive(previa);
            if (previa)
            {
                lineaPrevia.SetPosition(0, gm.BarricadaPreviaA.transform.position);
                lineaPrevia.SetPosition(1, gm.BarricadaPreviaB.transform.position);
                Color cp = Paleta.Aviso;
                cp.a = 0.5f + 0.4f * Mathf.Sin(t * 10f);
                lineaPrevia.startColor = cp;
                lineaPrevia.endColor = cp;
            }
        }
    }
}
