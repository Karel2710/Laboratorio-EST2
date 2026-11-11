using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el movimiento del enemigo a lo largo de la ruta calculada por el grafo (Dijkstra)
/// o por los waypoints lineales de levelManager (respaldo).
/// Integra ralentización por criogenia, terreno ponderado, daño a la base y anti-bloqueo.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class enemyMovement : MonoBehaviour
{
    /// <summary>Registro de todos los enemigos en movimiento (evita búsquedas costosas por frame).</summary>
    public static readonly List<enemyMovement> Activos = new List<enemyMovement>();

    [Header("Referencias")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Atributos")]
    [SerializeField] private float moveSpeed = 2f;

    // ─── Estado de navegación por grafo ──────────────────────────────────────
    private List<PathNode> rutaGrafo = null;
    private int indiceNodoActual = 0;
    private PathNode nodoPrevio = null;

    // ─── Estado de navegación lineal ─────────────────────────────────────────
    private int pathIndex = 0;

    private bool usandoGrafo = false;
    private bool llego = false;
    private TowerDefenseGameplay.Enemigo componenteEnemigo;

    public void ConfigurarVelocidad(float nuevaVelocidad)
    {
        moveSpeed = nuevaVelocidad;
    }

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }

    /// <summary>Nodo del grafo hacia el que camina actualmente (null en modo lineal).</summary>
    public PathNode NodoObjetivo =>
        usandoGrafo && rutaGrafo != null && indiceNodoActual < rutaGrafo.Count ? rutaGrafo[indiceNodoActual] : null;

    /// <summary>Último nodo del grafo que dejó atrás.</summary>
    public PathNode NodoPrevio => nodoPrevio;

    /// <summary>
    /// Costo restante hasta la base. Las torres lo usan para priorizar al "Primero" o al "Último".
    /// </summary>
    public float DistanciaRestante
    {
        get
        {
            PathNode obj = NodoObjetivo;
            if (obj != null)
            {
                float resto = obj.costoAFin == float.MaxValue ? 0f : obj.costoAFin;
                return Vector2.Distance(transform.position, obj.transform.position) + resto;
            }

            levelManager lm = ObtenerLevelManager();
            if (lm == null || lm.Path == null || pathIndex >= lm.Path.Length || lm.Path[pathIndex] == null) return 0f;
            float total = Vector2.Distance(transform.position, lm.Path[pathIndex].position);
            for (int i = pathIndex; i < lm.Path.Length - 1; i++)
            {
                if (lm.Path[i] != null && lm.Path[i + 1] != null)
                    total += Vector2.Distance(lm.Path[i].position, lm.Path[i + 1].position);
            }
            return total;
        }
    }

    #region Unity Lifecycle

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // Físicas anti-atasco: sin gravedad, sin rotación y colisionador en modo trigger
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        componenteEnemigo = GetComponent<TowerDefenseGameplay.Enemigo>();
    }

    private void OnEnable()
    {
        if (!Activos.Contains(this)) Activos.Add(this);
    }

    private void OnDisable()
    {
        Activos.Remove(this);
    }

    private void Start()
    {
        if (componenteEnemigo == null) componenteEnemigo = GetComponent<TowerDefenseGameplay.Enemigo>();
        InicializarRuta();
    }

    private void Update()
    {
        if (llego) return;

        Transform objActual = ObtenerTransformObjetivo();
        if (objActual == null)
        {
            AvanzarSiguienteObjetivo();
            return;
        }

        if (Vector2.Distance(transform.position, objActual.position) <= 0.22f)
        {
            AvanzarSiguienteObjetivo();
        }
    }

    private void FixedUpdate()
    {
        if (rb == null || llego) return;

        Transform objActual = ObtenerTransformObjetivo();
        if (objActual == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float multiplicador = componenteEnemigo != null ? componenteEnemigo.VelocidadEfectiva : 1f;

        // Terreno ponderado: en Sendero/Pantano se avanza más lento (el peso de la arista es tiempo)
        PathNode obj = NodoObjetivo;
        if (obj != null && nodoPrevio != null && nodoPrevio.vecinos.Contains(obj))
        {
            multiplicador /= Mathf.Max(1f, nodoPrevio.MultiplicadorHacia(obj));
        }

        Vector2 delta = (Vector2)objActual.position - rb.position;
        float paso = moveSpeed * multiplicador;
        // Evita sobrepasar el nodo a velocidades de juego altas
        float maxVel = delta.magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        rb.linearVelocity = delta.normalized * Mathf.Min(paso, maxVel);
    }

    #endregion

    #region Inicialización y Navegación de Ruta

    private levelManager ObtenerLevelManager()
    {
        if (levelManager.main != null) return levelManager.main;
        levelManager lm = FindFirstObjectByType<levelManager>();
        if (lm != null) levelManager.main = lm;
        return lm;
    }

    private void InicializarRuta()
    {
        if (usandoGrafo) return; // la ruta ya fue asignada externamente

        if (PathGraph.main != null)
        {
            List<PathNode> ruta = PathGraph.main.ObtenerRutaActual();
            if (ruta != null && ruta.Count > 1)
            {
                rutaGrafo = new List<PathNode>(ruta);
                usandoGrafo = true;
                indiceNodoActual = 0;
                nodoPrevio = null;
                // Si aparece sobre el nodo inicial, apuntar directamente al siguiente
                if (Vector2.Distance(transform.position, rutaGrafo[0].transform.position) <= 0.3f)
                {
                    nodoPrevio = rutaGrafo[0];
                    indiceNodoActual = 1;
                }
                return;
            }
        }

        // Modo lineal estándar usando levelManager
        usandoGrafo = false;
        pathIndex = 0;
    }

    /// <summary>
    /// Asigna una nueva ruta calculada por Dijkstra. El primer nodo de la lista es
    /// el siguiente destino inmediato del enemigo.
    /// </summary>
    public void ActualizarRuta(List<PathNode> nuevaRuta)
    {
        if (nuevaRuta == null || nuevaRuta.Count == 0) return;

        PathNode objetivoAnterior = NodoObjetivo;
        if (objetivoAnterior != null && nuevaRuta[0] != objetivoAnterior && nuevaRuta[0] == nodoPrevio)
        {
            // Da media vuelta sobre la misma arista: el nodo que tenía delante pasa a ser el previo
            nodoPrevio = objetivoAnterior;
        }

        rutaGrafo = new List<PathNode>(nuevaRuta);
        usandoGrafo = true;
        indiceNodoActual = 0;
    }

    private Transform ObtenerTransformObjetivo()
    {
        if (usandoGrafo)
        {
            PathNode n = NodoObjetivo;
            return n != null ? n.transform : null;
        }

        levelManager lm = ObtenerLevelManager();
        if (lm != null && lm.Path != null && pathIndex < lm.Path.Length)
        {
            return lm.Path[pathIndex];
        }
        return null;
    }

    private void AvanzarSiguienteObjetivo()
    {
        if (usandoGrafo && rutaGrafo != null)
        {
            PathNode alcanzado = NodoObjetivo;
            if (alcanzado != null) nodoPrevio = alcanzado;
            indiceNodoActual++;
            if (indiceNodoActual >= rutaGrafo.Count)
            {
                if (alcanzado == null || alcanzado.esFinal)
                {
                    LlegarALaBase();
                }
                else if (PathGraph.main != null)
                {
                    // La ruta terminó antes de la base (grafo modificado): pedir una nueva
                    List<PathNode> ruta = PathGraph.main.RutaDesdeNodo(alcanzado);
                    if (ruta != null && ruta.Count > 1)
                    {
                        rutaGrafo = ruta;
                        indiceNodoActual = 1;
                    }
                    else
                    {
                        LlegarALaBase();
                    }
                }
            }
            return;
        }

        levelManager lm = ObtenerLevelManager();
        if (lm != null && lm.Path != null)
        {
            pathIndex++;
            if (pathIndex >= lm.Path.Length) LlegarALaBase();
        }
        else
        {
            LlegarALaBase();
        }
    }

    private void LlegarALaBase()
    {
        if (llego) return;
        llego = true;

        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (TowerDefenseGameplay.GameManager.Instance != null)
        {
            int danio = componenteEnemigo != null ? componenteEnemigo.danioABase : 10;
            TowerDefenseGameplay.GameManager.Instance.EnemigoLlegoALaBase(danio, componenteEnemigo);
        }

        Destroy(gameObject);
    }

    #endregion
}
