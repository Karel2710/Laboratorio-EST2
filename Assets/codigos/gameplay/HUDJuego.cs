using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GrafosEST2;
using TowerDefenseOnline;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Interfaz visual (HUD) del juego, generada 100% por código.
    /// Barra de estado, barra de menús, barra de construcción (6 torres), habilidades activas
    /// con recarga, panel flotante de inspección de torre, ayuda y pantalla de fin de partida.
    /// </summary>
    public class HUDJuego : MonoBehaviour
    {
        public static HUDJuego Instance { get; private set; }

        private Text txtBaseVida, txtOleadaInfo, txtOro, txtJugadorXP, txtPuntosDAG, txtPuntaje;
        private Button btnIniciarOleada, btnAuto, btnGrafo, btnDaltonico, btnSonido, btnLab, btnVista;
        private readonly Button[] btnVelocidad = new Button[4];
        private readonly Button[] btnNivel = new Button[3];
        private readonly Dictionary<TipoTorre, Button> btnTorres = new Dictionary<TipoTorre, Button>();
        private readonly Dictionary<TipoTorre, Outline> outTorres = new Dictionary<TipoTorre, Outline>();
        private readonly Button[] btnHabilidades = new Button[4];
        private GameObject panelLab;

        private GameObject panelBanner;
        private Text txtBanner;
        private float bannerRestante = 0f;

        private RectTransform canvasRT;
        private RectTransform panelTorre;
        private Text txtTorreTitulo, txtTorreStats;
        private Button btnMejorar, btnPrioridad, btnVender;
        private Torre torreInspeccionada;

        private GameObject panelGrafo;
        private Text txtGrafo;
        private GameObject panelAyuda;
        private GameObject panelFin;
        private Text txtFinTitulo, txtFinDetalle;
        private Button btnSiguienteNivel;

        public static HUDJuego Asegurar()
        {
            if (Instance != null) return Instance;
            HUDJuego existente = FindFirstObjectByType<HUDJuego>();
            if (existente != null) return existente;
            GameObject go = new GameObject("HUDJuego_CanvasHost");
            return go.AddComponent<HUDJuego>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ConstruirHUD();
        }

        private void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnEstadoCambiado += ActualizarHUD;
                gm.OnNotificacion += MostrarBannerNotificacion;
                gm.OnTorreSeleccionada += MostrarInspeccionTorre;
                gm.OnFinPartida += MostrarFinPartida;
            }
            Paleta.OnCambio += ActualizarHUD;
            ActualizarHUD();
            MostrarBannerNotificacion("🎮 ¡Bienvenido Comandante! Elige una torre abajo, colócala en una casilla y pulsa ▶ Iniciar Oleada. (❓ Ayuda para ver las instrucciones)");
        }

        private void OnDestroy()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.OnEstadoCambiado -= ActualizarHUD;
                gm.OnNotificacion -= MostrarBannerNotificacion;
                gm.OnTorreSeleccionada -= MostrarInspeccionTorre;
                gm.OnFinPartida -= MostrarFinPartida;
            }
            Paleta.OnCambio -= ActualizarHUD;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            // Banner con temporizador en tiempo real (funciona también en pausa)
            if (bannerRestante > 0f)
            {
                bannerRestante -= Time.unscaledDeltaTime;
                if (bannerRestante <= 0f && panelBanner != null) panelBanner.SetActive(false);
            }

            ActualizarTextoOleada(gm);
            ActualizarHabilidades(gm);
            if (torreInspeccionada != null) PosicionarPanelTorre();
            if (panelGrafo != null && panelGrafo.activeSelf) ActualizarPanelGrafo();
        }

        #region Actualización

        public void ActualizarHUD()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || txtBaseVida == null) return;

            string escudo = gm.escudoBaseActual > 0 ? $" <color=#44EEFF>(+{gm.escudoBaseActual:0} 🛡)</color>" : "";
            txtBaseVida.color = gm.vidaBaseActual > 40 ? Color.white : Paleta.Negativo;
            txtBaseVida.text = $"🏰 Base: <b>{gm.vidaBaseActual:0}/{gm.vidaBaseMaxima:0}</b>{escudo}";

            txtOro.text = $"💰 Oro: <color=#FFDD44><b>{gm.oroActual}</b></color>";
            txtPuntaje.text = $"🎯 Puntos: <color=#FFB347><b>{gm.puntaje}</b></color>";

            Jugador j = gm.jugadorActivo;
            if (j != null)
            {
                string apoyo = gm.jugadorApoyo != null ? $" +{gm.jugadorApoyo.nombre}" : "";
                txtJugadorXP.text = $"⭐ {j.nombre}{apoyo} · Nvl {j.nivel} <color=#66DDFF>({j.experiencia}/{j.experienciaSiguienteNivel})</color>";
                string aviso = j.puntosHabilidad > 0 ? " <color=#FFDD44><b>¡Úsalos!</b></color>" : "";
                txtPuntosDAG.text = $"💎 DAG: <color={Paleta.Hex(Paleta.Positivo)}><b>{j.puntosHabilidad}</b></color>{aviso}";
            }

            ActualizarTextoOleada(gm);

            UIFabrica.Rotular(btnAuto, gm.autoOleada ? "🔁 Auto: SÍ" : "🔁 Auto: NO");
            UIFabrica.Colorear(btnAuto, gm.autoOleada ? new Color(0.15f, 0.5f, 0.4f) : new Color(0.22f, 0.26f, 0.34f));
            for (int i = 0; i < btnVelocidad.Length; i++)
                UIFabrica.Colorear(btnVelocidad[i], gm.velocidadJuego == i ? new Color(0.85f, 0.55f, 0.1f) : new Color(0.2f, 0.25f, 0.33f));
            for (int i = 0; i < btnNivel.Length; i++)
            {
                Color baseCol = i == 0 ? new Color(0.15f, 0.52f, 0.25f) : (i == 1 ? new Color(0.78f, 0.48f, 0.08f) : new Color(0.78f, 0.18f, 0.18f));
                UIFabrica.Colorear(btnNivel[i], gm.nivelActual == i ? baseCol : baseCol * 0.5f + new Color(0, 0, 0, 0.5f));
            }

            foreach (TipoTorre tipo in Torre.TodosLosTipos)
            {
                if (!btnTorres.ContainsKey(tipo)) continue;
                int costo = gm.ObtenerCostoTorre(tipo);
                bool puede = gm.oroActual >= costo;
                bool sel = gm.tipoTorreAConstruir == tipo;
                Color c = Color.Lerp(new Color(0.08f, 0.1f, 0.15f), Torre.ColorTipo(tipo), sel ? 0.6f : 0.32f);
                UIFabrica.Colorear(btnTorres[tipo], puede ? c : c * 0.45f + new Color(0, 0, 0, 0.55f));
                UIFabrica.Rotular(btnTorres[tipo], $"{Torre.Icono(tipo)} {Torre.Nombre(tipo)}\n<color=#FFDD44>{costo} Oro</color>");
                outTorres[tipo].effectColor = sel ? Paleta.Dorado : new Color(0.3f, 0.5f, 0.7f, 0.35f);
                outTorres[tipo].effectDistance = sel ? new Vector2(3, -3) : new Vector2(1, -1);
            }

            UIFabrica.Rotular(btnVista, Vista25D.Activa ? "🧊 2.5D: SÍ" : "🧊 2.5D: NO");
            UIFabrica.Colorear(btnVista, Vista25D.Activa ? new Color(0.2f, 0.5f, 0.45f) : new Color(0.22f, 0.26f, 0.34f));
            UIFabrica.Colorear(btnDaltonico, Paleta.Daltonico ? new Color(0.15f, 0.45f, 0.75f) : new Color(0.22f, 0.26f, 0.34f));
            UIFabrica.Colorear(btnLab, gm.modoLaboratorio ? new Color(0.6f, 0.3f, 0.7f) : new Color(0.22f, 0.26f, 0.34f));
            if (panelLab != null) panelLab.SetActive(gm.modoLaboratorio);

            ActualizarHabilidades(gm);
            if (torreInspeccionada != null) RefrescarPanelTorre();
        }

        private void ActualizarTextoOleada(GameManager gm)
        {
            if (txtOleadaInfo == null) return;
            bool puedeIniciar = false;

            switch (gm.estado)
            {
                case EstadoJuego.GenerandoOleada:
                case EstadoJuego.OleadaEnCurso:
                    txtOleadaInfo.text = $"⚔️ Oleada {gm.oleadaActual}/{gm.totalOleadas} · Enemigos: <color={Paleta.Hex(Paleta.Negativo)}><b>{gm.enemigosRestantesEnOleada}</b></color>";
                    break;
                case EstadoJuego.GameOver:
                    txtOleadaInfo.text = $"<color={Paleta.Hex(Paleta.Negativo)}>💀 DERROTA · Base destruida</color>";
                    break;
                case EstadoJuego.Victoria:
                    txtOleadaInfo.text = "<color=#FFDD44>👑 ¡VICTORIA TOTAL!</color>";
                    break;
                default:
                    puedeIniciar = !gm.oleadasControladasPorAnfitrion;
                    txtOleadaInfo.text = gm.cuentaRegresivaOleada > 0f
                        ? $"⏳ Oleada {gm.oleadaActual}/{gm.totalOleadas} en <color=#FFDD44>{gm.cuentaRegresivaOleada:0.0}s</color>"
                        : $"🛠️ Preparación · Oleada {gm.oleadaActual}/{gm.totalOleadas}";
                    break;
            }
            if (btnIniciarOleada != null && btnIniciarOleada.gameObject.activeSelf != puedeIniciar)
                btnIniciarOleada.gameObject.SetActive(puedeIniciar);
        }

        private void ActualizarHabilidades(GameManager gm)
        {
            for (int i = 0; i < btnHabilidades.Length; i++)
            {
                Button b = btnHabilidades[i];
                if (b == null) continue;
                HabilidadActiva h = (HabilidadActiva)i;
                bool visible = gm.HabilidadDesbloqueada(h);
                if (b.gameObject.activeSelf != visible) b.gameObject.SetActive(visible);
                if (!visible) continue;

                float cd = gm.CooldownRestante(h);
                bool activa = (h == HabilidadActiva.Sobrecarga && gm.sobrecargaActiva) || (h == HabilidadActiva.Barricada && gm.apuntandoBarricada);
                string estado = activa ? "<color=#FFDD44>ACTIVA</color>" : (cd > 0f ? $"<color=#FF9966>⏳ {cd:0}s</color>" : $"<color={Paleta.Hex(Paleta.Positivo)}>LISTA</color>");
                UIFabrica.Rotular(b, $"{GameManager.NombreHabilidad(h)}\n{estado}");
                b.interactable = cd <= 0f || activa;
            }
        }

        public void MostrarBannerNotificacion(string mensaje)
        {
            if (txtBanner == null || panelBanner == null) return;
            txtBanner.text = mensaje;
            panelBanner.SetActive(true);
            bannerRestante = Mathf.Clamp(2.5f + mensaje.Length * 0.03f, 3f, 7f);
        }

        #endregion

        #region Construcción del HUD

        private Text TextoBarra(Transform padre, string nombre, float ancho)
        {
            Text t = UIFabrica.CrearTexto(padre, nombre, "", 14, TextAnchor.MiddleLeft, Color.white, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.gameObject.AddComponent<LayoutElement>().preferredWidth = ancho;
            return t;
        }

        private Button BotonBarra(Transform padre, string nombre, string texto, Color color, float ancho, UnityEngine.Events.UnityAction accion)
        {
            Button b = UIFabrica.CrearBoton(padre, nombre, texto, color, 13);
            LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = ancho;
            b.onClick.AddListener(accion);
            b.onClick.AddListener(() => { if (EfectosVisualesManager.Instance != null) EfectosVisualesManager.Instance.Sonido(TipoSonido.Click); });
            return b;
        }

        private RectTransform Barra(Transform padre, string nombre, float y, float alto, Color color)
        {
            RectTransform rt = UIFabrica.CrearPanel(padre, nombre, color);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(0, alto);
            HorizontalLayoutGroup hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 14, 5, 5);
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            return rt;
        }

        private void ConstruirHUD()
        {
            GameObject canvasObj = UIFabrica.CrearCanvas("Canvas_HUD_TowerDefense", 90);
            canvasObj.transform.SetParent(transform, false);
            canvasRT = canvasObj.GetComponent<RectTransform>();
            Transform raiz = canvasObj.transform;

            // ── 1. Barra de estado ────────────────────────────────────────────
            RectTransform barraEstado = Barra(raiz, "BarraEstado", 0, 46, new Color(0.06f, 0.09f, 0.14f, 0.96f));
            txtBaseVida = TextoBarra(barraEstado, "TxtBase", 215);
            txtOleadaInfo = TextoBarra(barraEstado, "TxtOleada", 300);
            txtOro = TextoBarra(barraEstado, "TxtOro", 125);
            txtPuntaje = TextoBarra(barraEstado, "TxtPuntaje", 150);
            txtJugadorXP = TextoBarra(barraEstado, "TxtJugador", 300);
            txtPuntosDAG = TextoBarra(barraEstado, "TxtDAG", 160);

            RectTransform espacio = UIFabrica.CrearContenedor(barraEstado, "Espacio");
            espacio.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            btnIniciarOleada = BotonBarra(barraEstado, "BtnIniciarOleada", "▶ Iniciar Oleada", new Color(0.18f, 0.62f, 0.33f), 150,
                () => { if (GameManager.Instance != null) GameManager.Instance.IniciarOleada(); });
            btnAuto = BotonBarra(barraEstado, "BtnAuto", "🔁 Auto: NO", new Color(0.22f, 0.26f, 0.34f), 110,
                () => { if (GameManager.Instance != null) GameManager.Instance.AlternarAutoOleada(); });

            string[] etiquetasVel = { "⏸", "▶ 1x", "⏩ 2x", "⏩ 3x" };
            for (int i = 0; i < 4; i++)
            {
                int v = i;
                btnVelocidad[i] = BotonBarra(barraEstado, "BtnVel" + i, etiquetasVel[i], new Color(0.2f, 0.25f, 0.33f), i == 0 ? 40 : 62,
                    () => { if (GameManager.Instance != null) GameManager.Instance.EstablecerVelocidad(v); });
            }

            // ── 2. Barra de menús ─────────────────────────────────────────────
            RectTransform barraMenu = Barra(raiz, "BarraMenu", -46, 40, new Color(0.05f, 0.07f, 0.11f, 0.94f));
            BotonBarra(barraMenu, "BtnHabilidadesDAG", "⚡ Armería / Mejoras", new Color(0.32f, 0.22f, 0.75f), 160,
                () => { if (ControladorHabilidadesUI.Instance != null) ControladorHabilidadesUI.Instance.AlternarVisibilidadPanel(); });
            BotonBarra(barraMenu, "BtnComandantesBST", "👥 Comandantes", new Color(0.12f, 0.45f, 0.65f), 135,
                () => { if (ControladorArbolUI.Instance != null) ControladorArbolUI.Instance.AlternarVisibilidadPanel(); });
            btnGrafo = BotonBarra(barraMenu, "BtnVerGrafo", "🗺️ Ver Grafo Dijkstra", new Color(0.1f, 0.5f, 0.42f), 180, AlternarGrafo);
            BotonBarra(barraMenu, "BtnLabGrafos", "🔬 Laboratorio Grafos", new Color(0.45f, 0.3f, 0.6f), 175,
                () => SimuladorGrafosUI.Asegurar().AlternarPanel());
            BotonBarra(barraMenu, "BtnRanking", "🏆 Ranking", new Color(0.62f, 0.46f, 0.08f), 110,
                () => LeaderboardJSON.Asegurar().AlternarPanel());
            BotonBarra(barraMenu, "BtnOnline", "🌐 Online / Multijugador", new Color(0.1f, 0.42f, 0.72f), 195,
                () => ControladorOnlineUI.Asegurar().AlternarPanel());

            for (int i = 0; i < 3; i++)
            {
                int n = i;
                btnNivel[i] = BotonBarra(barraMenu, "BtnNivel" + (i + 1), "Nivel " + (i + 1), new Color(0.3f, 0.3f, 0.3f), 78,
                    () => { if (GameManager.Instance != null) GameManager.Instance.CargarNivel(n); });
            }

            RectTransform espacio2 = UIFabrica.CrearContenedor(barraMenu, "Espacio");
            espacio2.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            btnVista = BotonBarra(barraMenu, "BtnVista25D", "🧊 2.5D", new Color(0.22f, 0.26f, 0.34f), 96,
                () => Vista25D.Asegurar().Alternar());
            BotonBarra(barraMenu, "BtnAyuda", "❓ Ayuda", new Color(0.2f, 0.4f, 0.55f), 90, () => panelAyuda.SetActive(!panelAyuda.activeSelf));
            btnDaltonico = BotonBarra(barraMenu, "BtnDaltonico", "👁 Daltonismo", new Color(0.22f, 0.26f, 0.34f), 120, () =>
            {
                Paleta.AlternarDaltonico();
                MostrarBannerNotificacion(Paleta.Daltonico
                    ? "👁 Modo daltonismo ACTIVADO: paleta azul/naranja en barras de vida, casillas y avisos."
                    : "👁 Modo daltonismo desactivado.");
            });
            btnSonido = BotonBarra(barraMenu, "BtnSonido", "🔊", new Color(0.22f, 0.26f, 0.34f), 44, () =>
            {
                EfectosVisualesManager fx = EfectosVisualesManager.Instance;
                if (fx == null) return;
                fx.sonidoActivo = !fx.sonidoActivo;
                UIFabrica.Rotular(btnSonido, fx.sonidoActivo ? "🔊" : "🔇");
            });
            btnLab = BotonBarra(barraMenu, "BtnLab", "🧪", new Color(0.22f, 0.26f, 0.34f), 44,
                () => { if (GameManager.Instance != null) GameManager.Instance.AlternarModoLaboratorio(); });

            // ── 3. Barra inferior de construcción (6 torres) ──────────────────
            RectTransform barraConst = UIFabrica.CrearPanel(raiz, "BarraConstruccion", new Color(0.05f, 0.07f, 0.12f, 0.96f), new Color(0.3f, 0.6f, 0.85f, 0.6f));
            UIFabrica.Anclar(barraConst, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(1040, 72));
            HorizontalLayoutGroup hlgC = barraConst.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlgC.padding = new RectOffset(10, 10, 8, 8);
            hlgC.spacing = 10;
            hlgC.childControlWidth = true;
            hlgC.childControlHeight = true;
            hlgC.childForceExpandWidth = true;
            hlgC.childForceExpandHeight = true;

            foreach (TipoTorre tipo in Torre.TodosLosTipos)
            {
                TipoTorre t = tipo;
                Button b = UIFabrica.CrearBoton(barraConst, "BtnTorre" + tipo, "", Torre.ColorTipo(tipo), 13);
                Outline o = b.gameObject.AddComponent<Outline>();
                b.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.SeleccionarTipoTorre(t); });
                btnTorres[tipo] = b;
                outTorres[tipo] = o;
            }

            // ── 4. Habilidades activas (derecha) ──────────────────────────────
            RectTransform colHab = UIFabrica.CrearContenedor(raiz, "ColumnaHabilidades");
            UIFabrica.Anclar(colHab, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12, 40), new Vector2(200, 330));
            VerticalLayoutGroup vlgH = colHab.gameObject.AddComponent<VerticalLayoutGroup>();
            vlgH.spacing = 8;
            vlgH.childControlWidth = true;
            vlgH.childControlHeight = false;
            vlgH.childForceExpandHeight = false;
            vlgH.childAlignment = TextAnchor.UpperCenter;

            Color[] colHabs = { new Color(0.8f, 0.5f, 0.1f), new Color(0.55f, 0.42f, 0.15f), new Color(0.7f, 0.22f, 0.15f), new Color(0.1f, 0.5f, 0.7f) };
            for (int i = 0; i < 4; i++)
            {
                HabilidadActiva h = (HabilidadActiva)i;
                Button b = UIFabrica.CrearBoton(colHab, "BtnHab" + h, GameManager.NombreHabilidad(h), colHabs[i], 13);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 54);
                Outline o = b.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(1f, 1f, 1f, 0.25f);
                b.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.UsarHabilidad(h); });
                b.gameObject.SetActive(false);
                btnHabilidades[i] = b;
            }

            // Panel del Modo Laboratorio / Profesor (oculto por defecto)
            RectTransform lab = UIFabrica.CrearPanel(colHab, "PanelLaboratorio", new Color(0.15f, 0.08f, 0.2f, 0.92f), new Color(0.7f, 0.4f, 0.9f, 0.6f));
            lab.sizeDelta = new Vector2(200, 78);
            lab.gameObject.AddComponent<LayoutElement>().preferredHeight = 78;
            Text tLab = UIFabrica.CrearTexto(lab, "T", "🧪 MODO LABORATORIO", 11, TextAnchor.UpperCenter, new Color(0.85f, 0.7f, 1f), true);
            UIFabrica.Estirar(tLab, 0, 0, 0, 4);
            Button l1 = UIFabrica.CrearBoton(lab, "Oro", "+500 Oro", new Color(0.5f, 0.4f, 0.1f), 11);
            UIFabrica.Colocar(l1, 6, -22, 60, 48);
            l1.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.LabDarOro(); });
            Button l2 = UIFabrica.CrearBoton(lab, "Pts", "+5 Pts\nDAG", new Color(0.15f, 0.45f, 0.3f), 11);
            UIFabrica.Colocar(l2, 70, -22, 60, 48);
            l2.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.LabDarPuntos(); });
            Button l3 = UIFabrica.CrearBoton(lab, "Kill", "Limpiar\noleada", new Color(0.5f, 0.2f, 0.2f), 11);
            UIFabrica.Colocar(l3, 134, -22, 60, 48);
            l3.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.LabEliminarEnemigos(); });
            panelLab = lab.gameObject;
            panelLab.SetActive(false);

            // ── 5. Banner de notificaciones ───────────────────────────────────
            RectTransform banner = UIFabrica.CrearPanel(raiz, "PanelBanner", new Color(0.05f, 0.07f, 0.11f, 0.93f), new Color(0.4f, 0.8f, 1f, 0.7f));
            UIFabrica.Anclar(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(980, 56));
            banner.GetComponent<Image>().raycastTarget = false;
            txtBanner = UIFabrica.CrearTexto(banner, "Texto", "", 15, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.5f), true);
            UIFabrica.Estirar(txtBanner, 12, 4, 12, 4);
            panelBanner = banner.gameObject;
            panelBanner.SetActive(false);

            ConstruirPanelTorre(raiz);
            ConstruirPanelGrafo(raiz);
            ConstruirPanelAyuda(raiz);
            ConstruirPanelFin(raiz);
        }

        #endregion

        #region Panel Flotante de Inspección de Torre

        private void ConstruirPanelTorre(Transform raiz)
        {
            panelTorre = UIFabrica.CrearPanel(raiz, "PanelInspeccionTorre", new Color(0.06f, 0.08f, 0.13f, 0.97f), new Color(1f, 0.82f, 0.25f, 0.7f));
            panelTorre.anchorMin = Vector2.zero;
            panelTorre.anchorMax = Vector2.zero;
            panelTorre.pivot = new Vector2(0f, 0.5f);
            panelTorre.sizeDelta = new Vector2(310, 268);

            txtTorreTitulo = UIFabrica.CrearTexto(panelTorre, "Titulo", "", 15, TextAnchor.MiddleLeft, Paleta.Dorado, true);
            UIFabrica.Colocar(txtTorreTitulo, 12, -6, 250, 28);

            Button cerrar = UIFabrica.CrearBoton(panelTorre, "Cerrar", "✕", new Color(0.6f, 0.18f, 0.18f), 12);
            UIFabrica.Colocar(cerrar, 276, -6, 26, 24);
            cerrar.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.SeleccionarTorre(null); });

            txtTorreStats = UIFabrica.CrearTexto(panelTorre, "Stats", "", 13, TextAnchor.UpperLeft, new Color(0.88f, 0.92f, 0.98f));
            UIFabrica.Colocar(txtTorreStats, 12, -38, 286, 104);

            btnMejorar = UIFabrica.CrearBoton(panelTorre, "BtnMejorar", "⬆️ Mejorar", new Color(0.15f, 0.55f, 0.3f), 13);
            UIFabrica.Colocar(btnMejorar, 12, -148, 286, 34);
            btnMejorar.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.MejorarTorre(torreInspeccionada); });

            btnPrioridad = UIFabrica.CrearBoton(panelTorre, "BtnPrioridad", "🎯 Prioridad", new Color(0.15f, 0.4f, 0.62f), 13);
            UIFabrica.Colocar(btnPrioridad, 12, -187, 286, 34);
            btnPrioridad.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.CambiarPrioridad(torreInspeccionada); });

            btnVender = UIFabrica.CrearBoton(panelTorre, "BtnVender", "💰 Vender", new Color(0.62f, 0.25f, 0.15f), 13);
            UIFabrica.Colocar(btnVender, 12, -226, 286, 34);
            btnVender.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.VenderTorre(torreInspeccionada); });

            panelTorre.gameObject.SetActive(false);
        }

        private void MostrarInspeccionTorre(Torre t)
        {
            torreInspeccionada = t;
            if (panelTorre == null) return;
            panelTorre.gameObject.SetActive(t != null);
            if (t != null)
            {
                RefrescarPanelTorre();
                PosicionarPanelTorre();
            }
        }

        private void RefrescarPanelTorre()
        {
            Torre t = torreInspeccionada;
            GameManager gm = GameManager.Instance;
            if (t == null || gm == null) return;

            bool propia = gm.EsTorrePropia(t);
            string estrellas = new string('★', t.nivel) + new string('☆', Torre.NIVEL_MAXIMO - t.nivel);
            txtTorreTitulo.text = $"{Torre.Icono(t.tipo)} {Torre.Nombre(t.tipo)}  <color=#FFFFFF>{estrellas}</color>";

            string red = t.gradoMST > 0
                ? $"<color=#44EEFF>Red MST: {t.gradoMST} enlace(s) · +{t.bonoDanioRed * 100f:0}% daño · +{t.bonoCadenciaRed * 100f:0}% cadencia{(t.enRedConRadar ? " · 📡" : "")}</color>"
                : "<color=#94a3b8>Sin enlace MST (acerca otra torre a ≤ 5.5 u)</color>";
            string dueno = propia ? "" : $"\n<color=#FFB347>Propietario: {t.propietarioNombre}</color>";
            txtTorreStats.text = $"{t.ResumenEstadisticas()}\n{red}{dueno}";

            bool max = t.nivel >= Torre.NIVEL_MAXIMO;
            int costo = t.CostoMejora;
            UIFabrica.Rotular(btnMejorar, max ? "⭐ Nivel máximo alcanzado" : $"⬆️ Mejorar a Nivel {t.nivel + 1}  ({costo} Oro)");
            btnMejorar.interactable = propia && !max && gm.oroActual >= costo;
            UIFabrica.Rotular(btnPrioridad, t.EsRadar ? "🎯 Sin ataque (soporte de red)" : $"🎯 Prioridad: {Torre.NombrePrioridad(t.prioridad)}");
            btnPrioridad.interactable = !t.EsRadar;
            UIFabrica.Rotular(btnVender, $"💰 Vender  (+{t.ValorVenta} Oro)");
            btnVender.interactable = propia;
        }

        private void PosicionarPanelTorre()
        {
            Camera cam = Camera.main;
            if (cam == null || torreInspeccionada == null || canvasRT == null) return;

            Vector3 vp = cam.WorldToViewportPoint(torreInspeccionada.transform.position);
            Vector2 tam = canvasRT.rect.size;
            Vector2 pos = new Vector2(vp.x * tam.x, vp.y * tam.y);

            // A la derecha de la torre; si no cabe, a la izquierda. Se mantiene dentro de la pantalla.
            bool derecha = pos.x + 60 + panelTorre.sizeDelta.x < tam.x - 220;
            panelTorre.pivot = new Vector2(derecha ? 0f : 1f, 0.5f);
            float x = pos.x + (derecha ? 60 : -60);
            float mitad = panelTorre.sizeDelta.y * 0.5f;
            float y = Mathf.Clamp(pos.y, 96 + mitad, tam.y - 100 - mitad);
            panelTorre.anchoredPosition = new Vector2(x, y);
        }

        #endregion

        #region Panel de Información del Grafo (Dijkstra + MST)

        private void ConstruirPanelGrafo(Transform raiz)
        {
            RectTransform rt = UIFabrica.CrearPanel(raiz, "PanelInfoGrafo", new Color(0.04f, 0.07f, 0.1f, 0.92f), new Color(0.2f, 0.9f, 0.6f, 0.6f));
            UIFabrica.Anclar(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12, 96), new Vector2(430, 200));
            rt.GetComponent<Image>().raycastTarget = false;
            txtGrafo = UIFabrica.CrearTexto(rt, "Texto", "", 12, TextAnchor.UpperLeft, new Color(0.85f, 0.95f, 0.9f));
            UIFabrica.Estirar(txtGrafo, 10, 8, 10, 8);
            panelGrafo = rt.gameObject;
            panelGrafo.SetActive(false);
        }

        private void AlternarGrafo()
        {
            VisualizadorGrafoRutas vis = VisualizadorGrafoRutas.Asegurar();
            vis.Alternar();
            panelGrafo.SetActive(vis.Visible);
            UIFabrica.Colorear(btnGrafo, vis.Visible ? new Color(0.1f, 0.75f, 0.55f) : new Color(0.1f, 0.5f, 0.42f));
            if (vis.Visible) MostrarBannerNotificacion("🗺️ Grafo de caminos visible: nodos, pesos por arista y ruta óptima de Dijkstra resaltada.");
        }

        private void ActualizarPanelGrafo()
        {
            PathGraph g = PathGraph.main;
            if (g == null || txtGrafo == null) return;
            RedTorresMST red = RedTorresMST.Instance;

            string bloqueos = "";
            for (int i = 0; i < g.bloqueos.Count; i++)
            {
                PathGraph.Bloqueo b = g.bloqueos[i];
                if (b.a == null || b.b == null) continue;
                bloqueos += $"\n<color=#FF9966>🚧 N{b.a.nodeId}–N{b.b.nodeId} bloqueada" + (b.tiempoRestante >= 0 ? $" ({b.tiempoRestante:0}s)" : "") + "</color>";
            }

            txtGrafo.text =
                $"<color=#66FFBB><b>GRAFO DE CAMINOS · DIJKSTRA</b></color>\n" +
                $"Vértices: <b>{g.nodos.Count}</b> · Aristas: <b>{g.ObtenerAristas().Count}</b> · Recálculos: <b>{g.RecalculosDijkstra}</b>\n" +
                $"<color=#FFE066>{g.DescribirRuta()}</color>{bloqueos}\n" +
                "Peso = distancia × terreno:  <color=#7FD4FF>━ Camino ×1.0</color>  <color=#FFC266>━ Sendero ×1.8</color>  <color=#B8E07A>━ Pantano ×3.0</color>\n" +
                (red != null
                    ? $"\n<color=#44EEFF><b>RED DE TORRES · KRUSKAL (MST)</b></color>\n" +
                      $"Torres: <b>{Torre.Activas.Count}</b> · Candidatas: <b>{red.AristasCandidatas}</b> · En el MST: <b>{red.aristasMST.Count}</b> · Descartadas (ciclo): <b>{red.AristasDescartadas}</b>\n" +
                      $"Peso total: <b>{red.PesoTotal:0.0}</b> · Componentes: <b>{red.Componentes}</b>"
                    : "");
        }

        #endregion

        #region Ayuda y Fin de Partida

        private void ConstruirPanelAyuda(Transform raiz)
        {
            RectTransform rt = UIFabrica.CrearPanel(raiz, "PanelAyuda", Paleta.FondoPanel, new Color(0.3f, 0.7f, 1f, 0.7f));
            UIFabrica.Anclar(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640));
            panelAyuda = rt.gameObject;
            UIFabrica.CrearEncabezado(rt, "❓ AYUDA — CÓMO JUGAR", Paleta.Cian, () => panelAyuda.SetActive(false));

            UIFabrica.CrearScroll(rt, "Scroll", Paleta.FondoOscuro, out RectTransform contenido);
            RectTransform sc = contenido.parent.parent.GetComponent<RectTransform>();
            UIFabrica.Colocar(sc, 16, -56, 868, 568);

            Text t = UIFabrica.CrearTexto(contenido, "Texto", "", 14, TextAnchor.UpperLeft, new Color(0.9f, 0.93f, 0.98f));
            t.text =
                "<color=#66DDFF><b>OBJETIVO</b></color>\n" +
                "Defiende la Base (reactor hexagonal al final del camino) de las oleadas enemigas. Si la vida de la base llega a 0, pierdes.\n\n" +
                "<color=#66DDFF><b>CONSTRUIR Y GESTIONAR TORRES</b></color>\n" +
                "• Elige una torre en la barra inferior y haz clic en una casilla libre. El círculo muestra su alcance exacto.\n" +
                "• Haz clic sobre una torre ya construida para abrir su panel: <b>Mejorar</b> (Nivel 1→3), cambiar <b>Prioridad</b> de blanco o <b>Vender</b> (70%).\n" +
                "• 🔨 Centinela: equilibrada · ❄️ Criogénica: ralentiza · 💥 Cañón: daño en área · ⚡ Láser: rayo continuo que se intensifica\n" +
                "• 🌩️ Tesla: arcos en cadena · 📡 Radar: amplía la red y revela enemigos furtivos.\n\n" +
                "<color=#66DDFF><b>RED DE ENERGÍA (Kruskal / MST)</b></color>\n" +
                "Las torres a 5.5 unidades o menos se enlazan con rayos de energía. Cada enlace da +15% de daño y +5% de cadencia (tope 35%). " +
                "Colócalas cerca para formar la red; al vender o construir, el árbol de expansión mínima se recalcula.\n\n" +
                "<color=#66DDFF><b>CAMINOS (Dijkstra)</b></color>\n" +
                "Los enemigos siguen siempre la ruta de menor costo. Pulsa <b>🗺️ Ver Grafo Dijkstra</b> para ver nodos, pesos y la ruta óptima. " +
                "Con la habilidad <b>🚧 Barricada</b> bloqueas un tramo 15 s y los enemigos se desvían por la ruta alternativa (más lenta).\n\n" +
                "<color=#66DDFF><b>PROGRESIÓN</b></color>\n" +
                "• Cada enemigo da oro y experiencia; cada oleada superada da +1 Punto de Habilidad.\n" +
                "• Gasta los puntos en <b>⚡ Armería</b> (grafo dirigido acíclico): cada mejora exige sus prerrequisitos.\n" +
                "• Las 4 habilidades activas aparecen a la derecha cuando las desbloqueas.\n" +
                "• En <b>👥 Comandantes</b> registras perfiles (árbol BST/AVL), eliges clase y asignas un comandante principal y uno de apoyo.\n\n" +
                "<color=#66DDFF><b>ENEMIGOS</b></color>\n" +
                "◆ Corredor: rápido y frágil · ● Infantería: estándar · ⬢ Blindado: resiste balas, débil a explosiones\n" +
                "✚ Sanador: cura a sus aliados (elimínalo primero) · ▲ Furtivo: casi invisible sin Radar · ■ Blindado Pesado: tiene escudo (láser, Tesla o EMP)\n" +
                "✹ Jefe: aparece cada 4 oleadas.\n\n" +
                "<color=#66DDFF><b>CONTROLES</b></color>\n" +
                "Clic izquierdo: construir / seleccionar · Clic derecho o Esc: cancelar · ⏸ ▶ ⏩: pausa y velocidad · 🔁 Auto: oleadas automáticas\n" +
                "Nivel 1/2/3: reinicia la partida con otra dificultad y otra topología de caminos.\n\n" +
                "<color=#66DDFF><b>VISTA 2.5D</b></color>\n" +
                "🧊 2.5D alterna entre la vista inclinada con modelos 3D y la vista 2D cenital. " +
                "Es solo visual: caminos, grafos, torres y oleadas funcionan igual en ambas. " +
                "En 2.5D cada enemigo lleva un aro del color de su tipo. Ambiente: invierno nocturno (nieve, luz fría y neblina). " +
                "Modelos: Kenney Tower Defense Kit y KayKit Dungeon Pack (licencia CC0).\n\n" +
                "<color=#66DDFF><b>ACCESIBILIDAD</b></color>\n" +
                "👁 Daltonismo: cambia verde/rojo por azul/naranja. Cada enemigo tiene una silueta distinta además del color. " +
                "Todos los avisos sonoros tienen también un mensaje visual en el banner superior. 🔊 silencia el sonido.\n\n" +
                "<color=#66DDFF><b>MULTIJUGADOR</b></color>\n" +
                "🌐 Online: crea una sala (servidor TCP) o únete por IP/código en la red local; o usa la Sala Simulada para jugar con comandantes controlados por IA sin conexión.";
            panelAyuda.SetActive(false);
        }

        private void ConstruirPanelFin(Transform raiz)
        {
            RectTransform rt = UIFabrica.CrearPanel(raiz, "PanelFinPartida", new Color(0.04f, 0.06f, 0.1f, 0.97f), Paleta.Dorado);
            UIFabrica.Anclar(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(620, 360));
            panelFin = rt.gameObject;

            txtFinTitulo = UIFabrica.CrearTexto(rt, "Titulo", "", 34, TextAnchor.MiddleCenter, Paleta.Dorado, true);
            UIFabrica.Colocar(txtFinTitulo, 0, -18, 620, 60);
            txtFinDetalle = UIFabrica.CrearTexto(rt, "Detalle", "", 16, TextAnchor.UpperCenter, Color.white);
            UIFabrica.Colocar(txtFinDetalle, 20, -90, 580, 170);

            Button reintentar = UIFabrica.CrearBoton(rt, "BtnReintentar", "🔁 Reintentar", new Color(0.18f, 0.55f, 0.32f), 15);
            UIFabrica.Colocar(reintentar, 30, -290, 175, 46);
            reintentar.onClick.AddListener(() =>
            {
                panelFin.SetActive(false);
                if (GameManager.Instance != null) GameManager.Instance.ReiniciarNivel();
            });

            Button ranking = UIFabrica.CrearBoton(rt, "BtnRanking", "🏆 Ver Ranking", new Color(0.62f, 0.46f, 0.08f), 15);
            UIFabrica.Colocar(ranking, 222, -290, 175, 46);
            ranking.onClick.AddListener(() => LeaderboardJSON.Asegurar().MostrarPanel());

            btnSiguienteNivel = UIFabrica.CrearBoton(rt, "BtnSiguiente", "⏭ Siguiente Nivel", new Color(0.15f, 0.42f, 0.7f), 15);
            UIFabrica.Colocar(btnSiguienteNivel, 414, -290, 175, 46);
            btnSiguienteNivel.onClick.AddListener(() =>
            {
                panelFin.SetActive(false);
                GameManager gm = GameManager.Instance;
                if (gm != null) gm.CargarNivel(Mathf.Min(gm.nivelActual + 1, GameManager.Niveles.Length - 1));
            });

            panelFin.SetActive(false);
        }

        private void MostrarFinPartida(bool victoria, int total)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || panelFin == null) return;

            txtFinTitulo.text = victoria ? "👑 ¡VICTORIA!" : "💀 DERROTA";
            txtFinTitulo.color = victoria ? Paleta.Dorado : Paleta.Negativo;
            string motivacion = victoria
                ? "¡Defensa impecable, Comandante! La red resistió todas las oleadas."
                : "Toda derrota enseña: prueba a enlazar tus torres en red y a mejorar las más cercanas a la base.";
            txtFinDetalle.text =
                $"<i>{motivacion}</i>\n\n" +
                $"{gm.Nivel.nombre}  ·  Oleadas superadas: <b>{gm.oleadasCompletadas}/{gm.totalOleadas}</b>\n" +
                $"Enemigos derrotados: <b>{gm.enemigosDerrotados}</b>  ·  Vida de la base: <b>{gm.vidaBaseActual:0}</b>\n\n" +
                $"<size=26><color=#FFDD44>PUNTAJE FINAL: <b>{total}</b></color></size>\n" +
                "<color=#94a3b8>Guardado en el 🏆 Ranking y en la ficha del comandante.</color>";

            btnSiguienteNivel.gameObject.SetActive(victoria && gm.nivelActual < GameManager.Niveles.Length - 1);
            panelFin.SetActive(true);
            SetVisibleFinTrasReinicio();
        }

        private void SetVisibleFinTrasReinicio()
        {
            // El panel se oculta solo al cargar otro nivel desde la barra de menús
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            gm.OnEstadoCambiado -= OcultarFinSiReinicio;
            gm.OnEstadoCambiado += OcultarFinSiReinicio;
        }

        private void OcultarFinSiReinicio()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.PartidaTerminada && panelFin != null && panelFin.activeSelf) panelFin.SetActive(false);
        }

        #endregion
    }
}
