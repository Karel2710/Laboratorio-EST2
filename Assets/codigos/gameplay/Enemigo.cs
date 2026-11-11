using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    public enum TipoEnemigo
    {
        Corredor,
        Infanteria,
        Blindado,
        Jefe,
        Sanador,
        VelozFurtivo,
        BlindadoPesado
    }

    public enum TipoDanio
    {
        Cinetico,
        Hielo,
        Explosivo,
        Energia,
        Electrico
    }

    /// <summary>
    /// Componente que gestiona la vida, escudo, estados (ralentización, congelación),
    /// resistencias por tipo de daño y recompensas del enemigo.
    /// </summary>
    [RequireComponent(typeof(enemyMovement))]
    public class Enemigo : MonoBehaviour
    {
        /// <summary>Registro de enemigos vivos (las torres lo consultan sin búsquedas costosas).</summary>
        public static readonly List<Enemigo> Activos = new List<Enemigo>();

        [Header("Estadísticas de Vida")]
        public float vidaMaxima = 50f;
        public float vidaActual;
        public float escudoMaximo = 0f;
        public float escudoActual = 0f;

        [Header("Recompensas Progresivas")]
        public int recompensaXP = 15;
        public int recompensaOro = 10;
        public int danioABase = 10;
        public int puntos = 10;

        [Header("Tipo y Arquetipo")]
        public TipoEnemigo tipoEnemigo = TipoEnemigo.Infanteria;
        public bool esMercenario = false;

        [Header("Efectos")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float factorRalentizacion = 1f;
        private float tiempoRalentizacionRestante = 0f;
        private float congeladoRestante = 0f;
        private float flashRestante = 0f;
        private Color colorFlash = Color.white;
        private Color colorOriginal = Color.white;
        private bool estaMuerto = false;
        private float temporizadorCura = 0f;
        private enemyMovement movimiento;

        private Transform barraVida;
        private Transform barraEscudo;
        private SpriteRenderer srBarraVida;
        private GameObject contenedorBarraVida;
        private float anchoBarra = 0.9f;
        private float altoBarra = 0.12f;

        private static readonly Color colorFrio = new Color(0.35f, 0.85f, 1f);

        public bool EstaMuerto => estaMuerto;
        public bool EstaCongelado => congeladoRestante > 0f;
        public bool EsFurtivo => tipoEnemigo == TipoEnemigo.VelozFurtivo;
        public float VelocidadEfectiva => congeladoRestante > 0f ? 0f : factorRalentizacion;
        public float DistanciaRestante => movimiento != null ? movimiento.DistanciaRestante : 0f;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) colorOriginal = spriteRenderer.color;
            movimiento = GetComponent<enemyMovement>();
            vidaActual = vidaMaxima;
            CrearBarraVida();
        }

        private void OnEnable()
        {
            if (!Activos.Contains(this)) Activos.Add(this);
            Paleta.OnCambio += ActualizarBarraVida;
        }

        private void OnDisable()
        {
            Activos.Remove(this);
            Paleta.OnCambio -= ActualizarBarraVida;
        }

        /// <summary>Multiplicador de daño recibido según arquetipo y tipo de daño (resistencias y debilidades).</summary>
        public static float Resistencia(TipoEnemigo tipo, TipoDanio danio)
        {
            switch (tipo)
            {
                case TipoEnemigo.Corredor:
                    return danio == TipoDanio.Explosivo ? 1.2f : 1f;
                case TipoEnemigo.Blindado:
                    if (danio == TipoDanio.Cinetico) return 0.7f;
                    if (danio == TipoDanio.Explosivo) return 1.25f;
                    if (danio == TipoDanio.Energia) return 1.2f;
                    return 1f;
                case TipoEnemigo.Jefe:
                    return danio == TipoDanio.Electrico ? 0.8f : 1f;
                case TipoEnemigo.Sanador:
                    return danio == TipoDanio.Electrico ? 1.3f : 1f;
                case TipoEnemigo.VelozFurtivo:
                    if (danio == TipoDanio.Explosivo) return 0.6f;
                    if (danio == TipoDanio.Electrico) return 1.4f;
                    if (danio == TipoDanio.Hielo) return 1.2f;
                    return 1f;
                case TipoEnemigo.BlindadoPesado:
                    if (danio == TipoDanio.Cinetico) return 0.5f;
                    if (danio == TipoDanio.Explosivo) return 0.9f;
                    if (danio == TipoDanio.Energia) return 1.35f;
                    if (danio == TipoDanio.Electrico) return 1.2f;
                    if (danio == TipoDanio.Hielo) return 0.8f;
                    return 1f;
                default:
                    return 1f;
            }
        }

        public void ConfigurarTipo(TipoEnemigo tipo, int oleada, float multVida = 1f, float multVelocidad = 1f)
        {
            tipoEnemigo = tipo;
            if (movimiento == null) movimiento = GetComponent<enemyMovement>();

            // Físicas anti-atasco: el collider siempre es trigger
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            float velocidad = 2f;
            float escala = 0.9f;
            escudoMaximo = 0f;

            switch (tipo)
            {
                case TipoEnemigo.Corredor:
                    vidaMaxima = 30f + oleada * 8f; recompensaXP = 10 + oleada * 2; recompensaOro = 8 + oleada * 2;
                    danioABase = 8; velocidad = 3.4f; escala = 0.70f; puntos = 10;
                    AjustarBarra(0.70f, 0.10f, 0.65f);
                    break;
                case TipoEnemigo.Infanteria:
                    vidaMaxima = 60f + oleada * 16f; recompensaXP = 16 + oleada * 3; recompensaOro = 12 + oleada * 2;
                    danioABase = 12; velocidad = 2.0f; escala = 0.90f; puntos = 15;
                    AjustarBarra(0.85f, 0.11f, 0.70f);
                    break;
                case TipoEnemigo.Blindado:
                    vidaMaxima = 135f + oleada * 40f; recompensaXP = 30 + oleada * 6; recompensaOro = 24 + oleada * 4;
                    danioABase = 22; velocidad = 1.25f; escala = 1.20f; puntos = 30;
                    AjustarBarra(1.05f, 0.13f, 0.75f);
                    break;
                case TipoEnemigo.Jefe:
                    vidaMaxima = 320f + oleada * 95f; recompensaXP = 90 + oleada * 22; recompensaOro = 65 + oleada * 16;
                    danioABase = 38; velocidad = 0.90f; escala = 1.50f; puntos = 150;
                    AjustarBarra(1.25f, 0.15f, 0.85f);
                    break;
                case TipoEnemigo.Sanador:
                    vidaMaxima = 70f + oleada * 18f; recompensaXP = 26 + oleada * 4; recompensaOro = 20 + oleada * 3;
                    danioABase = 10; velocidad = 1.7f; escala = 0.95f; puntos = 35;
                    AjustarBarra(0.85f, 0.11f, 0.72f);
                    break;
                case TipoEnemigo.VelozFurtivo:
                    vidaMaxima = 38f + oleada * 9f; recompensaXP = 18 + oleada * 3; recompensaOro = 14 + oleada * 3;
                    danioABase = 12; velocidad = 3.9f; escala = 0.75f; puntos = 25;
                    AjustarBarra(0.70f, 0.10f, 0.65f);
                    break;
                case TipoEnemigo.BlindadoPesado:
                    vidaMaxima = 210f + oleada * 55f; recompensaXP = 48 + oleada * 8; recompensaOro = 38 + oleada * 6;
                    danioABase = 30; velocidad = 0.95f; escala = 1.35f; puntos = 60;
                    escudoMaximo = vidaMaxima * 0.4f;
                    AjustarBarra(1.15f, 0.14f, 0.82f);
                    break;
            }

            vidaMaxima *= multVida;
            escudoMaximo *= multVida;
            transform.localScale = Vector3.one * escala;
            if (movimiento != null) movimiento.ConfigurarVelocidad(velocidad * multVelocidad);

            colorOriginal = EsFurtivo ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            if (spriteRenderer != null) spriteRenderer.color = colorOriginal;

            // Bonificación de DAG: Cazador de Recompensas
            if (GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_CAZA_RECOMPENSAS"))
            {
                recompensaOro = Mathf.RoundToInt(recompensaOro * 1.35f);
                recompensaXP = Mathf.RoundToInt(recompensaXP * 1.25f);
            }

            vidaActual = vidaMaxima;
            escudoActual = escudoMaximo;
            ActualizarBarraVida();
        }

        public void ConfigurarDificultad(int oleada, bool esJefe = false)
        {
            ConfigurarTipo(esJefe ? TipoEnemigo.Jefe : TipoEnemigo.Infanteria, oleada);
        }

        private void Update()
        {
            if (estaMuerto) return;
            float dt = Time.deltaTime;

            if (congeladoRestante > 0f) congeladoRestante -= dt;

            if (tiempoRalentizacionRestante > 0f)
            {
                tiempoRalentizacionRestante -= dt;
                if (tiempoRalentizacionRestante <= 0f) factorRalentizacion = 1f;
            }

            // Color resultante: destello de daño > hielo > normal
            if (spriteRenderer != null)
            {
                if (flashRestante > 0f)
                {
                    flashRestante -= Time.unscaledDeltaTime;
                    spriteRenderer.color = colorFlash;
                }
                else if (congeladoRestante > 0f) spriteRenderer.color = new Color(0.1f, 0.95f, 1f, colorOriginal.a);
                else if (tiempoRalentizacionRestante > 0f) spriteRenderer.color = new Color(colorFrio.r, colorFrio.g, colorFrio.b, colorOriginal.a);
                else spriteRenderer.color = colorOriginal;
            }

            // Sanador: emite pulsos que curan a los aliados cercanos
            if (tipoEnemigo == TipoEnemigo.Sanador)
            {
                temporizadorCura -= dt;
                if (temporizadorCura <= 0f)
                {
                    temporizadorCura = 2.5f;
                    EmitirPulsoCurativo();
                }
            }
        }

        private void EmitirPulsoCurativo()
        {
            const float radio = 2.2f;
            bool curoAlguno = false;
            for (int i = 0; i < Activos.Count; i++)
            {
                Enemigo e = Activos[i];
                if (e == null || e == this || e.estaMuerto) continue;
                if (e.vidaActual >= e.vidaMaxima) continue;
                if (Vector2.Distance(transform.position, e.transform.position) > radio) continue;
                e.Curar(e.vidaMaxima * 0.07f);
                curoAlguno = true;
            }
            if (EfectosVisualesManager.Instance != null)
            {
                Color c = Paleta.Positivo;
                c.a = curoAlguno ? 0.9f : 0.35f;
                EfectosVisualesManager.Instance.OndaExpansiva(transform.position, radio, c, 0.5f);
            }
        }

        public void Curar(float cantidad)
        {
            if (estaMuerto) return;
            vidaActual = Mathf.Min(vidaMaxima, vidaActual + cantidad);
            ActualizarBarraVida();
        }

        /// <summary>
        /// Aplica daño considerando resistencias y escudo. Devuelve el daño efectivo infligido.
        /// </summary>
        public float RecibirDanio(float danio, bool esCritico = false, TipoDanio tipoDanio = TipoDanio.Cinetico, bool mostrarTexto = true)
        {
            if (estaMuerto) return 0f;

            float efectivo = danio * Resistencia(tipoEnemigo, tipoDanio);
            float restante = efectivo;

            if (escudoActual > 0f)
            {
                // El daño de energía y eléctrico es más eficaz contra escudos
                float factorEscudo = (tipoDanio == TipoDanio.Energia || tipoDanio == TipoDanio.Electrico) ? 1.5f : 1f;
                float absorbido = Mathf.Min(escudoActual, restante * factorEscudo);
                escudoActual -= absorbido;
                restante -= absorbido / factorEscudo;
            }

            vidaActual -= restante;
            flashRestante = 0.08f;
            colorFlash = esCritico ? Color.yellow : (Paleta.Daltonico ? Color.white : new Color(1f, 0.35f, 0.35f));
            ActualizarBarraVida();

            if (mostrarTexto && EfectosVisualesManager.Instance != null)
            {
                Vector3 pos = transform.position + new Vector3(Random.Range(-0.15f, 0.15f), 0.45f, 0f);
                if (esCritico)
                    EfectosVisualesManager.Instance.TextoFlotante(pos, $"¡CRÍTICO! {efectivo:0}", new Color(1f, 0.55f, 0.1f), 1.5f);
                else if (tipoDanio == TipoDanio.Hielo)
                    EfectosVisualesManager.Instance.TextoFlotante(pos, $"{efectivo:0}", new Color(0.45f, 0.8f, 1f), 0.9f);
                else if (tipoDanio == TipoDanio.Electrico)
                    EfectosVisualesManager.Instance.TextoFlotante(pos, $"{efectivo:0}", new Color(0.8f, 0.7f, 1f), 0.9f);
                else
                    EfectosVisualesManager.Instance.TextoFlotante(pos, $"{efectivo:0}", Color.white, 0.9f);
            }

            if (vidaActual <= 0f) Morir();
            return efectivo;
        }

        /// <summary>Ralentización criogénica. Los jefes resisten la mitad del efecto.</summary>
        public void AplicarRalentizacion(float porcentaje, float duracion)
        {
            if (estaMuerto) return;
            if (tipoEnemigo == TipoEnemigo.Jefe) porcentaje *= 0.5f;
            float nuevoFactor = Mathf.Clamp(1f - porcentaje, 0.2f, 1f);
            factorRalentizacion = Mathf.Min(factorRalentizacion, nuevoFactor);
            tiempoRalentizacionRestante = Mathf.Max(tiempoRalentizacionRestante, duracion);
        }

        /// <summary>Detiene en seco al enemigo (Cero Absoluto / Pulso EMP).</summary>
        public void Congelar(float duracion)
        {
            if (estaMuerto) return;
            if (tipoEnemigo == TipoEnemigo.Jefe) duracion *= 0.6f;
            congeladoRestante = Mathf.Max(congeladoRestante, duracion);
        }

        public void DestruirEscudo()
        {
            if (escudoActual <= 0f) return;
            escudoActual = 0f;
            ActualizarBarraVida();
            if (EfectosVisualesManager.Instance != null)
                EfectosVisualesManager.Instance.TextoFlotante(transform.position + Vector3.up * 0.6f, "ESCUDO ROTO", Paleta.Cian, 1.1f);
        }

        private void Morir()
        {
            if (estaMuerto) return;
            estaMuerto = true;

            if (EfectosVisualesManager.Instance != null)
            {
                EfectosVisualesManager fx = EfectosVisualesManager.Instance;
                bool jefe = tipoEnemigo == TipoEnemigo.Jefe;
                fx.Explosion(transform.position, SpritesEnemigos.ColorPrincipal(tipoEnemigo), jefe ? 36 : 12, jefe ? 5f : 3f);
                if (!esMercenario || recompensaOro > 0)
                    fx.TextoFlotante(transform.position + Vector3.up * 0.3f, $"+{recompensaOro} Oro", Paleta.Dorado, 1.05f);
                if (jefe)
                {
                    fx.OndaExpansiva(transform.position, 2.6f, new Color(1f, 0.5f, 0.15f), 0.5f);
                    fx.SacudirCamara(0.28f, 0.45f);
                }
                fx.Sonido(jefe ? TipoSonido.Explosion : TipoSonido.Baja);
            }

            if (GameManager.Instance != null) GameManager.Instance.OnEnemigoDerrotado(this);
            Destroy(gameObject);
        }

        #region Barra de Vida y Escudo

        private void AjustarBarra(float ancho, float alto, float offsetY)
        {
            anchoBarra = ancho;
            altoBarra = alto;
            if (contenedorBarraVida != null)
            {
                contenedorBarraVida.transform.localPosition = new Vector3(0, offsetY, 0);
                Transform fondo = contenedorBarraVida.transform.Find("Fondo");
                if (fondo != null) fondo.localScale = new Vector3(ancho + 0.04f, alto + 0.04f, 1f);
            }
        }

        private void CrearBarraVida()
        {
            contenedorBarraVida = new GameObject("BarraVidaContenedor");
            contenedorBarraVida.transform.SetParent(transform, false);
            contenedorBarraVida.transform.localPosition = new Vector3(0, 0.65f, 0);

            SpriteRenderer fondo = GraficosProc.CrearSprite(contenedorBarraVida.transform, "Fondo", GraficosProc.Cuadrado, new Color(0.05f, 0.05f, 0.08f, 0.9f), 10);
            fondo.transform.localScale = new Vector3(anchoBarra + 0.04f, altoBarra + 0.04f, 1f);

            srBarraVida = GraficosProc.CrearSprite(contenedorBarraVida.transform, "Vida", GraficosProc.Cuadrado, Paleta.Positivo, 11);
            barraVida = srBarraVida.transform;

            SpriteRenderer esc = GraficosProc.CrearSprite(contenedorBarraVida.transform, "Escudo", GraficosProc.Cuadrado, Paleta.Cian, 12);
            barraEscudo = esc.transform;
        }

        private void ActualizarBarraVida()
        {
            if (barraVida == null) return;
            float pct = Mathf.Clamp01(vidaActual / Mathf.Max(1f, vidaMaxima));
            barraVida.localScale = new Vector3(anchoBarra * pct, altoBarra, 1f);
            barraVida.localPosition = new Vector3(-(anchoBarra * 0.5f) * (1f - pct), 0, 0);
            if (srBarraVida != null) srBarraVida.color = Color.Lerp(Paleta.Negativo, Paleta.Positivo, pct);

            if (barraEscudo != null)
            {
                float pe = escudoMaximo > 0f ? Mathf.Clamp01(escudoActual / escudoMaximo) : 0f;
                barraEscudo.gameObject.SetActive(pe > 0f);
                barraEscudo.localScale = new Vector3(anchoBarra * pe, altoBarra * 0.6f, 1f);
                barraEscudo.localPosition = new Vector3(-(anchoBarra * 0.5f) * (1f - pe), altoBarra * 0.95f, 0);
            }
        }

        #endregion
    }

    /// <summary>
    /// Sprites procedurales de los enemigos. Cada arquetipo tiene una silueta distinta
    /// (no solo un color) para que sea distinguible también en modo daltónico.
    /// </summary>
    public static class SpritesEnemigos
    {
        private static readonly Dictionary<TipoEnemigo, Sprite> cache = new Dictionary<TipoEnemigo, Sprite>();

        public static Color ColorPrincipal(TipoEnemigo tipo)
        {
            switch (tipo)
            {
                case TipoEnemigo.Corredor: return new Color(1f, 0.82f, 0.15f);
                case TipoEnemigo.Infanteria: return new Color(0.3f, 0.85f, 0.5f);
                case TipoEnemigo.Blindado: return new Color(0.62f, 0.38f, 0.9f);
                case TipoEnemigo.Jefe: return new Color(0.95f, 0.22f, 0.2f);
                case TipoEnemigo.Sanador: return new Color(0.45f, 1f, 0.75f);
                case TipoEnemigo.VelozFurtivo: return new Color(0.7f, 0.75f, 0.9f);
                default: return new Color(0.55f, 0.7f, 0.85f);
            }
        }

        public static string Nombre(TipoEnemigo tipo)
        {
            switch (tipo)
            {
                case TipoEnemigo.VelozFurtivo: return "Veloz Furtivo";
                case TipoEnemigo.BlindadoPesado: return "Blindado Pesado";
                case TipoEnemigo.Infanteria: return "Infantería";
                default: return tipo.ToString();
            }
        }

        public static Sprite Obtener(TipoEnemigo tipo)
        {
            if (cache.TryGetValue(tipo, out Sprite s) && s != null) return s;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 c0 = new Vector2(size / 2f, size / 2f);
            float maxR = size / 2f - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c0.x, dy = y - c0.y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float nd = d / maxR;
                    float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                    Color c = Color.clear;

                    switch (tipo)
                    {
                        case TipoEnemigo.Corredor:
                        {
                            float m = (ax + ay) / maxR;
                            if (m <= 1f)
                            {
                                if (m > 0.88f) c = new Color(0.18f, 0.12f, 0.02f);
                                else if (m > 0.68f) c = new Color(1f, 0.55f, 0.05f);
                                else if (m > 0.35f) c = new Color(1f, 0.88f, 0.15f);
                                else c = new Color(1f, 1f, 0.8f);
                            }
                            break;
                        }
                        case TipoEnemigo.Infanteria:
                            if (nd <= 1f)
                            {
                                if (nd > 0.88f) c = new Color(0.08f, 0.22f, 0.12f);
                                else if (nd > 0.70f) c = new Color(0.25f, 0.78f, 0.45f);
                                else if (nd > 0.55f) c = new Color(0.12f, 0.45f, 0.22f);
                                else if (ax < 2 || ay < 2) c = new Color(0.05f, 0.18f, 0.08f);
                                else c = new Color(0.55f, 0.95f, 0.68f);
                            }
                            break;
                        case TipoEnemigo.Blindado:
                        {
                            float o = Mathf.Max(ax, ay, (ax + ay) * 0.72f) / maxR;
                            if (o <= 1f)
                            {
                                if (o > 0.88f) c = new Color(0.12f, 0.08f, 0.24f);
                                else if (o > 0.70f) c = new Color(0.58f, 0.32f, 0.85f);
                                else if (o > 0.52f) c = new Color(0.28f, 0.14f, 0.45f);
                                else if (o > 0.25f) c = new Color(0.75f, 0.50f, 0.98f);
                                else c = new Color(0.15f, 0.08f, 0.28f);
                            }
                            break;
                        }
                        case TipoEnemigo.Jefe:
                        {
                            float ang = Mathf.Atan2(dy, dx);
                            float r = maxR * (0.88f + Mathf.Sin(ang * 8f) * 0.12f);
                            if (d <= r)
                            {
                                if (d > r - 4f) c = new Color(0.25f, 0.02f, 0.02f);
                                else if (nd > 0.62f) c = new Color(0.92f, 0.18f, 0.18f);
                                else if (nd > 0.42f) c = new Color(0.55f, 0.06f, 0.06f);
                                else if (nd > 0.18f) c = new Color(1f, 0.62f, 0.12f);
                                else c = new Color(1f, 0.98f, 0.5f);
                            }
                            break;
                        }
                        case TipoEnemigo.Sanador:
                            // Círculo menta con una cruz médica blanca
                            if (nd <= 1f)
                            {
                                bool cruz = (ax < 5f && ay < 17f) || (ay < 5f && ax < 17f);
                                if (nd > 0.88f) c = new Color(0.05f, 0.3f, 0.22f);
                                else if (cruz) c = Color.white;
                                else if (nd > 0.68f) c = new Color(0.3f, 0.95f, 0.7f);
                                else c = new Color(0.12f, 0.55f, 0.42f);
                            }
                            break;
                        case TipoEnemigo.VelozFurtivo:
                        {
                            // Dardo triangular apuntando hacia arriba
                            float t = (dy + maxR) / (2f * maxR);          // 0 abajo, 1 arriba
                            float medio = maxR * 0.85f * (1f - t);
                            if (t >= 0.05f && t <= 1f && ax <= medio)
                            {
                                float borde = medio - ax;
                                if (borde < 3f || t < 0.12f) c = new Color(0.15f, 0.17f, 0.3f);
                                else if (ax < 3f) c = new Color(0.95f, 0.98f, 1f);
                                else c = new Color(0.62f, 0.68f, 0.9f);
                            }
                            break;
                        }
                        case TipoEnemigo.BlindadoPesado:
                        {
                            // Bloque cuadrado de acero con remaches y núcleo
                            float q = Mathf.Max(ax, ay) / maxR;
                            if (q <= 0.92f)
                            {
                                bool remache = (Mathf.Abs(ax - maxR * 0.62f) < 3f && Mathf.Abs(ay - maxR * 0.62f) < 3f);
                                if (q > 0.80f) c = new Color(0.12f, 0.16f, 0.22f);
                                else if (remache) c = new Color(0.95f, 0.85f, 0.4f);
                                else if (q > 0.55f) c = new Color(0.5f, 0.62f, 0.75f);
                                else if (q > 0.3f) c = new Color(0.28f, 0.36f, 0.48f);
                                else c = new Color(0.3f, 0.95f, 1f);
                            }
                            break;
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 48);
            cache[tipo] = s;
            return s;
        }
    }
}
