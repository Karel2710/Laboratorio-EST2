using UnityEngine;

namespace RedEnergiaMST
{
    /// <summary>
    /// Nodo de la red de energía. Cada torre activa en escena tiene un TowerNode asociado
    /// que referencia su TowerBehavior o Torre (TowerDefenseGameplay) y expone
    /// las estadísticas base para que el EnergyNetworkManager pueda aplicar buffs.
    /// </summary>
    public class TowerNode : MonoBehaviour
    {
        // ─── Referencias a componentes de torre ─────────────────────────────────
        /// <summary>Referencia al componente Torre (namespace TowerDefenseGameplay).</summary>
        [HideInInspector] public TowerDefenseGameplay.Torre torreGameplay;

        /// <summary>Referencia al componente TowerBehavior (sistema alternativo de torre).</summary>
        [HideInInspector] public TowerBehavior torreBehavior;

        // ─── Buffs de sinergia de red ────────────────────────────────────────────
        /// <summary>Número de conexiones directas que tiene este nodo en el MST actual.</summary>
        [HideInInspector] public int grado = 0;

        /// <summary>Multiplicador de daño acumulado por sinergia de red. Valor base = 1.0f.</summary>
        [HideInInspector] public float multiplicadorDano = 1.0f;

        /// <summary>Multiplicador de rango acumulado por sinergia de red. Valor base = 1.0f.</summary>
        [HideInInspector] public float multiplicadorRango = 1.0f;

        // ─── Constantes de sinergia ──────────────────────────────────────────────
        /// <summary>Porcentaje de daño adicional por cada conexión directa en el MST (+15%).</summary>
        public const float BONUS_DANO_POR_CONEXION = 0.15f;

        /// <summary>Porcentaje de rango adicional por cada conexión directa en el MST (+10%).</summary>
        public const float BONUS_RANGO_POR_CONEXION = 0.10f;

        // ─── Valores base cacheados ──────────────────────────────────────────────
        /// <summary>Valor de daño base original de la torre (antes de cualquier buff de red).</summary>
        [HideInInspector] public float danioBaseOriginal;

        /// <summary>Valor de alcance/rango base original de la torre (antes de cualquier buff de red).</summary>
        [HideInInspector] public float rangoBaseOriginal;

        private void Awake()
        {
            // Auto-buscar los componentes de torre en el mismo objeto o en sus hijos
            torreGameplay = GetComponentInChildren<TowerDefenseGameplay.Torre>();
            torreBehavior  = GetComponentInChildren<TowerBehavior>();
            CachearValoresBase();
        }

        /// <summary>
        /// Guarda los valores estadísticos originales de la torre para poder restaurarlos
        /// cuando se recalcule el MST y los buffs cambien.
        /// </summary>
        public void CachearValoresBase()
        {
            if (torreGameplay != null)
            {
                danioBaseOriginal = torreGameplay.danioBase;
                rangoBaseOriginal = torreGameplay.alcance;
            }
            else if (torreBehavior != null)
            {
                danioBaseOriginal = torreBehavior.danoPorDisparo;
                rangoBaseOriginal = torreBehavior.rangoAtaque;
            }
        }

        /// <summary>
        /// Aplica los multiplicadores de sinergia calculados por el MST sobre los atributos
        /// reales de la torre. Restaura primero los valores base para evitar composición de buffs.
        /// </summary>
        public void AplicarBuffsDeRed()
        {
            if (torreGameplay != null)
            {
                torreGameplay.danioBase = danioBaseOriginal * multiplicadorDano;
                torreGameplay.alcance   = rangoBaseOriginal * multiplicadorRango;
            }
            else if (torreBehavior != null)
            {
                torreBehavior.danoPorDisparo = danioBaseOriginal * multiplicadorDano;
                torreBehavior.rangoAtaque    = rangoBaseOriginal * multiplicadorRango;
            }
        }

        /// <summary>
        /// Elimina todos los buffs de sinergia y restaura los valores base originales.
        /// Se llama cuando la torre abandona la red (por venta o destrucción).
        /// </summary>
        public void EliminarBuffsDeRed()
        {
            multiplicadorDano  = 1.0f;
            multiplicadorRango = 1.0f;
            grado              = 0;
            AplicarBuffsDeRed(); // Escribe los valores base en la torre
        }

        /// <summary>
        /// Calcula los multiplicadores en función del grado de conexión en el MST.
        /// Se invoca después de que el EnergyNetworkManager determina el MST.
        /// </summary>
        /// <param name="gradoEnMST">Número de aristas del MST que inciden sobre este nodo.</param>
        public void ActualizarMultiplicadores(int gradoEnMST)
        {
            grado              = gradoEnMST;
            multiplicadorDano  = 1.0f + grado * BONUS_DANO_POR_CONEXION;
            multiplicadorRango = 1.0f + grado * BONUS_RANGO_POR_CONEXION;
        }
    }
}
