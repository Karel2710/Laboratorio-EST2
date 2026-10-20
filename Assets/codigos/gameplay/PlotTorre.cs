using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Gestiona las casillas de terreno (Plots) donde el jugador puede colocar torres de defensa.
    /// Soporta interacción mediante New Input System, Legacy Input y EventSystems.
    /// </summary>
    public class PlotTorre : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int costoTorre = 50;
        public Torre torreInstanciada = null;

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

        #region Eventos de Ratón / Legacy Input

        private void OnMouseEnter()
        {
            ResaltarHover();
        }

        private void OnMouseExit()
        {
            RestaurarColor();
        }

        private void OnMouseDown()
        {
            ConstruirTorre();
        }

        #endregion

        #region Eventos de EventSystem / New Input System

        public void OnPointerEnter(PointerEventData eventData)
        {
            ResaltarHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            RestaurarColor();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ConstruirTorre();
            }
        }

        #endregion

        public bool ConstruirTorre(bool esGratis = false)
        {
            if (TieneTorre)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.MostrarNotificacion($"Esta casilla ya contiene una torre {torreInstanciada.tipo}.");
                }
                return false;
            }

            TipoTorre tipo = GameManager.Instance != null ? GameManager.Instance.tipoTorreAConstruir : TipoTorre.Basica;
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

            // Crear objeto de la torre seleccionada
            GameObject torreObj = new GameObject($"Torre_{tipo}_{name}");
            torreObj.transform.position = transform.position;
            torreObj.transform.SetParent(transform, true);

            torreInstanciada = torreObj.AddComponent<Torre>();
            torreInstanciada.ConfigurarTipo(tipo);

            RestaurarColor();

            if (GameManager.Instance != null)
            {
                string infoCosto = esGratis ? "gratuita" : $"-{costo} de Oro";
                GameManager.Instance.MostrarNotificacion($"🔨 ¡Torre {tipo} construida en {name}! ({infoCosto})");
            }

            return true;
        }
    }
}
