using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using GrafosEST2;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Interfaz visual (HUD) del juego.
    /// Muestra vida de la base, progreso de oleadas, experiencia, oro,
    /// puntos de habilidad ganados en combate y banner de notificaciones.
    /// </summary>
    public class HUDJuego : MonoBehaviour
    {
        public static HUDJuego Instance { get; private set; }

        private Text txtBaseVida;
        private Text txtOleadaInfo;
        private Text txtOro;
        private Text txtJugadorXP;
        private Text txtPuntosDAG;
        private Button btnIniciarOleada;
        private Button btnHabilidades;
        private Button btnComandantes;
        private Button btnSobrecarga;
        private Text txtBannerCentro;
        private GameObject panelBanner;

        [Header("Controles de Construcción Libre")]
        private Button btnTorreBasica;
        private Button btnTorreHielo;
        private Button btnTorreCanon;
        private Button btnTorreLaser;
        private Outline outTorreBasica;
        private Outline outTorreHielo;
        private Outline outTorreCanon;
        private Outline outTorreLaser;

        private Font fuenteUI;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializarHUD()
        {
            if (FindFirstObjectByType<HUDJuego>() == null)
            {
                GameObject go = new GameObject("HUDJuego_CanvasHost");
                go.AddComponent<HUDJuego>();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuenteUI == null) fuenteUI = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fuenteUI == null) fuenteUI = Font.CreateDynamicFontFromOSFont("Arial", 14);

            ConstruirHUDAutomaticamente();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEstadoCambiado += ActualizarHUD;
                GameManager.Instance.OnNotificacion += MostrarBannerNotificacion;
            }

            ActualizarHUD();
            MostrarBannerNotificacion("🛡️ Haz clic en los cuadrantes del mapa para colocar defensas 🏹");
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEstadoCambiado -= ActualizarHUD;
                GameManager.Instance.OnNotificacion -= MostrarBannerNotificacion;
            }
        }

        private void Update()
        {
            // Actualizar cuenta regresiva suavemente
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.cuentaRegresivaOleada > 0 &&
                    (GameManager.Instance.estado == EstadoJuego.EsperandoInicio || GameManager.Instance.estado == EstadoJuego.OleadaCompletada))
                {
                    txtOleadaInfo.text = $"⏳ Siguiente oleada en: <color=#FFDD44>{GameManager.Instance.cuentaRegresivaOleada:0.0}s</color>";
                    if (btnIniciarOleada != null) btnIniciarOleada.gameObject.SetActive(true);
                }
            }
        }

        public void ActualizarHUD()
        {
            if (GameManager.Instance == null) return;

            // 1. Vida de Base y Escudo
            if (txtBaseVida != null)
            {
                string escudoStr = GameManager.Instance.escudoBaseActual > 0 ? $" <color=#44EEFF>(+{GameManager.Instance.escudoBaseActual:0} Escudo)</color>" : "";
                Color colorVida = GameManager.Instance.vidaBaseActual > 40 ? Color.white : new Color(1f, 0.35f, 0.35f);
                txtBaseVida.color = colorVida;
                txtBaseVida.text = $"🏰 Base: <b>{GameManager.Instance.vidaBaseActual:0}/{GameManager.Instance.vidaBaseMaxima:0}</b>{escudoStr}";
            }

            // 2. Información de Oleada
            if (txtOleadaInfo != null)
            {
                if (GameManager.Instance.estado == EstadoJuego.GenerandoOleada || GameManager.Instance.estado == EstadoJuego.OleadaEnCurso)
                {
                    txtOleadaInfo.text = $"⚔️ Oleada {GameManager.Instance.oleadaActual}/{GameManager.Instance.totalOleadas}  |  Enemigos: <color=#FF6666>{GameManager.Instance.enemigosRestantesEnOleada}</color>";
                    if (btnIniciarOleada != null) btnIniciarOleada.gameObject.SetActive(false);
                }
                else if (GameManager.Instance.estado == EstadoJuego.GameOver)
                {
                    txtOleadaInfo.text = "<color=#FF4444>💀 DERROTA - Base Destruida</color>";
                    if (btnIniciarOleada != null) btnIniciarOleada.gameObject.SetActive(false);
                }
                else if (GameManager.Instance.estado == EstadoJuego.Victoria)
                {
                    txtOleadaInfo.text = "<color=#FFDD44>👑 ¡VICTORIA TOTAL!</color>";
                    if (btnIniciarOleada != null) btnIniciarOleada.gameObject.SetActive(false);
                }
            }

            // 3. Oro
            if (txtOro != null)
            {
                txtOro.text = $"💰 Oro: <color=#FFDD44>{GameManager.Instance.oroActual}</color>";
            }

            // 4. Jugador, Nivel y XP Progresiva
            if (txtJugadorXP != null && GameManager.Instance.jugadorActivo != null)
            {
                var j = GameManager.Instance.jugadorActivo;
                txtJugadorXP.text = $"⭐ Nvl {j.nivel}  (XP: <color=#66DDFF>{j.experiencia}/{j.experienciaSiguienteNivel}</color>)";
            }

            // 5. Puntos de Habilidad Ganados (DAG)
            if (txtPuntosDAG != null && GameManager.Instance.jugadorActivo != null)
            {
                int pts = GameManager.Instance.jugadorActivo.puntosHabilidad;
                string aviso = pts > 0 ? " <color=#FFDD44><b>[¡Gástalos!]</b></color>" : "";
                txtPuntosDAG.text = $"💎 Pts DAG: <color=#44FF88><b>{pts}</b></color>{aviso}";
            }

            // 6. Botón de Sobrecarga
            if (btnSobrecarga != null)
            {
                bool tieneSobrecarga = GameManager.Instance.TieneHabilidadDAG("HAB_SOBRECARGA");
                btnSobrecarga.gameObject.SetActive(tieneSobrecarga);
            }

            // 7. Barra de Construcción de Torres (Resaltado y Asequibilidad)
            if (GameManager.Instance != null)
            {
                TipoTorre sel = GameManager.Instance.tipoTorreAConstruir;
                ActualizarBotonConstruccion(btnTorreBasica, outTorreBasica, TipoTorre.Basica, sel == TipoTorre.Basica, 50);
                ActualizarBotonConstruccion(btnTorreHielo, outTorreHielo, TipoTorre.Hielo, sel == TipoTorre.Hielo, 75);
                ActualizarBotonConstruccion(btnTorreCanon, outTorreCanon, TipoTorre.Canon, sel == TipoTorre.Canon, 90);
                ActualizarBotonConstruccion(btnTorreLaser, outTorreLaser, TipoTorre.Laser, sel == TipoTorre.Laser, 120);
            }
        }

        public void MostrarBannerNotificacion(string mensaje)
        {
            if (txtBannerCentro != null && panelBanner != null)
            {
                txtBannerCentro.text = mensaje;
                StopAllCoroutines();
                StartCoroutine(AnimarBanner());
            }
        }

        private IEnumerator AnimarBanner()
        {
            panelBanner.SetActive(true);
            yield return new WaitForSeconds(3.5f);
            panelBanner.SetActive(false);
        }

        #region Generación de HUD

        private void ConstruirHUDAutomaticamente()
        {
            // Canvas del HUD
            GameObject canvasObj = new GameObject("Canvas_HUD_TowerDefense");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // Justo detrás de los paneles modales

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // 1. Barra Superior Principal
            GameObject barraTop = new GameObject("BarraTopHUD");
            barraTop.transform.SetParent(canvasObj.transform, false);

            RectTransform rtBarra = barraTop.AddComponent<RectTransform>();
            rtBarra.anchorMin = new Vector2(0, 1);
            rtBarra.anchorMax = new Vector2(1, 1);
            rtBarra.pivot = new Vector2(0.5f, 1);
            rtBarra.sizeDelta = new Vector2(0, 52);

            Image imgBarra = barraTop.AddComponent<Image>();
            imgBarra.color = new Color(0.08f, 0.11f, 0.16f, 0.94f);

            Outline outBarra = barraTop.AddComponent<Outline>();
            outBarra.effectColor = new Color(0.2f, 0.4f, 0.6f, 0.5f);
            outBarra.effectDistance = new Vector2(0, -2);

            HorizontalLayoutGroup hlg = barraTop.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.spacing = 10;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;

            // Elementos de la barra
            txtBaseVida = CrearTextoHUD(barraTop.transform, "TxtBaseVida", 175);
            txtOleadaInfo = CrearTextoHUD(barraTop.transform, "TxtOleada", 250);
            txtOro = CrearTextoHUD(barraTop.transform, "TxtOro", 115);
            txtJugadorXP = CrearTextoHUD(barraTop.transform, "TxtJugadorXP", 185);
            txtPuntosDAG = CrearTextoHUD(barraTop.transform, "TxtPuntosDAG", 155);

            // Botón Iniciar Oleada Ahora
            btnIniciarOleada = CrearBotonHUD(barraTop.transform, "BtnIniciarOleada", "▶ Iniciar Oleada", new Color(0.2f, 0.65f, 0.35f), 125);
            btnIniciarOleada.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.IniciarOleada();
                }
            });

            // Botón Único de Acceso a la Armería y Árbol de Talentos (DAG)
            btnHabilidades = CrearBotonHUD(barraTop.transform, "BtnHabilidadesDAG", "⚡ Armería / Mejoras", new Color(0.32f, 0.22f, 0.75f), 145);
            btnHabilidades.onClick.AddListener(() =>
            {
                if (ControladorHabilidadesUI.Instance != null)
                {
                    ControladorHabilidadesUI.Instance.AlternarVisibilidadPanel();
                }
            });

            // Botón de Acceso a Comandantes (Árbol BST)
            btnComandantes = CrearBotonHUD(barraTop.transform, "BtnComandantesBST", "👥 Comandantes", new Color(0.12f, 0.45f, 0.65f), 130);
            btnComandantes.onClick.AddListener(() =>
            {
                if (ControladorArbolUI.Instance != null)
                {
                    ControladorArbolUI.Instance.AlternarVisibilidadPanel();
                }
            });

            // Botones de Selección de Dificultad / Nivel (flujo horizontal sin superposición)
            Button btnNivel1 = CrearBotonHUD(barraTop.transform, "BtnNivel1", "Nivel 1", new Color(0.15f, 0.52f, 0.25f), 75);
            btnNivel1.onClick.AddListener(() =>
            {
                if (EnemySpawner.instance != null)
                {
                    EnemySpawner.instance.CargarConfiguracionNivel(0);
                    if (GameManager.Instance != null)
                        GameManager.Instance.EmitirNotificacion("🗺️ Nivel 1 cargado");
                }
            });

            Button btnNivel2 = CrearBotonHUD(barraTop.transform, "BtnNivel2", "Nivel 2", new Color(0.78f, 0.48f, 0.08f), 75);
            btnNivel2.onClick.AddListener(() =>
            {
                if (EnemySpawner.instance != null)
                {
                    EnemySpawner.instance.CargarConfiguracionNivel(1);
                    if (GameManager.Instance != null)
                        GameManager.Instance.EmitirNotificacion("🗺️ Nivel 2 cargado");
                }
            });

            Button btnNivel3 = CrearBotonHUD(barraTop.transform, "BtnNivel3", "Nivel 3", new Color(0.78f, 0.18f, 0.18f), 75);
            btnNivel3.onClick.AddListener(() =>
            {
                if (EnemySpawner.instance != null)
                {
                    EnemySpawner.instance.CargarConfiguracionNivel(2);
                    if (GameManager.Instance != null)
                        GameManager.Instance.EmitirNotificacion("🗺️ Nivel 3 cargado");
                }
            });

            // Botón Habilidad Definitiva Sobrecarga
            btnSobrecarga = CrearBotonHUD(barraTop.transform, "BtnSobrecarga", "⚡ Sobrecarga", new Color(0.85f, 0.35f, 0.15f), 115);
            btnSobrecarga.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ActivarSobrecargaRed();
                }
            });
            btnSobrecarga.gameObject.SetActive(false); // Se activa si se desbloquea en DAG

            // 2. Barra Inferior de Selección y Construcción Libre de Torres
            GameObject barraConst = new GameObject("BarraConstruccion");
            barraConst.transform.SetParent(canvasObj.transform, false);

            RectTransform rtConst = barraConst.AddComponent<RectTransform>();
            rtConst.anchorMin = new Vector2(0.5f, 0f);
            rtConst.anchorMax = new Vector2(0.5f, 0f);
            rtConst.pivot = new Vector2(0.5f, 0f);
            rtConst.anchoredPosition = new Vector2(0, 16);
            rtConst.sizeDelta = new Vector2(760, 68);

            Image imgConst = barraConst.AddComponent<Image>();
            imgConst.color = new Color(0.06f, 0.08f, 0.13f, 0.96f);

            Outline outConst = barraConst.AddComponent<Outline>();
            outConst.effectColor = new Color(0.3f, 0.6f, 0.85f, 0.6f);
            outConst.effectDistance = new Vector2(0, 2);

            HorizontalLayoutGroup hlgConst = barraConst.AddComponent<HorizontalLayoutGroup>();
            hlgConst.padding = new RectOffset(14, 14, 8, 8);
            hlgConst.spacing = 12;
            hlgConst.childControlWidth = true;
            hlgConst.childControlHeight = true;
            hlgConst.childForceExpandWidth = true;

            btnTorreBasica = CrearBotonTorre(barraConst.transform, "BtnTorreBasica", "🔨 Centinela\n50 Oro", TipoTorre.Basica, out outTorreBasica);
            btnTorreHielo = CrearBotonTorre(barraConst.transform, "BtnTorreHielo", "❄️ Criogénica\n75 Oro", TipoTorre.Hielo, out outTorreHielo);
            btnTorreCanon = CrearBotonTorre(barraConst.transform, "BtnTorreCanon", "💥 Cañón Área\n90 Oro", TipoTorre.Canon, out outTorreCanon);
            btnTorreLaser = CrearBotonTorre(barraConst.transform, "BtnTorreLaser", "⚡ Haz Láser\n120 Oro", TipoTorre.Laser, out outTorreLaser);

            // 3. Banner de Notificaciones en el Centro Superior
            panelBanner = new GameObject("PanelBannerCentro");
            panelBanner.transform.SetParent(canvasObj.transform, false);

            RectTransform rtBanner = panelBanner.AddComponent<RectTransform>();
            rtBanner.anchorMin = new Vector2(0.5f, 0.88f);
            rtBanner.anchorMax = new Vector2(0.5f, 0.88f);
            rtBanner.pivot = new Vector2(0.5f, 0.5f);
            rtBanner.sizeDelta = new Vector2(620, 60);

            Image imgBanner = panelBanner.AddComponent<Image>();
            imgBanner.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

            Outline outBanner = panelBanner.AddComponent<Outline>();
            outBanner.effectColor = new Color(0.4f, 0.8f, 1f, 0.8f);
            outBanner.effectDistance = new Vector2(2, -2);

            GameObject txtBObj = new GameObject("TextoBanner");
            txtBObj.transform.SetParent(panelBanner.transform, false);
            txtBannerCentro = txtBObj.AddComponent<Text>();
            txtBannerCentro.font = fuenteUI;
            txtBannerCentro.fontSize = 15;
            txtBannerCentro.fontStyle = FontStyle.Bold;
            txtBannerCentro.color = new Color(1f, 0.9f, 0.4f);
            txtBannerCentro.alignment = TextAnchor.MiddleCenter;
            txtBannerCentro.supportRichText = true;

            RectTransform rtTxtB = txtBObj.GetComponent<RectTransform>();
            rtTxtB.anchorMin = Vector2.zero;
            rtTxtB.anchorMax = Vector2.one;
            rtTxtB.sizeDelta = Vector2.zero;

            panelBanner.SetActive(false);
        }

        private Button CrearBotonTorre(Transform padre, string nombre, string texto, TipoTorre tipo, out Outline outline)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);

            Image img = go.AddComponent<Image>();
            img.color = ObtenerColorBaseTorre(tipo);

            outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.4f, 0.6f, 0.5f);
            outline.effectDistance = new Vector2(1, -1);

            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SeleccionarTipoTorre(tipo);
                    ActualizarHUD();
                }
            });

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(go.transform, false);
            Text t = txtObj.AddComponent<Text>();
            t.font = fuenteUI;
            t.fontSize = 12;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = texto;

            RectTransform rtT = txtObj.GetComponent<RectTransform>();
            rtT.anchorMin = Vector2.zero;
            rtT.anchorMax = Vector2.one;
            rtT.sizeDelta = Vector2.zero;

            return btn;
        }

        private void ActualizarBotonConstruccion(Button btn, Outline outline, TipoTorre tipo, bool esSeleccionada, int costo)
        {
            if (btn == null || outline == null) return;

            bool puedePagar = GameManager.Instance != null && GameManager.Instance.OroActual >= costo;

            if (esSeleccionada)
            {
                outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f); // Resaltado dorado brillante activo
                outline.effectDistance = new Vector2(3, -3);
            }
            else
            {
                outline.effectColor = puedePagar ? new Color(0.3f, 0.5f, 0.7f, 0.4f) : new Color(0.3f, 0.2f, 0.2f, 0.4f);
                outline.effectDistance = new Vector2(1, -1);
            }

            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                Color baseCol = ObtenerColorBaseTorre(tipo);
                img.color = puedePagar ? (esSeleccionada ? baseCol * 1.25f : baseCol) : baseCol * 0.45f;
            }
        }

        private Color ObtenerColorBaseTorre(TipoTorre tipo)
        {
            switch (tipo)
            {
                case TipoTorre.Basica: return new Color(0.2f, 0.3f, 0.45f);
                case TipoTorre.Hielo: return new Color(0.12f, 0.4f, 0.6f);
                case TipoTorre.Canon: return new Color(0.45f, 0.28f, 0.15f);
                case TipoTorre.Laser: return new Color(0.4f, 0.15f, 0.55f);
                default: return new Color(0.2f, 0.2f, 0.2f);
            }
        }

        private Text CrearTextoHUD(Transform padre, string nombre, float ancho)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = ancho;

            Text txt = go.AddComponent<Text>();
            txt.font = fuenteUI;
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.supportRichText = true;

            return txt;
        }

        private Button CrearBotonHUD(Transform padre, string nombre, string texto, Color colorFondo, float ancho)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = ancho;
            le.preferredHeight = 36;

            Image img = go.AddComponent<Image>();
            img.color = colorFondo;

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = colorFondo * 1.15f;
            cb.pressedColor = colorFondo * 0.85f;
            btn.colors = cb;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(go.transform, false);
            Text t = textObj.AddComponent<Text>();
            t.font = fuenteUI;
            t.fontSize = 12;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = texto;

            RectTransform rtT = textObj.GetComponent<RectTransform>();
            rtT.anchorMin = Vector2.zero;
            rtT.anchorMax = Vector2.one;
            rtT.sizeDelta = Vector2.zero;

            return btn;
        }

        #endregion
    }
}
