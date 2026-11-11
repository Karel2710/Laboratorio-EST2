using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GrafosEST2;

namespace TowerDefenseGameplay
{
    public enum EstadoJuego
    {
        EsperandoInicio,
        GenerandoOleada,
        OleadaEnCurso,
        OleadaCompletada,
        GameOver,
        Victoria
    }

    public enum HabilidadActiva
    {
        Sobrecarga = 0,
        Barricada = 1,
        Bombardeo = 2,
        PulsoEMP = 3
    }

    /// <summary>Parámetros de cada nivel de dificultad.</summary>
    public class ConfigNivel
    {
        public string nombre;
        public int totalOleadas;
        public float multVida;
        public float multVelocidad;
        public int oroInicial;
        public int topologia;
        public float multPuntaje;
    }

    /// <summary>
    /// Director central del juego (Tower Defense).
    /// Controla niveles, oleadas, base, economía, construcción/mejora/venta de torres,
    /// habilidades activas, progresión del comandante y puntaje.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public static readonly ConfigNivel[] Niveles =
        {
            new ConfigNivel { nombre = "Nivel 1 · Recluta", totalOleadas = 8, multVida = 1.0f, multVelocidad = 1.0f, oroInicial = 200, topologia = 0, multPuntaje = 1.0f },
            new ConfigNivel { nombre = "Nivel 2 · Veterano", totalOleadas = 12, multVida = 1.3f, multVelocidad = 1.08f, oroInicial = 240, topologia = 1, multPuntaje = 1.5f },
            new ConfigNivel { nombre = "Nivel 3 · Élite", totalOleadas = 15, multVida = 1.65f, multVelocidad = 1.16f, oroInicial = 280, topologia = 2, multPuntaje = 2.2f }
        };

        [Header("Estado y Oleadas")]
        public EstadoJuego estado = EstadoJuego.EsperandoInicio;
        public int nivelActual = 0;
        public int oleadaActual = 1;
        public int totalOleadas = 8;
        public int enemigosRestantesEnOleada = 0;
        public float tiempoEntreOleadas = 8f;
        public float cuentaRegresivaOleada = 0f;
        public bool autoOleada = false;
        public int velocidadJuego = 1;
        public int semillaPartida = 0;

        [Header("Base")]
        public float vidaBaseMaxima = 100f;
        public float vidaBaseActual = 100f;
        public float escudoBaseActual = 0f;

        [Header("Economía y Escuadrón")]
        public int oroActual = 200;
        public Jugador jugadorActivo;
        public Jugador jugadorApoyo;
        public int bonoIngresoMercenarios = 0;

        [Header("Puntaje")]
        public int puntaje = 0;
        public int enemigosDerrotados = 0;
        public int oleadasCompletadas = 0;

        [Header("Construcción")]
        public TipoTorre tipoTorreAConstruir = TipoTorre.Basica;
        public Torre torreSeleccionada;

        [Header("Habilidades Activas")]
        public bool sobrecargaActiva = false;
        public bool apuntandoBarricada = false;
        public bool modoLaboratorio = false;

        /// <summary>En partidas online como cliente, el anfitrión decide cuándo inicia cada oleada.</summary>
        public bool oleadasControladasPorAnfitrion = false;

        private float tiempoSobrecargaRestante = 0f;
        private readonly float[] cooldowns = new float[4];
        private int spawnsPendientes = 0;
        private PlotTorre plotBajoCursor = null;
        private IndicadorAlcance indicador;
        private readonly List<PlotTorre> plotsOrdenados = new List<PlotTorre>();
        private bool finRegistrado = false;

        public bool SobrecargaActiva => sobrecargaActiva;
        public int OroActual => oroActual;
        public ConfigNivel Nivel => Niveles[Mathf.Clamp(nivelActual, 0, Niveles.Length - 1)];
        public bool PartidaTerminada => estado == EstadoJuego.GameOver || estado == EstadoJuego.Victoria;
        public PathNode BarricadaPreviaA { get; private set; }
        public PathNode BarricadaPreviaB { get; private set; }

        public event System.Action OnEstadoCambiado;
        public event System.Action<string> OnNotificacion;
        public event System.Action<Torre> OnTorreSeleccionada;
        public event System.Action<bool, int> OnFinPartida;

        // Eventos de acciones locales (el módulo online los replica al resto de la sala)
        public event System.Action<int, TipoTorre> OnTorreConstruidaLocal;
        public event System.Action<int> OnTorreMejoradaLocal;
        public event System.Action<int> OnTorreVendidaLocal;
        public event System.Action<HabilidadActiva, int, int> OnHabilidadUsadaLocal;
        public event System.Action<int, int> OnOleadaIniciadaLocal;

        #region Ciclo de Vida

        public static GameManager Asegurar()
        {
            if (Instance != null) return Instance;
            GameManager existente = FindFirstObjectByType<GameManager>();
            if (existente != null) return existente;
            GameObject go = new GameObject("GameManager_TowerDefense");
            return go.AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        private void Start()
        {
            VincularJugadorActivo();
            ConfigurarCasillasTerreno();
            PathGraph.Asegurar();
            RedTorresMST.Asegurar();
            EfectosVisualesManager.Asegurar();

            GameObject ind = new GameObject("IndicadorAlcance");
            indicador = ind.AddComponent<IndicadorAlcance>();

            CargarNivel(0, false);
            MostrarNotificacion("🎮 ¡Bienvenido Comandante! Elige una torre en la barra inferior, colócala en el mapa y pulsa ▶ Iniciar Oleada.");
        }

        private void Update()
        {
            GestionarEntradaMapa();

            // Cuenta regresiva entre oleadas (modo Auto-Oleada)
            if ((estado == EstadoJuego.EsperandoInicio || estado == EstadoJuego.OleadaCompletada) && cuentaRegresivaOleada > 0f)
            {
                cuentaRegresivaOleada -= Time.deltaTime;
                if (cuentaRegresivaOleada <= 0f) IniciarOleada();
            }

            // Fin de oleada: no quedan apariciones pendientes ni enemigos vivos
            enemigosRestantesEnOleada = spawnsPendientes + Enemigo.Activos.Count;
            if (estado == EstadoJuego.OleadaEnCurso && enemigosRestantesEnOleada <= 0)
            {
                CompletarOleada();
            }

            if (sobrecargaActiva)
            {
                tiempoSobrecargaRestante -= Time.deltaTime;
                if (tiempoSobrecargaRestante <= 0f)
                {
                    sobrecargaActiva = false;
                    MostrarNotificacion("⚡ Sobrecarga de red finalizada.");
                    NotificarCambio();
                }
            }

            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] > 0f) cooldowns[i] = Mathf.Max(0f, cooldowns[i] - Time.deltaTime);
            }
        }

        #endregion

        #region Escuadrón de Comandantes (BST) y Clases

        private void VincularJugadorActivo()
        {
            ControladorArbolUI arbolUI = ControladorArbolUI.Instance != null ? ControladorArbolUI.Instance : FindFirstObjectByType<ControladorArbolUI>();
            if (arbolUI != null && arbolUI.arbolJugadores != null)
            {
                arbolUI.AsegurarCargado();
                List<Jugador> lista = arbolUI.arbolJugadores.InOrden();
                if (lista.Count == 0)
                {
                    // Árbol vacío: se registra un comandante inicial para que la progresión se guarde
                    Jugador inicial = new Jugador(100, 1, "Comandante_1", 1, 0);
                    arbolUI.arbolJugadores.Insertar(inicial);
                    arbolUI.GuardarArbolEnDisco();
                    lista.Add(inicial);
                }
                if (jugadorActivo == null) jugadorActivo = lista[0];
            }

            if (jugadorActivo == null) jugadorActivo = new Jugador(100, 1, "Comandante_1", 1, 0);
            AsignarComandante(jugadorActivo, false);
        }

        /// <summary>Asigna el Comandante Principal o el de Apoyo del escuadrón.</summary>
        public void AsignarComandante(Jugador j, bool comoApoyo)
        {
            if (j == null) return;

            if (comoApoyo)
            {
                if (jugadorActivo != null && jugadorActivo.Id == j.Id)
                {
                    MostrarNotificacion("⚠️ El comandante principal no puede ser también el de apoyo.");
                    return;
                }
                jugadorApoyo = j;
                MostrarNotificacion($"🤝 {j.nombre} ({j.NombreClaseActual}) se une como Comandante de Apoyo (50% de su bono de clase).");
            }
            else
            {
                jugadorActivo = j;
                if (jugadorApoyo != null && jugadorApoyo.Id == j.Id) jugadorApoyo = null;

                ControladorHabilidadesUI hab = ControladorHabilidadesUI.Instance;
                if (hab != null)
                {
                    hab.jugadorActivo = j;
                    if (hab.sistemaHabilidades != null) hab.sistemaHabilidades.SincronizarConJugador(j);
                    hab.ActualizarTodo();
                }
            }

            ActualizarEscudoBase();
            if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();
            NotificarCambio();
        }

        /// <summary>
        /// Intensidad del bono de una clase: 1.0 si la tiene el comandante principal,
        /// 0.5 si la tiene el comandante de apoyo y 0 en otro caso.
        /// </summary>
        public float FactorClase(ClaseComandante clase)
        {
            if (clase == ClaseComandante.Recluta) return 0f;
            if (jugadorActivo != null && jugadorActivo.clase == clase) return 1f;
            if (jugadorApoyo != null && jugadorApoyo.clase == clase) return 0.5f;
            return 0f;
        }

        public bool TieneHabilidadDAG(string idHabilidad)
        {
            return jugadorActivo != null && jugadorActivo.TieneHabilidad(idHabilidad);
        }

        /// <summary>Se invoca al desbloquear una habilidad en el DAG para refrescar sus efectos.</summary>
        public void OnHabilidadDesbloqueada()
        {
            ActualizarEscudoBase();
            if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();
            GuardarProgreso();
            NotificarCambio();
        }

        public void ActualizarEscudoBase()
        {
            if (TieneHabilidadDAG("HAB_ESCUDO_BASE") && escudoBaseActual <= 0f && !PartidaTerminada) escudoBaseActual = 100f;
        }

        private void GuardarProgreso()
        {
            if (ControladorArbolUI.Instance != null) ControladorArbolUI.Instance.GuardarArbolEnDisco();
        }

        #endregion

        #region Niveles, Velocidad y Casillas

        /// <summary>
        /// Carga un nivel de dificultad: reinicia la partida y configura oleadas, multiplicadores,
        /// oro inicial y la topología de caminos del grafo.
        /// </summary>
        public void CargarNivel(int indice, bool notificar = true)
        {
            nivelActual = Mathf.Clamp(indice, 0, Niveles.Length - 1);
            ConfigNivel cfg = Nivel;

            StopAllCoroutines();
            spawnsPendientes = 0;

            // Limpiar enemigos y torres de la partida anterior
            List<Enemigo> enemigos = new List<Enemigo>(Enemigo.Activos);
            for (int i = 0; i < enemigos.Count; i++)
            {
                if (enemigos[i] != null) Destroy(enemigos[i].gameObject);
            }
            Enemigo.Activos.Clear();
            enemyMovement.Activos.Clear();

            SeleccionarTorre(null);
            for (int i = 0; i < plotsOrdenados.Count; i++)
            {
                if (plotsOrdenados[i] != null && plotsOrdenados[i].TieneTorre) plotsOrdenados[i].QuitarTorre();
            }

            totalOleadas = cfg.totalOleadas;
            oleadaActual = 1;
            oroActual = cfg.oroInicial;
            vidaBaseActual = vidaBaseMaxima;
            escudoBaseActual = 0f;
            puntaje = 0;
            enemigosDerrotados = 0;
            oleadasCompletadas = 0;
            bonoIngresoMercenarios = 0;
            sobrecargaActiva = false;
            apuntandoBarricada = false;
            finRegistrado = false;
            cuentaRegresivaOleada = 0f;
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = 0f;
            semillaPartida = Random.Range(1, 999999);
            estado = EstadoJuego.EsperandoInicio;
            EstablecerVelocidad(1);

            PathGraph.Asegurar().ConstruirAutomatico(cfg.topologia);
            ActualizarCasillasBloqueadas();
            ActualizarEscudoBase();
            if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();

            if (notificar)
            {
                MostrarNotificacion($"🗺️ {cfg.nombre}: {cfg.totalOleadas} oleadas · enemigos ×{cfg.multVida:0.0#} vida · {cfg.oroInicial} de oro · {PathGraph.main.ObtenerAristas().Count} aristas en el grafo.");
            }
            NotificarCambio();
        }

        public void ReiniciarNivel()
        {
            CargarNivel(nivelActual);
        }

        /// <summary>0 = pausa, 1/2/3 = velocidad de simulación.</summary>
        public void EstablecerVelocidad(int v)
        {
            velocidadJuego = Mathf.Clamp(v, 0, 3);
            Time.timeScale = velocidadJuego;
            NotificarCambio();
        }

        public void AlternarAutoOleada()
        {
            autoOleada = !autoOleada;
            if (autoOleada && estado == EstadoJuego.OleadaCompletada && cuentaRegresivaOleada <= 0f)
                cuentaRegresivaOleada = tiempoEntreOleadas;
            if (!autoOleada) cuentaRegresivaOleada = 0f;
            MostrarNotificacion(autoOleada ? "🔁 Auto-Oleada activada: la siguiente oleada inicia sola." : "⏹ Auto-Oleada desactivada.");
            NotificarCambio();
        }

        private void ConfigurarCasillasTerreno()
        {
            GameObject plotsParent = GameObject.Find("plots");
            if (plotsParent == null)
            {
                Debug.LogWarning("[GameManager] No se encontró el objeto 'plots' en la escena.");
                return;
            }

            plotsOrdenados.Clear();
            foreach (Transform hijo in plotsParent.transform)
            {
                // Regla anti-atasco: todo collider de casilla es trigger
                Collider2D[] cols = hijo.GetComponentsInChildren<Collider2D>(true);
                for (int c = 0; c < cols.Length; c++) cols[c].isTrigger = true;

                PlotTorre plot = hijo.GetComponent<PlotTorre>();
                if (plot == null) plot = hijo.gameObject.AddComponent<PlotTorre>();
                plotsOrdenados.Add(plot);
            }

            // Orden determinista (fila superior primero, izquierda a derecha): el índice de
            // casilla es idéntico en todas las máquinas de una partida online.
            plotsOrdenados.Sort((a, b) =>
            {
                int fy = Mathf.RoundToInt(b.transform.position.y * 4f).CompareTo(Mathf.RoundToInt(a.transform.position.y * 4f));
                return fy != 0 ? fy : a.transform.position.x.CompareTo(b.transform.position.x);
            });
            for (int i = 0; i < plotsOrdenados.Count; i++) plotsOrdenados[i].indice = i;

            Debug.Log($"[GameManager] {plotsOrdenados.Count} casillas listas para construcción.");
        }

        /// <summary>Desactiva las casillas que quedan sobre las rutas alternativas del grafo.</summary>
        private void ActualizarCasillasBloqueadas()
        {
            if (PathGraph.main == null) return;
            for (int i = 0; i < plotsOrdenados.Count; i++)
            {
                PlotTorre p = plotsOrdenados[i];
                if (p == null) continue;
                bool sobreRuta = PathGraph.main.DistanciaAlGrafo(p.transform.position, true) < 0.55f;
                p.deshabilitado = sobreRuta;
                p.gameObject.SetActive(!sobreRuta);
            }
        }

        public PlotTorre ObtenerPlot(int indice)
        {
            return indice >= 0 && indice < plotsOrdenados.Count ? plotsOrdenados[indice] : null;
        }

        public int CantidadPlots => plotsOrdenados.Count;

        #endregion

        #region Entrada del Jugador sobre el Mapa

        private void GestionarEntradaMapa()
        {
            if (!EntradaJuego.CursorEnMundo(out Vector2 punto)) return;

            bool click = EntradaJuego.ClickIzquierdo;
            bool cancelar = EntradaJuego.ClickDerecho || EntradaJuego.Escape;
            bool sobreUI = EntradaJuego.EsCursorSobreUI();

            // Modo de colocación de barricada: elegir una arista del grafo
            if (apuntandoBarricada)
            {
                LimpiarHover();
                BarricadaPreviaA = null;
                BarricadaPreviaB = null;

                if (cancelar)
                {
                    apuntandoBarricada = false;
                    MostrarNotificacion("Barricada cancelada.");
                    NotificarCambio();
                    return;
                }

                if (!sobreUI && PathGraph.main != null &&
                    PathGraph.main.AristaMasCercana(punto, out PathNode a, out PathNode b, out float d) && d < 1.2f)
                {
                    BarricadaPreviaA = a;
                    BarricadaPreviaB = b;
                    if (click) ColocarBarricada(a, b, false);
                }
                return;
            }

            if (sobreUI || PartidaTerminada)
            {
                LimpiarHover();
                return;
            }

            if (cancelar) SeleccionarTorre(null);

            // Casilla más cercana al cursor (tolerancia de medio tile)
            PlotTorre detectado = null;
            float mejor = 0.55f;
            List<PlotTorre> plots = PlotTorre.Todos;
            for (int i = 0; i < plots.Count; i++)
            {
                PlotTorre p = plots[i];
                if (p == null || p.deshabilitado) continue;
                Vector2 pp = p.transform.position;
                float d = Mathf.Max(Mathf.Abs(pp.x - punto.x), Mathf.Abs(pp.y - punto.y));
                if (d < mejor)
                {
                    mejor = d;
                    detectado = p;
                }
            }

            if (detectado != plotBajoCursor)
            {
                LimpiarHover();
                plotBajoCursor = detectado;
            }

            if (plotBajoCursor != null)
            {
                if (plotBajoCursor.TieneTorre)
                {
                    if (plotBajoCursor.torreInstanciada != torreSeleccionada)
                    {
                        plotBajoCursor.ResaltarSeleccion();
                        Torre t = plotBajoCursor.torreInstanciada;
                        indicador.Mostrar(t.transform.position, t.AlcanceEfectivo, Torre.ColorTipo(t.tipo), t.EsRadar, t);
                    }
                }
                else
                {
                    bool puede = oroActual >= ObtenerCostoTorre(tipoTorreAConstruir);
                    plotBajoCursor.ResaltarHover(puede);
                    indicador.Mostrar(plotBajoCursor.transform.position, Torre.AlcanceBase(tipoTorreAConstruir),
                        puede ? Torre.ColorTipo(tipoTorreAConstruir) : Paleta.Negativo, tipoTorreAConstruir == TipoTorre.Radar, null);
                }
            }
            else if (torreSeleccionada != null)
            {
                indicador.Mostrar(torreSeleccionada.transform.position, torreSeleccionada.AlcanceEfectivo,
                    Paleta.Dorado, torreSeleccionada.EsRadar, torreSeleccionada);
            }
            else
            {
                indicador.Ocultar();
            }

            if (click)
            {
                if (plotBajoCursor == null) SeleccionarTorre(null);
                else if (plotBajoCursor.TieneTorre) SeleccionarTorre(plotBajoCursor.torreInstanciada);
                else IntentarConstruir(plotBajoCursor);
            }
        }

        private void LimpiarHover()
        {
            if (plotBajoCursor != null)
            {
                bool esSeleccion = torreSeleccionada != null && plotBajoCursor.torreInstanciada == torreSeleccionada;
                if (!esSeleccion) plotBajoCursor.RestaurarColor();
                plotBajoCursor = null;
            }
            if (torreSeleccionada == null && indicador != null) indicador.Ocultar();
        }

        #endregion

        #region Construcción, Mejora y Venta de Torres

        public int ObtenerCostoTorre(TipoTorre tipo)
        {
            float costo = Torre.CostoBase(tipo) * (1f - 0.10f * FactorClase(ClaseComandante.EstrategaEconomico));
            return Mathf.RoundToInt(costo);
        }

        public void SeleccionarTipoTorre(TipoTorre tipo)
        {
            tipoTorreAConstruir = tipo;
            SeleccionarTorre(null);
            MostrarNotificacion($"🎯 {Torre.Icono(tipo)} {Torre.Nombre(tipo)} ({ObtenerCostoTorre(tipo)} Oro): {Torre.Descripcion(tipo)}");
            Sonar(TipoSonido.Click);
            NotificarCambio();
        }

        public void SeleccionarTorre(Torre t)
        {
            if (torreSeleccionada != null && torreSeleccionada.plot != null) torreSeleccionada.plot.RestaurarColor();
            torreSeleccionada = t;
            if (t != null && t.plot != null) t.plot.ResaltarSeleccion();
            if (t == null && indicador != null && plotBajoCursor == null) indicador.Ocultar();
            OnTorreSeleccionada?.Invoke(t);
        }

        private bool IntentarConstruir(PlotTorre plot)
        {
            if (plot == null || plot.TieneTorre || plot.deshabilitado) return false;

            int costo = ObtenerCostoTorre(tipoTorreAConstruir);
            if (oroActual < costo)
            {
                MostrarNotificacion($"¡Oro insuficiente! {Torre.Nombre(tipoTorreAConstruir)} cuesta {costo} de oro (tienes {oroActual}).");
                Sonar(TipoSonido.Error);
                return false;
            }

            Torre t = plot.ColocarTorre(tipoTorreAConstruir, costo);
            if (t == null) return false;

            oroActual -= costo;
            if (jugadorActivo != null) t.EstablecerPropietario(jugadorActivo.Id, jugadorActivo.nombre, ColorJugadorLocal);
            EfectoConstruccion(t);
            MostrarNotificacion($"🔨 {Torre.Nombre(t.tipo)} construida (-{costo} Oro). {DescribirRed(t)}");
            OnTorreConstruidaLocal?.Invoke(plot.indice, t.tipo);
            NotificarCambio();
            return true;
        }

        /// <summary>Color del anillo de propietario del jugador local (lo fija el módulo online).</summary>
        public Color ColorJugadorLocal = Color.clear;

        private static string DescribirRed(Torre t)
        {
            if (t.gradoMST <= 0) return "Sin enlace MST (hay que estar a ≤ 5.5 u de otra torre).";
            return $"Enlazada a la red MST: {t.gradoMST} conexión(es), +{t.bonoDanioRed * 100f:0}% daño.";
        }

        private void EfectoConstruccion(Torre t)
        {
            EfectosVisualesManager fx = EfectosVisualesManager.Instance;
            if (fx == null) return;
            fx.OndaExpansiva(t.transform.position, 0.9f, Torre.ColorTipo(t.tipo), 0.35f);
            fx.Explosion(t.transform.position, Torre.ColorTipo(t.tipo), 8, 2f);
            fx.Sonido(TipoSonido.Construir);
        }

        /// <summary>Construye una torre de otro jugador de la sala online (sin costo local).</summary>
        public bool ConstruirTorreRemota(int plotIndex, TipoTorre tipo, int propietarioId, string nombre, Color color)
        {
            PlotTorre plot = ObtenerPlot(plotIndex);
            if (plot == null || plot.TieneTorre || plot.deshabilitado) return false;
            Torre t = plot.ColocarTorre(tipo, Torre.CostoBase(tipo));
            if (t == null) return false;
            t.EstablecerPropietario(propietarioId, nombre, color);
            EfectoConstruccion(t);
            if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();
            NotificarCambio();
            return true;
        }

        public bool EsTorrePropia(Torre t)
        {
            return t != null && (t.propietarioId < 0 || jugadorActivo == null || t.propietarioId == jugadorActivo.Id);
        }

        public bool MejorarTorre(Torre t, bool remoto = false)
        {
            if (t == null || t.nivel >= Torre.NIVEL_MAXIMO) return false;
            int costo = t.CostoMejora;

            if (!remoto)
            {
                if (!EsTorrePropia(t))
                {
                    MostrarNotificacion($"Esta torre pertenece a {t.propietarioNombre}.");
                    return false;
                }
                if (oroActual < costo)
                {
                    MostrarNotificacion($"¡Oro insuficiente! La mejora cuesta {costo} de oro.");
                    Sonar(TipoSonido.Error);
                    return false;
                }
                oroActual -= costo;
            }

            t.SubirNivel(costo);
            Sonar(TipoSonido.Mejora);
            if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();
            if (!remoto)
            {
                MostrarNotificacion($"⬆️ {Torre.Nombre(t.tipo)} mejorada a Nivel {t.nivel} (-{costo} Oro).");
                if (t.plot != null) OnTorreMejoradaLocal?.Invoke(t.plot.indice);
                OnTorreSeleccionada?.Invoke(torreSeleccionada);
            }
            NotificarCambio();
            return true;
        }

        public bool VenderTorre(Torre t, bool remoto = false)
        {
            if (t == null || t.plot == null) return false;
            if (!remoto && !EsTorrePropia(t))
            {
                MostrarNotificacion($"Esta torre pertenece a {t.propietarioNombre}.");
                return false;
            }

            int reembolso = t.ValorVenta;
            PlotTorre plot = t.plot;
            Vector3 pos = t.transform.position;
            string nombre = Torre.Nombre(t.tipo);
            if (torreSeleccionada == t) SeleccionarTorre(null);
            plot.QuitarTorre();

            if (!remoto)
            {
                oroActual += reembolso;
                if (EfectosVisualesManager.Instance != null)
                    EfectosVisualesManager.Instance.TextoFlotante(pos, $"+{reembolso} Oro", Paleta.Dorado, 1.2f);
                MostrarNotificacion($"💰 {nombre} vendida: +{reembolso} de Oro. Red MST recalculada.");
                OnTorreVendidaLocal?.Invoke(plot.indice);
            }
            Sonar(TipoSonido.Click);
            NotificarCambio();
            return true;
        }

        public void CambiarPrioridad(Torre t)
        {
            if (t == null) return;
            int n = System.Enum.GetValues(typeof(PrioridadObjetivo)).Length;
            t.prioridad = (PrioridadObjetivo)(((int)t.prioridad + 1) % n);
            OnTorreSeleccionada?.Invoke(torreSeleccionada);
        }

        public void GastarOro(int cantidad)
        {
            oroActual = Mathf.Max(0, oroActual - cantidad);
            NotificarCambio();
        }

        #endregion

        #region Control de Oleadas

        public void IniciarOleada()
        {
            if (oleadasControladasPorAnfitrion)
            {
                MostrarNotificacion("🌐 En esta sala el anfitrión decide cuándo inicia la oleada.");
                return;
            }
            IniciarOleadaInterna(semillaPartida);
        }

        /// <summary>Inicio ordenado por el anfitrión de la sala (misma oleada y semilla en todos los clientes).</summary>
        public void IniciarOleadaRemota(int oleada, int semilla)
        {
            if (PartidaTerminada) return;
            oleadaActual = Mathf.Clamp(oleada, 1, totalOleadas);
            semillaPartida = semilla;
            IniciarOleadaInterna(semilla);
        }

        private void IniciarOleadaInterna(int semilla)
        {
            if (estado == EstadoJuego.GenerandoOleada || estado == EstadoJuego.OleadaEnCurso || PartidaTerminada) return;
            if (velocidadJuego == 0) EstablecerVelocidad(1);

            cuentaRegresivaOleada = 0f;
            OnOleadaIniciadaLocal?.Invoke(oleadaActual, semilla);
            StartCoroutine(GenerarOleadaCoroutine(semilla));
        }

        /// <summary>
        /// Composición de la oleada. Las dos primeras son fijas (tutorial) y el resto se genera
        /// con un presupuesto de amenaza y un generador pseudoaleatorio con semilla: la misma
        /// semilla produce la misma oleada en todas las máquinas de una sala online.
        /// </summary>
        public List<TipoEnemigo> ObtenerComposicionOleada(int oleada, int semilla)
        {
            List<TipoEnemigo> lista = new List<TipoEnemigo>();
            if (oleada == 1) { for (int i = 0; i < 6; i++) lista.Add(TipoEnemigo.Corredor); return lista; }
            if (oleada == 2) { for (int i = 0; i < 7; i++) lista.Add(TipoEnemigo.Infanteria); return lista; }

            System.Random rng = new System.Random(semilla * 31 + oleada * 977);
            float presupuesto = 6f + oleada * 3.2f;

            // (tipo, costo de amenaza, oleada mínima, peso de selección)
            TipoEnemigo[] tipos = { TipoEnemigo.Corredor, TipoEnemigo.Infanteria, TipoEnemigo.Blindado, TipoEnemigo.VelozFurtivo, TipoEnemigo.Sanador, TipoEnemigo.BlindadoPesado };
            float[] costos = { 1f, 1.5f, 3f, 2f, 3.5f, 5.5f };
            int[] minimo = { 1, 2, 3, 4, 5, 7 };
            float[] pesos = { 3f, 3f, 2f, 2f, 1f, 1.2f };

            int guard = 0;
            while (presupuesto >= 1f && guard++ < 200)
            {
                float total = 0f;
                for (int i = 0; i < tipos.Length; i++)
                    if (oleada >= minimo[i] && costos[i] <= presupuesto) total += pesos[i];
                if (total <= 0f) break;

                float r = (float)rng.NextDouble() * total;
                for (int i = 0; i < tipos.Length; i++)
                {
                    if (oleada < minimo[i] || costos[i] > presupuesto) continue;
                    r -= pesos[i];
                    if (r <= 0f)
                    {
                        lista.Add(tipos[i]);
                        presupuesto -= costos[i];
                        break;
                    }
                }
            }

            // Jefes: cada 4 oleadas y en la oleada final
            int jefes = 0;
            if (oleada % 4 == 0) jefes = 1 + oleada / 8;
            if (oleada == totalOleadas) jefes += 1;
            for (int i = 0; i < jefes; i++) lista.Insert(rng.Next(lista.Count / 2, lista.Count + 1), TipoEnemigo.Jefe);

            return lista;
        }

        private IEnumerator GenerarOleadaCoroutine(int semilla)
        {
            estado = EstadoJuego.GenerandoOleada;
            List<TipoEnemigo> enemigosOleada = ObtenerComposicionOleada(oleadaActual, semilla);
            spawnsPendientes += enemigosOleada.Count;

            MostrarNotificacion($"⚔️ ¡Oleada {oleadaActual}/{totalOleadas}! {ResumenComposicion(enemigosOleada)}");
            Sonar(TipoSonido.Oleada);
            NotificarCambio();

            float intervalo = Mathf.Max(0.45f, 1.15f - oleadaActual * 0.045f);
            for (int i = 0; i < enemigosOleada.Count; i++)
            {
                SpawnearEnemigo(enemigosOleada[i], false);
                spawnsPendientes--;
                yield return new WaitForSeconds(intervalo);
            }

            estado = EstadoJuego.OleadaEnCurso;
            NotificarCambio();
        }

        private static string ResumenComposicion(List<TipoEnemigo> lista)
        {
            Dictionary<TipoEnemigo, int> conteo = new Dictionary<TipoEnemigo, int>();
            for (int i = 0; i < lista.Count; i++)
            {
                conteo.TryGetValue(lista[i], out int c);
                conteo[lista[i]] = c + 1;
            }
            List<string> partes = new List<string>();
            foreach (var kv in conteo) partes.Add($"{kv.Value}× {SpritesEnemigos.Nombre(kv.Key)}");
            return string.Join(", ", partes);
        }

        public Vector3 PosicionSpawn
        {
            get
            {
                if (PathGraph.main != null)
                {
                    PathNode ini = PathGraph.main.ObtenerNodoInicio();
                    if (ini != null) return ini.transform.position;
                }
                if (levelManager.main != null && levelManager.main.Start != null) return levelManager.main.Start.position;
                return new Vector3(-6.03f, 6.78f, 0f);
            }
        }

        public Vector3 PosicionBase
        {
            get
            {
                if (PathGraph.main != null)
                {
                    PathNode fin = PathGraph.main.ObtenerNodoFin();
                    if (fin != null) return fin.transform.position;
                }
                return new Vector3(7f, -4.2f, 0f);
            }
        }

        private Enemigo SpawnearEnemigo(TipoEnemigo tipo, bool mercenario)
        {
            GameObject obj = new GameObject($"Enemy_{tipo}_{oleadaActual}");
            Vector2 jitter = Random.insideUnitCircle * 0.12f;
            obj.transform.position = PosicionSpawn + new Vector3(jitter.x, jitter.y, 0f);

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = SpritesEnemigos.Obtener(tipo);
            sr.sortingOrder = 8;

            // Físicas anti-atasco: collider trigger, sin gravedad y rotación congelada
            CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
            col.radius = 0.32f;
            col.isTrigger = true;

            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            obj.AddComponent<enemyMovement>();
            Enemigo e = obj.AddComponent<Enemigo>();
            e.ConfigurarTipo(tipo, oleadaActual, Nivel.multVida, Nivel.multVelocidad);
            if (mercenario)
            {
                e.esMercenario = true;
                e.recompensaOro = Mathf.RoundToInt(e.recompensaOro * 0.5f);
                sr.color = new Color(1f, 0.75f, 0.75f, sr.color.a);
            }
            return e;
        }

        /// <summary>Mercenarios enviados por un rival en modo competitivo: se suman a la presión actual.</summary>
        public void RecibirMercenarios(TipoEnemigo tipo, int cantidad, string remitente)
        {
            if (PartidaTerminada) return;
            MostrarNotificacion($"⚔️ ¡{remitente} te envía {cantidad}× {SpritesEnemigos.Nombre(tipo)} mercenario(s)!");
            StartCoroutine(MercenariosCoroutine(tipo, cantidad));
        }

        private IEnumerator MercenariosCoroutine(TipoEnemigo tipo, int cantidad)
        {
            spawnsPendientes += cantidad;
            if (estado == EstadoJuego.EsperandoInicio || estado == EstadoJuego.OleadaCompletada)
            {
                // Fuera de oleada: los mercenarios fuerzan una fase de combate
                estado = EstadoJuego.GenerandoOleada;
                cuentaRegresivaOleada = 0f;
                NotificarCambio();
                for (int i = 0; i < cantidad; i++)
                {
                    SpawnearEnemigo(tipo, true);
                    spawnsPendientes--;
                    yield return new WaitForSeconds(0.7f);
                }
                if (estado == EstadoJuego.GenerandoOleada) estado = EstadoJuego.OleadaEnCurso;
                mercenariosFueraDeOleada = true;
            }
            else
            {
                for (int i = 0; i < cantidad; i++)
                {
                    SpawnearEnemigo(tipo, true);
                    spawnsPendientes--;
                    yield return new WaitForSeconds(0.7f);
                }
            }
            NotificarCambio();
        }

        private bool mercenariosFueraDeOleada = false;

        #endregion

        #region Combate, Daño y Recompensas Progresivas

        public void OnEnemigoDerrotado(Enemigo enemigo)
        {
            if (enemigo == null) return;

            oroActual += enemigo.recompensaOro;
            enemigosDerrotados++;
            puntaje += Mathf.RoundToInt(enemigo.puntos * Nivel.multPuntaje);

            if (jugadorActivo != null)
            {
                bool subio = jugadorActivo.GanarExperiencia(enemigo.recompensaXP, out int niveles, out int puntosGanados);
                if (subio)
                {
                    MostrarNotificacion($"⭐ ¡SUBISTE A NIVEL {jugadorActivo.nivel}! +{puntosGanados} Punto(s) de Habilidad 💎 — ábrelos en ⚡ Armería.");
                    Sonar(TipoSonido.Mejora);
                    if (ControladorHabilidadesUI.Instance != null) ControladorHabilidadesUI.Instance.ActualizarTodo();
                }
            }
            // El comandante de apoyo recibe la mitad de la experiencia
            if (jugadorApoyo != null) jugadorApoyo.GanarExperiencia(enemigo.recompensaXP / 2, out _, out _);

            NotificarCambio();
        }

        public void EnemigoLlegoALaBase(int danio, Enemigo enemigo = null)
        {
            if (PartidaTerminada) return;

            if (escudoBaseActual > 0f)
            {
                float absorbido = Mathf.Min(escudoBaseActual, danio);
                escudoBaseActual -= absorbido;
                danio -= Mathf.RoundToInt(absorbido);
                MostrarNotificacion($"🛡️ El escudo de la base absorbió {absorbido:0} de daño.");
            }

            if (danio > 0)
            {
                vidaBaseActual -= danio;
                MostrarNotificacion($"💥 ¡La base recibió {danio} de daño! Vida restante: {Mathf.Max(0f, vidaBaseActual):0}");
            }

            EfectosVisualesManager fx = EfectosVisualesManager.Instance;
            if (fx != null)
            {
                fx.SacudirCamara(0.16f, 0.25f);
                fx.OndaExpansiva(PosicionBase, 1.4f, Paleta.Negativo, 0.4f);
                fx.Sonido(TipoSonido.Error);
            }

            if (vidaBaseActual <= 0f)
            {
                vidaBaseActual = 0f;
                FinalizarPartida(false);
            }
            NotificarCambio();
        }

        private void CompletarOleada()
        {
            // Fase de combate provocada solo por mercenarios: no cuenta como oleada superada
            if (mercenariosFueraDeOleada)
            {
                mercenariosFueraDeOleada = false;
                estado = oleadasCompletadas > 0 ? EstadoJuego.OleadaCompletada : EstadoJuego.EsperandoInicio;
                NotificarCambio();
                return;
            }

            estado = EstadoJuego.OleadaCompletada;
            oleadasCompletadas++;

            // Oro progresivo por oleada (+ bonos de DAG, clase e ingresos por mercenarios)
            float bono = 50 + oleadaActual * 25;
            if (TieneHabilidadDAG("HAB_GENERADOR")) bono *= 1.25f;
            bono *= 1f + 0.2f * FactorClase(ClaseComandante.EstrategaEconomico);
            int bonoOro = Mathf.RoundToInt(bono) + bonoIngresoMercenarios;

            // Componente aleatorio: suministro aéreo ocasional
            string extra = "";
            if (Random.value < 0.3f)
            {
                int suministro = Random.Range(20, 61);
                bonoOro += suministro;
                extra = $"\n🎁 ¡Suministro aéreo! +{suministro} Oro extra";
            }
            oroActual += bonoOro;
            puntaje += Mathf.RoundToInt(100 * oleadaActual * Nivel.multPuntaje);

            if (TieneHabilidadDAG("HAB_REPARACION_AUTO")) vidaBaseActual = Mathf.Min(vidaBaseMaxima, vidaBaseActual + 25f);
            if (TieneHabilidadDAG("HAB_ESCUDO_BASE")) escudoBaseActual = 100f;

            if (jugadorActivo != null)
            {
                jugadorActivo.AgregarPuntosHabilidad(1);
                if (ControladorHabilidadesUI.Instance != null) ControladorHabilidadesUI.Instance.ActualizarTodo();
            }
            MostrarNotificacion($"🏆 ¡OLEADA {oleadaActual} COMPLETADA!  +1 Punto de Habilidad 💎 | +{bonoOro} Oro 💰{extra}");
            Sonar(TipoSonido.Oleada);
            GuardarProgreso();

            if (oleadaActual >= totalOleadas)
            {
                FinalizarPartida(true);
                return;
            }

            oleadaActual++;
            cuentaRegresivaOleada = autoOleada && !oleadasControladasPorAnfitrion ? tiempoEntreOleadas : 0f;
            NotificarCambio();
        }

        /// <summary>Puntaje total de la partida, incluyendo bonos de fin de partida.</summary>
        public int CalcularPuntajeFinal(bool victoria)
        {
            int total = puntaje + Mathf.RoundToInt(vidaBaseActual * 5f * Nivel.multPuntaje) + oroActual / 5;
            if (victoria) total += Mathf.RoundToInt(1000 * Nivel.multPuntaje);
            return total;
        }

        private void FinalizarPartida(bool victoria)
        {
            if (finRegistrado) return;
            finRegistrado = true;

            estado = victoria ? EstadoJuego.Victoria : EstadoJuego.GameOver;
            apuntandoBarricada = false;
            cuentaRegresivaOleada = 0f;
            int total = CalcularPuntajeFinal(victoria);

            // Registrar en el Leaderboard y en la ficha del comandante (BST)
            if (jugadorActivo != null)
            {
                jugadorActivo.partidasJugadas++;
                if (total > jugadorActivo.mejorPuntaje) jugadorActivo.mejorPuntaje = total;
                jugadorActivo.oro = oroActual;
                LeaderboardJSON.Asegurar().RegistrarResultado(jugadorActivo.nombre, total, nivelActual + 1,
                    victoria ? totalOleadas : oleadaActual, victoria, jugadorActivo.NombreClaseActual);
            }
            GuardarProgreso();

            MostrarNotificacion(victoria
                ? $"👑 ¡VICTORIA TOTAL! Puntaje final: {total}"
                : $"💀 La base ha sido destruida. Puntaje final: {total}");
            Sonar(victoria ? TipoSonido.Victoria : TipoSonido.Derrota);
            if (EfectosVisualesManager.Instance != null && !victoria) EfectosVisualesManager.Instance.SacudirCamara(0.4f, 0.7f);

            OnFinPartida?.Invoke(victoria, total);
            NotificarCambio();
        }

        /// <summary>Sincronización autoritativa desde el anfitrión de la sala online.</summary>
        public void AplicarEstadoRemoto(int oleada, float vidaBase)
        {
            if (PartidaTerminada) return;
            vidaBaseActual = Mathf.Clamp(vidaBase, 0f, vidaBaseMaxima);
            if (estado == EstadoJuego.EsperandoInicio || estado == EstadoJuego.OleadaCompletada)
                oleadaActual = Mathf.Clamp(oleada, 1, totalOleadas);
            if (vidaBaseActual <= 0f) FinalizarPartida(false);
            NotificarCambio();
        }

        #endregion

        #region Habilidades Activas (desbloqueadas en el DAG)

        public static string IdHabilidad(HabilidadActiva h)
        {
            switch (h)
            {
                case HabilidadActiva.Sobrecarga: return "HAB_SOBRECARGA";
                case HabilidadActiva.Barricada: return "HAB_BARRICADA";
                case HabilidadActiva.Bombardeo: return "HAB_BOMBARDEO";
                default: return "HAB_PULSO_EMP";
            }
        }

        public static string NombreHabilidad(HabilidadActiva h)
        {
            switch (h)
            {
                case HabilidadActiva.Sobrecarga: return "⚡ Sobrecarga de Red";
                case HabilidadActiva.Barricada: return "🚧 Barricada Táctica";
                case HabilidadActiva.Bombardeo: return "☄️ Bombardeo Orbital";
                default: return "❄️ Pulso EMP";
            }
        }

        public static float CooldownTotal(HabilidadActiva h)
        {
            switch (h)
            {
                case HabilidadActiva.Sobrecarga: return 45f;
                case HabilidadActiva.Barricada: return 35f;
                case HabilidadActiva.Bombardeo: return 50f;
                default: return 55f;
            }
        }

        public bool HabilidadDesbloqueada(HabilidadActiva h) => modoLaboratorio || TieneHabilidadDAG(IdHabilidad(h));
        public float CooldownRestante(HabilidadActiva h) => cooldowns[(int)h];

        public void UsarHabilidad(HabilidadActiva h)
        {
            if (PartidaTerminada) return;
            if (!HabilidadDesbloqueada(h))
            {
                MostrarNotificacion($"🔒 {NombreHabilidad(h)} está bloqueada: desbloquéala en ⚡ Armería (árbol DAG).");
                Sonar(TipoSonido.Error);
                return;
            }
            if (cooldowns[(int)h] > 0f)
            {
                MostrarNotificacion($"⏳ {NombreHabilidad(h)} en recarga: {cooldowns[(int)h]:0}s.");
                return;
            }

            switch (h)
            {
                case HabilidadActiva.Sobrecarga:
                    ActivarSobrecargaRed();
                    cooldowns[(int)h] = CooldownTotal(h);
                    OnHabilidadUsadaLocal?.Invoke(h, -1, -1);
                    break;
                case HabilidadActiva.Barricada:
                    apuntandoBarricada = true;
                    SeleccionarTorre(null);
                    MostrarNotificacion("🚧 Haz clic sobre un tramo del camino para bloquearlo 15s (clic derecho o Esc cancela).");
                    break;
                case HabilidadActiva.Bombardeo:
                    StartCoroutine(BombardeoCoroutine());
                    cooldowns[(int)h] = CooldownTotal(h);
                    OnHabilidadUsadaLocal?.Invoke(h, -1, -1);
                    break;
                case HabilidadActiva.PulsoEMP:
                    EjecutarPulsoEMP();
                    cooldowns[(int)h] = CooldownTotal(h);
                    OnHabilidadUsadaLocal?.Invoke(h, -1, -1);
                    break;
            }
            NotificarCambio();
        }

        /// <summary>Habilidad activada por otro jugador de la sala (sin validar desbloqueo ni recarga locales).</summary>
        public void EjecutarHabilidadRemota(HabilidadActiva h, int nodoA, int nodoB, string autor)
        {
            if (PartidaTerminada) return;
            MostrarNotificacion($"🌐 {autor} usó {NombreHabilidad(h)}");
            switch (h)
            {
                case HabilidadActiva.Sobrecarga: ActivarSobrecargaRed(); break;
                case HabilidadActiva.Bombardeo: StartCoroutine(BombardeoCoroutine()); break;
                case HabilidadActiva.PulsoEMP: EjecutarPulsoEMP(); break;
                case HabilidadActiva.Barricada:
                    if (PathGraph.main != null && nodoA >= 0 && nodoB >= 0 && nodoA < PathGraph.main.nodos.Count && nodoB < PathGraph.main.nodos.Count)
                        ColocarBarricada(PathGraph.main.nodos[nodoA], PathGraph.main.nodos[nodoB], true);
                    break;
            }
            NotificarCambio();
        }

        public void ActivarSobrecargaRed()
        {
            sobrecargaActiva = true;
            tiempoSobrecargaRestante = 10f;
            int enRed = 0;
            for (int i = 0; i < Torre.Activas.Count; i++) if (Torre.Activas[i] != null && Torre.Activas[i].gradoMST > 0) enRed++;
            MostrarNotificacion($"⚡ ¡SOBRECARGA DE RED! +50% daño y doble cadencia en {enRed} torre(s) enlazada(s) por el MST durante 10s.");
            if (EfectosVisualesManager.Instance != null)
            {
                for (int i = 0; i < Torre.Activas.Count; i++)
                {
                    Torre t = Torre.Activas[i];
                    if (t != null && t.gradoMST > 0) EfectosVisualesManager.Instance.OndaExpansiva(t.transform.position, 1f, Paleta.Dorado, 0.5f);
                }
                EfectosVisualesManager.Instance.Sonido(TipoSonido.Habilidad);
            }
        }

        /// <summary>
        /// Barricada: retira la arista del grafo durante 15s. PathGraph ejecuta Dijkstra en vivo
        /// y redirige a los enemigos; si no existe ruta alternativa el bloqueo se rechaza.
        /// </summary>
        private void ColocarBarricada(PathNode a, PathNode b, bool remoto)
        {
            if (PathGraph.main == null || a == null || b == null) return;

            float costoAntes = PathGraph.main.CostoRutaActual();
            bool ok = PathGraph.main.BloquearArista(a, b, 15f);

            if (!ok)
            {
                if (!remoto)
                {
                    MostrarNotificacion($"🚫 No se puede bloquear N{a.nodeId}–N{b.nodeId}: no existe una ruta alternativa hacia la base. Elige un tramo con desvío.");
                    Sonar(TipoSonido.Error);
                }
                return;
            }

            apuntandoBarricada = false;
            BarricadaPreviaA = null;
            BarricadaPreviaB = null;
            float costoDespues = PathGraph.main.CostoRutaActual();
            MostrarNotificacion($"🚧 Barricada en N{a.nodeId}–N{b.nodeId}. Dijkstra recalculó la ruta: costo {costoAntes:0.0} → {costoDespues:0.0}. {enemyMovement.Activos.Count} enemigo(s) redirigido(s).");

            Vector3 medio = (a.transform.position + b.transform.position) * 0.5f;
            if (EfectosVisualesManager.Instance != null)
            {
                EfectosVisualesManager.Instance.OndaExpansiva(medio, 1.2f, Paleta.Aviso, 0.45f);
                EfectosVisualesManager.Instance.Sonido(TipoSonido.Habilidad);
            }

            if (!remoto)
            {
                cooldowns[(int)HabilidadActiva.Barricada] = CooldownTotal(HabilidadActiva.Barricada);
                OnHabilidadUsadaLocal?.Invoke(HabilidadActiva.Barricada, a.nodeId, b.nodeId);
            }
            NotificarCambio();
        }

        private IEnumerator BombardeoCoroutine()
        {
            MostrarNotificacion("☄️ ¡BOMBARDEO ORBITAL EN CAMINO!");
            EfectosVisualesManager fx = EfectosVisualesManager.Instance;
            if (fx != null)
            {
                fx.Sonido(TipoSonido.Habilidad);
                fx.SacudirCamara(0.2f, 1.6f);
            }

            float danio = (110f + oleadaActual * 28f) * Nivel.multVida;
            const int proyectiles = 16;
            for (int i = 0; i < proyectiles; i++)
            {
                Vector3 destino;
                if (Enemigo.Activos.Count > 0)
                {
                    Enemigo e = Enemigo.Activos[Random.Range(0, Enemigo.Activos.Count)];
                    destino = e != null ? e.transform.position : PosicionBase;
                }
                else
                {
                    // Sin enemigos: cae sobre un punto aleatorio de la ruta óptima
                    List<PathNode> ruta = PathGraph.main != null ? PathGraph.main.ObtenerRutaActual() : null;
                    destino = ruta != null && ruta.Count > 0 ? ruta[Random.Range(0, ruta.Count)].transform.position : PosicionBase;
                }
                destino += (Vector3)(Random.insideUnitCircle * 0.4f);

                GameObject obj = new GameObject("ProyectilOrbital");
                obj.transform.position = destino + new Vector3(Random.Range(-1.5f, 1.5f), 8f, 0f);
                Proyectil p = obj.AddComponent<Proyectil>();
                p.ConfigurarHaciaPunto(destino, danio, 1.6f);
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void EjecutarPulsoEMP()
        {
            List<Enemigo> lista = new List<Enemigo>(Enemigo.Activos);
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i] == null) continue;
                lista[i].Congelar(3.5f);
                lista[i].DestruirEscudo();
            }
            MostrarNotificacion($"❄️ ¡PULSO EMP CRIOGÉNICO! {lista.Count} enemigo(s) congelado(s) 3.5s y escudos destruidos.");
            EfectosVisualesManager fx = EfectosVisualesManager.Instance;
            if (fx != null)
            {
                fx.OndaExpansiva(Vector3.zero, 11f, new Color(0.3f, 0.9f, 1f), 0.8f);
                fx.OndaExpansiva(Vector3.zero, 7f, Color.white, 0.6f);
                fx.SacudirCamara(0.12f, 0.3f);
                fx.Sonido(TipoSonido.Habilidad);
            }
        }

        #endregion

        #region Modo Laboratorio / Profesor (pruebas rápidas en la sustentación)

        public void AlternarModoLaboratorio()
        {
            modoLaboratorio = !modoLaboratorio;
            MostrarNotificacion(modoLaboratorio
                ? "🧪 Modo Laboratorio ACTIVADO: habilidades activas disponibles y atajos de prueba visibles."
                : "🧪 Modo Laboratorio desactivado: progresión normal.");
            NotificarCambio();
        }

        public void LabDarOro()
        {
            if (!modoLaboratorio) return;
            oroActual += 500;
            NotificarCambio();
        }

        public void LabDarPuntos()
        {
            if (!modoLaboratorio || jugadorActivo == null) return;
            jugadorActivo.AgregarPuntosHabilidad(5);
            jugadorActivo.nivel = Mathf.Max(jugadorActivo.nivel, 5);
            if (ControladorHabilidadesUI.Instance != null) ControladorHabilidadesUI.Instance.ActualizarTodo();
            NotificarCambio();
        }

        public void LabEliminarEnemigos()
        {
            if (!modoLaboratorio) return;
            List<Enemigo> lista = new List<Enemigo>(Enemigo.Activos);
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i] != null) lista[i].RecibirDanio(999999f, false, TipoDanio.Energia, false);
            }
        }

        #endregion

        #region Utilidades y Notificaciones

        public void MostrarNotificacion(string mensaje)
        {
            Debug.Log($"[GameManager] {mensaje}");
            OnNotificacion?.Invoke(mensaje);
        }

        public void NotificarCambio()
        {
            OnEstadoCambiado?.Invoke();
        }

        private static void Sonar(TipoSonido s)
        {
            if (EfectosVisualesManager.Instance != null) EfectosVisualesManager.Instance.Sonido(s);
        }

        /// <summary>Compatibilidad con código previo.</summary>
        public static Sprite CrearSpriteEnemigo(TipoEnemigo tipo) => SpritesEnemigos.Obtener(tipo);

        #endregion
    }
}
