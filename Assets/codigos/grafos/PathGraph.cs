using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestor central del grafo de caminos.
/// - Almacena todos los PathNode de la escena.
/// - Ejecuta Dijkstra para encontrar la ruta más corta.
/// - Recalcula rutas cuando se coloca una torre.
/// - Dibuja el grafo completo con OnDrawGizmos().
/// </summary>
public class PathGraph : MonoBehaviour
{
    public static PathGraph main;

    [Header("Nodos del Grafo")]
    [Tooltip("Arrastra aquí todos los PathNode de la escena en el Inspector.")]
    public List<PathNode> nodos = new List<PathNode>();

    [Header("Opciones de Visualización")]
    public bool mostrarGizmos = true;
    public Color colorRutaActual = new Color(0f, 1f, 0.4f);

    // Ruta calculada actualmente (lista de nodos desde inicio hasta fin)
    private List<PathNode> rutaActual = new List<PathNode>();

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }
        main = this;
    }

    private void Start()
    {
        AsignarIdsAutomaticos();
        CalcularRutaPrincipal();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Configuración del Grafo

    /// <summary>
    /// Asigna IDs únicos a los nodos que aún no los tienen.
    /// </summary>
    public void AsignarIdsAutomaticos()
    {
        for (int i = 0; i < nodos.Count; i++)
        {
            if (nodos[i] != null)
                nodos[i].nodeId = i;
        }
        Debug.Log($"[PathGraph] Grafo inicializado con {nodos.Count} nodos.");
    }

    /// <summary>
    /// Registra dinámicamente un nodo en el grafo (útil si se crean nodos en tiempo de ejecución).
    /// </summary>
    public void RegistrarNodo(PathNode nodo)
    {
        if (!nodos.Contains(nodo))
        {
            nodo.nodeId = nodos.Count;
            nodos.Add(nodo);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Dijkstra

    /// <summary>
    /// Algoritmo de Dijkstra.
    /// Devuelve la lista de nodos que forman el camino más corto desde <paramref name="inicio"/>
    /// hasta <paramref name="fin"/>. Retorna null si no existe ruta.
    /// 
    /// Complejidad: O((V + E) log V) con cola de prioridad simplificada (lista ordenada).
    /// </summary>
    public List<PathNode> Dijkstra(PathNode inicio, PathNode fin)
    {
        if (inicio == null || fin == null)
        {
            Debug.LogWarning("[PathGraph] Dijkstra: nodo inicio o fin es null.");
            return null;
        }

        // ── 1. Inicialización ────────────────────────────────────────────────
        foreach (PathNode n in nodos)
        {
            if (n == null) continue;
            n.distancia = float.MaxValue;
            n.previo    = null;
            n.visitado  = false;
        }

        inicio.distancia = 0f;

        // Cola de prioridad simplificada: lista no ordenada (válido para grafos pequeños de TD)
        List<PathNode> cola = new List<PathNode>(nodos);

        // ── 2. Relajación de aristas ─────────────────────────────────────────
        while (cola.Count > 0)
        {
            // Extraer el nodo con menor distancia conocida (equivalente a priority-queue)
            PathNode actual = ExtraerMinimo(cola);
            if (actual == null || actual.distancia == float.MaxValue) break;
            if (actual == fin) break; // Ruta encontrada

            actual.visitado = true;

            foreach (PathNode vecino in actual.vecinos)
            {
                if (vecino == null || vecino.visitado) continue;

                float nuevaDistancia = actual.distancia + actual.PesoHacia(vecino);

                if (nuevaDistancia < vecino.distancia)
                {
                    vecino.distancia = nuevaDistancia;
                    vecino.previo    = actual;
                }
            }
        }

        // ── 3. Reconstrucción del camino ─────────────────────────────────────
        return ReconstruirCamino(fin);
    }

    /// <summary>
    /// Extrae y elimina de la lista el nodo con la menor distancia acumulada.
    /// </summary>
    private PathNode ExtraerMinimo(List<PathNode> cola)
    {
        PathNode minNodo = null;
        float minDist = float.MaxValue;

        foreach (PathNode n in cola)
        {
            if (n != null && !n.visitado && n.distancia < minDist)
            {
                minDist = n.distancia;
                minNodo = n;
            }
        }

        if (minNodo != null)
            cola.Remove(minNodo);

        return minNodo;
    }

    /// <summary>
    /// Reconstruye el camino desde el nodo fin hacia el inicio siguiendo los punteros previo.
    /// </summary>
    private List<PathNode> ReconstruirCamino(PathNode fin)
    {
        List<PathNode> camino = new List<PathNode>();
        PathNode actual = fin;

        while (actual != null)
        {
            camino.Insert(0, actual);
            actual = actual.previo;
        }

        // Si el primer nodo del camino no es el inicio con distancia 0, no hay ruta
        if (camino.Count == 0 || camino[0].distancia != 0f)
        {
            // El primer nodo debería ser el inicio; si su distancia no es 0, hubo un error
            // Verificamos si el fin fue alcanzado comparando si tiene previo o es inicio
        }

        return camino.Count > 1 ? camino : null;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Ruta Principal y Recálculo

    /// <summary>
    /// Calcula la ruta principal entre el nodo marcado como esInicio y el marcado como esFinal.
    /// Guarda el resultado en rutaActual y notifica a los enemigos activos.
    /// </summary>
    public void CalcularRutaPrincipal()
    {
        PathNode inicio = ObtenerNodoInicio();
        PathNode fin    = ObtenerNodoFin();

        if (inicio == null) { Debug.LogError("[PathGraph] No hay nodo marcado como esInicio."); return; }
        if (fin    == null) { Debug.LogError("[PathGraph] No hay nodo marcado como esFinal."); return; }

        List<PathNode> nuevaRuta = Dijkstra(inicio, fin);

        if (nuevaRuta == null || nuevaRuta.Count == 0)
        {
            Debug.LogWarning("[PathGraph] Dijkstra no encontró ruta entre inicio y fin.");
            return;
        }

        rutaActual = nuevaRuta;

        // Log de la ruta calculada
        string log = "[PathGraph] Ruta calculada: ";
        for (int i = 0; i < rutaActual.Count; i++)
            log += $"N{rutaActual[i].nodeId}" + (i < rutaActual.Count - 1 ? " → " : "");
        float costoTotal = fin.distancia;
        log += $"  (costo total: {costoTotal:F2})";
        Debug.Log(log);

        // Notificar a todos los enemigos activos para que recalculen su ruta
        NotificarEnemigos();
    }

    /// <summary>
    /// Llamado cuando una torre bloquea un camino.
    /// Desconecta los nodos adyacentes al punto bloqueado y recalcula la ruta.
    /// </summary>
    /// <param name="nodoA">Primer nodo de la arista bloqueada.</param>
    /// <param name="nodoB">Segundo nodo de la arista bloqueada.</param>
    public void BloquearArista(PathNode nodoA, PathNode nodoB)
    {
        if (nodoA == null || nodoB == null) return;
        nodoA.DesconectarDe(nodoB);
        nodoB.DesconectarDe(nodoA);
        Debug.Log($"[PathGraph] Arista bloqueada entre N{nodoA.nodeId} y N{nodoB.nodeId}. Recalculando ruta...");
        CalcularRutaPrincipal();
    }

    /// <summary>
    /// Reconecta una arista previamente bloqueada y recalcula la ruta.
    /// </summary>
    public void DesbloquearArista(PathNode nodoA, PathNode nodoB)
    {
        if (nodoA == null || nodoB == null) return;
        nodoA.ConectarCon(nodoB);
        nodoB.ConectarCon(nodoA);
        Debug.Log($"[PathGraph] Arista restaurada entre N{nodoA.nodeId} y N{nodoB.nodeId}. Recalculando ruta...");
        CalcularRutaPrincipal();
    }

    /// <summary>
    /// Devuelve la ruta óptima calculada actualmente (solo lectura).
    /// </summary>
    public List<PathNode> ObtenerRutaActual() => rutaActual;

    /// <summary>
    /// Notifica a todos los enemigos activos que deben actualizar su ruta.
    /// </summary>
    private void NotificarEnemigos()
    {
        enemyMovement[] enemigos = FindObjectsByType<enemyMovement>(FindObjectsSortMode.None);
        foreach (enemyMovement e in enemigos)
            e.ActualizarRuta(rutaActual);

        Debug.Log($"[PathGraph] {enemigos.Length} enemigo(s) notificados con la nueva ruta.");
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Helpers

    public PathNode ObtenerNodoInicio()
    {
        foreach (PathNode n in nodos)
            if (n != null && n.esInicio) return n;
        return null;
    }

    public PathNode ObtenerNodoFin()
    {
        foreach (PathNode n in nodos)
            if (n != null && n.esFinal) return n;
        return null;
    }

    /// <summary>
    /// Encuentra el nodo del grafo más cercano a una posición en el mundo.
    /// Útil para que un enemigo sepa desde qué nodo continuar tras un recálculo.
    /// </summary>
    public PathNode NodoMasCercano(Vector3 posicion)
    {
        PathNode cercano = null;
        float menorDist = float.MaxValue;

        foreach (PathNode n in nodos)
        {
            if (n == null) continue;
            float d = Vector3.Distance(posicion, n.transform.position);
            if (d < menorDist)
            {
                menorDist = d;
                cercano = n;
            }
        }
        return cercano;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!mostrarGizmos || rutaActual == null || rutaActual.Count < 2) return;

        // Dibuja la ruta óptima calculada por Dijkstra con una línea gruesa destacada
        Gizmos.color = colorRutaActual;
        for (int i = 0; i < rutaActual.Count - 1; i++)
        {
            if (rutaActual[i] == null || rutaActual[i + 1] == null) continue;
            Vector3 a = rutaActual[i].transform.position;
            Vector3 b = rutaActual[i + 1].transform.position;

            // Línea principal
            Gizmos.DrawLine(a, b);

            // Flecha de dirección en el punto medio
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = (b - a).normalized;
            Gizmos.DrawLine(mid, mid + Quaternion.Euler(0, 0,  35f) * (-dir) * 0.3f);
            Gizmos.DrawLine(mid, mid + Quaternion.Euler(0, 0, -35f) * (-dir) * 0.3f);
        }
    }

    #endregion
}
