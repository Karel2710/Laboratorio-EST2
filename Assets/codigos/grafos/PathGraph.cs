using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Gestor central del grafo de caminos (grafo no dirigido ponderado).
/// - Si no hay nodos asignados en el Inspector, se auto-construye a partir de levelManager
///   (ruta principal + rutas alternativas según la topología del nivel).
/// - Ejecuta Dijkstra para hallar la ruta óptima y la recalcula en vivo al bloquear aristas.
/// - Redirige a todos los enemigos vivos cuando el grafo cambia.
/// </summary>
public class PathGraph : MonoBehaviour
{
    public static PathGraph main;

    [Header("Nodos del Grafo")]
    [Tooltip("Opcional: arrastra aquí los PathNode de la escena. Si queda vacío se auto-construye.")]
    public List<PathNode> nodos = new List<PathNode>();

    [Header("Opciones de Visualización")]
    public bool mostrarGizmos = true;
    public Color colorRutaActual = new Color(0f, 1f, 0.4f);

    /// <summary>Arista retirada temporal o permanentemente del grafo.</summary>
    public class Bloqueo
    {
        public PathNode a;
        public PathNode b;
        public float multiplicador;
        public float tiempoRestante; // < 0 = permanente
        public float duracionTotal;
    }

    public readonly List<Bloqueo> bloqueos = new List<Bloqueo>();

    /// <summary>Se invoca cada vez que cambian aristas, pesos o la ruta óptima.</summary>
    public event System.Action OnGrafoCambiado;

    public int TopologiaActual { get; private set; } = 0;
    public bool EsAutoGenerado { get; private set; } = false;
    public int RecalculosDijkstra { get; private set; } = 0;

    private List<PathNode> rutaActual = new List<PathNode>();
    private bool nodosManuales = false;

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    /// <summary>Devuelve el grafo activo; lo crea por código si la escena no tiene uno.</summary>
    public static PathGraph Asegurar()
    {
        if (main != null) return main;
        PathGraph existente = FindFirstObjectByType<PathGraph>();
        if (existente != null)
        {
            main = existente;
            return main;
        }
        GameObject go = new GameObject("PathGraph_Auto");
        return go.AddComponent<PathGraph>();
    }

    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }
        main = this;
        nodos.RemoveAll(n => n == null);
        nodosManuales = nodos.Count > 0;
    }

    private void Start()
    {
        if (nodosManuales)
        {
            AsignarIdsAutomaticos();
            CalcularRutaPrincipal();
        }
        else if (nodos.Count == 0)
        {
            ConstruirAutomatico(TopologiaActual);
        }
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private void Update()
    {
        // Barricadas temporales: al expirar se restaura la arista y se recalcula Dijkstra
        bool expiro = false;
        for (int i = bloqueos.Count - 1; i >= 0; i--)
        {
            Bloqueo b = bloqueos[i];
            if (b.tiempoRestante < 0f) continue;
            b.tiempoRestante -= Time.deltaTime;
            if (b.tiempoRestante <= 0f)
            {
                bloqueos.RemoveAt(i);
                Reconectar(b);
                expiro = true;
            }
        }
        if (expiro) CalcularRutaPrincipal();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Construcción Automática del Grafo

    public void AsignarIdsAutomaticos()
    {
        for (int i = 0; i < nodos.Count; i++)
        {
            if (nodos[i] != null) nodos[i].nodeId = i;
        }
    }

    public void RegistrarNodo(PathNode nodo)
    {
        if (nodo != null && !nodos.Contains(nodo))
        {
            nodo.nodeId = nodos.Count;
            nodos.Add(nodo);
        }
    }

    private static Vector3? BuscarPuntoInicio(levelManager lm)
    {
        if (lm != null && lm.Start != null) return lm.Start.position;

        // El campo startPoint puede no estar asignado en el Inspector: buscar el objeto "Start"
        if (lm != null && lm.Path != null && lm.Path.Length > 0 && lm.Path[0] != null && lm.Path[0].parent != null)
        {
            Transform t = lm.Path[0].parent.Find("Start");
            if (t != null) return t.position;
        }
        GameObject go = GameObject.Find("Start");
        if (go != null) return go.transform.position;
        return null;
    }

    private PathNode CrearNodo(Vector3 pos, string etiqueta)
    {
        GameObject go = new GameObject(etiqueta);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        PathNode n = go.AddComponent<PathNode>();
        n.nodeId = nodos.Count;
        nodos.Add(n);
        go.name = $"N{n.nodeId}_{etiqueta}";
        return n;
    }

    private static void Conectar(PathNode a, PathNode b, float mult)
    {
        a.ConectarCon(b, mult);
        b.ConectarCon(a, mult);
    }

    /// <summary>
    /// Construye el grafo a partir de levelManager (Start + Path) y añade las rutas
    /// alternativas de la topología indicada:
    ///   0 = principal + Sendero Este | 1 = + Pantano Central | 2 = + Sendero Norte.
    /// </summary>
    public void ConstruirAutomatico(int topologia)
    {
        if (nodosManuales) { CalcularRutaPrincipal(); return; }

        TopologiaActual = Mathf.Clamp(topologia, 0, 2);
        EsAutoGenerado = true;

        for (int i = 0; i < nodos.Count; i++)
        {
            if (nodos[i] != null) Destroy(nodos[i].gameObject);
        }
        nodos.Clear();
        bloqueos.Clear();
        rutaActual = new List<PathNode>();

        levelManager lm = levelManager.main != null ? levelManager.main : FindFirstObjectByType<levelManager>();
        List<Vector3> puntos = new List<Vector3>();

        Vector3? inicio = BuscarPuntoInicio(lm);
        if (inicio.HasValue) puntos.Add(inicio.Value);

        if (lm != null && lm.Path != null)
        {
            for (int i = 0; i < lm.Path.Length; i++)
            {
                if (lm.Path[i] != null) puntos.Add(lm.Path[i].position);
            }
        }

        if (puntos.Count < 2)
        {
            Debug.LogWarning("[PathGraph] No hay waypoints suficientes en levelManager para construir el grafo.");
            OnGrafoCambiado?.Invoke();
            return;
        }

        // 1. Ruta principal (terreno Camino ×1.0)
        List<PathNode> principal = new List<PathNode>();
        for (int i = 0; i < puntos.Count; i++)
        {
            PathNode n = CrearNodo(puntos[i], i == 0 ? "Inicio" : (i == puntos.Count - 1 ? "Base" : "Ruta"));
            n.esInicio = i == 0;
            n.esFinal = i == puntos.Count - 1;
            principal.Add(n);
            if (i > 0) Conectar(principal[i - 1], n, 1f);
        }

        // 2. Rutas alternativas (bifurcaciones) para que Dijkstra tenga caminos reales que evaluar
        if (principal.Count >= 11)
        {
            // Sendero Este: desde el waypoint 6 baja por el borde derecho hasta el tramo final
            PathNode n6 = principal[6], n9 = principal[principal.Count - 2], nFin = principal[principal.Count - 1];
            float xE = Mathf.Lerp(n6.transform.position.x, nFin.transform.position.x, 0.67f);
            PathNode union = CrearNodo(new Vector3(xE, nFin.transform.position.y, 0f), "Union");
            n9.DesconectarDe(nFin);
            nFin.DesconectarDe(n9);
            Conectar(n9, union, 1f);
            Conectar(union, nFin, 1f);
            AgregarDesvio(n6, union, new[] { new Vector3(xE, n6.transform.position.y, 0f) }, TipoTerreno.Sendero, "SenderoEste");

            if (TopologiaActual >= 1)
            {
                // Pantano Central: atajo muy lento que corta el gran bucle derecho
                PathNode n4 = principal[4], n8 = principal[8];
                AgregarDesvio(n4, n8, new[] { new Vector3(n4.transform.position.x, n8.transform.position.y, 0f) }, TipoTerreno.Pantano, "Pantano");
            }

            if (TopologiaActual >= 2)
            {
                // Sendero Norte: rodeo paralelo en la zona superior
                PathNode n3 = principal[3], n5 = principal[5];
                AgregarDesvio(n3, n5, new[] { new Vector3(n3.transform.position.x, n5.transform.position.y, 0f) }, TipoTerreno.Sendero, "SenderoNorte");
            }
        }
        else if (principal.Count >= 5)
        {
            // Mapa genérico: un rodeo entre el segundo y el penúltimo nodo
            PathNode a = principal[1], b = principal[principal.Count - 2];
            Vector3 medio = (a.transform.position + b.transform.position) * 0.5f;
            Vector3 dir = (b.transform.position - a.transform.position).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f) * 2.2f;
            AgregarDesvio(a, b, new[] { medio + perp }, TopologiaActual >= 1 ? TipoTerreno.Pantano : TipoTerreno.Sendero, "Rodeo");
        }

        Debug.Log($"[PathGraph] Grafo auto-construido: {nodos.Count} nodos, {ObtenerAristas().Count} aristas (topología {TopologiaActual + 1}).");
        CalcularRutaPrincipal();
    }

    /// <summary>
    /// Añade una ruta alternativa entre dos nodos pasando por puntos intermedios.
    /// El multiplicador se ajusta para que el desvío cueste más que la ruta principal:
    /// así Dijkstra solo lo elige cuando el camino principal queda bloqueado.
    /// </summary>
    private void AgregarDesvio(PathNode desde, PathNode hasta, Vector3[] intermedios, TipoTerreno terreno, string etiqueta)
    {
        float costoPrincipal = float.MaxValue;
        List<PathNode> rutaBase = Dijkstra(desde, hasta);
        if (rutaBase != null) costoPrincipal = hasta.distancia;

        float longitud = 0f;
        Vector3 previo = desde.transform.position;
        for (int i = 0; i < intermedios.Length; i++)
        {
            longitud += Vector3.Distance(previo, intermedios[i]);
            previo = intermedios[i];
        }
        longitud += Vector3.Distance(previo, hasta.transform.position);

        float mult = PathNode.MultiplicadorDeTerreno(terreno);
        if (costoPrincipal < float.MaxValue && longitud > 0.01f)
        {
            mult = Mathf.Max(mult, 1.12f * costoPrincipal / longitud);
        }

        PathNode anterior = desde;
        for (int i = 0; i < intermedios.Length; i++)
        {
            PathNode n = CrearNodo(intermedios[i], etiqueta);
            Conectar(anterior, n, mult);
            anterior = n;
        }
        Conectar(anterior, hasta, mult);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Dijkstra

    /// <summary>
    /// Algoritmo de Dijkstra. Devuelve la lista de nodos del camino de menor costo desde
    /// <paramref name="inicio"/> hasta <paramref name="fin"/>, o null si no existe ruta.
    /// Si <paramref name="fin"/> es null calcula las distancias hacia todos los nodos.
    /// Complejidad: O(V^2) con extracción lineal del mínimo (grafo pequeño de Tower Defense).
    /// </summary>
    public List<PathNode> Dijkstra(PathNode inicio, PathNode fin)
    {
        if (inicio == null) return null;

        // 1. Inicialización
        for (int i = 0; i < nodos.Count; i++)
        {
            PathNode n = nodos[i];
            if (n == null) continue;
            n.distancia = float.MaxValue;
            n.previo = null;
            n.visitado = false;
        }
        inicio.distancia = 0f;

        // 2. Selección del mínimo + relajación de aristas
        while (true)
        {
            PathNode actual = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < nodos.Count; i++)
            {
                PathNode n = nodos[i];
                if (n != null && !n.visitado && n.distancia < minDist)
                {
                    minDist = n.distancia;
                    actual = n;
                }
            }

            if (actual == null) break;      // el resto es inalcanzable
            actual.visitado = true;
            if (actual == fin) break;       // ruta óptima hallada

            for (int i = 0; i < actual.vecinos.Count; i++)
            {
                PathNode vecino = actual.vecinos[i];
                if (vecino == null || vecino.visitado) continue;
                float nueva = actual.distancia + actual.PesoHacia(vecino);
                if (nueva < vecino.distancia)
                {
                    vecino.distancia = nueva;
                    vecino.previo = actual;
                }
            }
        }

        RecalculosDijkstra++;
        if (fin == null) return null;
        if (fin.distancia == float.MaxValue) return null;

        // 3. Reconstrucción del camino siguiendo los punteros 'previo'
        List<PathNode> camino = new List<PathNode>();
        PathNode p = fin;
        while (p != null)
        {
            camino.Insert(0, p);
            p = p.previo;
        }
        return camino;
    }

    /// <summary>
    /// Ejecuta Dijkstra desde la base hacia todos los nodos (el grafo es no dirigido) y guarda
    /// en cada nodo su costo hasta la base y el siguiente salto. Con una sola ejecución todos
    /// los enemigos, estén donde estén, conocen su ruta óptima.
    /// </summary>
    private void CalcularArbolHaciaFin()
    {
        PathNode fin = ObtenerNodoFin();
        for (int i = 0; i < nodos.Count; i++)
        {
            if (nodos[i] == null) continue;
            nodos[i].costoAFin = float.MaxValue;
            nodos[i].siguienteHaciaFin = null;
        }
        if (fin == null) return;

        Dijkstra(fin, null);
        for (int i = 0; i < nodos.Count; i++)
        {
            if (nodos[i] == null) continue;
            nodos[i].costoAFin = nodos[i].distancia;
            nodos[i].siguienteHaciaFin = nodos[i].previo;
        }
    }

    /// <summary>Ruta óptima desde un nodo cualquiera hasta la base (incluye el nodo inicial).</summary>
    public List<PathNode> RutaDesdeNodo(PathNode desde)
    {
        if (desde == null || desde.costoAFin == float.MaxValue) return null;
        List<PathNode> ruta = new List<PathNode>();
        PathNode p = desde;
        int guard = 0;
        while (p != null && guard++ < 512)
        {
            ruta.Add(p);
            if (p.esFinal) break;
            p = p.siguienteHaciaFin;
        }
        return ruta;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Ruta Principal, Bloqueos y Recálculo en Vivo

    /// <summary>
    /// Recalcula la ruta óptima inicio → base y redirige a todos los enemigos vivos.
    /// </summary>
    public bool CalcularRutaPrincipal()
    {
        PathNode inicio = ObtenerNodoInicio();
        PathNode fin = ObtenerNodoFin();
        if (inicio == null || fin == null)
        {
            Debug.LogWarning("[PathGraph] Falta el nodo de inicio o el nodo final.");
            return false;
        }

        CalcularArbolHaciaFin();
        List<PathNode> nueva = RutaDesdeNodo(inicio);
        if (nueva == null || nueva.Count < 2)
        {
            Debug.LogWarning("[PathGraph] Dijkstra no encontró ruta entre inicio y fin.");
            return false;
        }

        rutaActual = nueva;
        Debug.Log("[PathGraph] " + DescribirRuta());
        NotificarEnemigos();
        OnGrafoCambiado?.Invoke();
        return true;
    }

    private bool ExisteRutaInicioFin()
    {
        PathNode inicio = ObtenerNodoInicio();
        PathNode fin = ObtenerNodoFin();
        return inicio != null && fin != null && Dijkstra(inicio, fin) != null;
    }

    private void Reconectar(Bloqueo b)
    {
        if (b.a == null || b.b == null) return;
        Conectar(b.a, b.b, b.multiplicador);
    }

    /// <summary>
    /// Retira la arista (nodoA, nodoB) del grafo y recalcula Dijkstra al instante.
    /// Si el bloqueo dejara la base inalcanzable se revierte y devuelve false.
    /// </summary>
    /// <param name="duracion">Segundos que dura el bloqueo; negativo = permanente.</param>
    public bool BloquearArista(PathNode nodoA, PathNode nodoB, float duracion = -1f)
    {
        if (nodoA == null || nodoB == null || !nodoA.vecinos.Contains(nodoB)) return false;

        Bloqueo bloqueo = new Bloqueo
        {
            a = nodoA,
            b = nodoB,
            multiplicador = nodoA.MultiplicadorHacia(nodoB),
            tiempoRestante = duracion,
            duracionTotal = duracion
        };

        nodoA.DesconectarDe(nodoB);
        nodoB.DesconectarDe(nodoA);

        if (!ExisteRutaInicioFin())
        {
            // No se puede aislar la base: se restaura la arista
            Reconectar(bloqueo);
            CalcularArbolHaciaFin();
            return false;
        }

        bloqueos.Add(bloqueo);
        Debug.Log($"[PathGraph] Arista bloqueada entre N{nodoA.nodeId} y N{nodoB.nodeId}. Recalculando Dijkstra...");
        CalcularRutaPrincipal();
        return true;
    }

    /// <summary>Restaura una arista bloqueada y recalcula la ruta.</summary>
    public void DesbloquearArista(PathNode nodoA, PathNode nodoB)
    {
        if (nodoA == null || nodoB == null) return;
        for (int i = bloqueos.Count - 1; i >= 0; i--)
        {
            Bloqueo b = bloqueos[i];
            if ((b.a == nodoA && b.b == nodoB) || (b.a == nodoB && b.b == nodoA))
            {
                bloqueos.RemoveAt(i);
                Reconectar(b);
                CalcularRutaPrincipal();
                return;
            }
        }
        // Sin registro previo: conexión directa (compatibilidad)
        Conectar(nodoA, nodoB, 1f);
        CalcularRutaPrincipal();
    }

    public void DesbloquearTodo()
    {
        for (int i = 0; i < bloqueos.Count; i++) Reconectar(bloqueos[i]);
        bool habia = bloqueos.Count > 0;
        bloqueos.Clear();
        if (habia) CalcularRutaPrincipal();
    }

    public List<PathNode> ObtenerRutaActual() => rutaActual;

    public float CostoRutaActual()
    {
        PathNode inicio = ObtenerNodoInicio();
        return inicio != null ? inicio.costoAFin : 0f;
    }

    public string DescribirRuta()
    {
        if (rutaActual == null || rutaActual.Count == 0) return "Sin ruta";
        StringBuilder sb = new StringBuilder("Ruta óptima (Dijkstra): ");
        for (int i = 0; i < rutaActual.Count; i++)
        {
            sb.Append('N').Append(rutaActual[i].nodeId);
            if (i < rutaActual.Count - 1) sb.Append(" → ");
        }
        sb.Append($"  | costo {CostoRutaActual():F1}");
        return sb.ToString();
    }

    /// <summary>
    /// Redirige a cada enemigo vivo por su nueva ruta óptima. Un enemigo que va por la arista
    /// (previo → objetivo) puede seguir hacia 'objetivo' o regresar a 'previo': se elige el de menor costo.
    /// </summary>
    private void NotificarEnemigos()
    {
        List<enemyMovement> lista = enemyMovement.Activos;
        for (int i = 0; i < lista.Count; i++)
        {
            enemyMovement e = lista[i];
            if (e == null) continue;

            PathNode objetivo = e.NodoObjetivo;
            PathNode previo = e.NodoPrevio;
            Vector3 pos = e.transform.position;
            PathNode elegido = null;

            if (objetivo == null)
            {
                elegido = NodoMasCercano(pos, true);
            }
            else
            {
                bool aristaVigente = previo == null || previo.vecinos.Contains(objetivo);
                float mult = aristaVigente && previo != null ? previo.MultiplicadorHacia(objetivo) : 1f;
                float costoObjetivo = objetivo.costoAFin == float.MaxValue ? float.MaxValue
                    : Vector3.Distance(pos, objetivo.transform.position) * mult + objetivo.costoAFin;
                float costoPrevio = (previo == null || previo.costoAFin == float.MaxValue) ? float.MaxValue
                    : Vector3.Distance(pos, previo.transform.position) * mult + previo.costoAFin;

                if (costoObjetivo == float.MaxValue && costoPrevio == float.MaxValue) elegido = NodoMasCercano(pos, true);
                else elegido = costoObjetivo <= costoPrevio ? objetivo : previo;
            }

            List<PathNode> ruta = RutaDesdeNodo(elegido);
            if (ruta != null && ruta.Count > 0) e.ActualizarRuta(ruta);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Helpers

    public PathNode ObtenerNodoInicio()
    {
        for (int i = 0; i < nodos.Count; i++)
            if (nodos[i] != null && nodos[i].esInicio) return nodos[i];
        return null;
    }

    public PathNode ObtenerNodoFin()
    {
        for (int i = 0; i < nodos.Count; i++)
            if (nodos[i] != null && nodos[i].esFinal) return nodos[i];
        return null;
    }

    public PathNode NodoMasCercano(Vector3 posicion, bool soloConRuta = false)
    {
        PathNode cercano = null;
        float menorDist = float.MaxValue;
        for (int i = 0; i < nodos.Count; i++)
        {
            PathNode n = nodos[i];
            if (n == null) continue;
            if (soloConRuta && n.costoAFin == float.MaxValue) continue;
            float d = Vector3.Distance(posicion, n.transform.position);
            if (d < menorDist)
            {
                menorDist = d;
                cercano = n;
            }
        }
        return cercano;
    }

    /// <summary>Lista de aristas vigentes (cada par no dirigido aparece una vez).</summary>
    public List<KeyValuePair<PathNode, PathNode>> ObtenerAristas()
    {
        List<KeyValuePair<PathNode, PathNode>> lista = new List<KeyValuePair<PathNode, PathNode>>();
        for (int i = 0; i < nodos.Count; i++)
        {
            PathNode a = nodos[i];
            if (a == null) continue;
            for (int k = 0; k < a.vecinos.Count; k++)
            {
                PathNode b = a.vecinos[k];
                if (b == null) continue;
                if (b.vecinos.Contains(a) && a.nodeId > b.nodeId) continue; // evitar duplicado
                lista.Add(new KeyValuePair<PathNode, PathNode>(a, b));
            }
        }
        return lista;
    }

    public static float DistanciaPuntoSegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Arista vigente más cercana a un punto del mundo (para colocar barricadas).</summary>
    public bool AristaMasCercana(Vector2 punto, out PathNode nodoA, out PathNode nodoB, out float distancia)
    {
        nodoA = null;
        nodoB = null;
        distancia = float.MaxValue;
        List<KeyValuePair<PathNode, PathNode>> aristas = ObtenerAristas();
        for (int i = 0; i < aristas.Count; i++)
        {
            float d = DistanciaPuntoSegmento(punto, aristas[i].Key.transform.position, aristas[i].Value.transform.position);
            if (d < distancia)
            {
                distancia = d;
                nodoA = aristas[i].Key;
                nodoB = aristas[i].Value;
            }
        }
        return nodoA != null;
    }

    public bool EstaEnRutaActual(PathNode a, PathNode b)
    {
        for (int i = 0; i < rutaActual.Count - 1; i++)
        {
            if ((rutaActual[i] == a && rutaActual[i + 1] == b) || (rutaActual[i] == b && rutaActual[i + 1] == a)) return true;
        }
        return false;
    }

    /// <summary>Distancia mínima de un punto a cualquier arista (vigente o bloqueada) del grafo.</summary>
    public float DistanciaAlGrafo(Vector2 punto, bool soloAlternativas = false)
    {
        float min = float.MaxValue;
        List<KeyValuePair<PathNode, PathNode>> aristas = ObtenerAristas();
        for (int i = 0; i < aristas.Count; i++)
        {
            if (soloAlternativas && aristas[i].Key.MultiplicadorHacia(aristas[i].Value) <= 1.05f) continue;
            min = Mathf.Min(min, DistanciaPuntoSegmento(punto, aristas[i].Key.transform.position, aristas[i].Value.transform.position));
        }
        return min;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!mostrarGizmos || rutaActual == null || rutaActual.Count < 2) return;
        Gizmos.color = colorRutaActual;
        for (int i = 0; i < rutaActual.Count - 1; i++)
        {
            if (rutaActual[i] == null || rutaActual[i + 1] == null) continue;
            Gizmos.DrawLine(rutaActual[i].transform.position, rutaActual[i + 1].transform.position);
        }
    }

    #endregion
}
