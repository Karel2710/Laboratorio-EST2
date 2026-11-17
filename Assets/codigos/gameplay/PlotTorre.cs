using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Gestiona las casillas de terreno (Plots) donde el jugador puede colocar torres de defensa.
    ///
    /// El clic y el hover los detecta únicamente GameManager (raycast propio). Antes esta clase también
    /// escuchaba OnMouseDown y OnPointerClick, así que un solo clic llamaba ConstruirTorre hasta 3 veces
    /// y aparecía el aviso "ya contiene una torre" justo después de construir.
    /// </summary>
    public class PlotTorre : MonoBehaviour
    {
        public int costoTorre = 50;
        public Torre torreInstanciada = null;

        /// <summary>Posición de la casilla dentro de 'plots'. Identifica la casilla en los mensajes LAN.</summary>
        public int indiceRed = -1;

        private SpriteRenderer spriteRenderer;
        private Color colorOriginal;
        private Color colorHover = new Color(0.4f, 0.95f, 0.5f, 0.9f);
        private Color colorHoverInvalido = new Color(0.95f, 0.3f, 0.3f, 0.9f);

        public bool TieneTorre => torreInstanciada != null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                colorOriginal = spriteRenderer.color;
            }

            // Asegurar que todos los Collider2D sean Trigger para no bloquear jamás a los enemigos
            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            if (cols == null || cols.Length == 0)
            {
                BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(0.9f, 0.9f);
                box.isTrigger = true;
            }
            else
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    cols[i].isTrigger = true;
                }
            }
        }

        private void Start()
        {
            // Reasegurar trigger en Start en caso de que Unity cargue la escena con valores serializados previos
            foreach (var c in GetComponentsInChildren<Collider2D>(true))
            {
                c.isTrigger = true;
            }
        }

        public void ResaltarHover()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;

            if (TieneTorre)
            {
                spriteRenderer.color = colorHoverInvalido;
            }
            else
            {
                TipoTorre tipo = GameManager.Instance != null ? GameManager.Instance.tipoTorreAConstruir : TipoTorre.Basica;
                int costo = GameManager.Instance != null ? GameManager.Instance.ObtenerCostoTorre(tipo) : costoTorre;
                bool puedePagar = GameManager.Instance != null && GameManager.Instance.OroActual >= costo;
                spriteRenderer.color = puedePagar ? colorHover : colorHoverInvalido;
            }
        }

        public void RestaurarColor()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = colorOriginal;
            }
        }

        /// <summary>Construye la torre seleccionada actualmente en el GameManager.</summary>
        public bool ConstruirTorre(bool esGratis = false)
        {
            TipoTorre tipo = GameManager.Instance != null ? GameManager.Instance.tipoTorreAConstruir : TipoTorre.Basica;
            return ConstruirTorre(tipo, esGratis);
        }

        /// <summary>
        /// Construye una torre del tipo indicado (lógica autoritativa: un jugador o host LAN).
        /// </summary>
        public bool ConstruirTorre(TipoTorre tipo, bool esGratis = false)
        {
            if (TieneTorre)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.MostrarNotificacion($"Esta casilla ya contiene una torre {torreInstanciada.tipo}.");
                }
                return false;
            }

            int costo = GameManager.Instance != null ? GameManager.Instance.ObtenerCostoTorre(tipo) : costoTorre;

            if (!esGratis)
            {
                if (GameManager.Instance == null || GameManager.Instance.OroActual < costo)
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.MostrarNotificacion($"¡Oro insuficiente! Se requieren {costo} de oro para la Torre {tipo}.");
                    }
                    return false;
                }

                GameManager.Instance.GastarOro(costo);
            }

            CrearObjetoTorre(tipo, soloVisual: false);
            RestaurarColor();

            if (GameManager.Instance != null)
            {
                string infoCosto = esGratis ? "gratuita" : $"-{costo} de Oro";
                GameManager.Instance.MostrarNotificacion($"🔨 ¡Torre {tipo} construida en {name}! ({infoCosto})");
            }

            return true;
        }

        /// <summary>
        /// Cliente LAN: muestra la torre que existe en el host. No dispara ni cobra oro.
        /// </summary>
        public void ConstruirTorreVisual(TipoTorre tipo)
        {
            if (TieneTorre)
            {
                if (torreInstanciada.tipo == tipo) return;
                QuitarTorreVisual();
            }

            CrearObjetoTorre(tipo, soloVisual: true);
        }

        /// <summary>Cliente LAN: quita la torre visual (la torre dejó de existir en el host).</summary>
        public void QuitarTorreVisual()
        {
            if (!TieneTorre) return;
            RedEnergiaMST.EnergyNetworkManager.Instance?.DesregistrarTorre(torreInstanciada.gameObject);
            Destroy(torreInstanciada.gameObject);
            torreInstanciada = null;
        }

        private void CrearObjetoTorre(TipoTorre tipo, bool soloVisual)
        {
            GameObject torreObj = new GameObject($"Torre_{tipo}_{name}");
            torreObj.transform.position = transform.position;
            torreObj.transform.SetParent(transform, true);

            torreInstanciada = torreObj.AddComponent<Torre>();
            torreInstanciada.soloVisual = soloVisual;
            torreInstanciada.ConfigurarTipo(tipo);

            // ── Integración Red de Energía MST ──────────────────────────────────
            // Notificar al EnergyNetworkManager para que recalcule el MST con la nueva torre.
            // (En el cliente LAN también se calcula: así ambos PCs ven los mismos rayos de energía.)
            RedEnergiaMST.EnergyNetworkManager.Instance?.RegistrarTorre(torreObj);
            // ────────────────────────────────────────────────────────────────────
        }

        /// <summary>
        /// Vende y destruye la torre de esta casilla, devuelve parte del oro al jugador
        /// y notifica a la Red de Energía para que recalcule el MST sin esta torre.
        /// </summary>
        /// <param name="porcentajeReembolso">Fracción del costo de construcción devuelta (default 0.5 = 50%).</param>
        public bool VenderTorre(float porcentajeReembolso = 0.5f)
        {
            if (!TieneTorre) return false;

            // ── Integración Red de Energía MST ──────────────────────────────────
            // Desregistrar ANTES de destruir el GameObject para que TowerNode sea válido.
            RedEnergiaMST.EnergyNetworkManager.Instance?.DesregistrarTorre(torreInstanciada.gameObject);
            // ────────────────────────────────────────────────────────────────────

            int reembolso = Mathf.RoundToInt(GameManager.Instance != null
                ? GameManager.Instance.ObtenerCostoTorre(torreInstanciada.tipo) * porcentajeReembolso
                : costoTorre * porcentajeReembolso);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.oroActual += reembolso;
                GameManager.Instance.MostrarNotificacion(
                    $"💰 Torre {torreInstanciada.tipo} vendida. Reembolso: +{reembolso} de Oro."
                );
            }

            Destroy(torreInstanciada.gameObject);
            torreInstanciada = null;
            RestaurarColor();
            return true;
        }
    }
}
