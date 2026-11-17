using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        Victoria,
        EsperandoJugadores // Solo LAN: el host espera a que el cliente cargue la escena
    }

    /// <summary>A quién va dirigida una notificación (en LAN cada jugador ve solo las suyas).</summary>
    public enum DestinoNotificacion
    {
        Defensor,
        Atacante,
        Ambos
    }

    /// <summary>
    /// Director central del juego (Tower Defense).
    /// Controla la aparición de oleadas, salud de la base, economía (oro),
    /// y la progresión estricta de experiencia y puntos de habilidad (DAG).
    ///
    /// En multijugador LAN:
    ///  - En el host simula las rondas (el atacante decide qué enemigos salen).
    ///  - En el cliente actúa como "espejo": sus campos los escribe ClientePartida con el estado del host
    ///    y no ejecuta lógica de oleadas, para que el HUD funcione igual en ambos PCs.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Estado y Oleadas")]
        public EstadoJuego estado = EstadoJuego.EsperandoInicio;
        public int oleadaActual = 1;
        public int totalOleadas = 8;
        public int enemigosRestantesEnOleada = 0;
        public float tiempoEntreOleadas = 6f;
        public float cuentaRegresivaOleada = 0f;

        [Header("Dificultad (un jugador)")]
        public int nivelDificultad = 1;
        public float multiplicadorVidaEnemigos = 1f;
        public float multiplicadorIntervaloSpawn = 1f;
        private int oroInicialNivel = 150;

        [Header("Base")]
        public float vidaBaseMaxima = 100f;
        public float vidaBaseActual = 100f;
        public float escudoBaseActual = 0f;

        [Header("Economía y Jugador")]
        public int oroActual = 150;
        public Jugador jugadorActivo;

        [Header("Construcción Libre de Torres")]
        public TipoTorre tipoTorreAConstruir = TipoTorre.Basica;

        [Header("Habilidades Activas")]
        public bool sobrecargaActiva = false;
        private float tiempoSobrecargaRestante = 0f;
        public float enfriamientoSobrecargaRestante = 0f;
        public const float EnfriamientoSobrecargaMultijugador = 30f;
        private bool empUsadoEstaOleada = false;

        [Header("Multijugador LAN (rondas)")]
        public float duracionVentanaRonda = 45f;
        public float pausaEntreRondas = 10f;
        public float preparacionInicialMultijugador = 20f;
        public float tiempoRondaRestante = 0f;
        public bool ventanaEnvioAbierta = false;

        /// <summary>Casillas en el orden de la jerarquía de 'plots'. El índice identifica la casilla en la red.</summary>
        public List<PlotTorre> plotsOrdenados = new List<PlotTorre>();

        public bool SobrecargaActiva => sobrecargaActiva;
        public int OroActual => oroActual;

        public int ObtenerCostoTorre(TipoTorre tipo)
        {
            switch (tipo)
            {
                case TipoTorre.Basica: return 50;
                case TipoTorre.Hielo: return 75;
                case TipoTorre.Canon: return 90;
                case TipoTorre.Laser: return 120;
                default: return 50;
            }
        }

        public void SeleccionarTipoTorre(TipoTorre tipo)
        {
            tipoTorreAConstruir = tipo;
            int costo = ObtenerCostoTorre(tipo);
            MostrarNotificacionLocal($"🎯 Modo Construcción: Torre {tipo} seleccionada (Costo: {costo} Oro). Haz clic en una casilla para colocarla.");
            NotificarCambio();
        }

        public event System.Action OnEstadoCambiado;
        public event System.Action<string> OnNotificacion;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            VincularJugadorActivo();
            vidaBaseActual = vidaBaseMaxima;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ConfigurarCasillasTerreno();
            ActualizarEscudoBase();

            if (ModoJuego.EsMultijugador)
            {
                // Las rondas arrancan cuando el host confirma que ambos jugadores cargaron la escena
                estado = EstadoJuego.EsperandoJugadores;
                totalOleadas = 8;
                cuentaRegresivaOleada = 0f;
                MostrarNotificacionLocal("🌐 Sincronizando con el otro jugador...");
                NotificarCambio();
                return;
            }

            AplicarNivelDificultad(ModoJuego.NivelUnJugador, silencioso: true);

            // Tiempo de preparación inicial para que el jugador coloque sus primeras defensas libremente
            cuentaRegresivaOleada = 8f;
            estado = EstadoJuego.EsperandoInicio;
            MostrarNotificacion("🎮 ¡Bienvenido Comandante! Coloca tus torres libremente en el mapa usando la barra inferior.");
            NotificarCambio();
        }

        private PlotTorre plotBajoCursor = null;

        private void Update()
        {
            // Detección directa y universal de interacción con las casillas del mapa (New Input System + Legacy)
            GestionarRaycastConstruccion();

            // En el cliente LAN el estado lo escribe ClientePartida: no se simula nada aquí
            if (!ModoJuego.TieneAutoridad) return;

            // Gestión de cuenta regresiva entre oleadas
            if (estado == EstadoJuego.EsperandoInicio || estado == EstadoJuego.OleadaCompletada)
            {
                if (cuentaRegresivaOleada > 0)
                {
                    cuentaRegresivaOleada -= Time.deltaTime;
                    if (cuentaRegresivaOleada <= 0)
                    {
                        IniciarOleada();
                    }
                }
            }

            // Ventana de envío del atacante (LAN)
            if (ModoJuego.EsMultijugador && estado == EstadoJuego.OleadaEnCurso && ventanaEnvioAbierta)
            {
                tiempoRondaRestante -= Time.deltaTime;
                if (tiempoRondaRestante <= 0f)
                {
                    CerrarVentanaEnvio();
                }
            }

            if (enfriamientoSobrecargaRestante > 0f)
            {
                enfriamientoSobrecargaRestante = Mathf.Max(0f, enfriamientoSobrecargaRestante - Time.deltaTime);
            }

            // Gestión de Sobrecarga de Red (Habilidad Definitiva DAG)
            if (sobrecargaActiva)
            {
                tiempoSobrecargaRestante -= Time.deltaTime;
                if (tiempoSobrecargaRestante <= 0)
                {
                    sobrecargaActiva = false;
                    MostrarNotificacion("⚡ Sobrecarga de red finalizada.");
                    NotificarCambio();
                }
            }
        }

        private void GestionarRaycastConstruccion()
        {
            // El atacante no construye torres: su HUD maneja sus propios clics (habilidades con objetivo)
            if (ModoJuego.EsAtacanteLocal)
            {
                if (plotBajoCursor != null)
                {
                    plotBajoCursor.RestaurarColor();
                    plotBajoCursor = null;
                }
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null) return;

            // 1. Obtener coordenadas de pantalla del ratón / toque
            Vector2 mouseScreen = Vector2.zero;
            bool clickPresionado = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                mouseScreen = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                clickPresionado = UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
            }
            if (!clickPresionado && UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                mouseScreen = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
                clickPresionado = true;
            }
#endif
#if !ENABLE_INPUT_SYSTEM || UNITY_EDITOR
            if (mouseScreen == Vector2.zero)
            {
                mouseScreen = Input.mousePosition;
            }
            if (!clickPresionado)
            {
                clickPresionado = Input.GetMouseButtonDown(0);
            }
#endif

            // 2. Si el cursor está sobre la UI gráfica real (botones, modales), no procesar terreno
            if (EsCursorSobreUI(mouseScreen))
            {
                if (plotBajoCursor != null)
                {
                    plotBajoCursor.RestaurarColor();
                    plotBajoCursor = null;
                }
                return;
            }

            // 3. Proyectar rayo al plano 2D del mundo (Z = 0)
            float distCamZ = Mathf.Abs(cam.transform.position.z);
            Vector3 mouseMundo = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, distCamZ));
            Vector2 punto2D = new Vector2(mouseMundo.x, mouseMundo.y);

            PlotTorre plotDetectado = null;

            // Prueba 1: Raycast directo
            RaycastHit2D hit = Physics2D.Raycast(punto2D, Vector2.zero);
            if (hit.collider != null)
            {
                plotDetectado = hit.collider.GetComponent<PlotTorre>() ?? hit.collider.GetComponentInParent<PlotTorre>();
            }

            // Prueba 2: OverlapPoint por si el raycast pasó por el borde
            if (plotDetectado == null)
            {
                Collider2D col = Physics2D.OverlapPoint(punto2D);
                if (col != null)
                {
                    plotDetectado = col.GetComponent<PlotTorre>() ?? col.GetComponentInParent<PlotTorre>();
                }
            }

            // Prueba 3: Círculo de proximidad pequeño (tolerancia de clic de 0.5 unidades)
            if (plotDetectado == null && clickPresionado)
            {
                Collider2D[] cercanos = Physics2D.OverlapCircleAll(punto2D, 0.5f);
                for (int i = 0; i < cercanos.Length; i++)
                {
                    PlotTorre p = cercanos[i].GetComponent<PlotTorre>() ?? cercanos[i].GetComponentInParent<PlotTorre>();
                    if (p != null)
                    {
                        plotDetectado = p;
                        break;
                    }
                }
            }

            // 4. Actualizar efectos visuales de hover
            if (plotDetectado != plotBajoCursor)
            {
                if (plotBajoCursor != null)
                {
                    plotBajoCursor.RestaurarColor();
                }

                plotBajoCursor = plotDetectado;

                if (plotBajoCursor != null)
                {
                    plotBajoCursor.ResaltarHover();
                }
            }

            // 5. Ejecutar construcción al hacer clic (único punto de entrada: PlotTorre ya no escucha clics por su cuenta)
            if (clickPresionado && plotDetectado != null)
            {
                ComandosJugador.ConstruirTorre(plotDetectado, tipoTorreAConstruir);
            }
        }

        public static bool EsCursorSobreUI(Vector2 mouseScreen)
        {
            if (EventSystem.current == null) return false;

            PointerEventData ped = new PointerEventData(EventSystem.current)
            {
                position = mouseScreen
            };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);

            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].module is GraphicRaycaster)
                {
                    return true;
                }
            }
            return false;
        }

        private void VincularJugadorActivo()
        {
            if (ControladorHabilidadesUI.Instance != null && ControladorHabilidadesUI.Instance.jugadorActivo != null)
            {
                jugadorActivo = ControladorHabilidadesUI.Instance.jugadorActivo;
            }
            else
            {
                ControladorArbolUI ctrlArbol = FindFirstObjectByType<ControladorArbolUI>();
                if (ctrlArbol != null && ctrlArbol.arbolJugadores != null)
                {
                    List<Jugador> lista = ctrlArbol.arbolJugadores.InOrden();
                    if (lista.Count > 0)
                    {
                        jugadorActivo = lista[0];
                    }
                }
            }

            if (jugadorActivo == null)
            {
                jugadorActivo = new Jugador(100, 1, "Comandante_1", 1, 0);
            }

            if (ModoJuego.EsMultijugador)
            {
                // En LAN el defensor juega con una copia nueva de su comandante (nivel 1, sin habilidades)
                // para que la partida sea pareja; al final solo se guardan victorias/derrotas en el BST.
                jugadorActivo = new Jugador(100, ModoJuego.IdDefensor, ModoJuego.NombreDefensor, 1, 0);
                if (ControladorHabilidadesUI.Instance != null)
                {
                    ControladorHabilidadesUI.Instance.AsignarJugador(jugadorActivo);
                }
            }
        }

        #region Configuración del Terreno y Casillas

        private void ConfigurarCasillasTerreno()
        {
            // Asegurar que la cámara principal tenga Physics2DRaycaster para compatibilidad completa
            Camera cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam != null && cam.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
            {
                cam.gameObject.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
            }

            // Buscar el contenedor de casillas 'plots'
            GameObject plotsParent = GameObject.Find("plots");
            if (plotsParent == null)
            {
                Transform[] todosT = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < todosT.Length; i++)
                {
                    if (todosT[i].name == "plots" && todosT[i].gameObject.scene.isLoaded)
                    {
                        plotsParent = todosT[i].gameObject;
                        break;
                    }
                }
            }

            plotsOrdenados.Clear();

            if (plotsParent != null)
            {
                // Hijos directos en el orden de la jerarquía: es idéntico en ambos PCs,
                // por eso sirve como identificador de casilla en los mensajes de red.
                Transform contenedor = plotsParent.transform;
                for (int i = 0; i < contenedor.childCount; i++)
                {
                    Transform hijo = contenedor.GetChild(i);

                    // Asegurar que todos los Collider2D sean trigger para evitar colisiones físicas
                    Collider2D[] cols = hijo.GetComponentsInChildren<Collider2D>(true);
                    if (cols == null || cols.Length == 0)
                    {
                        BoxCollider2D bc = hijo.gameObject.AddComponent<BoxCollider2D>();
                        bc.size = new Vector2(0.9f, 0.9f);
                        bc.isTrigger = true;
                    }
                    else
                    {
                        for (int c = 0; c < cols.Length; c++)
                        {
                            cols[c].isTrigger = true;
                        }
                    }

                    PlotTorre plot = hijo.GetComponent<PlotTorre>();
                    if (plot == null)
                    {
                        plot = hijo.gameObject.AddComponent<PlotTorre>();
                    }
                    plot.indiceRed = plotsOrdenados.Count;
                    plotsOrdenados.Add(plot);
                }

                Debug.Log($"[GameManager] Configuración de casillas completada. {plotsOrdenados.Count} casillas listas para construcción.");
            }
            else
            {
                Debug.LogWarning("[GameManager] No se encontró el objeto 'plots' en la escena.");
            }
        }

        public PlotTorre ObtenerPlotPorIndice(int indice)
        {
            if (indice < 0 || indice >= plotsOrdenados.Count) return null;
            return plotsOrdenados[indice];
        }

        public Vector3 ObtenerPosicionBase()
        {
            if (levelManager.main != null && levelManager.main.Path != null && levelManager.main.Path.Length > 0)
            {
                Transform ultimo = levelManager.main.Path[levelManager.main.Path.Length - 1];
                if (ultimo != null) return ultimo.position;
            }
            return new Vector3(7f, -4.2f, 0f);
        }

        #endregion

        #region Dificultad (Nivel 1 / 2 / 3)

        /// <summary>
        /// Cambia el nivel de dificultad del modo un jugador. Solo se permite antes de la primera oleada.
        /// </summary>
        public void ConfigurarNivel(int indice)
        {
            if (ModoJuego.EsMultijugador) return;

            if (estado != EstadoJuego.EsperandoInicio || oleadaActual != 1)
            {
                MostrarNotificacion("⚠️ El nivel solo se puede cambiar antes de que empiece la primera oleada.");
                return;
            }

            AplicarNivelDificultad(indice, silencioso: false);
        }

        private void AplicarNivelDificultad(int indice, bool silencioso)
        {
            indice = Mathf.Clamp(indice, 0, 2);
            ModoJuego.NivelUnJugador = indice;
            nivelDificultad = indice;

            string nombre;
            int oroInicial;
            switch (indice)
            {
                case 0:
                    nombre = "Nivel 1 (Fácil)";
                    totalOleadas = 5;
                    multiplicadorVidaEnemigos = 0.8f;
                    multiplicadorIntervaloSpawn = 1.15f;
                    oroInicial = 175;
                    break;
                case 2:
                    nombre = "Nivel 3 (Difícil)";
                    totalOleadas = 10;
                    multiplicadorVidaEnemigos = 1.35f;
                    multiplicadorIntervaloSpawn = 0.85f;
                    oroInicial = 125;
                    break;
                default:
                    nombre = "Nivel 2 (Normal)";
                    totalOleadas = 8;
                    multiplicadorVidaEnemigos = 1f;
                    multiplicadorIntervaloSpawn = 1f;
                    oroInicial = 150;
                    break;
            }

            // Ajustar el oro por la diferencia de oro inicial (no regala ni quita lo ya invertido en torres)
            oroActual = Mathf.Max(0, oroActual + (oroInicial - oroInicialNivel));
            oroInicialNivel = oroInicial;

            if (!silencioso)
            {
                MostrarNotificacion($"🗺️ {nombre}: {totalOleadas} oleadas · vida de enemigos x{multiplicadorVidaEnemigos:0.##} · oro inicial {oroInicial}");
            }
            NotificarCambio();
        }

        #endregion

        #region Control de Oleadas

        public void IniciarOleada()
        {
            if (!ModoJuego.TieneAutoridad) return;
            if (estado == EstadoJuego.EsperandoJugadores) return;
            if (estado == EstadoJuego.GenerandoOleada || estado == EstadoJuego.OleadaEnCurso) return;
            if (estado == EstadoJuego.GameOver || estado == EstadoJuego.Victoria) return;

            cuentaRegresivaOleada = 0f;
            empUsadoEstaOleada = false;

            if (ModoJuego.EsMultijugador)
            {
                IniciarRondaMultijugador();
                return;
            }

            StartCoroutine(GenerarOleadaCoroutine());
        }

        private List<TipoEnemigo> ObtenerComposicionOleada(int oleada)
        {
            List<TipoEnemigo> lista = new List<TipoEnemigo>();
            switch (oleada)
            {
                case 1:
                    for (int i = 0; i < 5; i++) lista.Add(TipoEnemigo.Corredor);
                    break;
                case 2:
                    for (int i = 0; i < 7; i++) lista.Add(TipoEnemigo.Infanteria);
                    break;
                case 3:
                    for (int i = 0; i < 5; i++) lista.Add(TipoEnemigo.Infanteria);
                    for (int i = 0; i < 2; i++) lista.Add(TipoEnemigo.Blindado);
                    lista.Add(TipoEnemigo.Jefe);
                    break;
                case 4:
                    for (int i = 0; i < 9; i++) lista.Add(TipoEnemigo.Corredor);
                    for (int i = 0; i < 4; i++) lista.Add(TipoEnemigo.Infanteria);
                    break;
                case 5:
                    for (int i = 0; i < 6; i++) lista.Add(TipoEnemigo.Blindado);
                    for (int i = 0; i < 5; i++) lista.Add(TipoEnemigo.Infanteria);
                    lista.Add(TipoEnemigo.Jefe);
                    break;
                case 6:
                    for (int i = 0; i < 8; i++) lista.Add(TipoEnemigo.Corredor);
                    for (int i = 0; i < 6; i++) lista.Add(TipoEnemigo.Blindado);
                    for (int i = 0; i < 2; i++) lista.Add(TipoEnemigo.Jefe);
                    break;
                case 7:
                    for (int i = 0; i < 8; i++) lista.Add(TipoEnemigo.Corredor);
                    for (int i = 0; i < 8; i++) lista.Add(TipoEnemigo.Infanteria);
                    for (int i = 0; i < 8; i++) lista.Add(TipoEnemigo.Blindado);
                    break;
                default:
                    for (int i = 0; i < 10; i++) lista.Add(TipoEnemigo.Corredor);
                    for (int i = 0; i < 8; i++) lista.Add(TipoEnemigo.Blindado);
                    for (int i = 0; i < 3; i++) lista.Add(TipoEnemigo.Jefe);
                    break;
            }
            return lista;
        }

        private IEnumerator GenerarOleadaCoroutine()
        {
            estado = EstadoJuego.GenerandoOleada;
            List<TipoEnemigo> enemigosOleada = ObtenerComposicionOleada(oleadaActual);
            enemigosRestantesEnOleada = enemigosOleada.Count;

            MostrarNotificacion($"⚔️ ¡Comenzando Oleada {oleadaActual}/{totalOleadas}! ({enemigosRestantesEnOleada} enemigos entrantes)");
            NotificarCambio();

            Transform spawnPoint = null;
            if (levelManager.main != null && levelManager.main.Start != null)
            {
                spawnPoint = levelManager.main.Start;
            }

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : new Vector3(-5.94f, 6.57f, 0);

            for (int i = 0; i < enemigosOleada.Count; i++)
            {
                SpawmearEnemigo(spawnPos, enemigosOleada[i]);
                float intervalo = (1.2f - Mathf.Clamp(oleadaActual * 0.05f, 0f, 0.5f)) * multiplicadorIntervaloSpawn;
                yield return new WaitForSeconds(intervalo);
            }

            if (estado == EstadoJuego.GenerandoOleada)
            {
                estado = EstadoJuego.OleadaEnCurso;
            }
            NotificarCambio();
        }

        private Enemigo CrearEnemigo(Vector3 pos, TipoEnemigo tipo)
        {
            GameObject enemigoObj = new GameObject($"Enemy_{tipo}_{oleadaActual}");
            enemigoObj.transform.position = pos;

            SpriteRenderer sr = enemigoObj.AddComponent<SpriteRenderer>();
            sr.sprite = CrearSpriteEnemigo(tipo);
            sr.sortingOrder = 8;

            CircleCollider2D col = enemigoObj.AddComponent<CircleCollider2D>();
            col.radius = 0.32f;
            col.isTrigger = true; // Trigger para que jamás se tranque físicamente con las casillas de torres

            Rigidbody2D rb = enemigoObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true; // Rotación congelada para estabilidad de trayectoria

            enemigoObj.AddComponent<enemyMovement>();
            Enemigo compEnemigo = enemigoObj.AddComponent<Enemigo>();
            compEnemigo.ConfigurarTipo(tipo, oleadaActual);
            return compEnemigo;
        }

        private void SpawmearEnemigo(Vector3 pos, TipoEnemigo tipo)
        {
            CrearEnemigo(pos, tipo);
        }

        #endregion

        #region Multijugador LAN: rondas controladas por el atacante

        /// <summary>
        /// Llamado por ServidorPartida cuando ambos jugadores cargaron la escena.
        /// Empieza la preparación inicial (el defensor coloca sus primeras torres).
        /// </summary>
        public void IniciarPreparacionMultijugador()
        {
            if (!ModoJuego.EsMultijugador || !ModoJuego.EsHost) return;
            if (estado != EstadoJuego.EsperandoJugadores) return;

            estado = EstadoJuego.EsperandoInicio;
            oleadaActual = 1;
            totalOleadas = 8;
            cuentaRegresivaOleada = preparacionInicialMultijugador;

            if (ControladorAtacante.Instance != null)
            {
                ControladorAtacante.Instance.AlIniciarPartida();
            }

            Notificar($"🛡️ {ModoJuego.NombreDefensor} (Defensor) vs ⚔️ {ModoJuego.NombreAtacante} (Atacante). " +
                      $"{preparacionInicialMultijugador:0} s de preparación.", DestinoNotificacion.Ambos);
            NotificarCambio();
        }

        private void IniciarRondaMultijugador()
        {
            estado = EstadoJuego.OleadaEnCurso;
            ventanaEnvioAbierta = true;
            tiempoRondaRestante = duracionVentanaRonda;
            enemigosRestantesEnOleada = 0;

            if (ControladorAtacante.Instance != null)
            {
                ControladorAtacante.Instance.AlIniciarRonda(oleadaActual);
            }

            Notificar($"⚔️ ¡Ronda {oleadaActual}/{totalOleadas}! El atacante tiene {duracionVentanaRonda:0} s para enviar tropas.", DestinoNotificacion.Ambos);
            NotificarCambio();
        }

        private void CerrarVentanaEnvio()
        {
            tiempoRondaRestante = 0f;
            ventanaEnvioAbierta = false;

            if (ControladorAtacante.Instance != null)
            {
                ControladorAtacante.Instance.AlCerrarVentana();
            }

            Notificar("⏱️ Se cerró la ventana de envío. La ronda termina cuando no quede ningún enemigo.", DestinoNotificacion.Ambos);
            ComprobarFinDeOleada();
            NotificarCambio();
        }

        /// <summary>
        /// Crea un enemigo enviado por el atacante que recorre la ruta indicada (calculada con Dijkstra).
        /// </summary>
        public Enemigo SpawnearEnemigoEnRuta(TipoEnemigo tipo, List<PathNode> ruta)
        {
            if (ruta == null || ruta.Count < 2) return null;

            Enemigo enemigo = CrearEnemigo(ruta[0].transform.position, tipo);
            enemyMovement mov = enemigo.GetComponent<enemyMovement>();
            if (mov != null)
            {
                mov.AsignarRuta(ruta);
            }

            if (ControladorAtacante.Instance != null)
            {
                ControladorAtacante.Instance.AplicarModificadoresEnemigo(enemigo);
            }

            enemigosRestantesEnOleada++;
            NotificarCambio();
            return enemigo;
        }

        #endregion

        #region Combate, Daño y Recompensas Progresivas

        public void OnEnemigoDerrotado(Enemigo enemigo)
        {
            if (enemigo == null) return;

            // 1. Otorgar Oro
            oroActual += enemigo.recompensaOro;

            // 2. Otorgar Experiencia Progresiva
            if (jugadorActivo != null)
            {
                bool subioNivel = jugadorActivo.GanarExperiencia(enemigo.recompensaXP, out int nivelesSubidos, out int puntosGanados);
                if (subioNivel)
                {
                    MostrarNotificacion($"⭐ ¡SUBISTE A NIVEL {jugadorActivo.nivel}! Ganaste +{puntosGanados} Punto de Habilidad 💎");
                    if (ControladorHabilidadesUI.Instance != null)
                    {
                        ControladorHabilidadesUI.Instance.ActualizarTodo();
                    }
                }
            }

            enemigosRestantesEnOleada--;
            ComprobarFinDeOleada();
            NotificarCambio();
        }

        public void EnemigoLlegoALaBase(int danio)
        {
            int danioOriginal = danio;

            // Si hay escudo activo (habilidad DAG), absorbe el daño primero
            if (escudoBaseActual > 0)
            {
                float danioAlEscudo = Mathf.Min(escudoBaseActual, danio);
                escudoBaseActual -= danioAlEscudo;
                danio -= (int)danioAlEscudo;
                MostrarNotificacion($"🛡️ Escudo de base absorbió {danioAlEscudo} de daño.");
            }

            if (danio > 0)
            {
                vidaBaseActual -= danio;
                Notificar($"💥 ¡La base recibió {danio} de daño! Vida restante: {vidaBaseActual}", DestinoNotificacion.Ambos);
            }

            if (ModoJuego.EsMultijugador && ControladorAtacante.Instance != null)
            {
                ControladorAtacante.Instance.AlGolpearBase(danioOriginal, danio);
            }

            if (vidaBaseActual <= 0)
            {
                vidaBaseActual = 0;
                GameOver();
            }
            else
            {
                IntentarPulsoEMP();
            }

            enemigosRestantesEnOleada--;
            ComprobarFinDeOleada();
            NotificarCambio();
        }

        /// <summary>
        /// Habilidad DAG "Pulso de Emergencia EMP": si la base baja del 35% de vida,
        /// aturde a todos los enemigos durante 2.5 s. Se activa como máximo una vez por oleada.
        /// </summary>
        private void IntentarPulsoEMP()
        {
            if (empUsadoEstaOleada) return;
            if (vidaBaseActual >= vidaBaseMaxima * 0.35f) return;
            if (!TieneHabilidadDAG("HAB_PULSO_EMP")) return;

            empUsadoEstaOleada = true;
            List<Enemigo> enemigos = new List<Enemigo>(Enemigo.Activos);
            for (int i = 0; i < enemigos.Count; i++)
            {
                if (enemigos[i] != null) enemigos[i].Aturdir(2.5f);
            }

            EfectoPulso.Emitir(ObtenerPosicionBase(), 7f, TipoEfectoVisual.PulsoEMP);
            Notificar($"⚡ ¡PULSO EMP! La base aturdió a {enemigos.Count} enemigos durante 2.5 s.", DestinoNotificacion.Ambos);
        }

        private void ComprobarFinDeOleada()
        {
            if (estado == EstadoJuego.GameOver || estado == EstadoJuego.Victoria) return;

            if (ModoJuego.EsMultijugador)
            {
                // La ronda termina cuando se cerró la ventana de envío y no queda nada en el mapa ni en cola
                if (estado != EstadoJuego.OleadaEnCurso || ventanaEnvioAbierta) return;
                if (ControladorAtacante.Instance != null && ControladorAtacante.Instance.EnviosPendientes > 0) return;
                if (enemigosRestantesEnOleada > 0) return;

                enemigosRestantesEnOleada = 0;
                CompletarOleada();
                return;
            }

            if (enemigosRestantesEnOleada <= 0)
            {
                enemigosRestantesEnOleada = 0;
                CompletarOleada();
            }
        }

        /// <summary>Lo llama ControladorAtacante cuando termina de soltar su cola de envíos.</summary>
        public void RevisarFinDeRonda()
        {
            ComprobarFinDeOleada();
        }

        private void CompletarOleada()
        {
            estado = EstadoJuego.OleadaCompletada;

            // RECOMPENSA PROGRESIVA POR OLEADA:
            // Oro progresivo que escala cada oleada para permitir comprar más torretas
            int bonoOro = 50 + (oleadaActual * 25);
            if (TieneHabilidadDAG("HAB_GENERADOR"))
            {
                bonoOro = Mathf.RoundToInt(bonoOro * 1.25f); // +25% de oro por Habilidad DAG Generador
            }
            oroActual += bonoOro;

            // Sinergias defensivas tras cada oleada
            if (TieneHabilidadDAG("HAB_REPARACION_AUTO"))
            {
                vidaBaseActual = Mathf.Min(vidaBaseMaxima, vidaBaseActual + 25f);
            }
            if (TieneHabilidadDAG("HAB_ESCUDO_BASE"))
            {
                escudoBaseActual = 100f;
            }

            // Otorgar estrictamente +1 Punto de Habilidad para gastar en el Árbol DAG
            if (jugadorActivo != null)
            {
                jugadorActivo.AgregarPuntosHabilidad(1);
                string nombreRonda = ModoJuego.EsMultijugador ? "RONDA" : "OLEADA";
                MostrarNotificacion($"🏆 ¡{nombreRonda} {oleadaActual} SUPERADA!\n+1 Punto de Habilidad (DAG) 💎 | +{bonoOro} Oro 💰");
                if (ControladorHabilidadesUI.Instance != null)
                {
                    ControladorHabilidadesUI.Instance.ActualizarTodo();
                }
            }

            if (ModoJuego.EsMultijugador && ControladorAtacante.Instance != null)
            {
                ControladorAtacante.Instance.AlCompletarRonda(oleadaActual);
            }

            if (oleadaActual >= totalOleadas)
            {
                Victoria();
                return;
            }

            oleadaActual++;
            cuentaRegresivaOleada = ModoJuego.EsMultijugador ? pausaEntreRondas : tiempoEntreOleadas;
            NotificarCambio();
        }

        private void GameOver()
        {
            estado = EstadoJuego.GameOver;
            ventanaEnvioAbierta = false;

            if (ModoJuego.EsMultijugador)
            {
                Notificar($"💀 ¡La base cayó! Gana el atacante: {ModoJuego.NombreAtacante}.", DestinoNotificacion.Ambos);
                NotificarCambio();
                if (ServidorPartida.Instancia != null)
                {
                    ServidorPartida.Instancia.FinalizarPartida(RolJugador.Atacante);
                }
                return;
            }

            MostrarNotificacion("💀 ¡FIN DE LA PARTIDA! La base ha sido destruida.");
            NotificarCambio();
        }

        private void Victoria()
        {
            estado = EstadoJuego.Victoria;
            ventanaEnvioAbierta = false;

            if (ModoJuego.EsMultijugador)
            {
                Notificar($"👑 ¡La base resistió las {totalOleadas} rondas! Gana el defensor: {ModoJuego.NombreDefensor}.", DestinoNotificacion.Ambos);
                NotificarCambio();
                if (ServidorPartida.Instancia != null)
                {
                    ServidorPartida.Instancia.FinalizarPartida(RolJugador.Defensor);
                }
                return;
            }

            MostrarNotificacion("👑 ¡VICTORIA TOTAL! Has derrotado todas las oleadas enemigas.");
            NotificarCambio();
        }

        #endregion

        #region Integración con Habilidades DAG

        public bool TieneHabilidadDAG(string idHabilidad)
        {
            if (jugadorActivo != null)
            {
                return jugadorActivo.TieneHabilidad(idHabilidad);
            }
            return false;
        }

        public void ActualizarEscudoBase()
        {
            if (TieneHabilidadDAG("HAB_ESCUDO_BASE"))
            {
                escudoBaseActual = 100f;
            }
        }

        public void ActivarSobrecargaRed()
        {
            if (!TieneHabilidadDAG("HAB_SOBRECARGA"))
            {
                MostrarNotificacion("Bloqueado: Requiere desbloquear 'Sobrecarga de Red' en el Árbol DAG.");
                return;
            }

            if (sobrecargaActiva)
            {
                MostrarNotificacion("La Sobrecarga de Red ya se encuentra activa.");
                return;
            }

            if (ModoJuego.EsMultijugador && enfriamientoSobrecargaRestante > 0f)
            {
                MostrarNotificacion($"⏳ Sobrecarga de Red en recarga: {enfriamientoSobrecargaRestante:0} s.");
                return;
            }

            sobrecargaActiva = true;
            tiempoSobrecargaRestante = 10f;
            if (ModoJuego.EsMultijugador)
            {
                // En LAN tiene recarga para que no quede activa toda la partida
                enfriamientoSobrecargaRestante = tiempoSobrecargaRestante + EnfriamientoSobrecargaMultijugador;
            }
            Notificar("⚡ ¡SOBRECARGA DE RED ACTIVADA! +50% daño y doble cadencia en todas las torres por 10s.", DestinoNotificacion.Ambos);
            NotificarCambio();
        }

        public void GastarOro(int cantidad)
        {
            oroActual -= cantidad;
            NotificarCambio();
        }

        #endregion

        #region Utilidades y Notificaciones

        /// <summary>Notificación para el defensor (comportamiento original del juego).</summary>
        public void MostrarNotificacion(string mensaje)
        {
            Notificar(mensaje, DestinoNotificacion.Defensor);
        }

        /// <summary>
        /// Envía una notificación al jugador indicado. En un jugador solo se muestran las del defensor;
        /// en LAN el host muestra las suyas y reenvía al cliente las que le corresponden.
        /// </summary>
        public void Notificar(string mensaje, DestinoNotificacion destino)
        {
            if (!ModoJuego.EsMultijugador)
            {
                if (destino != DestinoNotificacion.Atacante) MostrarNotificacionLocal(mensaje);
                return;
            }

            if (ModoJuego.EsClienteRemoto)
            {
                // En el cliente solo se generan mensajes locales (errores de UI, avisos propios)
                MostrarNotificacionLocal(mensaje);
                return;
            }

            bool localEsDestino = destino == DestinoNotificacion.Ambos ||
                                  (destino == DestinoNotificacion.Defensor && ModoJuego.RolLocal == RolJugador.Defensor) ||
                                  (destino == DestinoNotificacion.Atacante && ModoJuego.RolLocal == RolJugador.Atacante);
            bool remotoEsDestino = destino == DestinoNotificacion.Ambos || !localEsDestino;

            if (localEsDestino) MostrarNotificacionLocal(mensaje);
            if (remotoEsDestino && ServidorPartida.Instancia != null) ServidorPartida.Instancia.EnviarNotificacion(mensaje);
        }

        /// <summary>Muestra el mensaje en el banner de este PC sin reenviarlo por red.</summary>
        public void MostrarNotificacionLocal(string mensaje)
        {
            Debug.Log($"[GameManager] {mensaje}");
            OnNotificacion?.Invoke(mensaje);
        }

        public void NotificarCambio()
        {
            OnEstadoCambiado?.Invoke();
        }

        private static Dictionary<TipoEnemigo, Sprite> spritesEnemigosCache = new Dictionary<TipoEnemigo, Sprite>();

        public static Sprite CrearSpriteEnemigo(TipoEnemigo tipo)
        {
            if (spritesEnemigosCache.ContainsKey(tipo) && spritesEnemigosCache[tipo] != null)
            {
                return spritesEnemigosCache[tipo];
            }

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 centro = new Vector2(size / 2f, size / 2f);
            float maxR = size / 2f - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float d = Vector2.Distance(pos, centro);
                    float normD = d / maxR;

                    Color c = Color.clear;

                    switch (tipo)
                    {
                        case TipoEnemigo.Corredor:
                            // Diamante / Flecha ágil dorada con núcleo brillante
                            float distManhattan = (Mathf.Abs(x - centro.x) + Mathf.Abs(y - centro.y)) / maxR;
                            if (distManhattan <= 1.0f)
                            {
                                if (distManhattan > 0.88f) c = new Color(0.18f, 0.12f, 0.02f, 1f); // Borde oscuro
                                else if (distManhattan > 0.68f) c = new Color(1f, 0.55f, 0.05f, 1f); // Anillo ámbar
                                else if (distManhattan > 0.35f) c = new Color(1f, 0.88f, 0.15f, 1f); // Cuerpo dorado
                                else c = new Color(1f, 1f, 0.80f, 1f); // Brillo central
                            }
                            break;

                        case TipoEnemigo.Infanteria:
                            // Esfera táctica militar con blindaje esmeralda y retícula central
                            if (normD <= 1.0f)
                            {
                                if (normD > 0.88f) c = new Color(0.08f, 0.22f, 0.12f, 1f); // Borde blindado oscuro
                                else if (normD > 0.70f) c = new Color(0.25f, 0.78f, 0.45f, 1f); // Verde militar
                                else if (normD > 0.55f) c = new Color(0.12f, 0.45f, 0.22f, 1f); // Anillo intermedio
                                else if (Mathf.Abs(x - centro.x) < 2 || Mathf.Abs(y - centro.y) < 2) c = new Color(0.05f, 0.18f, 0.08f, 1f); // Cruz táctica
                                else c = new Color(0.55f, 0.95f, 0.68f, 1f); // Núcleo claro
                            }
                            break;

                        case TipoEnemigo.Blindado:
                            // Placa pesada octagonal de titanio púrpura con reactor reforzado
                            float octD = Mathf.Max(Mathf.Abs(x - centro.x), Mathf.Abs(y - centro.y), (Mathf.Abs(x - centro.x) + Mathf.Abs(y - centro.y)) * 0.72f) / maxR;
                            if (octD <= 1.0f)
                            {
                                if (octD > 0.88f) c = new Color(0.12f, 0.08f, 0.24f, 1f); // Borde exterior titanio
                                else if (octD > 0.70f) c = new Color(0.58f, 0.32f, 0.85f, 1f); // Blindaje púrpura
                                else if (octD > 0.52f) c = new Color(0.28f, 0.14f, 0.45f, 1f); // Ranura de remaches
                                else if (octD > 0.25f) c = new Color(0.75f, 0.50f, 0.98f, 1f); // Placa central
                                else c = new Color(0.15f, 0.08f, 0.28f, 1f); // Reactor central
                            }
                            break;

                        case TipoEnemigo.Jefe:
                            // Titán carmesí con corona de picos y reactor de plasma ardiente
                            float ang = Mathf.Atan2(y - centro.y, x - centro.x);
                            float picos = Mathf.Sin(ang * 8f) * 0.12f;
                            float radioConPicos = maxR * (0.88f + picos);
                            if (d <= radioConPicos)
                            {
                                if (d > radioConPicos - 4f) c = new Color(0.25f, 0.02f, 0.02f, 1f); // Borde amenazante
                                else if (normD > 0.62f) c = new Color(0.92f, 0.18f, 0.18f, 1f); // Carmesí sangre
                                else if (normD > 0.42f) c = new Color(0.55f, 0.06f, 0.06f, 1f); // Blindaje interior
                                else if (normD > 0.18f) c = new Color(1f, 0.62f, 0.12f, 1f); // Plasma ardiente
                                else c = new Color(1f, 0.98f, 0.50f, 1f); // Núcleo blanco caliente
                            }
                            break;
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 48);
            spritesEnemigosCache[tipo] = sp;
            return sp;
        }

        private Sprite CrearSpriteCirculo()
        {
            return CrearSpriteEnemigo(TipoEnemigo.Infanteria);
        }

        #endregion
    }
}
