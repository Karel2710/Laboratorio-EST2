using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el movimiento del enemigo a lo largo de la ruta calculada por el grafo (Dijkstra).
/// Mantiene compatibilidad con el arreglo lineal legacy de levelManager si el grafo no existe.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class enemyMovement : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Atributos")]
    [SerializeField] private float moveSpeed = 2f;

    // ─── Estado de navegación por grafo ──────────────────────────────────────
    private List<PathNode> rutaGrafo = null;   // Ruta de nodos calculada por Dijkstra
    private int indiceNodoActual = 0;          // Índice del nodo objetivo actual en rutaGrafo

    // ─── Estado de navegación legacy (arreglo lineal) ─────────────────────────
    private Transform targetLegacy = null;
    private int pathIndexLegacy = 0;

    // ─── Modo activo ──────────────────────────────────────────────────────────
    private bool usandoGrafo = false;

    // ─────────────────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        InicializarRuta();
    }

    private void Update()
    {
        if (usandoGrafo)
            ActualizarMovimientoGrafo();
        else
            ActualizarMovimientoLegacy();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        Vector2 destino = ObtenerPosicionDestino();
        if (destino == Vector2.zero && !HayDestino()) return;

        Vector2 dir = (destino - rb.position).normalized;
        rb.linearVelocity = dir * moveSpeed;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Inicialización de Ruta

    /// <summary>
    /// Intenta usar el grafo primero; si no existe, cae al sistema legacy.
    /// </summary>
    private void InicializarRuta()
    {
        if (PathGraph.main != null)
        {
            List<PathNode> ruta = PathGraph.main.ObtenerRutaActual();
            if (ruta != null && ruta.Count > 0)
            {
                EstablecerRutaGrafo(ruta);
                return;
            }
        }

        // Fallback: sistema legacy con arreglo Transform[]
        usandoGrafo = false;
        pathIndexLegacy = 0;
        ObtenerSiguienteObjetivoLegacy();
        Debug.Log("[enemyMovement] Usando modo legacy (arreglo lineal).");
    }

    /// <summary>
    /// Asigna la ruta de nodos del grafo y posiciona al enemigo en el nodo más cercano a él.
    /// Llamado también cuando el grafo recalcula la ruta en tiempo real.
    /// </summary>
    public void ActualizarRuta(List<PathNode> nuevaRuta)
    {
        if (nuevaRuta == null || nuevaRuta.Count == 0) return;
        EstablecerRutaGrafo(nuevaRuta);
    }

    private void EstablecerRutaGrafo(List<PathNode> ruta)
    {
        rutaGrafo = ruta;
        usandoGrafo = true;

        // Buscar el nodo más cercano en la ruta para continuar desde ahí
        // (evita que el enemigo retroceda si está a mitad del camino)
        indiceNodoActual = ObtenerIndiceNodoCercano();
    }

    /// <summary>
    /// Devuelve el índice en rutaGrafo del nodo más cercano a la posición actual del enemigo.
    /// </summary>
    private int ObtenerIndiceNodoCercano()
    {
        if (rutaGrafo == null || rutaGrafo.Count == 0) return 0;

        int indiceMasCercano = 0;
        float menorDistancia = float.MaxValue;

        for (int i = 0; i < rutaGrafo.Count; i++)
        {
            if (rutaGrafo[i] == null) continue;
            float d = Vector3.Distance(transform.position, rutaGrafo[i].transform.position);
            if (d < menorDistancia)
            {
                menorDistancia = d;
                indiceMasCercano = i;
            }
        }

        // Si el más cercano ya fue pasado, avanzar al siguiente
        return Mathf.Min(indiceMasCercano, rutaGrafo.Count - 1);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Movimiento por Grafo

    private void ActualizarMovimientoGrafo()
    {
        if (rutaGrafo == null || indiceNodoActual >= rutaGrafo.Count)
        {
            // Llegó al final: destruir enemigo (llegó a la base)
            LlegarALaBase();
            return;
        }

        PathNode nodoObjetivo = rutaGrafo[indiceNodoActual];
        if (nodoObjetivo == null)
        {
            indiceNodoActual++;
            return;
        }

        float distancia = Vector2.Distance(transform.position, nodoObjetivo.transform.position);
        if (distancia <= 0.15f)
        {
            indiceNodoActual++;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Movimiento Legacy (compatibilidad)

    private void ActualizarMovimientoLegacy()
    {
        if (targetLegacy == null)
        {
            ObtenerSiguienteObjetivoLegacy();
            if (targetLegacy == null) return;
        }

        if (Vector2.Distance(transform.position, targetLegacy.position) <= 0.1f)
        {
            pathIndexLegacy++;
            ObtenerSiguienteObjetivoLegacy();
        }
    }

    private void ObtenerSiguienteObjetivoLegacy()
    {
        if (levelManager.main != null && levelManager.main.Path != null && levelManager.main.Path.Length > 0)
        {
            if (pathIndexLegacy >= 0 && pathIndexLegacy < levelManager.main.Path.Length)
                targetLegacy = levelManager.main.Path[pathIndexLegacy];
            else
            {
                targetLegacy = null;
                LlegarALaBase();
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Helpers

    /// <summary>
    /// Devuelve la posición 2D del destino actual (grafo o legacy).
    /// </summary>
    private Vector2 ObtenerPosicionDestino()
    {
        if (usandoGrafo)
        {
            if (rutaGrafo != null && indiceNodoActual < rutaGrafo.Count && rutaGrafo[indiceNodoActual] != null)
                return rutaGrafo[indiceNodoActual].transform.position;
        }
        else
        {
            if (targetLegacy != null)
                return targetLegacy.position;
        }
        return Vector2.zero;
    }

    /// <summary>
    /// Verifica si existe un destino válido actualmente.
    /// </summary>
    private bool HayDestino()
    {
        if (usandoGrafo)
            return rutaGrafo != null && indiceNodoActual < rutaGrafo.Count;
        else
            return targetLegacy != null;
    }

    /// <summary>
    /// Acción cuando el enemigo llega a la base.
    /// </summary>
    private void LlegarALaBase()
    {
        rb.linearVelocity = Vector2.zero;
        Destroy(gameObject);
    }

    /// <summary>
    /// Expone la velocidad para que EnemySpawner pueda configurarla.
    /// </summary>
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }

    #endregion
}
