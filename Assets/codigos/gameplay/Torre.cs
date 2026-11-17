using System.Collections.Generic;
using UnityEngine;
using GrafosEST2;

namespace TowerDefenseGameplay
{
    public enum TipoTorre
    {
        Basica,
        Hielo,
        Canon,
        Laser,
        Tesla,
        Radar
    }

    public enum PrioridadObjetivo
    {
        Primero,
        Ultimo,
        MasFuerte,
        MasDebil,
        MasCercano
    }

    /// <summary>
    /// Torre de defensa: 6 tipos, mejoras de nivel 1 a 3 y prioridad de blanco configurable.
    /// Sus atributos se modifican en tiempo real según las habilidades del DAG,
    /// la clase del comandante y la sinergia de la red MST (Kruskal).
    /// </summary>
    public class Torre : MonoBehaviour
    {
        public static readonly List<Torre> Activas = new List<Torre>();
        private static int contadorIds = 0;

        [Header("Configuración de Torre")]
        public TipoTorre tipo = TipoTorre.Basica;
        public int nivel = 1;
        public float alcance = 3.8f;
        public float cadenciaDisparoBase = 0.8f;
        public float danioBase = 22f;
        public int costoConstruccion = 50;
        public int inversionTotal = 0;
        public PrioridadObjetivo prioridad = PrioridadObjetivo.Primero;

        [Header("Propietario (multijugador)")]
        public int propietarioId = -1;
        public string propietarioNombre = "";
        public Color colorPropietario = Color.clear;
        public PlotTorre plot;

        [Header("Red MST (Kruskal)")]
        public int gradoMST = 0;
        public float bonoDanioRed = 0f;
        public float bonoCadenciaRed = 0f;
        public bool enRedConRadar = false;
        public int componenteMST = -1;

        public int idTorre;
        public const int NIVEL_MAXIMO = 3;

        [Header("Multijugador LAN")]
        [Tooltip("Cliente LAN: la torre solo se dibuja (el host es quien dispara de verdad).")]
        public bool soloVisual = false;
        private float hackeadaHasta = 0f;
        private bool hackeadaVisual = false;
        private bool tinteHackeoAplicado = false;
        private Enemigo laserVisualObjetivo;
        private float laserVisualHasta = 0f;
        private Color colorBaseGuardado, colorAnilloGuardado, colorCanonGuardado;

        /// <summary>Escala local base (compensa la escala de la casilla padre).</summary>
        public Vector3 escalaBase = Vector3.one;

        private Transform canonRotatorio;
        private Transform barril;
        private SpriteRenderer srBase, srAnillo, srCanon, srBrilloRed, srPropietario, srAnilloElite;
        private readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
        private LineRenderer hazLaser;
        private Vector3 posBarril = new Vector3(0, 0.28f, 0);
        private float retroceso = 0f;

        private float temporizadorDisparo = 0f;
        private float temporizadorBusqueda = 0f;
        private Enemigo objetivoActual;
        private float tiempoEnfoqueLaser = 0f;
        private float acumuladorLaser = 0f;
        private float temporizadorRadar = 0f;
        private bool visualesListos = false;

        // ── Vista 2.5D ──
        private Transform raiz3D;      // cuerpo de la torre (modelo Kenney según tipo y nivel)
        private Transform arma3D;      // cabezal que gira hacia el objetivo
        private Transform canon3D;     // pieza que retrocede al disparar
        private Transform giro3D;      // pieza decorativa animada (cristal, orbe)
        private Vector3 posCanon3D;
        private Vector3 punta3D;       // boca del arma en espacio local del cabezal
        private float alturaCuerpo3D;
        private float anguloRadar;
        private int versionVista = -1;

        /// <summary>Altura del cabezal en la vista 2.5D (0 en 2D). Se usa para los rayos de la red MST.</summary>
        public float AlturaVisual => Vista25D.Activa && arma3D != null ? Vista25D.ALTURA_PAD + alturaCuerpo3D + 0.15f : 0f;

        public bool EsRadar => tipo == TipoTorre.Radar;
        public string NombreCorto => Sigla(tipo) + idTorre;
        public Enemigo ObjetivoActual => objetivoActual;

        /// <summary>True mientras la habilidad "Hackeo" del atacante LAN tiene desactivada esta torre.</summary>
        public bool EstaHackeada => soloVisual ? hackeadaVisual : Time.time < hackeadaHasta;

        /// <summary>Índice de la casilla donde está la torre (identificador en la red LAN).</summary>
        public int IndicePlot => plot != null ? plot.indice : -1;

        #region Catálogo de Tipos

        public static readonly TipoTorre[] TodosLosTipos =
        {
            TipoTorre.Basica, TipoTorre.Hielo, TipoTorre.Canon, TipoTorre.Laser, TipoTorre.Tesla, TipoTorre.Radar
        };

        public static string Nombre(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Basica: return "Centinela";
                case TipoTorre.Hielo: return "Criogénica";
                case TipoTorre.Canon: return "Cañón Área";
                case TipoTorre.Laser: return "Haz Láser";
                case TipoTorre.Tesla: return "Bobina Tesla";
                default: return "Torre Radar";
            }
        }

        public static string Icono(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Basica: return "🔨";
                case TipoTorre.Hielo: return "❄️";
                case TipoTorre.Canon: return "💥";
                case TipoTorre.Laser: return "⚡";
                case TipoTorre.Tesla: return "🌩️";
                default: return "📡";
            }
        }

        public static string Sigla(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Basica: return "C";
                case TipoTorre.Hielo: return "H";
                case TipoTorre.Canon: return "K";
                case TipoTorre.Laser: return "L";
                case TipoTorre.Tesla: return "T";
                default: return "R";
            }
        }

        public static string Descripcion(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Basica: return "Cadencia equilibrada. Daño cinético.";
                case TipoTorre.Hielo: return "Ralentiza y aplica escarcha.";
                case TipoTorre.Canon: return "Proyectil explosivo con daño en área.";
                case TipoTorre.Laser: return "Rayo continuo: el daño crece mientras enfoca al mismo objetivo.";
                case TipoTorre.Tesla: return "Arcos eléctricos que rebotan en cadena (BFS) entre enemigos cercanos.";
                default: return "Nodo MST: amplía el radio de enlace; +25% alcance y +15% crítico a su red. Revela furtivos.";
            }
        }

        public static int CostoBase(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Basica: return 50;
                case TipoTorre.Hielo: return 75;
                case TipoTorre.Canon: return 90;
                case TipoTorre.Laser: return 120;
                case TipoTorre.Tesla: return 110;
                default: return 85;
            }
        }

        public static Color ColorTipo(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Basica: return new Color(0.35f, 0.55f, 0.8f);
                case TipoTorre.Hielo: return new Color(0.3f, 0.85f, 1f);
                case TipoTorre.Canon: return new Color(0.9f, 0.52f, 0.15f);
                case TipoTorre.Laser: return new Color(0.88f, 0.28f, 1f);
                case TipoTorre.Tesla: return new Color(0.55f, 0.6f, 1f);
                default: return new Color(0.3f, 0.95f, 0.6f);
            }
        }

        public static float AlcanceBase(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Basica: return 3.8f;
                case TipoTorre.Hielo: return 3.4f;
                case TipoTorre.Canon: return 4.2f;
                case TipoTorre.Laser: return 4.8f;
                case TipoTorre.Tesla: return 3.6f;
                default: return 4.5f;
            }
        }

        public static TipoDanio DanioDe(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Hielo: return TipoDanio.Hielo;
                case TipoTorre.Canon: return TipoDanio.Explosivo;
                case TipoTorre.Laser: return TipoDanio.Energia;
                case TipoTorre.Tesla: return TipoDanio.Electrico;
                default: return TipoDanio.Cinetico;
            }
        }

        public static string NombrePrioridad(PrioridadObjetivo p)
        {
            switch (p)
            {
                case PrioridadObjetivo.Primero: return "Primero";
                case PrioridadObjetivo.Ultimo: return "Último";
                case PrioridadObjetivo.MasFuerte: return "Más Fuerte";
                case PrioridadObjetivo.MasDebil: return "Más Débil";
                default: return "Más Cercano";
            }
        }

        #endregion

        #region Ciclo de Vida

        private void Awake()
        {
            idTorre = ++contadorIds;
            ConstruirVisualesTorre();
            AplicarParametrosSegunTipo();
        }

        private void OnEnable()
        {
            if (!Activas.Contains(this)) Activas.Add(this);
        }

        private void OnDisable()
        {
            Activas.Remove(this);
        }

        public void ConfigurarTipo(TipoTorre nuevoTipo)
        {
            tipo = nuevoTipo;
            nivel = 1;
            AplicarParametrosSegunTipo();
            ActualizarColoresVisuales();
        }

        private void AplicarParametrosSegunTipo()
        {
            alcance = AlcanceBase(tipo);
            costoConstruccion = CostoBase(tipo);
            switch (tipo)
            {
                case TipoTorre.Basica: cadenciaDisparoBase = 0.8f; danioBase = 22f; break;
                case TipoTorre.Hielo: cadenciaDisparoBase = 1.1f; danioBase = 16f; break;
                case TipoTorre.Canon: cadenciaDisparoBase = 1.6f; danioBase = 50f; break;
                case TipoTorre.Laser: cadenciaDisparoBase = 0.1f; danioBase = 38f; break; // daño por segundo
                case TipoTorre.Tesla: cadenciaDisparoBase = 1.3f; danioBase = 30f; break;
                case TipoTorre.Radar: cadenciaDisparoBase = 0f; danioBase = 0f; break;
            }
        }

        #endregion

        #region Estadísticas Efectivas (DAG + Clase + MST + Nivel)

        private float MultNivelDanio => nivel == 1 ? 1f : (nivel == 2 ? 1.5f : 2.2f);
        private float MultNivelAlcance => 1f + 0.1f * (nivel - 1);
        private float MultNivelCadencia => 1f - 0.12f * (nivel - 1);

        private static bool Hab(string id) => GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG(id);
        private static float Clase(ClaseComandante c) => GameManager.Instance != null ? GameManager.Instance.FactorClase(c) : 0f;
        private bool SobrecargaAplica => GameManager.Instance != null && GameManager.Instance.SobrecargaActiva && gradoMST > 0;

        public float AlcanceEfectivo
        {
            get
            {
                float a = alcance * MultNivelAlcance;
                if (enRedConRadar && !EsRadar) a *= 1.25f;
                if (tipo == TipoTorre.Hielo && Hab("HAB_VENTISCA")) a *= 1.2f;
                return a;
            }
        }

        public float DanioEfectivo
        {
            get
            {
                float d = danioBase * MultNivelDanio;
                if (Hab("HAB_CALIBRE_PESADO")) d *= 1.3f;
                if (tipo == TipoTorre.Laser && Hab("HAB_TORRE_LASER")) d *= 1.3f;
                d *= 1f + bonoDanioRed;
                d *= 1f + 0.15f * Clase(ClaseComandante.Artillero);
                if (tipo == TipoTorre.Hielo) d *= 1f + 0.25f * Clase(ClaseComandante.Criomante);
                if (SobrecargaAplica) d *= 1.5f;
                return d;
            }
        }

        public float CadenciaEfectiva
        {
            get
            {
                float c = cadenciaDisparoBase * MultNivelCadencia;
                if (Hab("HAB_BALAS_RAPIDAS")) c *= 0.75f;
                c /= 1f + bonoCadenciaRed;
                if (SobrecargaAplica) c *= 0.5f;
                return Mathf.Max(c, 0.12f);
            }
        }

        public float ProbabilidadCritico => (Hab("HAB_DISPARO_CRITICO") ? 0.25f : 0f) + (enRedConRadar ? 0.15f : 0f);

        public int SaltosTesla => 4 + (nivel - 1) + (Hab("HAB_ARCO_VOLTAICO") ? 2 : 0);

        public int CostoMejora
        {
            get
            {
                if (nivel >= NIVEL_MAXIMO) return 0;
                float c = CostoBase(tipo) * (0.6f + 0.3f * (nivel - 1));
                if (Hab("HAB_INGENIERIA")) c *= 0.8f;
                return Mathf.RoundToInt(c);
            }
        }

        public int ValorVenta => Mathf.RoundToInt(inversionTotal * (Hab("HAB_RECICLAJE") ? 0.85f : 0.70f));

        /// <summary>Sube la torre un nivel (el pago lo gestiona GameManager).</summary>
        public bool SubirNivel(int costoPagado)
        {
            if (nivel >= NIVEL_MAXIMO) return false;
            nivel++;
            inversionTotal += costoPagado;
            ActualizarColoresVisuales();
            if (EfectosVisualesManager.Instance != null)
            {
                EfectosVisualesManager.Instance.OndaExpansiva(transform.position, 1.1f, Paleta.Dorado, 0.4f);
                EfectosVisualesManager.Instance.TextoFlotante(transform.position + Vector3.up * 0.5f, $"NIVEL {nivel}", Paleta.Dorado, 1.2f);
            }
            return true;
        }

        public void ReiniciarSinergiaRed()
        {
            gradoMST = 0;
            bonoDanioRed = 0f;
            bonoCadenciaRed = 0f;
            enRedConRadar = false;
            componenteMST = -1;
        }

        public void RefrescarIndicadores()
        {
            if (srBrilloRed != null)
            {
                srBrilloRed.enabled = gradoMST > 0;
                srBrilloRed.color = enRedConRadar ? new Color(0.3f, 1f, 0.6f, 0.35f) : new Color(0.25f, 0.9f, 1f, 0.3f);
            }
        }

        public string ResumenEstadisticas()
        {
            if (EsRadar)
            {
                return $"Radio de enlace MST: <b>{RedTorresMST.RadioEnlace(true):0.0}</b>\n" +
                       $"Alcance de detección: <b>{AlcanceEfectivo:0.0}</b>\n" +
                       "Aura: +25% alcance y +15% crítico a su red";
            }
            string dps = tipo == TipoTorre.Laser ? $"{DanioEfectivo:0}/s (hasta ×{MaxRampaLaser:0.#})" : $"{DanioEfectivo:0}";
            string cad = tipo == TipoTorre.Laser ? "continuo" : $"{CadenciaEfectiva:0.00}s";
            return $"Daño: <b>{dps}</b>  ({DanioDe(tipo)})\n" +
                   $"Cadencia: <b>{cad}</b>   Alcance: <b>{AlcanceEfectivo:0.0}</b>\n" +
                   $"Crítico: <b>{ProbabilidadCritico * 100f:0}%</b>" + (tipo == TipoTorre.Tesla ? $"   Rebotes: <b>{SaltosTesla}</b>" : "");
        }

        private float MaxRampaLaser => Hab("HAB_FOCO_LASER") ? 3f : 2f;

        #endregion

        #region Combate

        private void Update()
        {
            float dt = Time.deltaTime;
            if (versionVista != Vista25D.Version) AplicarModoVista();
            AnimarVisuales(dt);
            ActualizarTinteHackeo();

            if (soloVisual)
            {
                ActualizarVisualRemota();
                return;
            }

            if (EstaHackeada)
            {
                objetivoActual = null;
                if (hazLaser != null) hazLaser.enabled = false;
                return;
            }

            if (EsRadar)
            {
                ActualizarRadar(dt);
                return;
            }

            temporizadorBusqueda -= dt;
            if (!ObjetivoValido(objetivoActual) || (temporizadorBusqueda <= 0f && tipo != TipoTorre.Laser))
            {
                Enemigo previo = objetivoActual;
                objetivoActual = BuscarObjetivo();
                temporizadorBusqueda = 0.2f;
                if (objetivoActual != previo) tiempoEnfoqueLaser = 0f;
            }

            if (objetivoActual == null)
            {
                if (hazLaser != null) hazLaser.enabled = false;
                tiempoEnfoqueLaser = 0f;
                temporizadorDisparo = Mathf.Max(0f, temporizadorDisparo - dt);
                return;
            }

            ApuntarHaciaObjetivo();

            if (tipo == TipoTorre.Laser)
            {
                DispararLaserContinuo(dt);
                return;
            }

            temporizadorDisparo -= dt;
            if (temporizadorDisparo <= 0f)
            {
                if (tipo == TipoTorre.Tesla) DispararTesla();
                else DispararProyectil();
                temporizadorDisparo = CadenciaEfectiva;
            }
        }

        private bool ObjetivoValido(Enemigo e)
        {
            if (e == null || e.EstaMuerto) return false;
            return PuedeApuntar(e, Vector2.Distance(transform.position, e.transform.position));
        }

        /// <summary>Los furtivos solo son visibles a corta distancia, salvo que la torre esté en una red con Radar.</summary>
        private bool PuedeApuntar(Enemigo e, float distancia)
        {
            float a = AlcanceEfectivo;
            if (e.EsFurtivo && !enRedConRadar) a *= 0.55f;
            return distancia <= a;
        }

        private Enemigo BuscarObjetivo()
        {
            Enemigo mejor = null;
            float mejorValor = float.MaxValue;
            List<Enemigo> lista = Enemigo.Activos;

            for (int i = 0; i < lista.Count; i++)
            {
                Enemigo e = lista[i];
                if (e == null || e.EstaMuerto) continue;
                float dist = Vector2.Distance(transform.position, e.transform.position);
                if (!PuedeApuntar(e, dist)) continue;

                // Se minimiza 'valor': cada prioridad define su propia métrica
                float valor;
                switch (prioridad)
                {
                    case PrioridadObjetivo.Primero: valor = e.DistanciaRestante; break;
                    case PrioridadObjetivo.Ultimo: valor = -e.DistanciaRestante; break;
                    case PrioridadObjetivo.MasFuerte: valor = -(e.vidaActual + e.escudoActual); break;
                    case PrioridadObjetivo.MasDebil: valor = e.vidaActual + e.escudoActual; break;
                    default: valor = dist; break;
                }

                if (valor < mejorValor)
                {
                    mejorValor = valor;
                    mejor = e;
                }
            }
            return mejor;
        }

        private void ApuntarHaciaObjetivo()
        {
            if (canonRotatorio == null || objetivoActual == null) return;
            Vector2 dir = (objetivoActual.transform.position - transform.position).normalized;
            float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            canonRotatorio.rotation = Quaternion.Euler(0, 0, angulo);
            if (arma3D != null) arma3D.rotation = Quaternion.Euler(0, 0, angulo) * Recursos25D.RotModelo;
        }

        private Vector3 PuntaCanon
        {
            get
            {
                if (Vista25D.Activa && arma3D != null) return arma3D.TransformPoint(punta3D);
                return canonRotatorio != null ? canonRotatorio.position + canonRotatorio.up * 0.5f : transform.position;
            }
        }

        private float CalcularDanio(out bool critico)
        {
            float d = DanioEfectivo;
            critico = Random.value < ProbabilidadCritico;
            if (critico) d *= 2f;
            return d;
        }

        private void DispararProyectil()
        {
            float danio = CalcularDanio(out bool critico);
            bool esHielo = tipo == TipoTorre.Hielo;
            bool esSplash = tipo == TipoTorre.Canon;

            GameObject projObj = new GameObject("ProyectilTorre");
            projObj.transform.position = PuntaCanon;
            Proyectil p = projObj.AddComponent<Proyectil>();
            p.tipoDanio = DanioDe(tipo);

            // Ralentización: completa en torres de hielo, ligera en el resto con Munición Criogénica (DAG)
            float crio = Clase(ClaseComandante.Criomante);
            bool ralentiza = esHielo || Hab("HAB_TORRE_HIELO");
            p.porcentajeRalentizacion = esHielo ? 0.45f + 0.15f * crio + (Hab("HAB_TORRE_HIELO") ? 0.1f : 0f) : 0.2f;
            p.duracionRalentizacion = esHielo ? (Hab("HAB_VENTISCA") ? 4f : 2.5f) + crio : 1.5f;

            if (esSplash)
            {
                p.radioSplash = (Hab("HAB_CANON_AREA") ? 2.4f : 1.6f) * (1f + 0.2f * Clase(ClaseComandante.Artillero)) * (1f + 0.08f * (nivel - 1));
            }

            bool congela = esHielo && Hab("HAB_CERO_ABSOLUTO") && Random.value < 0.2f;
            p.Configurar(objetivoActual, danio, critico, ralentiza, esSplash, congela);
            p.colorEstela = ColorTipo(tipo);
            if (ServidorPartida.Instancia != null)
                ServidorPartida.Instancia.RegistrarDisparo(this, objetivoActual, critico, ralentiza, esSplash, congela);

            retroceso = esSplash ? 0.16f : 0.09f;
            if (EfectosVisualesManager.Instance != null)
            {
                EfectosVisualesManager.Instance.Destello(PuntaCanon, ColorTipo(tipo), esSplash ? 0.55f : 0.35f);
                EfectosVisualesManager.Instance.Sonido(esSplash ? TipoSonido.Canon : TipoSonido.Disparo);
            }
        }

        /// <summary>
        /// Rayo continuo: aplica daño por tics y aumenta cuanto más tiempo enfoca al mismo objetivo.
        /// </summary>
        private void DispararLaserContinuo(float dt)
        {
            tiempoEnfoqueLaser += dt;
            float rampa = Mathf.Lerp(1f, MaxRampaLaser, Mathf.Clamp01(tiempoEnfoqueLaser / 2.5f));
            acumuladorLaser += dt;

            if (hazLaser != null)
            {
                hazLaser.enabled = true;
                hazLaser.SetPosition(0, PuntaCanon);
                hazLaser.SetPosition(1, objetivoActual.PuntoImpacto);
                float ancho = 0.05f + 0.05f * (rampa - 1f) + 0.015f * Mathf.Sin(Time.time * 40f);
                hazLaser.startWidth = ancho;
                hazLaser.endWidth = ancho * 0.7f;
                Color c = Color.Lerp(ColorTipo(TipoTorre.Laser), Color.white, (rampa - 1f) / 2.5f);
                hazLaser.startColor = c;
                hazLaser.endColor = new Color(1f, 1f, 1f, 0.9f);
            }

            const float tic = 0.15f;
            while (acumuladorLaser >= tic && objetivoActual != null && !objetivoActual.EstaMuerto)
            {
                acumuladorLaser -= tic;
                if (ServidorPartida.Instancia != null)
                    ServidorPartida.Instancia.RegistrarDisparo(this, objetivoActual, false, false, false, false);
                float d = DanioEfectivo * rampa * tic;
                bool critico = Random.value < ProbabilidadCritico * 0.5f;
                if (critico) d *= 2f;
                objetivoActual.RecibirDanio(d, critico, TipoDanio.Energia, critico || Random.value < 0.35f);
            }
        }

        /// <summary>
        /// Arco en cadena: recorrido BFS local sobre el "grafo de proximidad" de enemigos.
        /// Desde el objetivo inicial la descarga salta a vecinos no visitados dentro del radio de salto.
        /// </summary>
        private void DispararTesla()
        {
            float danio = CalcularDanio(out bool critico);
            int maxObjetivos = SaltosTesla;
            const float radioSalto = 2.3f;

            Queue<Enemigo> cola = new Queue<Enemigo>();
            Dictionary<Enemigo, Vector3> origen = new Dictionary<Enemigo, Vector3>();
            Dictionary<Enemigo, int> profundidad = new Dictionary<Enemigo, int>();
            HashSet<Enemigo> visitados = new HashSet<Enemigo>();

            if (ServidorPartida.Instancia != null)
                ServidorPartida.Instancia.RegistrarDisparo(this, objetivoActual, critico, false, false, false);

            cola.Enqueue(objetivoActual);
            visitados.Add(objetivoActual);
            origen[objetivoActual] = PuntaCanon;
            profundidad[objetivoActual] = 0;

            int golpeados = 0;
            EfectosVisualesManager fx = EfectosVisualesManager.Instance;

            while (cola.Count > 0 && golpeados < maxObjetivos)
            {
                Enemigo actual = cola.Dequeue();
                if (actual == null || actual.EstaMuerto) continue;

                Vector3 posActual = actual.PuntoImpacto;
                int prof = profundidad[actual];
                if (fx != null) fx.Rayo(origen[actual], posActual, ColorTipo(TipoTorre.Tesla), 0.16f, 0.07f);

                // Encolar vecinos antes de aplicar daño (el enemigo puede morir y destruirse)
                List<Enemigo> lista = Enemigo.Activos;
                for (int i = 0; i < lista.Count; i++)
                {
                    Enemigo v = lista[i];
                    if (v == null || v.EstaMuerto || visitados.Contains(v)) continue;
                    if (Vector2.Distance(posActual, v.transform.position) > radioSalto) continue;
                    visitados.Add(v);
                    origen[v] = posActual;
                    profundidad[v] = prof + 1;
                    cola.Enqueue(v);
                }

                actual.RecibirDanio(danio * Mathf.Pow(0.8f, prof), critico && prof == 0, TipoDanio.Electrico);
                golpeados++;
            }

            retroceso = 0.05f;
            if (fx != null)
            {
                fx.Destello(PuntaCanon, ColorTipo(TipoTorre.Tesla), 0.6f);
                fx.Sonido(TipoSonido.Electrico);
            }
        }

        private void ActualizarRadar(float dt)
        {
            temporizadorRadar -= dt;
            if (temporizadorRadar <= 0f)
            {
                temporizadorRadar = 2.2f;
                if (EfectosVisualesManager.Instance != null)
                    EfectosVisualesManager.Instance.OndaExpansiva(transform.position, 1.4f + 0.2f * nivel, new Color(0.3f, 1f, 0.6f, 0.55f), 0.9f);
            }
        }

        #endregion

        #region Multijugador LAN (hackeo y torres visuales del cliente)

        public void Hackear(float duracion)
        {
            hackeadaHasta = Mathf.Max(hackeadaHasta, Time.time + duracion);
            objetivoActual = null;
        }

        /// <summary>Cliente LAN: refleja el estado de hackeo recibido del host.</summary>
        public void EstablecerHackeadaVisual(bool hackeada)
        {
            hackeadaVisual = hackeada;
        }

        /// <summary>Cliente LAN: iguala el nivel de la torre al del host (con su efecto de mejora).</summary>
        public void EstablecerNivelVisual(int nuevoNivel)
        {
            nuevoNivel = Mathf.Clamp(nuevoNivel, 1, NIVEL_MAXIMO);
            while (nivel < nuevoNivel) SubirNivel(0);
            if (nivel > nuevoNivel)
            {
                nivel = nuevoNivel;
                ActualizarColoresVisuales();
            }
        }

        /// <summary>Cliente LAN: dibuja el disparo que hizo esta torre en el host.</summary>
        public void DispararVisual(Enemigo replica, bool critico, bool hielo, bool splash, bool congela)
        {
            if (replica == null) return;
            ApuntarHacia(replica.transform.position);
            EfectosVisualesManager fx = EfectosVisualesManager.Instance;

            switch (tipo)
            {
                case TipoTorre.Laser:
                    laserVisualObjetivo = replica;
                    laserVisualHasta = Time.time + 0.25f;
                    break;
                case TipoTorre.Tesla:
                    if (fx != null)
                    {
                        fx.Rayo(PuntaCanon, replica.PuntoImpacto, ColorTipo(TipoTorre.Tesla), 0.16f, 0.07f);
                        fx.Destello(PuntaCanon, ColorTipo(TipoTorre.Tesla), 0.6f);
                    }
                    retroceso = 0.05f;
                    break;
                case TipoTorre.Radar:
                    break;
                default:
                    GameObject projObj = new GameObject("ProyectilVisual");
                    projObj.transform.position = PuntaCanon;
                    Proyectil p = projObj.AddComponent<Proyectil>();
                    if (splash) p.radioSplash = Hab("HAB_CANON_AREA") ? 2.4f : 1.6f;
                    p.ConfigurarVisual(replica, DanioDe(tipo), critico, hielo, splash, congela);
                    p.colorEstela = ColorTipo(tipo);
                    retroceso = splash ? 0.16f : 0.09f;
                    if (fx != null) fx.Destello(PuntaCanon, ColorTipo(tipo), splash ? 0.55f : 0.35f);
                    break;
            }
        }

        private void ApuntarHacia(Vector3 destino)
        {
            Vector2 dir = (destino - transform.position).normalized;
            if (dir.sqrMagnitude < 0.0001f) return;
            float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            if (canonRotatorio != null && !EsRadar) canonRotatorio.rotation = Quaternion.Euler(0, 0, angulo);
            if (arma3D != null && !EsRadar) arma3D.rotation = Quaternion.Euler(0, 0, angulo) * Recursos25D.RotModelo;
        }

        /// <summary>Cliente LAN: apunta a la réplica más cercana y mantiene el haz láser recibido.</summary>
        private void ActualizarVisualRemota()
        {
            if (EsRadar) return;

            bool laser = tipo == TipoTorre.Laser && laserVisualObjetivo != null && Time.time < laserVisualHasta && !EstaHackeada;
            if (hazLaser != null)
            {
                hazLaser.enabled = laser;
                if (laser)
                {
                    hazLaser.SetPosition(0, PuntaCanon);
                    hazLaser.SetPosition(1, laserVisualObjetivo.PuntoImpacto);
                    float ancho = 0.07f + 0.015f * Mathf.Sin(Time.time * 40f);
                    hazLaser.startWidth = ancho;
                    hazLaser.endWidth = ancho * 0.7f;
                }
            }
            if (EstaHackeada) return;
            if (laser)
            {
                ApuntarHacia(laserVisualObjetivo.transform.position);
                return;
            }

            Enemigo cercano = null;
            float menor = AlcanceEfectivo;
            List<Enemigo> replicas = Enemigo.Replicas;
            for (int i = 0; i < replicas.Count; i++)
            {
                if (replicas[i] == null) continue;
                float d = Vector2.Distance(transform.position, replicas[i].transform.position);
                if (d <= menor)
                {
                    menor = d;
                    cercano = replicas[i];
                }
            }
            if (cercano != null) ApuntarHacia(cercano.transform.position);
        }

        /// <summary>Tinte verde apagado mientras la torre está hackeada (2D y 2.5D).</summary>
        private void ActualizarTinteHackeo()
        {
            bool hackeada = EstaHackeada;
            if (hackeada == tinteHackeoAplicado || !visualesListos) return;
            tinteHackeoAplicado = hackeada;

            if (hackeada)
            {
                colorBaseGuardado = srBase.color;
                colorAnilloGuardado = srAnillo.color;
                colorCanonGuardado = srCanon.color;
                srBase.color = new Color(0.25f, 0.25f, 0.25f);
                srAnillo.color = new Color(0.55f, 1f, 0.35f);
                srCanon.color = new Color(0.15f, 0.15f, 0.15f);
            }
            else
            {
                srBase.color = colorBaseGuardado;
                srAnillo.color = colorAnilloGuardado;
                srCanon.color = colorCanonGuardado;
            }

            Color tinte = hackeada ? new Color(0.35f, 1f, 0.3f, 0.55f) : new Color(0f, 0f, 0f, 0f);
            if (raiz3D != null) Recursos25D.Tenir(raiz3D.GetComponentsInChildren<Renderer>(true), tinte);
            if (arma3D != null) Recursos25D.Tenir(arma3D.GetComponentsInChildren<Renderer>(true), tinte);
            if (hackeada && EfectosVisualesManager.Instance != null)
                EfectosVisualesManager.Instance.TextoFlotante(transform.position + Vector3.up * 0.5f, "HACKEADA", new Color(0.55f, 1f, 0.35f), 1.1f);
        }

        #endregion

        #region Vista 2.5D (modelos Kenney + cabezal por tipo)

        /// <summary>Muestra los sprites 2D o el modelo 3D según la vista activa.</summary>
        private void AplicarModoVista()
        {
            versionVista = Vista25D.Version;
            bool v3 = Vista25D.Activa;
            if (!visualesListos) return;

            srBase.enabled = !v3;
            srAnillo.enabled = !v3;
            srCanon.enabled = !v3;
            for (int i = 0; i < pips.Count; i++) pips[i].enabled = !v3 && i < nivel;
            srAnilloElite.enabled = !v3 && nivel >= NIVEL_MAXIMO;

            // El brillo de red y el anillo de propietario quedan sobre la plataforma en 2.5D
            float zSuelo = v3 ? -(Vista25D.ALTURA_PAD + 0.005f) / Mathf.Max(0.01f, transform.lossyScale.z) : 0f;
            srBrilloRed.transform.localPosition = new Vector3(0f, 0f, zSuelo);
            srPropietario.transform.localPosition = new Vector3(0f, 0f, zSuelo);

            if (raiz3D != null) raiz3D.gameObject.SetActive(v3);
            if (arma3D != null) arma3D.gameObject.SetActive(v3);
            if (hazLaser != null) hazLaser.enabled = false;
        }

        private Material Mat(Color c, Color emision = default) => Recursos25D.MaterialColor(c, emision);

        // Pieza y variante de color del kit Kenney Tower Defense para cada tipo de torre
        private static string FormaTorre(TipoTorre t) => t == TipoTorre.Laser || t == TipoTorre.Tesla ? "square" : "round";
        private static string VarianteTorre(TipoTorre t)
        {
            switch (t)
            {
                case TipoTorre.Canon: return "b";
                case TipoTorre.Hielo: return "c";
                case TipoTorre.Tesla: return "c";
                default: return "a";
            }
        }

        private const float ESCALA_TORRE = 0.55f;

        /// <summary>
        /// Torre 3D armada con piezas del kit Kenney Tower Defense: crece con el nivel
        /// (nivel 1 = base + almenas, nivel 2 = cuerpo + almenas, nivel 3 = cuerpo + piso + almenas
        /// con anillo dorado) y lleva encima el arma de su tipo, que gira hacia el objetivo.
        /// </summary>
        private void ConstruirVisuales3D()
        {
            if (raiz3D != null) Destroy(raiz3D.gameObject);
            if (arma3D != null) Destroy(arma3D.gameObject);
            canon3D = null;
            giro3D = null;

            Color acento = ColorTipo(tipo);
            float k = ESCALA_TORRE;
            string TD(string n) => Recursos25D.TD + n;

            // ── Cuerpo ──
            raiz3D = Recursos25D.CrearRaizModelo(transform, "Torre3D");
            raiz3D.position = new Vector3(transform.position.x, transform.position.y, -Vista25D.ALTURA_PAD);

            float alto;
            float radio = 0.5f * k;
            if (tipo == TipoTorre.Radar)
            {
                // Atalaya de madera
                string pieza = nivel == 1 ? "wood-structure" : "wood-structure-high";
                float e = nivel == 3 ? k * 1.05f : k;
                alto = Escenario25D.Apilar(raiz3D, Vector3.zero, e, 0f, Recursos25D.Nieve(pieza));
                if (alto < 0.05f)
                {
                    alto = nivel == 1 ? 0.3f : 0.55f;
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, raiz3D, new Vector3(0f, alto * 0.5f, 0f), new Vector3(0.45f, alto * 0.5f, 0.45f), Mat(new Color(0.5f, 0.35f, 0.2f)));
                }
                Recursos25D.Primitiva(PrimitiveType.Cube, raiz3D, new Vector3(0f, alto + 0.02f, 0f), new Vector3(0.5f, 0.04f, 0.5f), Mat(new Color(0.55f, 0.38f, 0.25f)));
                alto += 0.04f;
                alturaCuerpo3D = alto;
            }
            else
            {
                string forma = FormaTorre(tipo), v = VarianteTorre(tipo);
                string pie = forma == "round" ? TD("tower-round-base") : TD("tower-square-bottom-" + v);
                List<string> piezas = new List<string>();
                if (nivel == 1) piezas.Add(pie);
                else piezas.Add(TD($"tower-{forma}-bottom-{v}"));
                if (nivel >= 3) piezas.Add(TD($"tower-{forma}-middle-{v}"));
                float yTop;

                // El pie cuadrado se aplana en nivel 1 para que la torre crezca de forma visible
                if (nivel == 1 && forma == "square")
                {
                    Transform t = Recursos25D.CrearModelo(pie, raiz3D, Vector3.zero, new Vector3(k, k * 0.45f, k));
                    yTop = t != null && Recursos25D.Limites(t, raiz3D, out Bounds bp) ? bp.max.y : 0f;
                }
                else yTop = Escenario25D.Apilar(raiz3D, Vector3.zero, k, 0f, piezas.ToArray());

                alto = Escenario25D.Apilar(raiz3D, Vector3.zero, k, yTop, TD($"tower-{forma}-top-{v}"));
                if (alto < 0.05f)
                {
                    alto = nivel == 1 ? 0.4f : (nivel == 2 ? 0.6f : 0.9f);
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, raiz3D, new Vector3(0f, alto * 0.5f, 0f), new Vector3(0.55f, alto * 0.5f, 0.55f), Mat(new Color(0.55f, 0.55f, 0.58f)));
                    yTop = alto;
                }

                // Franja del color del tipo (identificación rápida, igual que en 2D)
                float yFranja = Mathf.Max(0.04f, yTop * 0.55f);
                if (forma == "round")
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, raiz3D, new Vector3(0f, yFranja, 0f), new Vector3(radio * 2.08f, 0.025f, radio * 2.08f), Mat(acento, acento * 0.35f));
                else
                    Recursos25D.Primitiva(PrimitiveType.Cube, raiz3D, new Vector3(0f, yFranja, 0f), new Vector3(radio * 2.06f, 0.05f, radio * 2.06f), Mat(acento, acento * 0.35f));

                // Nivel máximo: franja dorada justo debajo de las almenas
                if (nivel >= NIVEL_MAXIMO)
                {
                    Material oro = Mat(Paleta.Dorado, Paleta.Dorado * 0.4f);
                    Vector3 pos = new Vector3(0f, yTop - 0.03f, 0f);
                    if (forma == "round") Recursos25D.Primitiva(PrimitiveType.Cylinder, raiz3D, pos, new Vector3(radio * 2.16f, 0.022f, radio * 2.16f), oro);
                    else Recursos25D.Primitiva(PrimitiveType.Cube, raiz3D, pos, new Vector3(radio * 2.12f, 0.045f, radio * 2.12f), oro);
                }

                // El arma se apoya dentro de las almenas, no sobre el borde
                alturaCuerpo3D = Mathf.Lerp(yTop, alto, 0.45f);
            }

            // ── Cabezal (gira hacia el objetivo) ──
            GameObject goArma = new GameObject("Cabezal3D");
            goArma.transform.SetParent(transform, false);
            arma3D = goArma.transform;
            float s = transform.lossyScale.x;
            arma3D.localScale = Mathf.Abs(s) > 0.0001f ? Vector3.one / s : Vector3.one;
            arma3D.position = new Vector3(transform.position.x, transform.position.y, -(Vista25D.ALTURA_PAD + alturaCuerpo3D));
            arma3D.rotation = canonRotatorio != null ? canonRotatorio.rotation * Recursos25D.RotModelo : Recursos25D.RotModelo;

            float escalaArma = 1f + 0.1f * (nivel - 1);
            float kArma = k * 1.05f * escalaArma;
            Material mMetal = Mat(new Color(0.22f, 0.24f, 0.29f));
            Material mAcento = Mat(acento, acento * 0.45f);
            Material mBrillo = Mat(Color.Lerp(acento, Color.white, 0.35f), acento * 0.9f);

            string modeloArma = null;
            switch (tipo)
            {
                case TipoTorre.Basica: modeloArma = "weapon-ballista"; break;
                case TipoTorre.Canon: modeloArma = nivel >= 3 ? "weapon-catapult" : "weapon-cannon"; break;
                case TipoTorre.Laser: modeloArma = "weapon-turret"; break;
            }

            if (modeloArma != null)
            {
                Transform arma = Recursos25D.CrearModelo(TD(modeloArma), arma3D, Vector3.zero, Vector3.one * kArma);
                if (arma != null)
                {
                    canon3D = arma; // todo el arma retrocede al disparar
                    Recursos25D.Limites(arma, arma3D, out Bounds ba);
                    punta3D = new Vector3(0f, ba.center.y + ba.extents.y * 0.35f, ba.max.z);
                    if (tipo == TipoTorre.Laser)
                    {
                        // Lente brillante en la boca de la torreta
                        Recursos25D.Primitiva(PrimitiveType.Sphere, arma, new Vector3(0f, (punta3D.y) / kArma, ba.max.z / kArma), Vector3.one * 0.22f, mBrillo);
                    }
                }
            }

            if (canon3D == null)
            {
                switch (tipo)
                {
                    case TipoTorre.Hielo:
                    {
                        Transform cristal = Recursos25D.CrearModelo(TD("tower-round-crystals"), arma3D, Vector3.zero, Vector3.one * k * 0.75f * escalaArma);
                        if (cristal != null)
                        {
                            giro3D = cristal;
                            Recursos25D.Tenir(cristal.GetComponentsInChildren<Renderer>(true), new Color(0.45f, 0.92f, 1f, 0.6f));
                            Recursos25D.Limites(cristal, arma3D, out Bounds bc);
                            punta3D = new Vector3(0f, bc.max.y * 0.8f, 0f);
                        }
                        else
                        {
                            giro3D = new GameObject("Cristal").transform;
                            giro3D.SetParent(arma3D, false);
                            Recursos25D.Primitiva(PrimitiveType.Cube, giro3D, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.2f * escalaArma,
                                Mat(new Color(0.55f, 0.95f, 1f), new Color(0.15f, 0.5f, 0.6f)), new Vector3(45f, 0f, 45f));
                            punta3D = new Vector3(0f, 0.3f, 0f) * escalaArma;
                        }
                        break;
                    }

                    case TipoTorre.Tesla:
                        Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.15f, 0f), new Vector3(0.06f, 0.15f, 0.06f) * escalaArma, mMetal);
                        Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.15f, 0f), new Vector3(0.28f, 0.008f, 0.28f) * escalaArma, mAcento);
                        Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.22f, 0f), new Vector3(0.22f, 0.008f, 0.22f) * escalaArma, mAcento);
                        giro3D = Recursos25D.Primitiva(PrimitiveType.Sphere, arma3D, new Vector3(0f, 0.36f, 0f) * escalaArma, Vector3.one * 0.22f * escalaArma, mBrillo);
                        punta3D = new Vector3(0f, 0.36f, 0f) * escalaArma;
                        break;

                    case TipoTorre.Radar:
                        Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.1f, 0f), new Vector3(0.05f, 0.1f, 0.05f) * escalaArma, mMetal);
                        Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.26f, 0.03f) * escalaArma,
                            new Vector3(0.42f, 0.015f, 0.42f) * escalaArma, Mat(new Color(0.85f, 0.88f, 0.92f)), new Vector3(60f, 0f, 0f));
                        Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.26f, 0.03f) * escalaArma,
                            new Vector3(0.2f, 0.018f, 0.2f) * escalaArma, mAcento, new Vector3(60f, 0f, 0f));
                        giro3D = Recursos25D.Primitiva(PrimitiveType.Sphere, arma3D, new Vector3(0f, 0.32f, 0.16f) * escalaArma, Vector3.one * 0.07f * escalaArma, mBrillo);
                        punta3D = new Vector3(0f, 0.26f, 0f) * escalaArma;
                        break;

                    default: // Respaldo si falta el modelo del arma
                        Recursos25D.Primitiva(PrimitiveType.Cube, arma3D, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.15f, 0.24f) * escalaArma, mMetal);
                        canon3D = Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.13f, 0.2f) * escalaArma,
                            new Vector3(0.08f, 0.17f, 0.08f) * escalaArma, mAcento, new Vector3(90f, 0f, 0f));
                        punta3D = new Vector3(0f, 0.13f, 0.38f) * escalaArma;
                        break;
                }
            }

            if (canon3D != null) posCanon3D = canon3D.localPosition;
        }

        private void AnimarVisuales3D(float dt)
        {
            float t = Time.time + idTorre * 0.7f;
            float e = 1f + 0.1f * (nivel - 1);
            if (tipo == TipoTorre.Radar && arma3D != null)
            {
                anguloRadar -= 120f * dt;
                arma3D.rotation = Quaternion.Euler(0f, 0f, anguloRadar) * Recursos25D.RotModelo;
                if (giro3D != null) giro3D.gameObject.SetActive(Mathf.Repeat(t, 1.2f) < 0.6f);
            }
            else if (tipo == TipoTorre.Hielo && giro3D != null)
            {
                // Los cristales flotan y giran despacio (no se apuntan: el hielo dispara en todas direcciones)
                giro3D.localPosition = new Vector3(0f, 0.03f * Mathf.Sin(t * 2.4f) * e, 0f);
                giro3D.localRotation = Quaternion.Euler(0f, t * 50f, 0f);
            }
            else if (tipo == TipoTorre.Tesla && giro3D != null)
            {
                float esc = (0.22f + 0.03f * Mathf.Sin(t * 9f)) * e;
                giro3D.localScale = Vector3.one * esc;
            }
        }

        #endregion

        #region Construcción Visual Automática

        private void ConstruirVisualesTorre()
        {
            srBrilloRed = GraficosProc.CrearSprite(transform, "BrilloRed", GraficosProc.Brillo, new Color(0.25f, 0.9f, 1f, 0.3f), 3, 2.1f);
            srBrilloRed.enabled = false;

            srPropietario = GraficosProc.CrearSprite(transform, "AnilloPropietario", GraficosProc.Anillo, Color.clear, 4, 1.18f);
            srPropietario.enabled = false;

            srBase = GraficosProc.CrearSprite(transform, "BaseTorre", GraficosProc.Circulo, Color.white, 5, 0.9f);
            srAnilloElite = GraficosProc.CrearSprite(transform, "AnilloElite", GraficosProc.Anillo, Paleta.Dorado, 6, 1.02f);
            srAnilloElite.enabled = false;
            srAnillo = GraficosProc.CrearSprite(transform, "Anillo", GraficosProc.Circulo, Color.white, 6, 0.6f);

            GameObject canonObj = new GameObject("CanonRotatorio");
            canonObj.transform.SetParent(transform, false);
            canonRotatorio = canonObj.transform;

            srCanon = GraficosProc.CrearSprite(canonRotatorio, "Barril", GraficosProc.Cuadrado, Color.white, 7);
            barril = srCanon.transform;
            barril.localPosition = posBarril;

            for (int i = 0; i < NIVEL_MAXIMO; i++)
            {
                SpriteRenderer pip = GraficosProc.CrearSprite(transform, "Pip" + i, GraficosProc.Estrella, Paleta.Dorado, 9, 0.2f);
                pip.transform.localPosition = new Vector3(-0.22f + 0.22f * i, -0.52f, 0);
                pips.Add(pip);
            }

            visualesListos = true;
            ActualizarColoresVisuales();
        }

        public void EstablecerPropietario(int id, string nombre, Color color)
        {
            propietarioId = id;
            propietarioNombre = nombre;
            colorPropietario = color;
            if (srPropietario != null)
            {
                srPropietario.enabled = color.a > 0.01f;
                srPropietario.color = color;
            }
        }

        public void ActualizarColoresVisuales()
        {
            if (!visualesListos) return;

            Color acento = ColorTipo(tipo);
            float brilloNivel = 1f + 0.12f * (nivel - 1);
            srBase.color = Color.Lerp(new Color(0.12f, 0.14f, 0.2f), acento, 0.22f);
            srAnillo.color = acento * brilloNivel;
            srCanon.color = Color.Lerp(new Color(0.08f, 0.09f, 0.13f), acento, 0.25f + 0.15f * (nivel - 1));
            srAnillo.sprite = GraficosProc.Circulo;
            srCanon.sprite = GraficosProc.Cuadrado;
            srCanon.enabled = true;
            posBarril = new Vector3(0, 0.28f, 0);

            switch (tipo)
            {
                case TipoTorre.Basica: barril.localScale = new Vector3(0.2f + 0.03f * nivel, 0.55f, 1f); break;
                case TipoTorre.Hielo:
                    barril.localScale = new Vector3(0.26f, 0.5f, 1f);
                    srAnillo.sprite = GraficosProc.Hexagono;
                    break;
                case TipoTorre.Canon: barril.localScale = new Vector3(0.34f + 0.04f * nivel, 0.62f, 1f); break;
                case TipoTorre.Laser:
                    barril.localScale = new Vector3(0.14f, 0.72f, 1f);
                    srAnillo.sprite = GraficosProc.Diamante;
                    if (hazLaser == null)
                    {
                        hazLaser = GraficosProc.CrearLinea(transform, "HazLaser", acento, Color.white, 0.07f, 14);
                        hazLaser.enabled = false;
                    }
                    break;
                case TipoTorre.Tesla:
                    // Bobina: esfera central en lugar de cañón
                    srCanon.sprite = GraficosProc.Circulo;
                    barril.localScale = new Vector3(0.3f, 0.3f, 1f);
                    posBarril = Vector3.zero;
                    srCanon.color = Color.white;
                    srAnillo.sprite = GraficosProc.Anillo;
                    break;
                case TipoTorre.Radar:
                    // Antena giratoria triangular
                    srCanon.sprite = GraficosProc.Triangulo;
                    barril.localScale = new Vector3(0.42f, 0.5f, 1f);
                    posBarril = new Vector3(0, 0.12f, 0);
                    srAnillo.sprite = GraficosProc.Anillo;
                    break;
            }
            barril.localPosition = posBarril;
            srAnillo.transform.localScale = Vector3.one * (tipo == TipoTorre.Tesla || tipo == TipoTorre.Radar ? 0.78f : 0.6f);

            // Evolución visual por nivel
            transform.localScale = escalaBase * (1f + 0.1f * (nivel - 1));
            srAnilloElite.enabled = nivel >= NIVEL_MAXIMO;
            for (int i = 0; i < pips.Count; i++) pips[i].enabled = i < nivel;

            if (srPropietario != null)
            {
                srPropietario.enabled = colorPropietario.a > 0.01f;
                srPropietario.color = colorPropietario;
            }
            RefrescarIndicadores();
            ConstruirVisuales3D();
            AplicarModoVista();
            tinteHackeoAplicado = false; // los colores se regeneraron: el tinte de hackeo se reaplica si sigue activo
        }

        private void AnimarVisuales(float dt)
        {
            if (retroceso > 0f)
            {
                retroceso = Mathf.Max(0f, retroceso - dt * 0.9f);
                if (barril != null) barril.localPosition = posBarril - Vector3.up * retroceso;
                if (canon3D != null) canon3D.localPosition = posCanon3D - Vector3.forward * retroceso;
            }

            if (Vista25D.Activa) AnimarVisuales3D(dt);

            if (tipo == TipoTorre.Radar && canonRotatorio != null)
            {
                canonRotatorio.Rotate(0, 0, -120f * dt);
            }
            else if (tipo == TipoTorre.Tesla && srCanon != null)
            {
                float pulso = 0.28f + 0.05f * Mathf.Sin(Time.time * 9f + idTorre);
                barril.localScale = new Vector3(pulso, pulso, 1f);
            }

            if (srAnilloElite != null && srAnilloElite.enabled)
            {
                srAnilloElite.transform.Rotate(0, 0, 40f * dt);
            }

            if (srBrilloRed != null && srBrilloRed.enabled)
            {
                Color c = srBrilloRed.color;
                bool sobre = SobrecargaAplica;
                c.a = (sobre ? 0.6f : 0.26f) + 0.1f * Mathf.Sin(Time.unscaledTime * (sobre ? 10f : 3f) + idTorre);
                if (sobre) { c.r = 1f; c.g = 0.8f; c.b = 0.25f; }
                else if (!enRedConRadar) { c.r = 0.25f; c.g = 0.9f; c.b = 1f; }
                else { c.r = 0.3f; c.g = 1f; c.b = 0.6f; }
                srBrilloRed.color = c;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, AlcanceEfectivo);
        }

        #endregion
    }
}
