using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GrafosEST2
{
    /// <summary>
    /// 🔬 Laboratorio de Grafos: simulador interactivo para crear vértices y aristas ponderadas
    /// (dirigidas o no), ver la Matriz y la Lista de Adyacencia en vivo y ejecutar paso a paso
    /// BFS, DFS, Dijkstra, Kruskal y el ordenamiento topológico de Kahn.
    /// </summary>
    public class SimuladorGrafosUI : MonoBehaviour
    {
        public static SimuladorGrafosUI Instance { get; private set; }

        private Grafo<string> grafo = new Grafo<string>(false);
        private readonly Dictionary<string, Vector2> posiciones = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Color> colorNodo = new Dictionary<string, Color>();
        private readonly Dictionary<string, Color> colorArista = new Dictionary<string, Color>();
        private readonly Dictionary<string, string> etiquetaNodo = new Dictionary<string, string>();

        private GameObject panel;
        private RectTransform lienzo;
        private Transform capaAristas, capaNodos;
        private Text textoEstructuras, textoBitacora, textoSeleccion;
        private InputField inputPeso;
        private Button btnDirigido, btnVelocidad;
        private string seleccionA, seleccionB;
        private int contadorNodos = 0;
        private float pausaPaso = 0.7f;
        private Coroutine animacion;
        private readonly StringBuilder bitacora = new StringBuilder();

        private const float ANCHO_LIENZO = 900f, ALTO_LIENZO = 660f;
        private static readonly Color COLOR_BASE = new Color(0.15f, 0.42f, 0.7f);
        private static readonly Color COLOR_VISITADO = new Color(0.15f, 0.7f, 0.4f);
        private static readonly Color COLOR_ACTUAL = new Color(1f, 0.75f, 0.15f);
        private static readonly Color COLOR_CAMINO = new Color(1f, 0.45f, 0.75f);
        private static readonly Color COLOR_DESCARTE = new Color(0.8f, 0.25f, 0.25f);

        public static SimuladorGrafosUI Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("SimuladorGrafosUI");
            return go.AddComponent<SimuladorGrafosUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ConstruirUI();
            CargarEjemplo();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void AlternarPanel()
        {
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) Redibujar();
        }

        #region Construcción de la Interfaz

        private Button Boton(Transform padre, string texto, Color color, float x, float y, float w, UnityEngine.Events.UnityAction accion, float h = 34)
        {
            Button b = UIFabrica.CrearBoton(padre, "Btn_" + texto, texto, color, 12);
            UIFabrica.Colocar(b, x, y, w, h);
            b.onClick.AddListener(accion);
            return b;
        }

        private void ConstruirUI()
        {
            GameObject canvas = UIFabrica.CrearCanvas("Canvas_LaboratorioGrafos", 106);
            canvas.transform.SetParent(transform, false);

            RectTransform rt = UIFabrica.CrearPanel(canvas.transform, "PanelSimulador", Paleta.FondoPanel, new Color(0.7f, 0.5f, 1f, 0.7f));
            UIFabrica.Anclar(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1560, 900));
            panel = rt.gameObject;
            UIFabrica.CrearEncabezado(rt, "🔬 LABORATORIO DE GRAFOS — Simulador Interactivo G = (V, E)", new Color(0.85f, 0.7f, 1f), () => panel.SetActive(false));

            Text ayuda = UIFabrica.CrearTexto(rt, "Ayuda",
                "Clic en el lienzo: crear vértice · Clic en un vértice: seleccionar origen (A) y luego destino (B) · Arrastrar: mover · Los algoritmos parten de A",
                12, TextAnchor.MiddleLeft, new Color(0.65f, 0.75f, 0.88f));
            UIFabrica.Colocar(ayuda, 18, -50, 900, 22);

            // Lienzo interactivo
            RectTransform fondo = UIFabrica.CrearPanel(rt, "Lienzo", new Color(0.03f, 0.05f, 0.08f, 1f), new Color(0.3f, 0.4f, 0.6f, 0.6f));
            UIFabrica.Colocar(fondo, 16, -76, ANCHO_LIENZO, ALTO_LIENZO);
            fondo.gameObject.AddComponent<RectMask2D>();
            fondo.gameObject.AddComponent<LienzoGrafoClick>().alClic = CrearVerticeEn;
            lienzo = fondo;
            // Coordenadas locales con origen en la esquina inferior izquierda
            capaAristas = UIFabrica.Estirar(UIFabrica.CrearContenedor(lienzo, "Aristas"));
            capaNodos = UIFabrica.Estirar(UIFabrica.CrearContenedor(lienzo, "Nodos"));

            // Bitácora del algoritmo (debajo del lienzo)
            UIFabrica.CrearScroll(rt, "ScrollBitacora", Paleta.FondoOscuro, out RectTransform contBit);
            UIFabrica.Colocar(contBit.parent.parent, 16, -744, ANCHO_LIENZO, 140);
            textoBitacora = UIFabrica.CrearTexto(contBit, "Bitacora", "", 13, TextAnchor.UpperLeft, new Color(0.75f, 0.95f, 0.85f));

            // Columna derecha: edición
            float x0 = 932f;
            Text t1 = UIFabrica.CrearTexto(rt, "T1", "EDICIÓN DEL GRAFO", 13, TextAnchor.MiddleLeft, Paleta.Cian, true);
            UIFabrica.Colocar(t1, x0, -76, 600, 20);

            btnDirigido = Boton(rt, "Tipo: No dirigido", new Color(0.2f, 0.4f, 0.6f), x0, -100, 200, AlternarDirigido);
            Text lp = UIFabrica.CrearTexto(rt, "LblPeso", "Peso:", 13, TextAnchor.MiddleRight, Color.white);
            UIFabrica.Colocar(lp, x0 + 205, -100, 50, 34);
            inputPeso = UIFabrica.CrearInput(rt, "InputPeso", "1");
            inputPeso.text = "1";
            UIFabrica.Colocar(inputPeso, x0 + 262, -100, 70, 34);
            Boton(rt, "➕ Conectar A → B", new Color(0.12f, 0.52f, 0.32f), x0 + 340, -100, 150, ConectarSeleccion);
            Boton(rt, "✂ Arista", new Color(0.55f, 0.3f, 0.15f), x0 + 498, -100, 114, EliminarAristaSeleccion);

            Boton(rt, "🗑 Eliminar vértice A", new Color(0.6f, 0.2f, 0.2f), x0, -140, 160, EliminarVerticeSeleccion);
            Boton(rt, "🧹 Limpiar todo", new Color(0.3f, 0.32f, 0.4f), x0 + 168, -140, 130, LimpiarTodo);
            Boton(rt, "📦 Ejemplo ponderado", new Color(0.3f, 0.35f, 0.6f), x0 + 306, -140, 150, CargarEjemplo);
            Boton(rt, "⚡ DAG Habilidades", new Color(0.42f, 0.25f, 0.7f), x0 + 464, -140, 148, CargarDAGHabilidades);

            textoSeleccion = UIFabrica.CrearTexto(rt, "Seleccion", "", 13, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.5f));
            UIFabrica.Colocar(textoSeleccion, x0, -180, 612, 24);

            // Algoritmos
            Text t2 = UIFabrica.CrearTexto(rt, "T2", "ALGORITMOS (animación paso a paso)", 13, TextAnchor.MiddleLeft, Paleta.Cian, true);
            UIFabrica.Colocar(t2, x0, -210, 600, 20);
            Boton(rt, "BFS · Anchura (Cola FIFO)", new Color(0.1f, 0.5f, 0.6f), x0, -234, 200, () => Ejecutar(AnimarBFS()));
            Boton(rt, "DFS · Profundidad (Pila LIFO)", new Color(0.1f, 0.45f, 0.7f), x0 + 206, -234, 200, () => Ejecutar(AnimarDFS()));
            Boton(rt, "Dijkstra · Camino mínimo A→B", new Color(0.65f, 0.45f, 0.1f), x0 + 412, -234, 200, () => Ejecutar(AnimarDijkstra()));
            Boton(rt, "Kruskal · MST (Union-Find)", new Color(0.15f, 0.55f, 0.35f), x0, -274, 200, () => Ejecutar(AnimarKruskal()));
            Boton(rt, "Kahn · Orden topológico", new Color(0.5f, 0.3f, 0.65f), x0 + 206, -274, 200, () => Ejecutar(AnimarKahn()));
            btnVelocidad = Boton(rt, "⏱ Velocidad: Normal", new Color(0.25f, 0.3f, 0.4f), x0 + 412, -274, 130, CambiarVelocidad);
            Boton(rt, "■ Detener", new Color(0.5f, 0.2f, 0.2f), x0 + 548, -274, 64, () => { Detener(); Redibujar(); });

            // Matriz y lista de adyacencia en vivo
            Text t3 = UIFabrica.CrearTexto(rt, "T3", "MATRIZ Y LISTA DE ADYACENCIA (en vivo)", 13, TextAnchor.MiddleLeft, Paleta.Cian, true);
            UIFabrica.Colocar(t3, x0, -318, 600, 20);
            UIFabrica.CrearScroll(rt, "ScrollEstructuras", Paleta.FondoOscuro, out RectTransform contEst);
            UIFabrica.Colocar(contEst.parent.parent, x0, -342, 612, 542);
            textoEstructuras = UIFabrica.CrearTexto(contEst, "Estructuras", "", 13, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.98f));

            panel.SetActive(false);
        }

        #endregion

        #region Edición

        private static string ClaveArista(string u, string v, bool dirigido)
        {
            if (dirigido) return u + ">" + v;
            return string.CompareOrdinal(u, v) < 0 ? u + "|" + v : v + "|" + u;
        }

        private string NuevoId()
        {
            string id;
            do
            {
                int n = contadorNodos++;
                id = n < 26 ? ((char)('A' + n)).ToString() : "V" + n;
            } while (grafo.ExisteNodo(id));
            return id;
        }

        private void CrearVerticeEn(Vector2 local)
        {
            if (grafo.CantidadVertices >= 26)
            {
                Anotar("⚠️ Máximo de 26 vértices en el lienzo.", true);
                return;
            }
            Detener();
            string id = NuevoId();
            grafo.AgregarNodo(id, id);
            posiciones[id] = Limitar(local);
            Anotar($"Vértice {id} creado.", true);
            Redibujar();
        }

        private static Vector2 Limitar(Vector2 p)
        {
            return new Vector2(Mathf.Clamp(p.x, 28f, ANCHO_LIENZO - 28f), Mathf.Clamp(p.y, 28f, ALTO_LIENZO - 28f));
        }

        private void SeleccionarNodo(string id)
        {
            if (seleccionA == null || (seleccionA != null && seleccionB != null)) { seleccionA = id; seleccionB = null; }
            else if (id == seleccionA) { seleccionA = null; }
            else seleccionB = id;
            Redibujar();
        }

        private void MoverNodo(string id, Vector2 local)
        {
            if (!posiciones.ContainsKey(id)) return;
            posiciones[id] = Limitar(local);
            Redibujar();
        }

        private float LeerPeso()
        {
            string txt = inputPeso != null ? inputPeso.text.Trim().Replace(',', '.') : "1";
            if (!float.TryParse(txt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float peso) || peso <= 0f)
            {
                Anotar("⚠️ Peso inválido: se usa 1. (Dijkstra requiere pesos positivos)", true);
                peso = 1f;
                if (inputPeso != null) inputPeso.text = "1";
            }
            return Mathf.Min(peso, 999f);
        }

        private void ConectarSeleccion()
        {
            if (seleccionA == null || seleccionB == null)
            {
                Anotar("⚠️ Seleccione dos vértices: origen (A) y destino (B).", true);
                return;
            }
            Detener();
            float peso = LeerPeso();
            grafo.EliminarArista(seleccionA, seleccionB);
            grafo.AgregarArista(seleccionA, seleccionB, peso);
            Anotar($"Arista {seleccionA} {(grafo.EsDirigido ? "→" : "—")} {seleccionB} con peso {peso:0.##}.", true);
            Redibujar();
        }

        private void EliminarAristaSeleccion()
        {
            if (seleccionA == null || seleccionB == null) { Anotar("⚠️ Seleccione los dos extremos de la arista.", true); return; }
            Detener();
            bool ok = grafo.EliminarArista(seleccionA, seleccionB);
            Anotar(ok ? $"Arista {seleccionA}–{seleccionB} eliminada." : "⚠️ No existe esa arista.", true);
            Redibujar();
        }

        private void EliminarVerticeSeleccion()
        {
            if (seleccionA == null) { Anotar("⚠️ Seleccione un vértice.", true); return; }
            Detener();
            grafo.EliminarNodo(seleccionA);
            posiciones.Remove(seleccionA);
            Anotar($"Vértice {seleccionA} eliminado junto con sus aristas.", true);
            seleccionA = seleccionB;
            seleccionB = null;
            Redibujar();
        }

        private void LimpiarTodo()
        {
            Detener();
            grafo = new Grafo<string>(grafo.EsDirigido);
            posiciones.Clear();
            contadorNodos = 0;
            seleccionA = seleccionB = null;
            Anotar("Lienzo vacío. Haga clic para crear vértices.", true);
            Redibujar();
        }

        private void AlternarDirigido()
        {
            Detener();
            // Se reconstruye el grafo con el otro tipo conservando vértices y aristas
            List<Grafo<string>.AristaSimple> aristas = grafo.ObtenerAristas();
            Grafo<string> nuevo = new Grafo<string>(!grafo.EsDirigido);
            foreach (string id in grafo.Nodos.Keys) nuevo.AgregarNodo(id, id);
            for (int i = 0; i < aristas.Count; i++) nuevo.AgregarArista(aristas[i].U, aristas[i].V, aristas[i].Peso);
            grafo = nuevo;
            Anotar(grafo.EsDirigido ? "Grafo DIRIGIDO: cada arista tiene sentido u → v." : "Grafo NO DIRIGIDO: cada arista es bidireccional.", true);
            Redibujar();
        }

        private void CargarEjemplo()
        {
            Detener();
            grafo = new Grafo<string>(false);
            posiciones.Clear();
            seleccionA = "A";
            seleccionB = "F";
            string[] ids = { "A", "B", "C", "D", "E", "F", "G" };
            Vector2[] pos =
            {
                new Vector2(110, 340), new Vector2(300, 540), new Vector2(300, 140), new Vector2(500, 340),
                new Vector2(680, 540), new Vector2(800, 330), new Vector2(660, 130)
            };
            for (int i = 0; i < ids.Length; i++)
            {
                grafo.AgregarNodo(ids[i], ids[i]);
                posiciones[ids[i]] = pos[i];
            }
            contadorNodos = ids.Length;
            grafo.AgregarArista("A", "B", 4); grafo.AgregarArista("A", "C", 2); grafo.AgregarArista("B", "C", 5);
            grafo.AgregarArista("B", "D", 10); grafo.AgregarArista("C", "D", 3); grafo.AgregarArista("B", "E", 7);
            grafo.AgregarArista("D", "E", 4); grafo.AgregarArista("D", "G", 8); grafo.AgregarArista("E", "F", 6);
            grafo.AgregarArista("G", "F", 1); grafo.AgregarArista("C", "G", 12);
            Anotar("Ejemplo cargado: grafo no dirigido ponderado de 7 vértices y 11 aristas.", true);
            if (panel != null) Redibujar();
        }

        /// <summary>Carga el DAG real de habilidades del juego (ideal para demostrar Kahn).</summary>
        private void CargarDAGHabilidades()
        {
            Detener();
            ControladorHabilidadesUI ctrl = ControladorHabilidadesUI.Instance;
            if (ctrl == null || ctrl.sistemaHabilidades == null) { Anotar("⚠️ El sistema de habilidades no está disponible.", true); return; }

            Grafo<Habilidad> dag = ctrl.sistemaHabilidades.Grafo;
            grafo = new Grafo<string>(true);
            posiciones.Clear();

            // Disposición por capas: la capa de un nodo es la longitud del camino más largo desde la raíz
            dag.OrdenamientoTopologicoKahn(out List<string> orden, out bool ciclo);
            Dictionary<string, int> capa = new Dictionary<string, int>();
            foreach (string id in orden)
            {
                if (!capa.ContainsKey(id)) capa[id] = 0;
                foreach (Arista<Habilidad> a in dag.Nodos[id].Vecinos)
                {
                    int c = capa[id] + 1;
                    if (!capa.ContainsKey(a.Destino.Id) || capa[a.Destino.Id] < c) capa[a.Destino.Id] = c;
                }
            }
            int maxCapa = 0;
            Dictionary<int, List<string>> porCapa = new Dictionary<int, List<string>>();
            foreach (string id in orden)
            {
                int c = capa[id];
                maxCapa = Mathf.Max(maxCapa, c);
                if (!porCapa.ContainsKey(c)) porCapa[c] = new List<string>();
                porCapa[c].Add(id);
            }

            Dictionary<string, string> corto = new Dictionary<string, string>();
            foreach (string id in orden)
            {
                string c = id.Replace("HAB_", "");
                corto[id] = c.Length > 9 ? c.Substring(0, 9) : c;
                grafo.AgregarNodo(corto[id], corto[id]);
            }
            foreach (KeyValuePair<int, List<string>> kv in porCapa)
            {
                float x = 70f + kv.Key * (ANCHO_LIENZO - 140f) / Mathf.Max(1, maxCapa);
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    float y = ALTO_LIENZO - (i + 1) * ALTO_LIENZO / (kv.Value.Count + 1);
                    posiciones[corto[kv.Value[i]]] = new Vector2(x, y);
                }
            }
            foreach (string id in orden)
                foreach (Arista<Habilidad> a in dag.Nodos[id].Vecinos)
                    grafo.AgregarArista(corto[id], corto[a.Destino.Id], 1f);

            seleccionA = corto.ContainsKey("HAB_BASE") ? corto["HAB_BASE"] : null;
            seleccionB = null;
            Anotar($"DAG de habilidades cargado: {grafo.CantidadVertices} vértices dirigidos. Pulse \"Kahn\" para ver el orden de desbloqueo.", true);
            Redibujar();
        }

        #endregion

        #region Dibujo

        private void Anotar(string linea, bool reiniciar = false)
        {
            if (reiniciar) bitacora.Length = 0;
            bitacora.AppendLine(linea);
            if (textoBitacora != null) textoBitacora.text = bitacora.ToString();
        }

        private void Redibujar()
        {
            if (lienzo == null) return;
            for (int i = capaAristas.childCount - 1; i >= 0; i--) Destroy(capaAristas.GetChild(i).gameObject);
            for (int i = capaNodos.childCount - 1; i >= 0; i--) Destroy(capaNodos.GetChild(i).gameObject);

            const float radio = 24f;
            List<Grafo<string>.AristaSimple> aristas = grafo.ObtenerAristas();
            for (int i = 0; i < aristas.Count; i++)
            {
                if (!posiciones.ContainsKey(aristas[i].U) || !posiciones.ContainsKey(aristas[i].V)) continue;
                Vector2 a = posiciones[aristas[i].U], b = posiciones[aristas[i].V];
                Vector2 dir = (b - a).normalized;
                string clave = ClaveArista(aristas[i].U, aristas[i].V, grafo.EsDirigido);
                bool resaltada = colorArista.TryGetValue(clave, out Color c);
                if (!resaltada) c = new Color(0.45f, 0.55f, 0.7f, 0.85f);

                // En grafos dirigidos con arista de ida y vuelta se separan ligeramente
                Vector2 desvio = grafo.EsDirigido && grafo.ExisteArista(aristas[i].V, aristas[i].U) ? new Vector2(-dir.y, dir.x) * 7f : Vector2.zero;
                Vector2 pa = a + dir * radio + desvio, pb = b - dir * radio + desvio;
                UIFabrica.CrearLineaUI(capaAristas, pa, pb, c, resaltada ? 5f : 2.5f);

                if (grafo.EsDirigido)
                {
                    Image punta = UIFabrica.CrearImagen(capaAristas, "Punta", GraficosProc.Triangulo, c);
                    RectTransform rp = punta.rectTransform;
                    rp.anchorMin = rp.anchorMax = Vector2.zero;
                    rp.pivot = new Vector2(0.5f, 0.5f);
                    rp.anchoredPosition = pb - dir * 6f;
                    rp.sizeDelta = new Vector2(16, 16);
                    rp.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                }

                Text peso = UIFabrica.CrearTexto(capaAristas, "Peso", aristas[i].Peso.ToString("0.##"), 13, TextAnchor.MiddleCenter, resaltada ? Color.white : new Color(1f, 0.88f, 0.55f), true);
                RectTransform rw = peso.rectTransform;
                rw.anchorMin = rw.anchorMax = Vector2.zero;
                rw.pivot = new Vector2(0.5f, 0.5f);
                rw.anchoredPosition = (pa + pb) * 0.5f + new Vector2(-dir.y, dir.x) * 12f;
                rw.sizeDelta = new Vector2(50, 20);
            }

            foreach (KeyValuePair<string, Vector2> kv in posiciones)
            {
                string id = kv.Key;
                bool esA = id == seleccionA, esB = id == seleccionB;
                Image borde = UIFabrica.CrearImagen(capaNodos, "Borde", GraficosProc.Circulo, esA ? Paleta.Dorado : (esB ? new Color(1f, 0.45f, 0.75f) : new Color(0.02f, 0.03f, 0.05f)));
                RectTransform rb = borde.rectTransform;
                rb.anchorMin = rb.anchorMax = Vector2.zero;
                rb.pivot = new Vector2(0.5f, 0.5f);
                rb.anchoredPosition = kv.Value;
                rb.sizeDelta = Vector2.one * (radio * 2f + (esA || esB ? 10f : 4f));

                Image nodo = UIFabrica.CrearImagen(capaNodos, "Nodo_" + id, GraficosProc.Circulo, colorNodo.TryGetValue(id, out Color cn) ? cn : COLOR_BASE);
                nodo.raycastTarget = true;
                RectTransform rn = nodo.rectTransform;
                rn.anchorMin = rn.anchorMax = Vector2.zero;
                rn.pivot = new Vector2(0.5f, 0.5f);
                rn.anchoredPosition = kv.Value;
                rn.sizeDelta = Vector2.one * radio * 2f;
                NodoGrafoUI ui = nodo.gameObject.AddComponent<NodoGrafoUI>();
                ui.id = id;
                ui.alClic = SeleccionarNodo;
                ui.alArrastrar = MoverNodo;

                Text t = UIFabrica.CrearTexto(nodo.transform, "Id", id, id.Length > 3 ? 9 : 15, TextAnchor.MiddleCenter, Color.white, true);
                UIFabrica.Estirar(t);

                if (etiquetaNodo.TryGetValue(id, out string extra))
                {
                    Text te = UIFabrica.CrearTexto(capaNodos, "Extra", extra, 12, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f), true);
                    RectTransform re = te.rectTransform;
                    re.anchorMin = re.anchorMax = Vector2.zero;
                    re.pivot = new Vector2(0.5f, 0f);
                    re.anchoredPosition = kv.Value + new Vector2(0, radio + 3f);
                    re.sizeDelta = new Vector2(90, 18);
                }
            }

            UIFabrica.Rotular(btnDirigido, grafo.EsDirigido ? "Tipo: Dirigido (u → v)" : "Tipo: No dirigido (u — v)");
            if (textoSeleccion != null)
                textoSeleccion.text = $"Origen A: <b>{seleccionA ?? "—"}</b>     Destino B: <b>{seleccionB ?? "—"}</b>     |V| = {grafo.CantidadVertices}   |E| = {aristas.Count}";

            if (textoEstructuras != null)
            {
                textoEstructuras.text =
                    "<color=#66ddff><b>MATRIZ DE ADYACENCIA</b></color>  (fila = origen, columna = destino, '.' = sin arista)\n" +
                    grafo.MatrizAdyacenciaToString() +
                    "\n<color=#66ddff><b>LISTA DE ADYACENCIA</b></color>  vértice → vecino(peso)\n" +
                    grafo.ListaAdyacenciaToString();
            }
        }

        #endregion

        #region Animación de Algoritmos

        private void Detener()
        {
            if (animacion != null) StopCoroutine(animacion);
            animacion = null;
            colorNodo.Clear();
            colorArista.Clear();
            etiquetaNodo.Clear();
        }

        private void Ejecutar(IEnumerator rutina)
        {
            Detener();
            if (grafo.CantidadVertices == 0) { Anotar("⚠️ El grafo está vacío.", true); return; }
            animacion = StartCoroutine(rutina);
        }

        private void CambiarVelocidad()
        {
            if (pausaPaso > 0.6f) { pausaPaso = 0.3f; UIFabrica.Rotular(btnVelocidad, "⏱ Velocidad: Rápida"); }
            else if (pausaPaso > 0.2f) { pausaPaso = 1.4f; UIFabrica.Rotular(btnVelocidad, "⏱ Velocidad: Lenta"); }
            else { pausaPaso = 0.7f; UIFabrica.Rotular(btnVelocidad, "⏱ Velocidad: Normal"); }
        }

        private WaitForSecondsRealtime Paso()
        {
            Redibujar();
            return new WaitForSecondsRealtime(pausaPaso);
        }

        private string Inicio()
        {
            if (seleccionA != null && grafo.ExisteNodo(seleccionA)) return seleccionA;
            foreach (string id in grafo.Nodos.Keys) return id;
            return null;
        }

        private void Marcar(string u, string v, Color c)
        {
            colorArista[ClaveArista(u, v, grafo.EsDirigido)] = c;
        }

        private IEnumerator AnimarBFS()
        {
            string inicio = Inicio();
            Anotar($"▶ BFS desde {inicio} — estructura auxiliar: COLA (FIFO). Complejidad O(V + E).", true);
            HashSet<string> visitados = new HashSet<string> { inicio };
            Queue<string> cola = new Queue<string>();
            cola.Enqueue(inicio);
            List<string> orden = new List<string>();

            while (cola.Count > 0)
            {
                string actual = cola.Dequeue();
                orden.Add(actual);
                colorNodo[actual] = COLOR_ACTUAL;
                foreach (Arista<string> a in grafo.Nodos[actual].Vecinos)
                {
                    if (visitados.Add(a.Destino.Id))
                    {
                        cola.Enqueue(a.Destino.Id);
                        Marcar(actual, a.Destino.Id, COLOR_VISITADO);
                    }
                }
                Anotar($"Visita {actual} · Cola: [{string.Join(", ", cola.ToArray())}]");
                yield return Paso();
                colorNodo[actual] = COLOR_VISITADO;
            }
            Anotar($"✓ Recorrido BFS: {string.Join(" → ", orden)}");
            Redibujar();
            animacion = null;
        }

        private IEnumerator AnimarDFS()
        {
            string inicio = Inicio();
            Anotar($"▶ DFS desde {inicio} — estructura auxiliar: PILA (LIFO). Complejidad O(V + E).", true);
            HashSet<string> visitados = new HashSet<string>();
            Stack<string> pila = new Stack<string>();
            Dictionary<string, string> padre = new Dictionary<string, string>();
            pila.Push(inicio);
            List<string> orden = new List<string>();

            while (pila.Count > 0)
            {
                string actual = pila.Pop();
                if (!visitados.Add(actual)) continue;
                orden.Add(actual);
                colorNodo[actual] = COLOR_ACTUAL;
                if (padre.TryGetValue(actual, out string p)) Marcar(p, actual, COLOR_VISITADO);

                List<Arista<string>> vecinos = grafo.Nodos[actual].Vecinos;
                for (int i = vecinos.Count - 1; i >= 0; i--)
                {
                    if (!visitados.Contains(vecinos[i].Destino.Id))
                    {
                        pila.Push(vecinos[i].Destino.Id);
                        padre[vecinos[i].Destino.Id] = actual;
                    }
                }
                Anotar($"Visita {actual} · Pila (tope primero): [{string.Join(", ", pila.ToArray())}]");
                yield return Paso();
                colorNodo[actual] = COLOR_VISITADO;
            }
            Anotar($"✓ Recorrido DFS: {string.Join(" → ", orden)}");
            Redibujar();
            animacion = null;
        }

        private IEnumerator AnimarDijkstra()
        {
            string inicio = Inicio();
            Anotar($"▶ Dijkstra desde {inicio} — relaja aristas eligiendo siempre el vértice pendiente de menor distancia.", true);
            List<string> orden = grafo.Dijkstra(inicio, out Dictionary<string, float> dist, out Dictionary<string, string> prev);

            for (int i = 0; i < orden.Count; i++)
            {
                string v = orden[i];
                colorNodo[v] = COLOR_ACTUAL;
                etiquetaNodo[v] = "d=" + dist[v].ToString("0.##");
                if (prev.TryGetValue(v, out string p)) Marcar(p, v, COLOR_VISITADO);
                Anotar($"Fija {v} con distancia {dist[v]:0.##}" + (prev.ContainsKey(v) ? $" (llega desde {prev[v]})" : " (origen)"));
                yield return Paso();
                colorNodo[v] = COLOR_VISITADO;
            }

            foreach (string id in grafo.Nodos.Keys)
                if (!etiquetaNodo.ContainsKey(id)) etiquetaNodo[id] = "d=∞";

            if (seleccionB != null && grafo.ExisteNodo(seleccionB))
            {
                List<string> camino = grafo.CaminoMinimo(inicio, seleccionB, out float costo);
                if (camino == null) Anotar($"✕ {seleccionB} es inalcanzable desde {inicio}.");
                else
                {
                    for (int i = 0; i < camino.Count; i++)
                    {
                        colorNodo[camino[i]] = COLOR_CAMINO;
                        if (i > 0) Marcar(camino[i - 1], camino[i], COLOR_CAMINO);
                    }
                    Anotar($"✓ Camino mínimo {inicio} → {seleccionB}: {string.Join(" → ", camino)} · costo total {costo:0.##}");
                }
            }
            else Anotar("✓ Distancias mínimas calculadas. Seleccione un destino B para resaltar el camino.");
            Redibujar();
            animacion = null;
        }

        private IEnumerator AnimarKruskal()
        {
            Anotar("▶ Kruskal — ordena las aristas por peso y las acepta si no forman ciclo (Union-Find). Complejidad O(E log E).", true);
            if (grafo.EsDirigido) Anotar("ℹ️ El MST se define sobre grafos no dirigidos: las aristas se tratan sin sentido.");

            List<string> ids = new List<string>(grafo.Nodos.Keys);
            Dictionary<string, int> indice = new Dictionary<string, int>();
            DisjointSet uf = new DisjointSet();
            for (int i = 0; i < ids.Count; i++) { indice[ids[i]] = i; uf.MakeSet(i); }

            List<Grafo<string>.AristaSimple> aristas = grafo.ObtenerAristas();
            aristas.Sort((a, b) => a.Peso.CompareTo(b.Peso));
            float total = 0f;
            int aceptadas = 0;

            for (int i = 0; i < aristas.Count; i++)
            {
                Grafo<string>.AristaSimple ar = aristas[i];
                bool ok = uf.Union(indice[ar.U], indice[ar.V]);
                Marcar(ar.U, ar.V, ok ? COLOR_VISITADO : COLOR_DESCARTE);
                if (ok)
                {
                    total += ar.Peso;
                    aceptadas++;
                    colorNodo[ar.U] = COLOR_VISITADO;
                    colorNodo[ar.V] = COLOR_VISITADO;
                    Anotar($"✓ {ar.U}—{ar.V} ({ar.Peso:0.##}) aceptada: Find({ar.U}) ≠ Find({ar.V}) → Union");
                }
                else Anotar($"✕ {ar.U}—{ar.V} ({ar.Peso:0.##}) descartada: mismos representantes, formaría ciclo");
                yield return Paso();
            }

            Anotar(uf.Conjuntos <= 1
                ? $"✓ MST completo: {aceptadas} aristas (V − 1), peso total {total:0.##}"
                : $"✓ Bosque de expansión mínima: {aceptadas} aristas, peso {total:0.##}, {uf.Conjuntos} componentes (grafo no conexo)");
            Redibujar();
            animacion = null;
        }

        private IEnumerator AnimarKahn()
        {
            Anotar("▶ Kahn — orden topológico: extrae repetidamente los vértices con grado de entrada 0.", true);
            if (!grafo.EsDirigido)
            {
                Anotar("✕ El orden topológico solo existe en grafos DIRIGIDOS acíclicos (DAG). Cambie el tipo a \"Dirigido\" o cargue el DAG de habilidades.");
                animacion = null;
                yield break;
            }

            Dictionary<string, int> grado = grafo.CalcularGradosEntrada();
            foreach (KeyValuePair<string, int> kv in grado) etiquetaNodo[kv.Key] = "in=" + kv.Value;
            Queue<string> cola = new Queue<string>();
            foreach (KeyValuePair<string, int> kv in grado) if (kv.Value == 0) cola.Enqueue(kv.Key);
            yield return Paso();

            List<string> orden = new List<string>();
            while (cola.Count > 0)
            {
                string u = cola.Dequeue();
                orden.Add(u);
                colorNodo[u] = COLOR_ACTUAL;
                etiquetaNodo[u] = "#" + orden.Count;
                foreach (Arista<string> a in grafo.Nodos[u].Vecinos)
                {
                    string v = a.Destino.Id;
                    grado[v]--;
                    Marcar(u, v, COLOR_VISITADO);
                    if (!orden.Contains(v)) etiquetaNodo[v] = "in=" + grado[v];
                    if (grado[v] == 0) cola.Enqueue(v);
                }
                Anotar($"Extrae {u} (grado de entrada 0) · Cola: [{string.Join(", ", cola.ToArray())}]");
                yield return Paso();
                colorNodo[u] = COLOR_VISITADO;
            }

            if (orden.Count < grafo.CantidadVertices)
            {
                foreach (string id in grafo.Nodos.Keys) if (!orden.Contains(id)) colorNodo[id] = COLOR_DESCARTE;
                Anotar($"✕ CICLO DETECTADO: solo se ordenaron {orden.Count} de {grafo.CantidadVertices} vértices. Los rojos pertenecen a un ciclo o dependen de él.");
            }
            else Anotar($"✓ Orden topológico: {string.Join(" → ", orden)}");
            Redibujar();
            animacion = null;
        }

        #endregion
    }
}
