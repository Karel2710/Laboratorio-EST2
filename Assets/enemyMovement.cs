using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el movimiento del enemigo a lo largo de la ruta calculada por el grafo (Dijkstra)
/// o por los waypoints lineales de levelManager.
/// Integra soporte para ralentización por criogenia, daño a la base y anti-bloqueo de rotación.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class enemyMovement : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Atributos")]
    [SerializeField] private float moveSpeed = 2f;

    // ─── Estado de navegación por grafo ──────────────────────────────────────
    private List<PathNode> rutaGrafo = null;
    private int indiceNodoActual = 0;

    // ─── Estado de navegación lineal ─────────────────────────────────────────
    private int pathIndex = 0;

    // ─── Modo activo y componentes ───────────────────────────────────────────
    private bool usandoGrafo = false;
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

    #region Unity Lifecycle

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        componenteEnemigo = GetComponent<TowerDefenseGameplay.Enemigo>();
    }

    private void Start()
    {
        if (componenteEnemigo == null)
        {
            componenteEnemigo = GetComponent<TowerDefenseGameplay.Enemigo>();
        }

        InicializarRuta();
    }

    private void Update()
    {
        Transform objActual = ObtenerTransformObjetivo();
        if (objActual == null)
        {
            AvanzarSiguienteObjetivo();
            return;
        }

        if (Vector2.Distance(transform.position, objActual.position) <= 0.35f)
        {
            AvanzarSiguienteObjetivo();
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        Transform objActual = ObtenerTransformObjetivo();
        if (objActual == null) return;

        float multiplicador = componenteEnemigo != null ? componenteEnemigo.VelocidadEfectiva : 1f;
        Vector2 dir = ((Vector2)objActual.position - rb.position).normalized;
        rb.linearVelocity = dir * (moveSpeed * multiplicador);
    }

    #endregion

    #region Inicialización y Navegación de Ruta

    private levelManager ObtenerLevelManager()
    {
        if (levelManager.main != null) return levelManager.main;
        levelManager lm = FindFirstObjectByType<levelManager>();
        if (lm != null)
        {
            levelManager.main = lm;
            return lm;
        }
        return null;
    }

    private void InicializarRuta()
    {
        if (PathGraph.main != null)
        {
            List<PathNode> ruta = PathGraph.main.ObtenerRutaActual();
            if (ruta != null && ruta.Count > 1)
            {
                EstablecerRutaGrafo(ruta);
                return;
            }
        }

        // Modo lineal estándar usando levelManager
        usandoGrafo = false;
        pathIndex = 0;
    }

    public void ActualizarRuta(List<PathNode> nuevaRuta)
    {
        if (nuevaRuta == null || nuevaRuta.Count < 2) return;
        EstablecerRutaGrafo(nuevaRuta);
    }

    private void EstablecerRutaGrafo(List<PathNode> ruta)
    {
        rutaGrafo = ruta;
        usandoGrafo = true;
        indiceNodoActual = ObtenerIndiceNodoCercano();
    }

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

        return Mathf.Min(indiceMasCercano, rutaGrafo.Count - 1);
    }

    private Transform ObtenerTransformObjetivo()
    {
        if (usandoGrafo && rutaGrafo != null && indiceNodoActual < rutaGrafo.Count && rutaGrafo[indiceNodoActual] != null)
        {
            return rutaGrafo[indiceNodoActual].transform;
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
            indiceNodoActual++;
            if (indiceNodoActual >= rutaGrafo.Count)
            {
                LlegarALaBase();
            }
            return;
        }

        levelManager lm = ObtenerLevelManager();
        if (lm != null && lm.Path != null)
        {
            pathIndex++;
            if (pathIndex >= lm.Path.Length)
            {
                LlegarALaBase();
            }
        }
    }

    private void LlegarALaBase()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (TowerDefenseGameplay.GameManager.Instance != null)
        {
            int danio = componenteEnemigo != null ? componenteEnemigo.danioABase : 10;
            TowerDefenseGameplay.GameManager.Instance.EnemigoLlegoALaBase(danio);
        }

        Destroy(gameObject);
    }

    #endregion
}
