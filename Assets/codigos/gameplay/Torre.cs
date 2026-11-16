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

        /// <summary>
        /// Torre 3D: cuerpo del kit que crece con el nivel (piedra → almenas → torre pintada con anillo
        /// dorado) y un cabezal con silueta propia por tipo, así se distinguen también sin color.
        /// </summary>
        private void ConstruirVisuales3D()
        {
            if (raiz3D != null) Destroy(raiz3D.gameObject);
            if (arma3D != null) Destroy(arma3D.gameObject);
            canon3D = null;
            giro3D = null;

            Color acento = ColorTipo(tipo);
            Color piedra = new Color(0.55f, 0.55f, 0.58f);

            // ── Cuerpo ──
            raiz3D = Recursos25D.CrearRaizModelo(transform, "Torre3D");
            raiz3D.position = new Vector3(transform.position.x, transform.position.y, -Vista25D.ALTURA_PAD);

            float ancho = 0.56f;
            float alto = nivel == 1 ? 0.5f : (nivel == 2 ? 0.66f : 0.8f);
            if (tipo == TipoTorre.Radar)
            {
                // Atalaya de madera
                Recursos25D.ModeloOCilindro("structure-poles", raiz3D, Vector3.zero, new Vector3(0.6f, alto, 0.6f), 0.55f, alto, new Color(0.5f, 0.35f, 0.2f));
                Recursos25D.CrearModelo("wood-floor", raiz3D, new Vector3(0f, alto, 0f), new Vector3(0.66f, 0.5f, 0.66f));
                alturaCuerpo3D = alto + 0.06f;
            }
            else
            {
                string cuerpo = nivel >= 3 || tipo == TipoTorre.Hielo || tipo == TipoTorre.Laser ? "tower-paint-base" : "tower-base";
                Recursos25D.ModeloOCilindro(cuerpo, raiz3D, Vector3.zero, new Vector3(ancho, alto, ancho), ancho, alto, piedra);
                alturaCuerpo3D = alto;
                if (nivel >= 2)
                {
                    Recursos25D.CrearModelo("tower-top", raiz3D, new Vector3(0f, alto, 0f), new Vector3(ancho, ancho, ancho));
                }
            }

            // Franja de color del tipo (identificación rápida) y anillo dorado de élite
            Recursos25D.Primitiva(PrimitiveType.Cylinder, raiz3D, new Vector3(0f, alto * 0.62f, 0f),
                new Vector3(ancho * 1.04f, 0.035f, ancho * 1.04f), Mat(acento, acento * 0.35f));
            if (nivel >= NIVEL_MAXIMO)
            {
                Recursos25D.Primitiva(PrimitiveType.Cylinder, raiz3D, new Vector3(0f, alto * 0.3f, 0f),
                    new Vector3(ancho * 1.06f, 0.03f, ancho * 1.06f), Mat(Paleta.Dorado, Paleta.Dorado * 0.4f));
            }

            // ── Cabezal ──
            GameObject goArma = new GameObject("Cabezal3D");
            goArma.transform.SetParent(transform, false);
            arma3D = goArma.transform;
            float s = transform.lossyScale.x;
            arma3D.localScale = Mathf.Abs(s) > 0.0001f ? Vector3.one / s : Vector3.one;
            arma3D.position = new Vector3(transform.position.x, transform.position.y, -(Vista25D.ALTURA_PAD + alturaCuerpo3D));
            arma3D.rotation = canonRotatorio != null ? canonRotatorio.rotation * Recursos25D.RotModelo : Recursos25D.RotModelo;

            float escalaArma = 1f + 0.12f * (nivel - 1);
            Color metal = new Color(0.22f, 0.24f, 0.29f);
            Material mMetal = Mat(metal);
            Material mAcento = Mat(acento, acento * 0.45f);
            Material mBrillo = Mat(Color.Lerp(acento, Color.white, 0.35f), acento * 0.9f);

            Transform pie = Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.035f, 0f), new Vector3(0.32f, 0.035f, 0.32f) * escalaArma, mMetal);
            Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.075f, 0f), new Vector3(0.34f, 0.012f, 0.34f) * escalaArma, mAcento);

            switch (tipo)
            {
                case TipoTorre.Basica:
                    Recursos25D.Primitiva(PrimitiveType.Cube, arma3D, new Vector3(0f, 0.16f, 0f), new Vector3(0.22f, 0.15f, 0.24f) * escalaArma, mMetal);
                    canon3D = Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.17f, 0.2f) * escalaArma,
                        new Vector3(0.08f, 0.17f, 0.08f) * escalaArma, Mat(new Color(0.15f, 0.16f, 0.2f)), new Vector3(90f, 0f, 0f));
                    punta3D = new Vector3(0f, 0.17f, 0.38f) * escalaArma;
                    break;

                case TipoTorre.Hielo:
                    giro3D = Recursos25D.Primitiva(PrimitiveType.Cube, arma3D, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.2f * escalaArma,
                        Mat(new Color(0.55f, 0.95f, 1f), new Color(0.15f, 0.5f, 0.6f)), new Vector3(45f, 0f, 45f));
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * 120f * Mathf.Deg2Rad;
                        Recursos25D.Primitiva(PrimitiveType.Cube, giro3D, new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * 0.9f,
                            Vector3.one * 0.3f, mBrillo, new Vector3(30f, i * 40f, 0f));
                    }
                    punta3D = new Vector3(0f, 0.3f, 0f) * escalaArma;
                    break;

                case TipoTorre.Canon:
                    Recursos25D.Primitiva(PrimitiveType.Sphere, arma3D, new Vector3(0f, 0.17f, 0f), Vector3.one * 0.3f * escalaArma, Mat(new Color(0.55f, 0.36f, 0.18f)));
                    canon3D = Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.18f, 0.17f) * escalaArma,
                        new Vector3(0.15f, 0.15f, 0.15f) * escalaArma, Mat(new Color(0.3f, 0.22f, 0.14f)), new Vector3(90f, 0f, 0f));
                    punta3D = new Vector3(0f, 0.18f, 0.33f) * escalaArma;
                    break;

                case TipoTorre.Laser:
                    Recursos25D.Primitiva(PrimitiveType.Cube, arma3D, new Vector3(0f, 0.15f, 0f), new Vector3(0.16f, 0.13f, 0.2f) * escalaArma, Mat(new Color(0.25f, 0.12f, 0.32f)));
                    canon3D = Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.16f, 0.22f) * escalaArma,
                        new Vector3(0.05f, 0.2f, 0.05f) * escalaArma, mMetal, new Vector3(90f, 0f, 0f));
                    Recursos25D.Primitiva(PrimitiveType.Sphere, canon3D, new Vector3(0f, 1.05f, 0f), new Vector3(1.8f, 0.45f, 1.8f), mBrillo);
                    punta3D = new Vector3(0f, 0.16f, 0.44f) * escalaArma;
                    break;

                case TipoTorre.Tesla:
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.13f, 0.06f) * escalaArma, mMetal);
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.2f, 0f), new Vector3(0.28f, 0.008f, 0.28f) * escalaArma, mAcento);
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.27f, 0f), new Vector3(0.22f, 0.008f, 0.22f) * escalaArma, mAcento);
                    giro3D = Recursos25D.Primitiva(PrimitiveType.Sphere, arma3D, new Vector3(0f, 0.4f, 0f) * escalaArma, Vector3.one * 0.22f * escalaArma, mBrillo);
                    punta3D = new Vector3(0f, 0.4f, 0f) * escalaArma;
                    break;

                default: // Radar
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.14f, 0f), new Vector3(0.05f, 0.1f, 0.05f) * escalaArma, mMetal);
                    Recursos25D.Primitiva(PrimitiveType.Cylinder, arma3D, new Vector3(0f, 0.3f, 0.03f) * escalaArma,
                        new Vector3(0.42f, 0.015f, 0.42f) * escalaArma, Mat(new Color(0.75f, 0.8f, 0.85f)), new Vector3(60f, 0f, 0f));
                    giro3D = Recursos25D.Primitiva(PrimitiveType.Sphere, arma3D, new Vector3(0f, 0.36f, 0.16f) * escalaArma, Vector3.one * 0.07f * escalaArma, mBrillo);
                    punta3D = new Vector3(0f, 0.3f, 0f) * escalaArma;
                    break;
            }

            if (canon3D != null) posCanon3D = canon3D.localPosition;
            if (pie != null) pie.name = "Pie";
        }

        private void AnimarVisuales3D(float dt)
        {
            float t = Time.time + idTorre * 0.7f;
            if (tipo == TipoTorre.Radar && arma3D != null)
            {
                anguloRadar -= 120f * dt;
                arma3D.rotation = Quaternion.Euler(0f, 0f, anguloRadar) * Recursos25D.RotModelo;
                if (giro3D != null) giro3D.gameObject.SetActive(Mathf.Repeat(t, 1.2f) < 0.6f);
            }
            else if (tipo == TipoTorre.Hielo && giro3D != null)
            {
                float e = 1f + 0.12f * (nivel - 1);
                giro3D.localPosition = new Vector3(0f, (0.3f + 0.03f * Mathf.Sin(t * 2.4f)) * e, 0f);
                giro3D.localRotation = Quaternion.Euler(0f, t * 70f, 0f) * Quaternion.Euler(45f, 0f, 45f);
            }
            else if (tipo == TipoTorre.Tesla && giro3D != null)
            {
                float e = (0.22f + 0.03f * Mathf.Sin(t * 9f)) * (1f + 0.12f * (nivel - 1));
                giro3D.localScale = Vector3.one * e;
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
