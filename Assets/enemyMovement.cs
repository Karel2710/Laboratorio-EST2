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

    // ─── Estado de navegación legacy (arreglo lineal) ─────────────────────────
    private Transform targetLegacy = null;
    private int pathIndexLegacy = 0;

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

        float multiplicador = componenteEnemigo != null ? componenteEnemigo.VelocidadEfectiva : 1f;
        Vector2 dir = (destino - rb.position).normalized;
        rb.linearVelocity = dir * (moveSpeed * multiplicador);
    }

    #endregion

    #region Inicialización de Ruta

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

        // Fallback: sistema legacy con arreglo Transform[] de levelManager
        usandoGrafo = false;
        pathIndexLegacy = 0;
        ObtenerSiguienteObjetivoLegacy();
    }

    public void ActualizarRuta(List<PathNode> nuevaRuta)
    {
        if (nuevaRuta == null || nuevaRuta.Count == 0) return;
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

    #endregion

    #region Movimiento por Grafo

    private void ActualizarMovimientoGrafo()
    {
        if (rutaGrafo == null || indiceNodoActual >= rutaGrafo.Count)
        {
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
        if (distancia <= 0.35f)
        {
            indiceNodoActual++;
        }
    }

    #endregion

    #region Movimiento Legacy

    private void ActualizarMovimientoLegacy()
    {
        if (targetLegacy == null)
        {
            ObtenerSiguienteObjetivoLegacy();
            if (targetLegacy == null) return;
        }

        if (Vector2.Distance(transform.position, targetLegacy.position) <= 0.35f)
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

    #region Helpers

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

    private bool HayDestino()
    {
        if (usandoGrafo)
            return rutaGrafo != null && indiceNodoActual < rutaGrafo.Count;
        else
            return targetLegacy != null;
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
