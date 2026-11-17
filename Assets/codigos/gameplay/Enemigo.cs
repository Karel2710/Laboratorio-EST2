using System.Collections;
using System.Collections.Generic;
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
    /// En LAN también recibe los efectos del atacante (escudo, curación, estampida, mejoras DAG de ataque).
    /// </summary>
    [RequireComponent(typeof(enemyMovement))]
    public class Enemigo : MonoBehaviour
    {
        /// <summary>Enemigos vivos en escena (lo usan torres, habilidades y la red).</summary>
        public static readonly List<Enemigo> Activos = new List<Enemigo>();
        private static int contadorIdRed = 0;

        // Bits del byte de estado que viaja por red hacia el cliente
        public const byte FlagRalentizado = 1;
        public const byte FlagEscudo = 2;
        public const byte FlagAcelerado = 4;
        public const byte FlagAturdido = 8;

        [Header("Estadísticas de Vida")]
        public float vidaMaxima = 50f;
        public float vidaActual;

        [Header("Recompensas Progresivas")]
        public int recompensaXP = 15;
        public int recompensaOro = 10;
        public int danioABase = 10;

        [Header("Efectos")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Multijugador / Mejoras de ataque")]
        public int idRed;
        public float multiplicadorVelocidadPasivo = 1f;
        public float regeneracionPorSegundo = 0f;   // fracción de la vida máxima por segundo
        public float resistenciaHielo = 0f;          // 0..1: cuánto se reduce la ralentización
        public float multiplicadorDanioArea = 1f;    // < 1 con "Placas Antiexplosión"

        private float factorRalentizacion = 1f;
        private float tiempoRalentizacionRestante = 0f;
        private Color colorOriginal;
        private bool estaMuerto = false;

        private float escudoActual = 0f;
        private float escudoHasta = 0f;
        private float multiplicadorVelocidadTemporal = 1f;
        private float velocidadTemporalHasta = 0f;
        private float aturdidoHasta = 0f;

        // Barra de vida visual en el mundo 2D
        private Transform barraVidaVerde;
        private GameObject contenedorBarraVida;
        private SpriteRenderer anilloEscudo;

        public bool EstaMuerto => estaMuerto;
        public bool TieneEscudo => escudoActual > 0f && Time.time < escudoHasta;
        public bool EstaAturdido => Time.time < aturdidoHasta;
        public bool EstaAcelerado => Time.time < velocidadTemporalHasta && multiplicadorVelocidadTemporal > 1f;
        public bool EstaRalentizado => tiempoRalentizacionRestante > 0f;

        public float VelocidadEfectiva
        {
            get
            {
                if (EstaAturdido) return 0f;
                float temporal = Time.time < velocidadTemporalHasta ? multiplicadorVelocidadTemporal : 1f;
                return factorRalentizacion * multiplicadorVelocidadPasivo * temporal;
            }
        }

        public byte FlagsRed
        {
            get
            {
                byte f = 0;
                if (EstaRalentizado) f |= FlagRalentizado;
                if (TieneEscudo) f |= FlagEscudo;
                if (EstaAcelerado) f |= FlagAcelerado;
                if (EstaAturdido) f |= FlagAturdido;
                return f;
            }
        }

        private void Awake()
        {
            idRed = ++contadorIdRed;

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

        private void OnEnable()
        {
            if (!Activos.Contains(this)) Activos.Add(this);
        }

        private void OnDisable()
        {
            Activos.Remove(this);
        }

        [Header("Tipo y Arquetipo")]
        public TipoEnemigo tipoEnemigo = TipoEnemigo.Infanteria;

        /// <summary>Escala visual por tipo (la usa también el proxy del cliente LAN).</summary>
        public static float EscalaPorTipo(TipoEnemigo tipo)
        {
            switch (tipo)
            {
                case TipoEnemigo.Corredor: return 0.70f;
                case TipoEnemigo.Blindado: return 1.20f;
                case TipoEnemigo.Jefe: return 1.50f;
                default: return 0.90f;
            }
        }

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
                    if (mov != null) mov.ConfigurarVelocidad(3.4f);
                    AjustarTamanioBarraVida(0.70f, 0.10f, 0.65f);
                    break;

                case TipoEnemigo.Infanteria:
                    vidaMaxima = 60f + (oleada * 16f);
                    recompensaXP = 16 + (oleada * 3);
                    recompensaOro = 12 + (oleada * 2);
                    danioABase = 12;
                    if (mov != null) mov.ConfigurarVelocidad(2.0f);
                    AjustarTamanioBarraVida(0.85f, 0.11f, 0.70f);
                    break;

                case TipoEnemigo.Blindado:
                    vidaMaxima = 135f + (oleada * 40f);
                    recompensaXP = 30 + (oleada * 6);
                    recompensaOro = 24 + (oleada * 4);
                    danioABase = 22;
                    if (mov != null) mov.ConfigurarVelocidad(1.25f);
                    AjustarTamanioBarraVida(1.05f, 0.13f, 0.75f);
                    break;

                case TipoEnemigo.Jefe:
                    vidaMaxima = 320f + (oleada * 95f);
                    recompensaXP = 90 + (oleada * 22);
                    recompensaOro = 65 + (oleada * 16);
                    danioABase = 38;
                    if (mov != null) mov.ConfigurarVelocidad(0.90f);
                    AjustarTamanioBarraVida(1.25f, 0.15f, 0.85f);
                    break;
            }

            transform.localScale = Vector3.one * EscalaPorTipo(tipo);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                colorOriginal = Color.white;
            }

            // Dificultad del modo un jugador (Nivel 1 / 2 / 3)
            if (!ModoJuego.EsMultijugador && GameManager.Instance != null)
            {
                vidaMaxima *= GameManager.Instance.multiplicadorVidaEnemigos;
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

        /// <summary>Multiplica la vida máxima (mejora "Blindaje Reforzado" del atacante).</summary>
        public void MultiplicarVidaMaxima(float factor)
        {
            vidaMaxima *= factor;
            vidaActual = vidaMaxima;
            ActualizarBarraVida();
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

            // Regeneración (mejora DAG de ataque)
            if (regeneracionPorSegundo > 0f && vidaActual < vidaMaxima && !estaMuerto)
            {
                vidaActual = Mathf.Min(vidaMaxima, vidaActual + vidaMaxima * regeneracionPorSegundo * Time.deltaTime);
                ActualizarBarraVida();
            }

            if (escudoActual > 0f && Time.time >= escudoHasta)
            {
                escudoActual = 0f;
            }

            if (anilloEscudo != null)
            {
                bool mostrar = TieneEscudo;
                if (anilloEscudo.enabled != mostrar) anilloEscudo.enabled = mostrar;
            }
        }

        /// <summary>
        /// Aplica daño al enemigo con soporte para impactos críticos.
        /// </summary>
        public void RecibirDanio(float danio, bool esCritico = false)
        {
            if (estaMuerto) return;

            // El escudo (habilidad del atacante) absorbe el daño primero
            if (TieneEscudo)
            {
                float absorbido = Mathf.Min(escudoActual, danio);
                escudoActual -= absorbido;
                danio -= absorbido;
            }

            if (danio <= 0f) return;

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

            // "Anticongelante" del atacante reduce el efecto del hielo
            porcentaje *= (1f - resistenciaHielo);

            factorRalentizacion = Mathf.Clamp(1f - porcentaje, 0.2f, 1f);
            tiempoRalentizacionRestante = duracion;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.35f, 0.85f, 1f); // Tinte azul frío
            }
        }

        /// <summary>Detiene al enemigo por completo (Pulso EMP del defensor).</summary>
        public void Aturdir(float duracion)
        {
            if (estaMuerto) return;
            aturdidoHasta = Mathf.Max(aturdidoHasta, Time.time + duracion);
        }

        /// <summary>Escudo temporal que absorbe daño (habilidades del atacante).</summary>
        public void AplicarEscudo(float cantidad, float duracion)
        {
            if (estaMuerto) return;
            escudoActual = Mathf.Max(escudoActual, cantidad);
            escudoHasta = Mathf.Max(escudoHasta, Time.time + duracion);
        }

        /// <summary>Cura una fracción de la vida máxima (habilidad "Curación de Campo").</summary>
        public void Curar(float fraccionVidaMaxima)
        {
            if (estaMuerto) return;
            vidaActual = Mathf.Min(vidaMaxima, vidaActual + vidaMaxima * fraccionVidaMaxima);
            ActualizarBarraVida();
        }

        /// <summary>Aumento temporal de velocidad ("Estampida", "Asalto Total").</summary>
        public void AplicarVelocidadTemporal(float multiplicador, float duracion)
        {
            if (estaMuerto) return;
            multiplicadorVelocidadTemporal = Mathf.Max(Time.time < velocidadTemporalHasta ? multiplicadorVelocidadTemporal : 1f, multiplicador);
            velocidadTemporalHasta = Mathf.Max(velocidadTemporalHasta, Time.time + duracion);
        }

        /// <summary>
        /// Marca al enemigo como retirado al llegar a la base, para que un proyectil que
        /// impacte en el mismo frame no lo cuente también como derrotado.
        /// </summary>
        public void Retirar()
        {
            estaMuerto = true;
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

            // Anillo de escudo (se ve solo mientras el escudo del atacante está activo)
            GameObject anillo = new GameObject("AnilloEscudo");
            anillo.transform.SetParent(transform, false);
            anilloEscudo = anillo.AddComponent<SpriteRenderer>();
            anilloEscudo.sprite = EfectoPulso.SpriteAnillo;
            anilloEscudo.color = new Color(0.35f, 0.9f, 1f, 0.85f);
            anilloEscudo.sortingOrder = 9;
            anillo.transform.localScale = Vector3.one * 1.25f;
            anilloEscudo.enabled = false;
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

        private static Sprite spriteCuadrado;

        public static Sprite ObtenerSpriteCuadrado()
        {
            if (spriteCuadrado != null) return spriteCuadrado;
            Texture2D tex = new Texture2D(2, 2);
            Color[] cols = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(cols);
            tex.Apply();
            spriteCuadrado = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
            return spriteCuadrado;
        }

        #endregion
    }
}
