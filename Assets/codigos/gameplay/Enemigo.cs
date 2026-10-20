using System.Collections;
using UnityEngine;

namespace TowerDefenseGameplay
{
    public enum TipoEnemigo
    {
        Corredor,
        Infanteria,
        Blindado,
        Jefe
    }

    /// <summary>
    /// Componente que gestiona la vida, estados (ralentización, daño)
    /// y recompensas progresivas del enemigo.
    /// </summary>
    [RequireComponent(typeof(enemyMovement))]
    public class Enemigo : MonoBehaviour
    {
        [Header("Estadísticas de Vida")]
        public float vidaMaxima = 50f;
        public float vidaActual;

        [Header("Recompensas Progresivas")]
        public int recompensaXP = 15;
        public int recompensaOro = 10;
        public int danioABase = 10;

        [Header("Efectos")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float factorRalentizacion = 1f;
        private float tiempoRalentizacionRestante = 0f;
        private Color colorOriginal;
        private bool estaMuerto = false;

        // Barra de vida visual en el mundo 2D
        private Transform barraVidaVerde;
        private GameObject contenedorBarraVida;

        public float VelocidadEfectiva => factorRalentizacion;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                colorOriginal = spriteRenderer.color;
            }
            else
            {
                colorOriginal = Color.white;
            }

            vidaActual = vidaMaxima;
            CrearBarraVida();
        }

        [Header("Tipo y Arquetipo")]
        public TipoEnemigo tipoEnemigo = TipoEnemigo.Infanteria;

        public void ConfigurarTipo(TipoEnemigo tipo, int oleada)
        {
            tipoEnemigo = tipo;
            enemyMovement mov = GetComponent<enemyMovement>();

            // Asegurar que el collider sea trigger para evitar atascamientos físicos
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            switch (tipo)
            {
                case TipoEnemigo.Corredor:
                    vidaMaxima = 30f + (oleada * 8f);
                    recompensaXP = 10 + (oleada * 2);
                    recompensaOro = 8 + (oleada * 2);
                    danioABase = 8;
                    transform.localScale = Vector3.one * 0.70f; // Ágil y ligero
                    if (mov != null) mov.ConfigurarVelocidad(3.4f);
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = Color.white;
                        colorOriginal = Color.white;
                    }
                    AjustarTamanioBarraVida(0.70f, 0.10f, 0.65f);
                    break;

                case TipoEnemigo.Infanteria:
                    vidaMaxima = 60f + (oleada * 16f);
                    recompensaXP = 16 + (oleada * 3);
                    recompensaOro = 12 + (oleada * 2);
                    danioABase = 12;
                    transform.localScale = Vector3.one * 0.90f; // Estándar equilibrado
                    if (mov != null) mov.ConfigurarVelocidad(2.0f);
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = Color.white;
                        colorOriginal = Color.white;
                    }
                    AjustarTamanioBarraVida(0.85f, 0.11f, 0.70f);
                    break;

                case TipoEnemigo.Blindado:
                    vidaMaxima = 135f + (oleada * 40f);
                    recompensaXP = 30 + (oleada * 6);
                    recompensaOro = 24 + (oleada * 4);
                    danioABase = 22;
                    transform.localScale = Vector3.one * 1.20f; // Tanque pesado proporcionado
                    if (mov != null) mov.ConfigurarVelocidad(1.25f);
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = Color.white;
                        colorOriginal = Color.white;
                    }
                    AjustarTamanioBarraVida(1.05f, 0.13f, 0.75f);
                    break;

                case TipoEnemigo.Jefe:
                    vidaMaxima = 320f + (oleada * 95f);
                    recompensaXP = 90 + (oleada * 22);
                    recompensaOro = 65 + (oleada * 16);
                    danioABase = 38;
                    transform.localScale = Vector3.one * 1.50f; // Titán imponente adaptado a la ruta
                    if (mov != null) mov.ConfigurarVelocidad(0.90f);
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = Color.white;
                        colorOriginal = Color.white;
                    }
                    AjustarTamanioBarraVida(1.25f, 0.15f, 0.85f);
                    break;
            }

            // Bonificación de DAG: Habilidad Caza de Recompensas
            if (GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_CAZA_RECOMPENSAS"))
            {
                recompensaOro = Mathf.RoundToInt(recompensaOro * 1.35f);
                recompensaXP = Mathf.RoundToInt(recompensaXP * 1.25f);
            }

            vidaActual = vidaMaxima;
            ActualizarBarraVida();
        }

        public void ConfigurarDificultad(int oleada, bool esJefe = false)
        {
            ConfigurarTipo(esJefe ? TipoEnemigo.Jefe : TipoEnemigo.Infanteria, oleada);
        }

        private void Update()
        {
            // Gestión del tiempo de ralentización (efecto de Torre de Hielo)
            if (tiempoRalentizacionRestante > 0)
            {
                tiempoRalentizacionRestante -= Time.deltaTime;
                if (tiempoRalentizacionRestante <= 0)
                {
                    factorRalentizacion = 1f;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = colorOriginal;
                    }
                }
            }
        }

        /// <summary>
        /// Aplica daño al enemigo con soporte para impactos críticos.
        /// </summary>
        public void RecibirDanio(float danio, bool esCritico = false)
        {
            if (estaMuerto) return;

            vidaActual -= danio;
            StartCoroutine(FlashDanio(esCritico));
            ActualizarBarraVida();

            if (vidaActual <= 0)
            {
                Morir();
            }
        }

        /// <summary>
        /// Aplica ralentización (congelamiento) proveniente de la Torre de Hielo desbloqueada en el DAG.
        /// </summary>
        public void AplicarRalentizacion(float porcentaje, float duracion)
        {
            if (estaMuerto) return;

            factorRalentizacion = Mathf.Clamp(1f - porcentaje, 0.2f, 1f);
            tiempoRalentizacionRestante = duracion;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.35f, 0.85f, 1f); // Tinte azul frío
            }
        }

        private IEnumerator FlashDanio(bool esCritico)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = esCritico ? Color.yellow : Color.red;
                yield return new WaitForSeconds(0.08f);
                if (!estaMuerto && spriteRenderer != null)
                {
                    spriteRenderer.color = (tiempoRalentizacionRestante > 0) ? new Color(0.35f, 0.85f, 1f) : colorOriginal;
                }
            }
        }

        private void Morir()
        {
            if (estaMuerto) return;
            estaMuerto = true;

            // Otorgar experiencia y oro progresivos
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEnemigoDerrotado(this);
            }

            Destroy(gameObject);
        }

        #region Barra de Vida 2D en Sprite

        private float anchoBarraActual = 0.9f;
        private float altoBarraActual = 0.12f;

        private void AjustarTamanioBarraVida(float ancho, float alto, float offsetY)
        {
            anchoBarraActual = ancho;
            altoBarraActual = alto;
            if (contenedorBarraVida != null)
            {
                contenedorBarraVida.transform.localPosition = new Vector3(0, offsetY, 0);
                Transform fondo = contenedorBarraVida.transform.Find("FondoRojo");
                if (fondo != null)
                {
                    fondo.localScale = new Vector3(ancho, alto, 1f);
                }
            }
        }

        private void CrearBarraVida()
        {
            contenedorBarraVida = new GameObject("BarraVidaContenedor");
            contenedorBarraVida.transform.SetParent(transform, false);
            contenedorBarraVida.transform.localPosition = new Vector3(0, 0.65f, 0);

            // Fondo Rojo
            GameObject fondo = new GameObject("FondoRojo");
            fondo.transform.SetParent(contenedorBarraVida.transform, false);
            SpriteRenderer srFondo = fondo.AddComponent<SpriteRenderer>();
            srFondo.sprite = ObtenerSpriteCuadrado();
            srFondo.color = new Color(0.7f, 0.1f, 0.1f, 0.9f);
            srFondo.sortingOrder = 10;
            fondo.transform.localScale = new Vector3(anchoBarraActual, altoBarraActual, 1f);

            // Barra Verde (Frente)
            GameObject verde = new GameObject("BarraVerde");
            verde.transform.SetParent(contenedorBarraVida.transform, false);
            SpriteRenderer srVerde = verde.AddComponent<SpriteRenderer>();
            srVerde.sprite = ObtenerSpriteCuadrado();
            srVerde.color = new Color(0.2f, 0.9f, 0.3f, 0.95f);
            srVerde.sortingOrder = 11;
            verde.transform.localScale = new Vector3(anchoBarraActual, altoBarraActual, 1f);
            barraVidaVerde = verde.transform;
        }

        private void ActualizarBarraVida()
        {
            if (barraVidaVerde != null)
            {
                float pct = Mathf.Clamp01(vidaActual / vidaMaxima);
                barraVidaVerde.localScale = new Vector3(anchoBarraActual * pct, altoBarraActual, 1f);
                barraVidaVerde.localPosition = new Vector3(-(anchoBarraActual * 0.5f) * (1f - pct), 0, 0);
            }
        }

        private Sprite ObtenerSpriteCuadrado()
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] cols = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        }

        #endregion
    }
}
