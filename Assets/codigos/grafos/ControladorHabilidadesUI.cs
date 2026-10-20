using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#else
using UnityEngine.EventSystems;
#endif

namespace GrafosEST2
{
    /// <summary>
    /// Controlador e Interfaz Gráfica interactiva para el sistema de Árbol de Habilidades
    /// basado en Grafos Dirigidos Acíclicos (DAG) y el Algoritmo de Kahn.
    /// Diseñado para experimentación en tiempo de ejecución y sustentación académica.
    /// </summary>
    public class ControladorHabilidadesUI : MonoBehaviour
    {
        public static ControladorHabilidadesUI Instance { get; private set; }

        [Header("Núcleo del Grafo de Habilidades")]
        public GrafoHabilidades sistemaHabilidades;

        [Header("Jugador Actual")]
        public Jugador jugadorActivo;

        [Header("Panel Principal y UI")]
        public GameObject panelHabilidades;
        [SerializeField] private bool abrirAlIniciar = false;
        [SerializeField] private bool autoGenerarUISiFalta = true;

        [Header("Componentes UI")]
        public Text textoInfoJugador;
        public Text textoConsolaFeedback;
        public Transform contenedorTarjetasHabilidades;
        public Dropdown dropdownOrigenCiclo;
        public Dropdown dropdownDestinoCiclo;

        private Font fuenteUI;
        private List<GameObject> tarjetasInstanciadas = new List<GameObject>();
        private string categoriaFiltroActual = "TODAS";
        private Dictionary<string, Button> botonesFiltro = new Dictionary<string, Button>();
        private Text txtProgresoArmeria;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializarEnEscena()
        {
            if (FindFirstObjectByType<ControladorHabilidadesUI>() == null)
            {
                GameObject host = new GameObject("SistemaHabilidadesDAG");
                host.AddComponent<ControladorHabilidadesUI>();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            sistemaHabilidades = new GrafoHabilidades();

            // Intentar enlazar con jugador del árbol BST existente o crear uno de prueba
            InicializarJugadorActivo();

            fuenteUI = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuenteUI == null) fuenteUI = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fuenteUI == null) fuenteUI = Font.CreateDynamicFontFromOSFont("Arial", 14);

            if (autoGenerarUISiFalta && panelHabilidades == null)
            {
                ConstruirUIAutomaticamente();
            }
        }

        private void Start()
        {
            if (panelHabilidades != null)
            {
                panelHabilidades.SetActive(abrirAlIniciar);
            }

            ActualizarTodo();
            MostrarFeedback("✅ Sistema de Habilidades (DAG) inicializado. Haga clic en '⚡ Habilidades (DAG)' para abrir.", new Color(0.3f, 0.9f, 0.5f));
        }

        private void InicializarJugadorActivo()
        {
            // Verificar si hay jugadores en ControladorArbolUI
            ControladorArbolUI ctrlArbol = FindFirstObjectByType<ControladorArbolUI>();
            if (ctrlArbol != null && ctrlArbol.arbolJugadores != null)
            {
                List<Jugador> jugadoresABB = ctrlArbol.arbolJugadores.InOrden();
                if (jugadoresABB.Count > 0)
                {
                    jugadorActivo = jugadoresABB[0];
                    sistemaHabilidades.SincronizarConJugador(jugadorActivo);
                    return;
                }
            }

            // Jugador por defecto si no existe ninguno en el árbol aún
            jugadorActivo = new Jugador(100, 1, "Comandante_1", 1, 0);
            sistemaHabilidades.SincronizarConJugador(jugadorActivo);
        }

        public void AlternarVisibilidadPanel()
        {
            if (panelHabilidades != null)
            {
                bool nuevoEstado = !panelHabilidades.activeSelf;
                panelHabilidades.SetActive(nuevoEstado);

                if (nuevoEstado)
                {
                    ActualizarTodo();
                }
            }
        }

        public void ActualizarTodo()
        {
            ActualizarEncabezadoJugador();
            RefrescarTarjetasHabilidades();
            ActualizarDropdownsCiclo();
        }

        private void ActualizarEncabezadoJugador()
        {
            if (textoInfoJugador == null || jugadorActivo == null) return;

            int total = 15;
            int desbloqueadas = 0;
            if (sistemaHabilidades != null)
            {
                var todas = sistemaHabilidades.ObtenerTodasLasHabilidades();
                for (int i = 0; i < todas.Count; i++)
                {
                    if (todas[i].desbloqueada || (jugadorActivo != null && jugadorActivo.TieneHabilidad(todas[i].id)))
                    {
                        desbloqueadas++;
                    }
                }
            }

            textoInfoJugador.text = $"👤 Comandante: <color=#66DDFF><b>{jugadorActivo.nombre}</b></color>  |  " +
                                   $"⭐ Nvl <color=#FFDD44><b>{jugadorActivo.nivel}</b></color> (XP: <color=#88DDFF>{jugadorActivo.experiencia}/{jugadorActivo.experienciaSiguienteNivel}</color>)  |  " +
                                   $"💎 Puntos Tácticos: <color=#44FF88><b>{jugadorActivo.puntosHabilidad}</b></color> pts  |  " +
                                   $"💰 Oro: <color=#FFD700><b>{jugadorActivo.oro}</b></color>";

            if (txtProgresoArmeria != null)
            {
                txtProgresoArmeria.text = $"Tecnologías: <color=#44FF88><b>{desbloqueadas}/{total} Activas</b></color>";
            }
        }

        public void FiltrarPorCategoria(string cat)
        {
            categoriaFiltroActual = cat;
            ActualizarEstiloBotonesFiltro();
            RefrescarTarjetasHabilidades();
        }

        private void ActualizarEstiloBotonesFiltro()
        {
            foreach (var kvp in botonesFiltro)
            {
                if (kvp.Value == null) continue;
                bool activo = kvp.Key == categoriaFiltroActual;
                Image img = kvp.Value.GetComponent<Image>();
                if (img != null)
                {
                    img.color = activo ? new Color(0.2f, 0.55f, 0.95f, 0.95f) : new Color(0.12f, 0.16f, 0.24f, 0.9f);
                }
            }
        }

        public void RefrescarTarjetasHabilidades()
        {
            if (contenedorTarjetasHabilidades == null) return;

            // Limpiar tarjetas previas
            for (int i = 0; i < tarjetasInstanciadas.Count; i++)
            {
                if (tarjetasInstanciadas[i] != null)
                {
                    Destroy(tarjetasInstanciadas[i]);
                }
            }
            tarjetasInstanciadas.Clear();

            List<Habilidad> habilidades = sistemaHabilidades.ObtenerTodasLasHabilidades();

            for (int i = 0; i < habilidades.Count; i++)
            {
                Habilidad hab = habilidades[i];
                if (categoriaFiltroActual != "TODAS" && hab.categoria != categoriaFiltroActual)
                {
                    continue;
                }
                GameObject tarjeta = CrearTarjetaHabilidad(hab);
                tarjeta.transform.SetParent(contenedorTarjetasHabilidades, false);
                tarjetasInstanciadas.Add(tarjeta);
            }
        }

        private GameObject CrearTarjetaHabilidad(Habilidad hab)
        {
            EstadoHabilidadEnum estado = sistemaHabilidades.ObtenerEstadoHabilidad(hab.id, jugadorActivo, out string detalleEstado);
            List<Habilidad> prereqs = sistemaHabilidades.ObtenerPrerrequisitos(hab.id);

            GameObject tarjeta = new GameObject($"Tarjeta_{hab.id}");
            Image imgFondo = tarjeta.AddComponent<Image>();

            // Colores según estado
            Color colorFondo;
            if (estado == EstadoHabilidadEnum.Desbloqueada)
                colorFondo = new Color(0.12f, 0.25f, 0.18f, 0.95f); // Verde oscuro
            else if (estado == EstadoHabilidadEnum.DisponibleParaDesbloquear)
                colorFondo = new Color(0.16f, 0.28f, 0.38f, 0.95f); // Azul/Cian oscuro
            else
                colorFondo = new Color(0.18f, 0.18f, 0.22f, 0.95f); // Gris oscuro

            imgFondo.color = colorFondo;

            Outline outline = tarjeta.AddComponent<Outline>();
            outline.effectColor = (estado == EstadoHabilidadEnum.DisponibleParaDesbloquear) ? new Color(0.2f, 0.8f, 1f, 0.8f) : new Color(0.3f, 0.3f, 0.35f, 0.5f);
            outline.effectDistance = new Vector2(1, -1);

            LayoutElement le = tarjeta.AddComponent<LayoutElement>();
            le.minHeight = 78;
            le.preferredHeight = 78;

            HorizontalLayoutGroup hlg = tarjeta.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 6, 6);
            hlg.spacing = 8;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // 1. Bloque de Información de la Habilidad
            GameObject infoObj = new GameObject("Info");
            infoObj.transform.SetParent(tarjeta.transform, false);
            VerticalLayoutGroup vlgInfo = infoObj.AddComponent<VerticalLayoutGroup>();
            vlgInfo.spacing = 2;
            vlgInfo.childControlWidth = true;
            vlgInfo.childControlHeight = false;

            LayoutElement leInfo = infoObj.AddComponent<LayoutElement>();
            leInfo.flexibleWidth = 1f;

            // Título y categoría
            GameObject tituloObj = new GameObject("Titulo");
            tituloObj.transform.SetParent(infoObj.transform, false);
            Text txtTitulo = tituloObj.AddComponent<Text>();
            txtTitulo.font = fuenteUI;
            txtTitulo.fontSize = 13;
            txtTitulo.fontStyle = FontStyle.Bold;
            txtTitulo.color = (estado == EstadoHabilidadEnum.Desbloqueada) ? new Color(0.4f, 1f, 0.6f) : Color.white;

            string catTag;
            switch (hab.categoria)
            {
                case "Ofensiva": catTag = "<color=#FF6644>[⚔️ OFENSIVA]</color>"; break;
                case "Control": catTag = "<color=#44DDFF>[❄️ CRIOGENIA]</color>"; break;
                case "Defensiva": catTag = "<color=#44FF88>[🛡️ BLINDAJE]</color>"; break;
                case "Economía": catTag = "<color=#FFDD44>[💰 LOGÍSTICA]</color>"; break;
                case "Definitiva": catTag = "<color=#EE66FF>[⚡ SUPREMA]</color>"; break;
                default: catTag = "<color=#88CCFF>[🔰 BÁSICA]</color>"; break;
            }
            txtTitulo.text = $"{hab.nombre}  {catTag}";

            // Descripción
            GameObject descObj = new GameObject("Descripcion");
            descObj.transform.SetParent(infoObj.transform, false);
            Text txtDesc = descObj.AddComponent<Text>();
            txtDesc.font = fuenteUI;
            txtDesc.fontSize = 11;
            txtDesc.color = new Color(0.85f, 0.88f, 0.95f);
            txtDesc.text = hab.descripcion;

            // Prerrequisitos y Costos
            GameObject reqObj = new GameObject("Requisitos");
            reqObj.transform.SetParent(infoObj.transform, false);
            Text txtReq = reqObj.AddComponent<Text>();
            txtReq.font = fuenteUI;
            txtReq.fontSize = 10;

            StringBuilder sbReq = new StringBuilder();
            sbReq.Append($"Costo: <color=#FFDD66><b>{hab.costoPuntos} Puntos</b></color> | Nvl Req: <color=#88DDFF>Nvl {hab.nivelRequerido}</color> | Requisitos: ");
            if (prereqs.Count == 0)
            {
                sbReq.Append("<color=#88FFAA>✓ Habilidad Inicial</color>");
            }
            else
            {
                List<string> reqsNombres = new List<string>();
                for (int p = 0; p < prereqs.Count; p++)
                {
                    bool cumplido = prereqs[p].desbloqueada || (jugadorActivo != null && jugadorActivo.TieneHabilidad(prereqs[p].id));
                    string colorTag = cumplido ? "#66FF88" : "#FF6666";
                    string checkTag = cumplido ? "✓" : "🔒";
                    reqsNombres.Add($"<color={colorTag}>{checkTag} {prereqs[p].nombre}</color>");
                }
                sbReq.Append(string.Join(", ", reqsNombres));
            }
            txtReq.text = sbReq.ToString();

            // 2. Botón de Acción o Badge de Estado
            GameObject botonObj = new GameObject("BotonAccion");
            botonObj.transform.SetParent(tarjeta.transform, false);
            LayoutElement leBoton = botonObj.AddComponent<LayoutElement>();
            leBoton.minWidth = 140;
            leBoton.preferredWidth = 140;

            if (estado == EstadoHabilidadEnum.Desbloqueada)
            {
                Image imgBadge = botonObj.AddComponent<Image>();
                imgBadge.color = new Color(0.2f, 0.6f, 0.3f, 0.8f);

                GameObject txtBadgeObj = new GameObject("Text");
                txtBadgeObj.transform.SetParent(botonObj.transform, false);
                Text txtBadge = txtBadgeObj.AddComponent<Text>();
                txtBadge.font = fuenteUI;
                txtBadge.fontSize = 13;
                txtBadge.fontStyle = FontStyle.Bold;
                txtBadge.alignment = TextAnchor.MiddleCenter;
                txtBadge.color = Color.white;
                txtBadge.text = "✓ ACTIVA";

                RectTransform rtBadge = txtBadgeObj.GetComponent<RectTransform>();
                rtBadge.anchorMin = Vector2.zero;
                rtBadge.anchorMax = Vector2.one;
                rtBadge.sizeDelta = Vector2.zero;
            }
            else if (estado == EstadoHabilidadEnum.DisponibleParaDesbloquear)
            {
                Image imgBtn = botonObj.AddComponent<Image>();
                imgBtn.color = new Color(0.15f, 0.65f, 0.9f);

                Button btn = botonObj.AddComponent<Button>();
                ColorBlock cb = btn.colors;
                cb.highlightedColor = new Color(0.25f, 0.75f, 1f);
                cb.pressedColor = new Color(0.1f, 0.5f, 0.7f);
                btn.colors = cb;

                string habIdCapturada = hab.id;
                btn.onClick.AddListener(() => DesbloquearHabilidad(habIdCapturada));

                GameObject txtBtnObj = new GameObject("Text");
                txtBtnObj.transform.SetParent(botonObj.transform, false);
                Text txtBtn = txtBtnObj.AddComponent<Text>();
                txtBtn.font = fuenteUI;
                txtBtn.fontSize = 12;
                txtBtn.fontStyle = FontStyle.Bold;
                txtBtn.alignment = TextAnchor.MiddleCenter;
                txtBtn.color = Color.white;
                txtBtn.text = $"MEJORAR\n(-{hab.costoPuntos} Pts)";

                RectTransform rtBtn = txtBtnObj.GetComponent<RectTransform>();
                rtBtn.anchorMin = Vector2.zero;
                rtBtn.anchorMax = Vector2.one;
                rtBtn.sizeDelta = Vector2.zero;
            }
            else
            {
                Image imgBloq = botonObj.AddComponent<Image>();
                imgBloq.color = new Color(0.25f, 0.25f, 0.3f, 0.6f);

                GameObject txtBloqObj = new GameObject("Text");
                txtBloqObj.transform.SetParent(botonObj.transform, false);
                Text txtBloq = txtBloqObj.AddComponent<Text>();
                txtBloq.font = fuenteUI;
                txtBloq.fontSize = 11;
                txtBloq.alignment = TextAnchor.MiddleCenter;
                txtBloq.color = new Color(0.7f, 0.7f, 0.75f);

                if (estado == EstadoHabilidadEnum.FaltanPrerrequisitos)
                    txtBloq.text = "🔒 Faltan\nRequisitos";
                else if (estado == EstadoHabilidadEnum.PuntosInsuficientes)
                    txtBloq.text = "🔒 Puntos\nInsuficientes";
                else
                    txtBloq.text = $"🔒 Requiere\nNvl {hab.nivelRequerido}";

                RectTransform rtBloq = txtBloqObj.GetComponent<RectTransform>();
                rtBloq.anchorMin = Vector2.zero;
                rtBloq.anchorMax = Vector2.one;
                rtBloq.sizeDelta = Vector2.zero;
            }

            return tarjeta;
        }

        #region Acciones de Usuario

        public void DesbloquearHabilidad(string idHabilidad)
        {
            if (sistemaHabilidades.IntentarDesbloquear(idHabilidad, jugadorActivo, out string mensaje))
            {
                MostrarFeedback(mensaje, new Color(0.3f, 0.95f, 0.45f));
                if (TowerDefenseGameplay.GameManager.Instance != null)
                {
                    TowerDefenseGameplay.GameManager.Instance.MostrarNotificacion($"⚡ ¡Mejora activada: {mensaje}!");
                }
                ActualizarTodo();
            }
            else
            {
                MostrarFeedback(mensaje, new Color(1f, 0.45f, 0.35f));
                if (TowerDefenseGameplay.GameManager.Instance != null)
                {
                    TowerDefenseGameplay.GameManager.Instance.MostrarNotificacion($"⚠️ {mensaje}");
                }
            }
        }

        public void EjecutarDemostracionKahn()
        {
            string reporteKahn = sistemaHabilidades.ObtenerOrdenKahnExplicado(out List<string> secuencia);
            MostrarFeedback(reporteKahn, new Color(0.4f, 0.9f, 1f));
        }

        public void MostrarMatrizAdyacencia()
        {
            string tablaMatriz = sistemaHabilidades.Grafo.MatrizAdyacenciaToString();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("📊 MATRIZ DE ADYACENCIA DEL GRAFO DE HABILIDADES (DAG):");
            sb.AppendLine("Filas = Origen (Requisito) | Columnas = Destino (Desbloqueo)");
            sb.AppendLine("Valor 1.0 = Existe precedencia dirigida u ➔ v");
            sb.AppendLine();
            sb.Append(tablaMatriz);
            MostrarFeedback(sb.ToString(), new Color(1f, 0.85f, 0.35f));
        }

        public void ProbarAgregarConexionCiclo()
        {
            if (dropdownOrigenCiclo == null || dropdownDestinoCiclo == null) return;

            string origen = dropdownOrigenCiclo.options[dropdownOrigenCiclo.value].text;
            string destino = dropdownDestinoCiclo.options[dropdownDestinoCiclo.value].text;

            bool permitida = sistemaHabilidades.ProbarAgregarAristaConValidacionDAG(origen, destino, out string mensaje);

            if (permitida)
            {
                MostrarFeedback(mensaje, new Color(0.3f, 0.95f, 0.45f));
                ActualizarTodo();
            }
            else
            {
                MostrarFeedback(mensaje, new Color(1f, 0.35f, 0.35f));
            }
        }

        public void DarPuntosPrueba()
        {
            if (jugadorActivo != null)
            {
                jugadorActivo.AgregarPuntosHabilidad(15);
                MostrarFeedback($"💎 +15 Puntos añadidos al jugador. Total actual: {jugadorActivo.puntosHabilidad} pts", new Color(0.3f, 0.85f, 1f));
                ActualizarTodo();
            }
        }

        public void SubirNivelPrueba()
        {
            if (jugadorActivo != null)
            {
                jugadorActivo.nivel++;
                MostrarFeedback($"⭐ ¡Nivel aumentado! Nivel actual del jugador: {jugadorActivo.nivel}", new Color(1f, 0.85f, 0.3f));
                ActualizarTodo();
            }
        }

        public void ReiniciarArbol()
        {
            sistemaHabilidades.InicializarArbolDefecto();
            sistemaHabilidades.ReiniciarHabilidades(jugadorActivo);
            MostrarFeedback("↺ Árbol y progreso de habilidades reiniciados a los valores por defecto.", new Color(1f, 0.7f, 0.3f));
            ActualizarTodo();
        }

        private void MostrarFeedback(string mensaje, Color color)
        {
            if (textoConsolaFeedback != null)
            {
                textoConsolaFeedback.text = $"[{System.DateTime.Now:HH:mm:ss}] {mensaje}";
                textoConsolaFeedback.color = color;
            }
            Debug.Log($"[ControladorHabilidadesUI] {mensaje}");
        }

        private void ActualizarDropdownsCiclo()
        {
            if (dropdownOrigenCiclo == null || dropdownDestinoCiclo == null) return;

            List<string> ids = new List<string>(sistemaHabilidades.Grafo.Nodos.Keys);

            dropdownOrigenCiclo.ClearOptions();
            dropdownDestinoCiclo.ClearOptions();

            dropdownOrigenCiclo.AddOptions(ids);
            dropdownDestinoCiclo.AddOptions(ids);

            // Seleccionar por defecto dos que demuestren un ciclo didáctico:
            // HAB_SOBRECARGA (final) apuntando a HAB_BASE (inicial)
            int idxSobrecarga = ids.IndexOf("HAB_SOBRECARGA");
            int idxBase = ids.IndexOf("HAB_BASE");

            if (idxSobrecarga >= 0) dropdownOrigenCiclo.value = idxSobrecarga;
            if (idxBase >= 0) dropdownDestinoCiclo.value = idxBase;
        }

        #endregion

        #region Generación Dinámica de UI en Tiempo de Ejecución

        private void ConstruirUIAutomaticamente()
        {
            // 1. Asegurar EventSystem
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }

            // 2. Buscar o crear Canvas
            GameObject canvasObj = GameObject.Find("Canvas_HabilidadesDAG");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("Canvas_HabilidadesDAG");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 101; // Encima de otros paneles

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 4. Panel Principal Modal
            panelHabilidades = new GameObject("PanelHabilidadesModal");
            panelHabilidades.transform.SetParent(canvasObj.transform, false);

            RectTransform rtPanel = panelHabilidades.AddComponent<RectTransform>();
            rtPanel.anchorMin = new Vector2(0.5f, 0.5f);
            rtPanel.anchorMax = new Vector2(0.5f, 0.5f);
            rtPanel.pivot = new Vector2(0.5f, 0.5f);
            rtPanel.sizeDelta = new Vector2(1060, 700);

            Image imgPanel = panelHabilidades.AddComponent<Image>();
            imgPanel.color = new Color(0.08f, 0.10f, 0.15f, 0.98f);

            Outline outlinePanel = panelHabilidades.AddComponent<Outline>();
            outlinePanel.effectColor = new Color(0.55f, 0.35f, 0.95f, 0.85f);
            outlinePanel.effectDistance = new Vector2(2, -2);

            VerticalLayoutGroup vlgPanel = panelHabilidades.AddComponent<VerticalLayoutGroup>();
            vlgPanel.padding = new RectOffset(20, 20, 16, 16);
            vlgPanel.spacing = 8;
            vlgPanel.childControlWidth = true;
            vlgPanel.childControlHeight = true;
            vlgPanel.childForceExpandWidth = true;
            vlgPanel.childForceExpandHeight = false;

            // --- BARRA SUPERIOR (Título de Videojuego + Botón Cerrar) ---
            GameObject barraSup = new GameObject("BarraSuperior");
            barraSup.transform.SetParent(panelHabilidades.transform, false);
            HorizontalLayoutGroup hlgSup = barraSup.AddComponent<HorizontalLayoutGroup>();
            hlgSup.childControlWidth = true;
            hlgSup.childControlHeight = true;
            hlgSup.childForceExpandWidth = false;

            LayoutElement leSup = barraSup.AddComponent<LayoutElement>();
            leSup.preferredHeight = 40;

            GameObject txtTituloObj = new GameObject("TituloPrincipal");
            txtTituloObj.transform.SetParent(barraSup.transform, false);
            Text txtTitulo = txtTituloObj.AddComponent<Text>();
            txtTitulo.font = fuenteUI;
            txtTitulo.fontSize = 20;
            txtTitulo.fontStyle = FontStyle.Bold;
            txtTitulo.color = new Color(0.92f, 0.84f, 1f);
            txtTitulo.text = "⚡ ARMERÍA TÁCTICA & ÁRBOL DE TALENTOS";

            LayoutElement leTitulo = txtTituloObj.AddComponent<LayoutElement>();
            leTitulo.flexibleWidth = 1f;

            Button btnCerrar = CrearBoton(barraSup.transform, "BtnCerrar", "✕", new Color(0.75f, 0.2f, 0.2f), fuenteUI);
            RectTransform rtCerrar = btnCerrar.GetComponent<RectTransform>();
            LayoutElement leCerrar = btnCerrar.gameObject.AddComponent<LayoutElement>();
            leCerrar.minWidth = 42;
            leCerrar.preferredWidth = 42;
            btnCerrar.onClick.AddListener(AlternarVisibilidadPanel);

            // --- BARRA DE ESTADO DEL JUGADOR & ACCIONES RÁPIDAS ---
            GameObject barraJugador = new GameObject("BarraJugador");
            barraJugador.transform.SetParent(panelHabilidades.transform, false);
            Image imgBarraJ = barraJugador.AddComponent<Image>();
            imgBarraJ.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

            HorizontalLayoutGroup hlgJ = barraJugador.AddComponent<HorizontalLayoutGroup>();
            hlgJ.padding = new RectOffset(14, 14, 6, 6);
            hlgJ.spacing = 12;
            hlgJ.childControlWidth = true;
            hlgJ.childControlHeight = true;
            hlgJ.childForceExpandWidth = false;

            LayoutElement leBarraJ = barraJugador.AddComponent<LayoutElement>();
            leBarraJ.preferredHeight = 42;

            GameObject txtJugObj = new GameObject("TextoJugador");
            txtJugObj.transform.SetParent(barraJugador.transform, false);
            textoInfoJugador = txtJugObj.AddComponent<Text>();
            textoInfoJugador.font = fuenteUI;
            textoInfoJugador.fontSize = 13;
            textoInfoJugador.color = Color.white;
            textoInfoJugador.alignment = TextAnchor.MiddleLeft;
            textoInfoJugador.supportRichText = true;

            GameObject lblProgresoObj = new GameObject("LblProgreso");
            lblProgresoObj.transform.SetParent(barraJugador.transform, false);
            txtProgresoArmeria = lblProgresoObj.AddComponent<Text>();
            txtProgresoArmeria.font = fuenteUI;
            txtProgresoArmeria.fontSize = 12;
            txtProgresoArmeria.color = new Color(0.7f, 0.85f, 1f, 0.95f);
            txtProgresoArmeria.alignment = TextAnchor.MiddleRight;
            txtProgresoArmeria.supportRichText = true;
            lblProgresoObj.AddComponent<LayoutElement>().preferredWidth = 230;

            Button btnReset = CrearBoton(barraJugador.transform, "BtnReset", "↺ Reset Puntos", new Color(0.55f, 0.25f, 0.25f), fuenteUI);
            btnReset.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            btnReset.onClick.AddListener(ReiniciarArbol);

            // --- BARRA DE FILTROS POR CATEGORÍA (PESTAÑAS) ---
            GameObject barraFiltros = new GameObject("BarraFiltrosCategorias");
            barraFiltros.transform.SetParent(panelHabilidades.transform, false);
            Image imgFiltros = barraFiltros.AddComponent<Image>();
            imgFiltros.color = new Color(0.07f, 0.09f, 0.14f, 0.95f);

            HorizontalLayoutGroup hlgFiltros = barraFiltros.AddComponent<HorizontalLayoutGroup>();
            hlgFiltros.padding = new RectOffset(8, 8, 4, 4);
            hlgFiltros.spacing = 8;
            hlgFiltros.childControlWidth = true;
            hlgFiltros.childControlHeight = true;
            hlgFiltros.childForceExpandWidth = true;

            LayoutElement leFiltros = barraFiltros.AddComponent<LayoutElement>();
            leFiltros.preferredHeight = 36;

            botonesFiltro.Clear();
            string[] cats = new string[] { "TODAS", "Ofensiva", "Control", "Defensiva", "Economía", "Definitiva" };
            string[] titulosCats = new string[] { "⭐ TODAS (15)", "⚔️ OFENSIVA", "❄️ CRIOGENIA", "🛡️ BLINDAJE", "💰 LOGÍSTICA", "⚡ SUPREMA" };

            for (int f = 0; f < cats.Length; f++)
            {
                string catId = cats[f];
                Button btnF = CrearBoton(barraFiltros.transform, $"BtnFiltro_{catId}", titulosCats[f], (catId == "TODAS") ? new Color(0.2f, 0.55f, 0.95f, 0.95f) : new Color(0.12f, 0.16f, 0.24f, 0.9f), fuenteUI);
                btnF.onClick.AddListener(() => FiltrarPorCategoria(catId));
                botonesFiltro[catId] = btnF;
            }

            // --- CUERPO PRINCIPAL (GRID SCROLLABLE EN 2 COLUMNAS) ---
            GameObject scrollObj = new GameObject("ScrollViewHabilidades");
            scrollObj.transform.SetParent(panelHabilidades.transform, false);
            LayoutElement leScroll = scrollObj.AddComponent<LayoutElement>();
            leScroll.preferredHeight = 490;
            leScroll.flexibleHeight = 1f;

            Image imgScroll = scrollObj.AddComponent<Image>();
            imgScroll.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 35f;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform rtViewport = viewport.AddComponent<RectTransform>();
            rtViewport.anchorMin = Vector2.zero;
            rtViewport.anchorMax = Vector2.one;
            rtViewport.offsetMin = Vector2.zero;
            rtViewport.offsetMax = new Vector2(-16, 0); // Deja espacio para el scrollbar vertical

            Image imgVp = viewport.AddComponent<Image>();
            imgVp.color = Color.white;
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Content con GridLayoutGroup en 2 columnas
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform rtContent = content.AddComponent<RectTransform>();
            rtContent.anchorMin = new Vector2(0, 1);
            rtContent.anchorMax = new Vector2(1, 1);
            rtContent.pivot = new Vector2(0.5f, 1);
            rtContent.sizeDelta = new Vector2(0, 0);

            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(488, 100);
            grid.spacing = new Vector2(14, 10);
            grid.padding = new RectOffset(6, 6, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = rtViewport;
            scrollRect.content = rtContent;
            contenedorTarjetasHabilidades = content.transform;

            // Barra de desplazamiento vertical estilizada (Sci-Fi Scrollbar)
            GameObject scrollbarObj = new GameObject("ScrollbarVertical");
            scrollbarObj.transform.SetParent(scrollObj.transform, false);
            RectTransform rtScrollbar = scrollbarObj.AddComponent<RectTransform>();
            rtScrollbar.anchorMin = new Vector2(1, 0);
            rtScrollbar.anchorMax = new Vector2(1, 1);
            rtScrollbar.pivot = new Vector2(1, 0.5f);
            rtScrollbar.sizeDelta = new Vector2(12, 0);
            rtScrollbar.anchoredPosition = Vector2.zero;

            Image imgSbTrack = scrollbarObj.AddComponent<Image>();
            imgSbTrack.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

            GameObject slidingArea = new GameObject("SlidingArea");
            slidingArea.transform.SetParent(scrollbarObj.transform, false);
            RectTransform rtSliding = slidingArea.AddComponent<RectTransform>();
            rtSliding.anchorMin = Vector2.zero;
            rtSliding.anchorMax = Vector2.one;
            rtSliding.sizeDelta = Vector2.zero;

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(slidingArea.transform, false);
            RectTransform rtHandle = handleObj.AddComponent<RectTransform>();
            rtHandle.sizeDelta = new Vector2(0, 40);

            Image imgHandle = handleObj.AddComponent<Image>();
            imgHandle.color = new Color(0.2f, 0.65f, 0.95f, 0.9f);

            Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.handleRect = rtHandle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // --- BARRA INFERIOR DE VIDEOJUEGO ---
            GameObject barraInferior = new GameObject("BarraInferiorTactica");
            barraInferior.transform.SetParent(panelHabilidades.transform, false);
            HorizontalLayoutGroup hlgInf = barraInferior.AddComponent<HorizontalLayoutGroup>();
            hlgInf.padding = new RectOffset(10, 10, 6, 6);
            hlgInf.spacing = 10;
            hlgInf.childControlWidth = true;
            hlgInf.childControlHeight = true;
            hlgInf.childForceExpandWidth = false;

            LayoutElement leInf = barraInferior.AddComponent<LayoutElement>();
            leInf.preferredHeight = 36;

            GameObject txtInfObj = new GameObject("TextoPie");
            txtInfObj.transform.SetParent(barraInferior.transform, false);
            Text txtPie = txtInfObj.AddComponent<Text>();
            txtPie.font = fuenteUI;
            txtPie.fontSize = 11;
            txtPie.color = new Color(0.6f, 0.75f, 0.85f);
            txtPie.alignment = TextAnchor.MiddleLeft;
            txtPie.text = "🛡️ <i>Las habilidades adquiridas se aplican instantáneamente en tiempo real a tus defensas y base.</i>";
            txtInfObj.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // --- PANELES ACADÉMICOS (OCULTOS POR DEFECTO PARA ENFOQUE 100% VIDEOJUEGO) ---
            GameObject panelAcademico = new GameObject("PanelAcademico");
            panelAcademico.transform.SetParent(panelHabilidades.transform, false);
            Image imgAcad = panelAcademico.AddComponent<Image>();
            imgAcad.color = new Color(0.11f, 0.14f, 0.2f, 0.95f);

            HorizontalLayoutGroup hlgAcad = panelAcademico.AddComponent<HorizontalLayoutGroup>();
            hlgAcad.padding = new RectOffset(10, 10, 8, 8);
            hlgAcad.spacing = 10;
            hlgAcad.childControlWidth = true;
            hlgAcad.childControlHeight = true;
            hlgAcad.childForceExpandWidth = false;

            LayoutElement leAcad = panelAcademico.AddComponent<LayoutElement>();
            leAcad.preferredHeight = 44;

            Button btnKahn = CrearBoton(panelAcademico.transform, "BtnKahn", "▶ Algoritmo Kahn (Orden Topológico)", new Color(0.2f, 0.55f, 0.85f), fuenteUI);
            btnKahn.gameObject.AddComponent<LayoutElement>().preferredWidth = 260;
            btnKahn.onClick.AddListener(EjecutarDemostracionKahn);

            Button btnMatriz = CrearBoton(panelAcademico.transform, "BtnMatriz", "📊 Matriz Adyacencia", new Color(0.65f, 0.45f, 0.15f), fuenteUI);
            btnMatriz.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            btnMatriz.onClick.AddListener(MostrarMatrizAdyacencia);

            // Controles de prueba de ciclo
            GameObject txtCicloObj = new GameObject("TxtCicloLabel");
            txtCicloObj.transform.SetParent(panelAcademico.transform, false);
            Text txtCicloLbl = txtCicloObj.AddComponent<Text>();
            txtCicloLbl.font = fuenteUI;
            txtCicloLbl.fontSize = 12;
            txtCicloLbl.alignment = TextAnchor.MiddleRight;
            txtCicloLbl.color = new Color(0.9f, 0.8f, 0.7f);
            txtCicloLbl.text = "🧪 Conectar:";
            txtCicloObj.AddComponent<LayoutElement>().preferredWidth = 80;

            dropdownOrigenCiclo = CrearDropdown(panelAcademico.transform, "DropdownOrigen", fuenteUI);
            dropdownOrigenCiclo.gameObject.AddComponent<LayoutElement>().preferredWidth = 145;

            GameObject txtFlechaObj = new GameObject("TxtFlecha");
            txtFlechaObj.transform.SetParent(panelAcademico.transform, false);
            Text txtFlecha = txtFlechaObj.AddComponent<Text>();
            txtFlecha.font = fuenteUI;
            txtFlecha.fontSize = 14;
            txtFlecha.fontStyle = FontStyle.Bold;
            txtFlecha.alignment = TextAnchor.MiddleCenter;
            txtFlecha.color = Color.cyan;
            txtFlecha.text = "➔";
            txtFlechaObj.AddComponent<LayoutElement>().preferredWidth = 20;

            dropdownDestinoCiclo = CrearDropdown(panelAcademico.transform, "DropdownDestino", fuenteUI);
            dropdownDestinoCiclo.gameObject.AddComponent<LayoutElement>().preferredWidth = 145;

            Button btnTestCiclo = CrearBoton(panelAcademico.transform, "BtnTestCiclo", "Probar Arista", new Color(0.65f, 0.25f, 0.45f), fuenteUI);
            btnTestCiclo.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;
            btnTestCiclo.onClick.AddListener(ProbarAgregarConexionCiclo);

            // Consola Feedback
            GameObject consolaObj = new GameObject("ConsolaFeedback");
            consolaObj.transform.SetParent(panelHabilidades.transform, false);
            Image imgConsola = consolaObj.AddComponent<Image>();
            imgConsola.color = new Color(0.04f, 0.05f, 0.08f, 0.98f);

            LayoutElement leConsola = consolaObj.AddComponent<LayoutElement>();
            leConsola.preferredHeight = 90;

            VerticalLayoutGroup vlgConsola = consolaObj.AddComponent<VerticalLayoutGroup>();
            vlgConsola.padding = new RectOffset(12, 12, 8, 8);
            vlgConsola.childControlWidth = true;
            vlgConsola.childControlHeight = true;

            GameObject txtConsolaObj = new GameObject("TextoConsola");
            txtConsolaObj.transform.SetParent(consolaObj.transform, false);
            textoConsolaFeedback = txtConsolaObj.AddComponent<Text>();
            textoConsolaFeedback.font = fuenteUI;
            textoConsolaFeedback.fontSize = 12;
            textoConsolaFeedback.color = new Color(0.4f, 0.9f, 0.6f);
            textoConsolaFeedback.alignment = TextAnchor.UpperLeft;
            textoConsolaFeedback.supportRichText = true;
            textoConsolaFeedback.text = "Sistema DAG activo.";

            // Ocultar por defecto para priorizar estética de videojuego
            panelAcademico.SetActive(false);
            consolaObj.SetActive(false);

            // Botón discreto en la barra inferior para sustentar o inspeccionar si el profesor lo requiere
            Button btnToggleInspector = CrearBoton(barraInferior.transform, "BtnToggleInspector", "🔬 Consola DAG", new Color(0.22f, 0.25f, 0.32f, 0.85f), fuenteUI);
            btnToggleInspector.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
            btnToggleInspector.onClick.AddListener(() =>
            {
                bool mostrar = !panelAcademico.activeSelf;
                panelAcademico.SetActive(mostrar);
                consolaObj.SetActive(mostrar);
                leScroll.preferredHeight = mostrar ? 380 : 520;
            });
        }

        private Button CrearBoton(Transform padre, string nombre, string textoBoton, Color colorFondo, Font fuente)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            Image img = go.AddComponent<Image>();
            img.color = colorFondo;

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = colorFondo * 1.15f;
            cb.pressedColor = colorFondo * 0.85f;
            btn.colors = cb;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(go.transform, false);
            Text text = textObj.AddComponent<Text>();
            text.font = fuente;
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = textoBoton;

            RectTransform rtText = textObj.GetComponent<RectTransform>();
            rtText.anchorMin = Vector2.zero;
            rtText.anchorMax = Vector2.one;
            rtText.offsetMin = Vector2.zero;
            rtText.offsetMax = Vector2.zero;

            return btn;
        }

        private Dropdown CrearDropdown(Transform padre, string nombre, Font fuente)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.3f);

            Dropdown dropdown = go.AddComponent<Dropdown>();

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(go.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.font = fuente;
            label.fontSize = 11;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;

            RectTransform rtLabel = labelObj.GetComponent<RectTransform>();
            rtLabel.anchorMin = Vector2.zero;
            rtLabel.anchorMax = Vector2.one;
            rtLabel.offsetMin = new Vector2(8, 2);
            rtLabel.offsetMax = new Vector2(-20, -2);

            dropdown.captionText = label;

            // Template mínimo para dropdown
            GameObject template = new GameObject("Template");
            template.transform.SetParent(go.transform, false);
            template.SetActive(false);
            Image imgT = template.AddComponent<Image>();
            imgT.color = new Color(0.15f, 0.18f, 0.25f, 0.98f);

            ScrollRect sr = template.AddComponent<ScrollRect>();
            sr.horizontal = false;

            RectTransform rtT = template.GetComponent<RectTransform>();
            rtT.anchorMin = new Vector2(0, 0);
            rtT.anchorMax = new Vector2(1, 0);
            rtT.pivot = new Vector2(0.5f, 1);
            rtT.sizeDelta = new Vector2(0, 150);

            GameObject vp = new GameObject("Viewport");
            vp.transform.SetParent(template.transform, false);
            vp.AddComponent<Image>();
            vp.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform rtVp = vp.GetComponent<RectTransform>();
            rtVp.anchorMin = Vector2.zero;
            rtVp.anchorMax = Vector2.one;
            rtVp.sizeDelta = Vector2.zero;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(vp.transform, false);
            RectTransform rtC = content.AddComponent<RectTransform>();
            rtC.anchorMin = new Vector2(0, 1);
            rtC.anchorMax = new Vector2(1, 1);
            rtC.pivot = new Vector2(0.5f, 1);
            rtC.sizeDelta = new Vector2(0, 24);

            GameObject item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            Toggle toggle = item.AddComponent<Toggle>();
            RectTransform rtItem = item.GetComponent<RectTransform>();
            rtItem.anchorMin = new Vector2(0, 0.5f);
            rtItem.anchorMax = new Vector2(1, 0.5f);
            rtItem.sizeDelta = new Vector2(0, 24);

            GameObject itemLabelObj = new GameObject("Item Label");
            itemLabelObj.transform.SetParent(item.transform, false);
            Text itemLabel = itemLabelObj.AddComponent<Text>();
            itemLabel.font = fuente;
            itemLabel.fontSize = 11;
            itemLabel.color = Color.white;
            itemLabel.alignment = TextAnchor.MiddleLeft;
            RectTransform rtItemLabel = itemLabelObj.GetComponent<RectTransform>();
            rtItemLabel.anchorMin = Vector2.zero;
            rtItemLabel.anchorMax = Vector2.one;
            rtItemLabel.offsetMin = new Vector2(8, 2);
            rtItemLabel.offsetMax = new Vector2(-8, -2);

            dropdown.template = rtT;
            dropdown.itemText = itemLabel;

            return dropdown;
        }

        #endregion
    }
}
