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

    /// <summary>
    /// Director central del juego (Tower Defense).
    /// Controla la aparición de oleadas, salud de la base, economía (oro),
    /// y la progresión estricta de experiencia y puntos de habilidad (DAG).
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
            MostrarNotificacion($"🎯 Modo Construcción: Torre {tipo} seleccionada (Costo: {costo} Oro). Haz clic en una casilla para colocarla.");
            NotificarCambio();
        }

        public event System.Action OnEstadoCambiado;
        public event System.Action<string> OnNotificacion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializarGameManager()
        {
            if (FindFirstObjectByType<GameManager>() == null)
            {
                GameObject go = new GameObject("GameManager_TowerDefense");
                go.AddComponent<GameManager>();
            }
        }

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

        private void Start()
        {
            ConfigurarCasillasTerreno();
            // Ya no se auto-colocan torres para dar 100% libertad al jugador
            ActualizarEscudoBase();

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

            // Gestión de Sobrecarga de Red (Habilidad Definitiva DAG)
            if (sobrecargaActiva)
            {
                tiempoSobrecargaRestante -= Time.deltaTime;
                if (tiempoSobrecargaRestante <= 0)
                {
                    sobrecargaActiva = false;
                    MostrarNotificacion("⚡ Sobrecarga de red finalizada.");
                }
            }
        }

        private void GestionarRaycastConstruccion()
        {
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

            // 2. Si el cursor está sobre la UI gráfica, no procesar terreno
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
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

            // 5. Ejecutar construcción al hacer clic
            if (clickPresionado && plotDetectado != null)
            {
                plotDetectado.ConstruirTorre();
            }
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

            if (plotsParent != null)
            {
                Transform[] hijos = plotsParent.GetComponentsInChildren<Transform>(true);
                int configurados = 0;

                for (int i = 0; i < hijos.Length; i++)
                {
                    if (hijos[i] != plotsParent.transform)
                    {
                        // Asegurar que todos los Collider2D sean trigger para evitar colisiones físicas
                        Collider2D[] cols = hijos[i].GetComponentsInChildren<Collider2D>(true);
                        if (cols == null || cols.Length == 0)
                        {
                            BoxCollider2D bc = hijos[i].gameObject.AddComponent<BoxCollider2D>();
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

                        // Asegurar PlotTorre
                        PlotTorre plot = hijos[i].GetComponent<PlotTorre>();
                        if (plot == null)
                        {
                            plot = hijos[i].gameObject.AddComponent<PlotTorre>();
                        }
                        configurados++;
                    }
                }

                Debug.Log($"[GameManager] Configuración de casillas completada. {configurados} casillas listas para construcción.");
            }
            else
            {
                Debug.LogWarning("[GameManager] No se encontró el objeto 'plots' en la escena.");
            }
        }

        private void ColocarTorresIniciales()
        {
            // Colocar 2 torres iniciales para que el jugador tenga defensas activas inmediatamente
            PlotTorre[] plots = FindObjectsByType<PlotTorre>(FindObjectsSortMode.None);
            int torresColocadas = 0;

            for (int i = 0; i < plots.Length; i++)
            {
                // Colocar cerca del primer tramo del camino
                if (!plots[i].TieneTorre && torresColocadas < 2)
                {
                    plots[i].ConstruirTorre(esGratis: true);
                    torresColocadas++;
                }
            }
        }

        #endregion

        #region Control de Oleadas

        public void IniciarOleada()
        {
            if (estado == EstadoJuego.GenerandoOleada || estado == EstadoJuego.OleadaEnCurso) return;
            if (estado == EstadoJuego.GameOver || estado == EstadoJuego.Victoria) return;

            cuentaRegresivaOleada = 0f;
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
                yield return new WaitForSeconds(1.2f - Mathf.Clamp(oleadaActual * 0.05f, 0f, 0.5f));
            }

            estado = EstadoJuego.OleadaEnCurso;
            NotificarCambio();
        }

        private void SpawmearEnemigo(Vector3 pos, TipoEnemigo tipo)
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
                MostrarNotificacion($"💥 ¡La base recibió {danio} de daño! Vida restante: {vidaBaseActual}");
            }

            if (vidaBaseActual <= 0)
            {
                vidaBaseActual = 0;
                GameOver();
            }

            enemigosRestantesEnOleada--;
            ComprobarFinDeOleada();
            NotificarCambio();
        }

        private void ComprobarFinDeOleada()
        {
            if (enemigosRestantesEnOleada <= 0 && estado != EstadoJuego.GameOver && estado != EstadoJuego.Victoria)
            {
                enemigosRestantesEnOleada = 0;
                CompletarOleada();
            }
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
                MostrarNotificacion($"🏆 ¡OLEADA {oleadaActual} COMPLETADA!\n+1 Punto de Habilidad (DAG) 💎 | +{bonoOro} Oro 💰");
                if (ControladorHabilidadesUI.Instance != null)
                {
                    ControladorHabilidadesUI.Instance.ActualizarTodo();
                }
            }

            if (oleadaActual >= totalOleadas)
            {
                Victoria();
                return;
            }

            oleadaActual++;
            cuentaRegresivaOleada = tiempoEntreOleadas;
            NotificarCambio();
        }

        private void GameOver()
        {
            estado = EstadoJuego.GameOver;
            MostrarNotificacion("💀 ¡FIN DE LA PARTIDA! La base ha sido destruida.");
            NotificarCambio();
        }

        private void Victoria()
        {
            estado = EstadoJuego.Victoria;
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

            sobrecargaActiva = true;
            tiempoSobrecargaRestante = 10f;
            MostrarNotificacion("⚡ ¡SOBRECARGA DE RED ACTIVADA! +50% cadencia y daño en todas las torres por 10s.");
        }

        public void GastarOro(int cantidad)
        {
            oroActual -= cantidad;
            NotificarCambio();
        }

        #endregion

        #region Utilidades y Notificaciones

        public void MostrarNotificacion(string mensaje)
        {
            Debug.Log($"[GameManager] {mensaje}");
            OnNotificacion?.Invoke(mensaje);
        }

        private void NotificarCambio()
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
