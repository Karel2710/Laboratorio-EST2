using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using GrafosEST2;
using TowerDefenseGameplay;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#else
using UnityEngine.EventSystems;
#endif

public class ControladorArbolUI : MonoBehaviour
{
    public static ControladorArbolUI Instance { get; private set; }

    public enum ModoRecorridoBST
    {
        InOrden,
        PreOrden,
        PostOrden
    }

    [Header("Instancia del Árbol")]
    public Arbol arbolJugadores = new Arbol();

    [Header("Modo de Recorrido Actual")]
    public ModoRecorridoBST modoActual = ModoRecorridoBST.InOrden;

    [Header("Campos de Entrada (InputFields)")]
    public InputField inputNombre;
    public InputField inputId;
    public InputField inputVida;
    public InputField inputBuscarId;

    [Header("Textos de Información y Métricas")]
    public Text textoFeedback;
    public Text textoVisualizacionArbol;
    public Text textoMetricasArbol;

    [Header("Botones de Recorrido BST")]
    public Button btnTabInOrden;
    public Button btnTabPreOrden;
    public Button btnTabPostOrden;

    [Header("Panel Principal")]
    public GameObject panelRegistro;
    [SerializeField] private bool abrirPanelAlIniciar = false;

    [Header("Auto-Generación de UI si no está asignada")]
    [SerializeField] private bool autoGenerarUISiFalta = true;

    [System.Serializable]
    private class DatosArbolJugadores
    {
        public List<Jugador> listaJugadores = new List<Jugador>();
        public bool modoAVL = false;
    }

    private bool arbolCargado = false;
    private ClaseComandante claseSeleccionada = ClaseComandante.Recluta;
    private Button btnClase;
    private GameObject panelVista;
    private RectTransform lienzoVista;
    private Text textoVistaMetricas;
    private Text textoBitacora;
    private Button btnAVL;
    private int idResaltado = -1;

    private string rutaArchivoArbol;
    private Color colorTabActivo = new Color(0.12f, 0.55f, 0.85f, 1f);
    private Color colorTabInactivo = new Color(0.12f, 0.16f, 0.24f, 0.95f);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        rutaArchivoArbol = System.IO.Path.Combine(Application.persistentDataPath, "jugadores_arbol.json");

        if (autoGenerarUISiFalta && (panelRegistro == null || inputNombre == null || inputId == null))
        {
            try
            {
                ConstruirUIAutomaticamente();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ControladorArbolUI] Error en ConstruirUIAutomaticamente: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    private void Start()
    {
        if (inputVida != null && string.IsNullOrEmpty(inputVida.text))
        {
            inputVida.text = "100";
        }

        if (panelRegistro != null)
        {
            panelRegistro.SetActive(abrirPanelAlIniciar);
        }

        AsegurarCargado();
        ActualizarEstilosTabs();
        ActualizarVisualizacionArbol();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Carga el árbol desde disco una sola vez (otros sistemas pueden pedirlo antes de Start).</summary>
    public void AsegurarCargado()
    {
        if (arbolCargado) return;
        arbolCargado = true;
        if (string.IsNullOrEmpty(rutaArchivoArbol))
            rutaArchivoArbol = System.IO.Path.Combine(Application.persistentDataPath, "jugadores_arbol.json");
        CargarArbolDesdeDisco();
    }

    /// <summary>
    /// Lee los campos de entrada, valida los datos e inserta el jugador en el árbol binario.
    /// </summary>
    public void RegistrarJugador()
    {
        if (inputNombre == null || inputId == null)
        {
            Debug.LogWarning("[ControladorArbolUI] Faltan referencias a los campos de entrada.");
            return;
        }

        string nombre = inputNombre.text.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            MostrarMensajeFeedback("⚠️ El nombre no puede estar vacío.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        if (!int.TryParse(inputId.text.Trim(), out int id))
        {
            MostrarMensajeFeedback("⚠️ El ID debe ser un número entero válido.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        if (id <= 0)
        {
            MostrarMensajeFeedback("⚠️ El ID debe ser mayor a 0.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        int vida = 100;
        if (inputVida != null && !string.IsNullOrEmpty(inputVida.text.Trim()))
        {
            if (!int.TryParse(inputVida.text.Trim(), out vida) || vida <= 0)
            {
                MostrarMensajeFeedback("⚠️ La vida debe ser un número positivo.", new Color(1f, 0.75f, 0.2f));
                return;
            }
        }

        Jugador nuevoJugador = new Jugador(vida, id, nombre, 1, 0);
        nuevoJugador.clase = claseSeleccionada;
        bool insertado = arbolJugadores.Insertar(nuevoJugador);

        if (insertado)
        {
            GuardarArbolEnDisco();

            // Si no había jugador activo en la partida, asignar este automáticamente
            if (GameManager.Instance != null && GameManager.Instance.jugadorActivo == null)
            {
                AsignarJugadorActivo(nuevoJugador);
            }

            MostrarMensajeFeedback($"✓ ¡Comandante '{nombre}' (ID: {id}) registrado con éxito!", new Color(0.2f, 0.95f, 0.45f));
            LimpiarInputsRegistro();
            ActualizarVisualizacionArbol();
        }
        else
        {
            MostrarMensajeFeedback($"✕ Error: Ya existe un comandante con el ID {id} en el BST.", new Color(1f, 0.35f, 0.35f));
        }
    }

    /// <summary>
    /// Busca un jugador en el árbol por el ID ingresado.
    /// </summary>
    public void BuscarJugador()
    {
        string textoId = string.Empty;

        if (inputBuscarId != null && !string.IsNullOrEmpty(inputBuscarId.text.Trim()))
        {
            textoId = inputBuscarId.text.Trim();
        }
        else if (inputId != null && !string.IsNullOrEmpty(inputId.text.Trim()))
        {
            textoId = inputId.text.Trim();
        }

        if (string.IsNullOrEmpty(textoId) || !int.TryParse(textoId, out int idABuscar))
        {
            MostrarMensajeFeedback("⚠️ Ingrese un ID numérico válido para buscar.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        Jugador encontrado = arbolJugadores.Buscar(idABuscar);
        if (encontrado != null)
        {
            int pasos = arbolJugadores.ProfundidadDe(idABuscar);
            MostrarMensajeFeedback($"✓ Localizado en {pasos} comparación(es): {encontrado.nombre} (ID: {encontrado.Id}, {encontrado.NombreClaseActual})", new Color(0.3f, 0.85f, 1f));
            idResaltado = idABuscar;
            RedibujarVistaGrafica();

            if (inputNombre != null) inputNombre.text = encontrado.nombre;
            if (inputId != null) inputId.text = encontrado.Id.ToString();
            if (inputVida != null) inputVida.text = encontrado.vida.ToString();
        }
        else
        {
            MostrarMensajeFeedback($"✕ No se encontró ningún comandante con ID: {idABuscar} en el BST.", new Color(1f, 0.35f, 0.35f));
        }
    }

    /// <summary>
    /// Asigna el jugador con el ID indicado en los campos de entrada como el comandante activo del juego.
    /// </summary>
    public void AsignarComandanteAlMando()
    {
        string textoId = string.Empty;
        if (inputId != null && !string.IsNullOrEmpty(inputId.text.Trim()))
        {
            textoId = inputId.text.Trim();
        }
        else if (inputBuscarId != null && !string.IsNullOrEmpty(inputBuscarId.text.Trim()))
        {
            textoId = inputBuscarId.text.Trim();
        }

        if (string.IsNullOrEmpty(textoId) || !int.TryParse(textoId, out int idABuscar))
        {
            MostrarMensajeFeedback("⚠️ Ingrese o busque un ID válido para asignar al mando.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        Jugador encontrado = arbolJugadores.Buscar(idABuscar);
        if (encontrado != null)
        {
            AsignarJugadorActivo(encontrado);
            MostrarMensajeFeedback($"🎯 ¡Comandante '{encontrado.nombre}' desplegado al Mando Táctico!", new Color(0.2f, 0.95f, 0.5f));
            ActualizarVisualizacionArbol();
        }
        else
        {
            MostrarMensajeFeedback($"✕ No se encontró ningún comandante con ID: {idABuscar}.", new Color(1f, 0.35f, 0.35f));
        }
    }

    private void AsignarJugadorActivo(Jugador j)
    {
        if (j == null) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AsignarComandante(j, false);
        }
        else if (ControladorHabilidadesUI.Instance != null)
        {
            ControladorHabilidadesUI.Instance.jugadorActivo = j;
            if (ControladorHabilidadesUI.Instance.sistemaHabilidades != null)
            {
                ControladorHabilidadesUI.Instance.sistemaHabilidades.SincronizarConJugador(j);
            }
            ControladorHabilidadesUI.Instance.ActualizarTodo();
        }

        if (HUDJuego.Instance != null)
        {
            HUDJuego.Instance.ActualizarHUD();
        }
    }

    /// <summary>
    /// Elimina un jugador del árbol según el ID especificado.
    /// </summary>
    public void EliminarJugador()
    {
        string textoId = string.Empty;

        if (inputBuscarId != null && !string.IsNullOrEmpty(inputBuscarId.text.Trim()))
        {
            textoId = inputBuscarId.text.Trim();
        }
        else if (inputId != null && !string.IsNullOrEmpty(inputId.text.Trim()))
        {
            textoId = inputId.text.Trim();
        }

        if (string.IsNullOrEmpty(textoId) || !int.TryParse(textoId, out int idAEliminar))
        {
            MostrarMensajeFeedback("⚠️ Ingrese un ID numérico válido para eliminar.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        bool eliminado = arbolJugadores.Eliminar(idAEliminar);
        if (eliminado)
        {
            GuardarArbolEnDisco();
            MostrarMensajeFeedback($"✓ Comandante con ID {idAEliminar} retirado del BST.", new Color(0.2f, 0.9f, 0.4f));
            LimpiarInputsRegistro();
            ActualizarVisualizacionArbol();
        }
        else
        {
            MostrarMensajeFeedback($"✕ No se pudo eliminar: El ID {idAEliminar} no existe.", new Color(1f, 0.35f, 0.35f));
        }
    }

    /// <summary>
    /// Cambia el modo de recorrido del árbol y actualiza la vista.
    /// </summary>
    public void CambiarModoRecorrido(ModoRecorridoBST nuevoModo)
    {
        modoActual = nuevoModo;
        ActualizarEstilosTabs();
        ActualizarVisualizacionArbol();
    }

    private void ActualizarEstilosTabs()
    {
        SetTabStyle(btnTabInOrden, modoActual == ModoRecorridoBST.InOrden);
        SetTabStyle(btnTabPreOrden, modoActual == ModoRecorridoBST.PreOrden);
        SetTabStyle(btnTabPostOrden, modoActual == ModoRecorridoBST.PostOrden);
    }

    private void SetTabStyle(Button btn, bool activo)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = activo ? colorTabActivo : colorTabInactivo;
        }
    }

    /// <summary>
    /// Guarda todos los nodos del árbol en un archivo JSON en disco para persistencia permanente.
    /// </summary>
    public void GuardarArbolEnDisco()
    {
        try
        {
            DatosArbolJugadores datos = new DatosArbolJugadores();
            // Pre-orden: al reinsertar en ese orden se reconstruye exactamente la misma forma del árbol
            datos.listaJugadores = arbolJugadores.PreOrden();
            datos.modoAVL = arbolJugadores.autoBalanceoAVL;

            string json = JsonUtility.ToJson(datos, true);
            System.IO.File.WriteAllText(rutaArchivoArbol, json);
            Debug.Log($"[ControladorArbolUI] Árbol guardado permanentemente en: {rutaArchivoArbol}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ControladorArbolUI] Error al guardar árbol en disco: {ex.Message}");
        }
    }

    /// <summary>
    /// Carga los datos guardados en disco y reconstruye el árbol binario.
    /// </summary>
    public void CargarArbolDesdeDisco()
    {
        try
        {
            if (System.IO.File.Exists(rutaArchivoArbol))
            {
                string json = System.IO.File.ReadAllText(rutaArchivoArbol);
                DatosArbolJugadores datos = JsonUtility.FromJson<DatosArbolJugadores>(json);

                if (datos != null && datos.listaJugadores != null && datos.listaJugadores.Count > 0)
                {
                    arbolJugadores.Limpiar();
                    arbolJugadores.autoBalanceoAVL = datos.modoAVL;
                    foreach (Jugador j in datos.listaJugadores)
                    {
                        if (j == null) continue;
                        if (j.habilidadesDesbloqueadas == null) j.habilidadesDesbloqueadas = new List<string>();
                        arbolJugadores.Insertar(j);
                    }
                    MostrarMensajeFeedback($"✓ BST cargado ({datos.listaJugadores.Count} comandantes en memoria).", new Color(0.2f, 0.9f, 0.4f));
                    ActualizarVisualizacionArbol();
                    return;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ControladorArbolUI] Error al cargar árbol desde disco: {ex.Message}");
        }

        MostrarMensajeFeedback("Terminal BST en línea. Ingrese datos para registrar un comandante.", new Color(0.3f, 0.85f, 1f));
        ActualizarVisualizacionArbol();
    }

    /// <summary>
    /// Actualiza el cuadro de texto y las métricas según el recorrido seleccionado.
    /// </summary>
    public void ActualizarVisualizacionArbol()
    {
        if (arbolJugadores == null) return;

        List<Jugador> jugadores;
        string tituloModo;

        switch (modoActual)
        {
            case ModoRecorridoBST.PreOrden:
                jugadores = arbolJugadores.PreOrden();
                tituloModo = "Pre-Orden (Raíz ➔ Izq ➔ Der)";
                break;
            case ModoRecorridoBST.PostOrden:
                jugadores = arbolJugadores.PostOrden();
                tituloModo = "Post-Orden (Izq ➔ Der ➔ Raíz)";
                break;
            case ModoRecorridoBST.InOrden:
            default:
                jugadores = arbolJugadores.InOrden();
                tituloModo = "In-Orden (Orden Ascendente por ID)";
                break;
        }

        int total = arbolJugadores.Contar();
        int altura = arbolJugadores.Altura();
        string raizId = arbolJugadores.root != null ? arbolJugadores.root.data.Id.ToString() : "Ninguna";

        if (textoMetricasArbol != null)
        {
            string tipoArbol = arbolJugadores.EsAVL() ? "<color=#44ff88>AVL ✓</color>" : "<color=#ff9966>desbalanceado</color>";
            textoMetricasArbol.text = $"📊 <b>Total:</b> <color=#00e5ff>{total}</color> | <b>Raíz:</b> <color=#ffd700>ID {raizId}</color> | <b>Altura:</b> <color=#44ff88>{altura}</color> | <b>FE raíz:</b> {arbolJugadores.FactorEquilibrio(arbolJugadores.root)} | {tipoArbol}";
        }

        RedibujarVistaGrafica();

        if (textoVisualizacionArbol == null) return;

        if (jugadores.Count == 0)
        {
            textoVisualizacionArbol.text = "<color=#94a3b8><i>El árbol BST de comandantes está vacío.\nUse el formulario superior para registrar el primer comandante.</i></color>";
            return;
        }

        Jugador comandanteActivo = GameManager.Instance != null ? GameManager.Instance.jugadorActivo : null;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<color=#66ddff><b>══ Recorrido {tituloModo} ══</b></color>");
        sb.AppendLine();

        for (int i = 0; i < jugadores.Count; i++)
        {
            Jugador j = jugadores[i];
            bool esActivo = comandanteActivo != null && comandanteActivo.Id == j.Id;
            bool esApoyo = GameManager.Instance != null && GameManager.Instance.jugadorApoyo != null && GameManager.Instance.jugadorApoyo.Id == j.Id;
            string tagActivo = esActivo ? "  <color=#44ff88><b>[★ EN MANDO TÁCTICO]</b></color>" : (esApoyo ? "  <color=#44eeff><b>[🤝 APOYO]</b></color>" : "");
            string numColor = esActivo ? "#44ff88" : "#00e5ff";

            sb.AppendLine($"<color={numColor}><b>#{i + 1:00}</b></color> <b>{j.nombre}</b> (ID: <color=#ffd700><b>{j.Id}</b></color>){tagActivo}");
            sb.AppendLine($"     <color=#c4b5fd>🎖 {j.NombreClaseActual}</color>  |  <color=#94a3b8>🏆 Mejor:</color> {j.mejorPuntaje} pts  |  <color=#94a3b8>Partidas:</color> {j.partidasJugadas}");
            sb.AppendLine($"     <color=#94a3b8>❤️ Vida:</color> {j.vida} PV  |  <color=#94a3b8>⭐ Nivel:</color> {j.nivel}  |  <color=#94a3b8>💎 Pts DAG:</color> {j.puntosHabilidad}  |  <color=#94a3b8>💰 Oro:</color> {j.oro}");
            sb.AppendLine();
        }

        textoVisualizacionArbol.text = sb.ToString();
    }

    public void LimpiarInputsRegistro()
    {
        if (inputNombre != null) inputNombre.text = string.Empty;
        if (inputId != null) inputId.text = string.Empty;
        if (inputVida != null) inputVida.text = "100";
        if (inputBuscarId != null) inputBuscarId.text = string.Empty;
    }

    public void AlternarVisibilidadPanel()
    {
        if (panelRegistro != null)
        {
            panelRegistro.SetActive(!panelRegistro.activeSelf);
            if (panelRegistro.activeSelf)
            {
                ActualizarVisualizacionArbol();
            }
        }
    }

    private void MostrarMensajeFeedback(string mensaje, Color color)
    {
        if (textoFeedback != null)
        {
            textoFeedback.text = mensaje;
            textoFeedback.color = color;
        }
        Debug.Log($"[ControladorArbolUI] {mensaje}");
    }

    #region Auto Construcción de UI

    /// <summary>
    /// Crea en tiempo de ejecución un Canvas interactivo con estética cyber-militar,
    /// pestañas de recorrido, formulario estilizado, scrollbar táctico y métricas de balance.
    /// Preserva además el banner de nivel superior.
    /// </summary>
    private void ConstruirUIAutomaticamente()
    {
        Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (fuente == null) fuente = Font.CreateDynamicFontFromOSFont("Arial", 14);

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

        // 2. Crear Canvas
        GameObject canvasObj = new GameObject("Canvas_JugadoresBST");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Botón flotante para abrir/cerrar el panel (tactical pill)
        Button botonToggle = CrearBoton(canvasObj.transform, "BotonTogglePanel", "👥 Mando BST", new Color(0.10f, 0.25f, 0.40f, 0.95f), fuente);
        RectTransform rtToggle = botonToggle.GetComponent<RectTransform>();
        rtToggle.anchorMin = new Vector2(1, 1);
        rtToggle.anchorMax = new Vector2(1, 1);
        rtToggle.pivot = new Vector2(1, 1);
        rtToggle.anchoredPosition = new Vector2(-20, -18);
        rtToggle.sizeDelta = new Vector2(145, 40);
        botonToggle.onClick.AddListener(AlternarVisibilidadPanel);
        // El HUD ya ofrece el botón "👥 Comandantes": el flotante se oculta para no tapar la barra superior
        botonToggle.gameObject.SetActive(false);

        Outline outToggle = botonToggle.gameObject.AddComponent<Outline>();
        outToggle.effectColor = new Color(0.0f, 0.9f, 1f, 0.45f);
        outToggle.effectDistance = new Vector2(1, -1);

        // 5. Panel Principal (Modal Cyber-Militar)
        panelRegistro = new GameObject("PanelRegistroJugadores");
        panelRegistro.transform.SetParent(canvasObj.transform, false);
        Image imgPanel = panelRegistro.AddComponent<Image>();
        imgPanel.color = new Color(0.07f, 0.09f, 0.14f, 0.97f);

        Outline outPanel = panelRegistro.AddComponent<Outline>();
        outPanel.effectColor = new Color(0.0f, 0.9f, 1f, 0.55f);
        outPanel.effectDistance = new Vector2(2, -2);

        RectTransform rtPanel = panelRegistro.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0, 1);
        rtPanel.anchorMax = new Vector2(0, 1);
        rtPanel.pivot = new Vector2(0, 1);
        rtPanel.anchoredPosition = new Vector2(20, -92);
        rtPanel.sizeDelta = new Vector2(500, 770);

        // 6. Encabezado Táctico
        GameObject headerObj = new GameObject("HeaderBST");
        headerObj.transform.SetParent(panelRegistro.transform, false);
        Image imgHeader = headerObj.AddComponent<Image>();
        imgHeader.color = new Color(0.04f, 0.06f, 0.10f, 0.98f);
        RectTransform rtHeader = headerObj.GetComponent<RectTransform>();
        rtHeader.anchorMin = new Vector2(0, 1);
        rtHeader.anchorMax = new Vector2(1, 1);
        rtHeader.pivot = new Vector2(0.5f, 1);
        rtHeader.sizeDelta = new Vector2(0, 56);

        Text titulo = CrearTexto(headerObj.transform, "Titulo", "👥 EXPEDIENTE DE COMANDANTES (BST)", 15, TextAnchor.MiddleLeft, new Color(0.0f, 0.9f, 1f), fuente, true);
        RectTransform rtTitulo = titulo.GetComponent<RectTransform>();
        rtTitulo.anchorMin = new Vector2(0, 0);
        rtTitulo.anchorMax = new Vector2(1, 1);
        rtTitulo.offsetMin = new Vector2(18, 16);
        rtTitulo.offsetMax = new Vector2(-50, 0);

        Text subtitulo = CrearTexto(headerObj.transform, "Subtitulo", "Binary Search Tree • Indexación y persistencia de perfiles", 10, TextAnchor.MiddleLeft, new Color(0.6f, 0.7f, 0.85f), fuente, false);
        RectTransform rtSub = subtitulo.GetComponent<RectTransform>();
        rtSub.anchorMin = new Vector2(0, 0);
        rtSub.anchorMax = new Vector2(1, 1);
        rtSub.offsetMin = new Vector2(18, 0);
        rtSub.offsetMax = new Vector2(-50, -18);

        Button btnCerrar = CrearBoton(headerObj.transform, "BtnCerrar", "✕", new Color(0.70f, 0.18f, 0.18f, 0.95f), fuente);
        SetRect(btnCerrar.GetComponent<RectTransform>(), 454, -12, 32, 32);
        btnCerrar.onClick.AddListener(AlternarVisibilidadPanel);

        panelRegistro.SetActive(abrirPanelAlIniciar);

        float yPos = -68;

        // Fila Nombre
        Text lblNombre = CrearTexto(panelRegistro.transform, "LblNombre", "👤 Nombre del Comandante:", 12, TextAnchor.MiddleLeft, new Color(0.85f, 0.90f, 0.98f), fuente, true);
        SetRect(lblNombre.GetComponent<RectTransform>(), 20, yPos, 460, 20);
        yPos -= 24;

        inputNombre = CrearCampoTexto(panelRegistro.transform, "InputNombre", "Ej: Coronel Reyes", fuente);
        SetRect(inputNombre.GetComponent<RectTransform>(), 20, yPos, 460, 34);
        yPos -= 42;

        // Fila ID y Vida (Horizontal)
        Text lblId = CrearTexto(panelRegistro.transform, "LblId", "🆔 ID Táctico (Clave BST):", 12, TextAnchor.MiddleLeft, new Color(0.85f, 0.90f, 0.98f), fuente, true);
        SetRect(lblId.GetComponent<RectTransform>(), 20, yPos, 220, 20);

        Text lblVida = CrearTexto(panelRegistro.transform, "LblVida", "❤️ Vida Inicial (PV):", 12, TextAnchor.MiddleLeft, new Color(0.85f, 0.90f, 0.98f), fuente, true);
        SetRect(lblVida.GetComponent<RectTransform>(), 260, yPos, 220, 20);
        yPos -= 24;

        inputId = CrearCampoTexto(panelRegistro.transform, "InputId", "Ej: 10", fuente);
        inputId.contentType = InputField.ContentType.IntegerNumber;
        SetRect(inputId.GetComponent<RectTransform>(), 20, yPos, 220, 34);

        inputVida = CrearCampoTexto(panelRegistro.transform, "InputVida", "100", fuente);
        inputVida.contentType = InputField.ContentType.IntegerNumber;
        SetRect(inputVida.GetComponent<RectTransform>(), 260, yPos, 220, 34);
        yPos -= 46;

        // Botones de acción principales (Fila 1)
        Button btnInsertar = CrearBoton(panelRegistro.transform, "BtnInsertar", "➕ Registrar en BST", new Color(0.10f, 0.52f, 0.32f), fuente);
        SetRect(btnInsertar.GetComponent<RectTransform>(), 20, yPos, 220, 36);
        btnInsertar.onClick.AddListener(RegistrarJugador);

        Button btnBuscar = CrearBoton(panelRegistro.transform, "BtnBuscar", "🔍 Buscar por ID", new Color(0.12f, 0.42f, 0.68f), fuente);
        SetRect(btnBuscar.GetComponent<RectTransform>(), 260, yPos, 220, 36);
        btnBuscar.onClick.AddListener(BuscarJugador);
        yPos -= 42;

        // Botones de acción (Fila 2: Eliminar, Limpiar, Asignar al Mando)
        Button btnEliminar = CrearBoton(panelRegistro.transform, "BtnEliminar", "🗑️ Eliminar ID", new Color(0.68f, 0.18f, 0.18f), fuente);
        SetRect(btnEliminar.GetComponent<RectTransform>(), 20, yPos, 140, 34);
        btnEliminar.onClick.AddListener(EliminarJugador);

        Button btnLimpiar = CrearBoton(panelRegistro.transform, "BtnLimpiar", "🧹 Limpiar", new Color(0.24f, 0.30f, 0.40f), fuente);
        SetRect(btnLimpiar.GetComponent<RectTransform>(), 170, yPos, 110, 34);
        btnLimpiar.onClick.AddListener(LimpiarInputsRegistro);

        Button btnAsignar = CrearBoton(panelRegistro.transform, "BtnAsignar", "🎯 Asignar al Mando", new Color(0.78f, 0.45f, 0.08f), fuente);
        SetRect(btnAsignar.GetComponent<RectTransform>(), 290, yPos, 190, 34);
        btnAsignar.onClick.AddListener(AsignarComandanteAlMando);
        yPos -= 40;

        // Fila 3: Especialización, Comandante de Apoyo y Vista Gráfica / AVL
        btnClase = CrearBoton(panelRegistro.transform, "BtnClase", "🎖 Clase: Recluta", new Color(0.36f, 0.25f, 0.62f), fuente);
        SetRect(btnClase.GetComponent<RectTransform>(), 20, yPos, 170, 34);
        btnClase.onClick.AddListener(CiclarClase);

        Button btnApoyo = CrearBoton(panelRegistro.transform, "BtnApoyo", "🤝 Asignar Apoyo", new Color(0.08f, 0.5f, 0.6f), fuente);
        SetRect(btnApoyo.GetComponent<RectTransform>(), 198, yPos, 130, 34);
        btnApoyo.onClick.AddListener(AsignarComandanteDeApoyo);

        Button btnVista = CrearBoton(panelRegistro.transform, "BtnVista", "🌳 Vista / AVL", new Color(0.15f, 0.5f, 0.3f), fuente);
        SetRect(btnVista.GetComponent<RectTransform>(), 336, yPos, 144, 34);
        btnVista.onClick.AddListener(AlternarVistaGrafica);
        yPos -= 44;

        // Barra de Feedback
        GameObject fondoFeedback = new GameObject("FondoFeedback");
        fondoFeedback.transform.SetParent(panelRegistro.transform, false);
        Image imgFb = fondoFeedback.AddComponent<Image>();
        imgFb.color = new Color(0.04f, 0.06f, 0.10f, 0.90f);
        SetRect(fondoFeedback.GetComponent<RectTransform>(), 20, yPos, 460, 32);

        textoFeedback = CrearTexto(fondoFeedback.transform, "TextoFeedback", "", 12, TextAnchor.MiddleCenter, Color.green, fuente, false);
        RectTransform rtFb = textoFeedback.GetComponent<RectTransform>();
        rtFb.anchorMin = Vector2.zero;
        rtFb.anchorMax = Vector2.one;
        rtFb.offsetMin = new Vector2(8, 2);
        rtFb.offsetMax = new Vector2(-8, -2);
        yPos -= 40;

        // Barra de Recorridos BST (Tabs: In-Orden, Pre-Orden, Post-Orden)
        Text lblTabs = CrearTexto(panelRegistro.transform, "LblTabs", "Modo de Recorrido BST:", 11, TextAnchor.MiddleLeft, new Color(0.70f, 0.80f, 0.95f), fuente, true);
        SetRect(lblTabs.GetComponent<RectTransform>(), 20, yPos, 460, 18);
        yPos -= 22;

        btnTabInOrden = CrearBoton(panelRegistro.transform, "TabInOrden", "🌿 In-Orden", colorTabActivo, fuente);
        SetRect(btnTabInOrden.GetComponent<RectTransform>(), 20, yPos, 146, 30);
        btnTabInOrden.onClick.AddListener(() => CambiarModoRecorrido(ModoRecorridoBST.InOrden));

        btnTabPreOrden = CrearBoton(panelRegistro.transform, "TabPreOrden", "🌲 Pre-Orden", colorTabInactivo, fuente);
        SetRect(btnTabPreOrden.GetComponent<RectTransform>(), 176, yPos, 146, 30);
        btnTabPreOrden.onClick.AddListener(() => CambiarModoRecorrido(ModoRecorridoBST.PreOrden));

        btnTabPostOrden = CrearBoton(panelRegistro.transform, "TabPostOrden", "🍂 Post-Orden", colorTabInactivo, fuente);
        SetRect(btnTabPostOrden.GetComponent<RectTransform>(), 332, yPos, 148, 30);
        btnTabPostOrden.onClick.AddListener(() => CambiarModoRecorrido(ModoRecorridoBST.PostOrden));
        yPos -= 36;

        // Métricas de Balance del Árbol (Banner Informativo)
        GameObject fondoMetricas = new GameObject("FondoMetricas");
        fondoMetricas.transform.SetParent(panelRegistro.transform, false);
        Image imgMetricas = fondoMetricas.AddComponent<Image>();
        imgMetricas.color = new Color(0.03f, 0.05f, 0.08f, 0.95f);
        SetRect(fondoMetricas.GetComponent<RectTransform>(), 20, yPos, 460, 26);

        textoMetricasArbol = CrearTexto(fondoMetricas.transform, "TextoMetricas", "📊 Total: 0 | Raíz BST: Ninguna | Niveles: 0", 11, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f), fuente, false);
        RectTransform rtMetricas = textoMetricasArbol.GetComponent<RectTransform>();
        rtMetricas.anchorMin = Vector2.zero;
        rtMetricas.anchorMax = Vector2.one;
        rtMetricas.offsetMin = Vector2.zero;
        rtMetricas.offsetMax = Vector2.zero;
        yPos -= 32;

        // Contenedor ScrollRect para Visualización del Árbol
        GameObject scrollObj = new GameObject("ScrollArbolJugadores", typeof(RectTransform));
        scrollObj.transform.SetParent(panelRegistro.transform, false);

        Image imgFondoScroll = scrollObj.AddComponent<Image>();
        imgFondoScroll.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);

        SetRect(scrollObj.GetComponent<RectTransform>(), 20, yPos, 460, 185);

        Outline outScroll = scrollObj.AddComponent<Outline>();
        outScroll.effectColor = new Color(0.15f, 0.25f, 0.35f, 0.6f);
        outScroll.effectDistance = new Vector2(1, -1);

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30f;

        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        RectTransform rtViewport = viewportObj.AddComponent<RectTransform>();
        rtViewport.anchorMin = Vector2.zero;
        rtViewport.anchorMax = Vector2.one;
        rtViewport.offsetMin = new Vector2(4, 4);
        rtViewport.offsetMax = new Vector2(-16, -4);

        Image imgMask = viewportObj.AddComponent<Image>();
        imgMask.color = Color.white;
        Mask mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform rtContent = contentObj.AddComponent<RectTransform>();
        rtContent.anchorMin = new Vector2(0, 1);
        rtContent.anchorMax = new Vector2(1, 1);
        rtContent.pivot = new Vector2(0.5f, 1);
        rtContent.sizeDelta = new Vector2(0, 300);

        VerticalLayoutGroup vlgContent = contentObj.AddComponent<VerticalLayoutGroup>();
        vlgContent.padding = new RectOffset(10, 10, 8, 8);
        vlgContent.childControlWidth = true;
        vlgContent.childControlHeight = true;
        vlgContent.childForceExpandWidth = true;
        vlgContent.childForceExpandHeight = false;

        ContentSizeFitter csfContent = contentObj.AddComponent<ContentSizeFitter>();
        csfContent.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        textoVisualizacionArbol = CrearTexto(contentObj.transform, "TextoVisualizacionArbol", "Cargando árbol BST...", 12, TextAnchor.UpperLeft, new Color(0.92f, 0.92f, 0.92f), fuente, false);
        textoVisualizacionArbol.supportRichText = true;

        // Scrollbar Vertical
        GameObject scrollbarObj = new GameObject("ScrollbarVertical");
        scrollbarObj.transform.SetParent(scrollObj.transform, false);
        RectTransform rtScrollbar = scrollbarObj.AddComponent<RectTransform>();
        rtScrollbar.anchorMin = new Vector2(1, 0);
        rtScrollbar.anchorMax = new Vector2(1, 1);
        rtScrollbar.pivot = new Vector2(1, 0.5f);
        rtScrollbar.anchoredPosition = new Vector2(-2, 0);
        rtScrollbar.sizeDelta = new Vector2(12, -8);

        Image imgSbTrack = scrollbarObj.AddComponent<Image>();
        imgSbTrack.color = new Color(0.08f, 0.12f, 0.18f, 0.8f);

        Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(scrollbarObj.transform, false);
        RectTransform rtHandle = handleObj.AddComponent<RectTransform>();
        rtHandle.sizeDelta = Vector2.zero;
        Image imgHandle = handleObj.AddComponent<Image>();
        imgHandle.color = new Color(0.0f, 0.8f, 0.95f, 0.85f);

        scrollbar.handleRect = rtHandle;
        scrollbar.targetGraphic = imgHandle;

        scrollRect.viewport = rtViewport;
        scrollRect.content = rtContent;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    #endregion

    #region Clases de Comandante, Escuadrón, Vista Gráfica y Balanceo AVL

    /// <summary>
    /// Cicla la especialización. Si el ID escrito ya existe en el árbol se cambia la clase de ese
    /// comandante; si no, la clase se aplicará al próximo comandante registrado.
    /// </summary>
    public void CiclarClase()
    {
        int n = System.Enum.GetValues(typeof(ClaseComandante)).Length;
        claseSeleccionada = (ClaseComandante)(((int)claseSeleccionada + 1) % n);
        if (btnClase != null) btnClase.GetComponentInChildren<Text>().text = "🎖 Clase: " + Jugador.NombreClase(claseSeleccionada);

        Jugador existente = null;
        if (inputId != null && int.TryParse(inputId.text.Trim(), out int id)) existente = arbolJugadores.Buscar(id);

        if (existente != null)
        {
            existente.clase = claseSeleccionada;
            GuardarArbolEnDisco();
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.jugadorActivo == existente) GameManager.Instance.AsignarComandante(existente, false);
                else if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();
            }
            MostrarMensajeFeedback($"🎖 {existente.nombre} ahora es {Jugador.NombreClase(claseSeleccionada)}: {Jugador.DescripcionClase(claseSeleccionada)}", new Color(0.8f, 0.7f, 1f));
            ActualizarVisualizacionArbol();
        }
        else
        {
            MostrarMensajeFeedback($"🎖 {Jugador.NombreClase(claseSeleccionada)}: {Jugador.DescripcionClase(claseSeleccionada)}", new Color(0.8f, 0.7f, 1f));
        }
    }

    public void AsignarComandanteDeApoyo()
    {
        string textoId = inputId != null && !string.IsNullOrEmpty(inputId.text.Trim()) ? inputId.text.Trim()
            : (inputBuscarId != null ? inputBuscarId.text.Trim() : string.Empty);

        if (!int.TryParse(textoId, out int id))
        {
            MostrarMensajeFeedback("⚠️ Escriba el ID del comandante que será el Apoyo del escuadrón.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        Jugador j = arbolJugadores.Buscar(id);
        if (j == null)
        {
            MostrarMensajeFeedback($"✕ No se encontró ningún comandante con ID: {id}.", new Color(1f, 0.35f, 0.35f));
            return;
        }
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.jugadorActivo != null && GameManager.Instance.jugadorActivo.Id == id)
        {
            MostrarMensajeFeedback("⚠️ Ese comandante ya es el Principal. Elija otro como Apoyo.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        GameManager.Instance.AsignarComandante(j, true);
        MostrarMensajeFeedback($"🤝 '{j.nombre}' ({j.NombreClaseActual}) es ahora el Comandante de Apoyo.", new Color(0.3f, 0.9f, 1f));
        ActualizarVisualizacionArbol();
    }

    public void AlternarVistaGrafica()
    {
        if (panelVista == null) ConstruirVistaGrafica();
        panelVista.SetActive(!panelVista.activeSelf);
        if (panelVista.activeSelf) RedibujarVistaGrafica();
    }

    private void ConstruirVistaGrafica()
    {
        Transform canvas = panelRegistro.transform.parent;
        RectTransform rt = UIFabrica.CrearPanel(canvas, "PanelVistaArbol", Paleta.FondoPanel, new Color(0.2f, 0.95f, 0.55f, 0.6f));
        UIFabrica.Colocar(rt, 535, -92, 980, 770);
        panelVista = rt.gameObject;
        UIFabrica.CrearEncabezado(rt, "🌳 VISOR JERÁRQUICO DEL ÁRBOL — BST vs AVL", new Color(0.4f, 1f, 0.65f), () => panelVista.SetActive(false));

        btnAVL = UIFabrica.CrearBoton(rt, "BtnAVL", "", new Color(0.2f, 0.3f, 0.4f), 13);
        UIFabrica.Colocar(btnAVL, 16, -56, 230, 36);
        btnAVL.onClick.AddListener(() =>
        {
            arbolJugadores.autoBalanceoAVL = !arbolJugadores.autoBalanceoAVL;
            GuardarArbolEnDisco();
            MostrarMensajeFeedback(arbolJugadores.autoBalanceoAVL
                ? "⚖️ Auto-balanceo AVL ACTIVADO: cada inserción/eliminación rota el árbol si |FE| > 1."
                : "⚖️ Auto-balanceo desactivado: el árbol se comporta como BST clásico.", new Color(0.4f, 1f, 0.65f));
            ActualizarVisualizacionArbol();
        });

        Button btnBalancear = UIFabrica.CrearBoton(rt, "BtnBalancear", "↻ Balancear ahora (rotaciones AVL)", new Color(0.12f, 0.5f, 0.35f), 13);
        UIFabrica.Colocar(btnBalancear, 254, -56, 270, 36);
        btnBalancear.onClick.AddListener(() =>
        {
            int alturaAntes = arbolJugadores.Altura();
            int rot = arbolJugadores.BalancearComoAVL();
            GuardarArbolEnDisco();
            MostrarMensajeFeedback($"↻ Árbol balanceado con {rot} rotación(es): altura {alturaAntes} → {arbolJugadores.Altura()}.", new Color(0.4f, 1f, 0.65f));
            ActualizarVisualizacionArbol();
        });

        Button btnDemo = UIFabrica.CrearBoton(rt, "BtnDemo", "🧪 Insertar 7 cadetes en orden", new Color(0.5f, 0.38f, 0.12f), 13);
        UIFabrica.Colocar(btnDemo, 532, -56, 240, 36);
        btnDemo.onClick.AddListener(InsertarCadetesDemo);

        Button btnQuitar = UIFabrica.CrearBoton(rt, "BtnQuitarDemo", "🗑 Quitar cadetes", new Color(0.5f, 0.2f, 0.2f), 13);
        UIFabrica.Colocar(btnQuitar, 780, -56, 184, 36);
        btnQuitar.onClick.AddListener(QuitarCadetesDemo);

        textoVistaMetricas = UIFabrica.CrearTexto(rt, "Metricas", "", 13, TextAnchor.MiddleLeft, new Color(0.9f, 0.95f, 1f));
        UIFabrica.Colocar(textoVistaMetricas, 18, -98, 946, 44);

        RectTransform fondo = UIFabrica.CrearPanel(rt, "Lienzo", new Color(0.03f, 0.05f, 0.08f, 1f), new Color(0.15f, 0.3f, 0.4f, 0.6f));
        UIFabrica.Colocar(fondo, 16, -146, 948, 480);
        fondo.gameObject.AddComponent<RectMask2D>();
        lienzoVista = fondo;

        Text leyenda = UIFabrica.CrearTexto(rt, "Leyenda",
            "<color=#ffd700>● Raíz</color>   <color=#44ff88>● En mando</color>   <color=#44eeff>● Apoyo</color>   <color=#ff6666>● |FE| > 1 (desbalanceado)</color>   <color=#ffffff>● Resultado de búsqueda</color>   —   FE = altura(izq) − altura(der)",
            12, TextAnchor.MiddleLeft, new Color(0.7f, 0.78f, 0.88f));
        UIFabrica.Colocar(leyenda, 18, -630, 946, 22);

        RectTransform fondoBit = UIFabrica.CrearPanel(rt, "FondoBitacora", Paleta.FondoOscuro);
        UIFabrica.Colocar(fondoBit, 16, -656, 948, 100);
        textoBitacora = UIFabrica.CrearTexto(fondoBit, "Bitacora", "", 12, TextAnchor.UpperLeft, new Color(0.75f, 0.95f, 0.8f));
        UIFabrica.Estirar(textoBitacora, 10, 6, 10, 6);

        panelVista.SetActive(false);
    }

    private void InsertarCadetesDemo()
    {
        int maxId = 0;
        foreach (Jugador j in arbolJugadores.InOrden()) maxId = Mathf.Max(maxId, j.Id);
        int rotAntes = arbolJugadores.rotacionesRealizadas;
        for (int i = 1; i <= 7; i++)
        {
            arbolJugadores.Insertar(new Jugador(100, maxId + i, "Cadete_" + (maxId + i), 1, 0));
        }
        GuardarArbolEnDisco();
        int rot = arbolJugadores.rotacionesRealizadas - rotAntes;
        MostrarMensajeFeedback(arbolJugadores.autoBalanceoAVL
            ? $"🧪 7 IDs consecutivos insertados con AVL: {rot} rotación(es) mantuvieron el árbol balanceado."
            : "🧪 7 IDs consecutivos insertados en BST: el árbol degenera en una lista (altura máxima).", new Color(1f, 0.85f, 0.4f));
        ActualizarVisualizacionArbol();
    }

    private void QuitarCadetesDemo()
    {
        int quitados = 0;
        foreach (Jugador j in arbolJugadores.InOrden())
        {
            if (j.nombre != null && j.nombre.StartsWith("Cadete_"))
            {
                bool enUso = GameManager.Instance != null && (GameManager.Instance.jugadorActivo == j || GameManager.Instance.jugadorApoyo == j);
                if (!enUso && arbolJugadores.Eliminar(j.Id)) quitados++;
            }
        }
        GuardarArbolEnDisco();
        MostrarMensajeFeedback($"🗑 {quitados} cadete(s) de demostración retirados del árbol.", new Color(0.3f, 0.85f, 1f));
        ActualizarVisualizacionArbol();
    }

    /// <summary>
    /// Dibuja el árbol como círculos unidos por líneas. X = posición in-orden, Y = profundidad.
    /// </summary>
    private void RedibujarVistaGrafica()
    {
        if (panelVista == null || !panelVista.activeSelf || lienzoVista == null) return;

        for (int i = lienzoVista.childCount - 1; i >= 0; i--) Destroy(lienzoVista.GetChild(i).gameObject);

        if (btnAVL != null)
        {
            btnAVL.GetComponentInChildren<Text>().text = arbolJugadores.autoBalanceoAVL ? "⚖️ Auto-Balanceo AVL: ACTIVADO" : "⚖️ Auto-Balanceo AVL: DESACTIVADO";
            btnAVL.GetComponent<Image>().color = arbolJugadores.autoBalanceoAVL ? new Color(0.1f, 0.6f, 0.4f) : new Color(0.25f, 0.3f, 0.4f);
        }

        int total = arbolJugadores.Contar();
        int altura = arbolJugadores.Altura();
        int alturaOptima = total > 0 ? Mathf.CeilToInt(Mathf.Log(total + 1, 2f)) : 0;
        if (textoVistaMetricas != null)
        {
            string estado = arbolJugadores.EsAVL() ? "<color=#44ff88><b>Balanceado (AVL)</b></color>" : "<color=#ff6666><b>Desbalanceado</b></color>";
            textoVistaMetricas.text =
                $"Nodos: <b>{total}</b>   Altura actual: <b>{altura}</b>   Altura mínima posible: <b>{alturaOptima}</b>   Estado: {estado}   Rotaciones acumuladas: <b>{arbolJugadores.rotacionesRealizadas}</b>\n" +
                $"<color=#94a3b8>Búsqueda en el peor caso: {altura} comparación(es). En un AVL es O(log n); en un BST degenerado es O(n).</color>";
        }
        if (textoBitacora != null)
        {
            StringBuilder sb = new StringBuilder("<b>Bitácora de rotaciones:</b>\n");
            List<string> bit = arbolJugadores.bitacoraRotaciones;
            if (bit.Count == 0) sb.Append("<color=#94a3b8>Sin rotaciones todavía. Active el auto-balanceo o pulse \"Balancear ahora\".</color>");
            for (int i = Mathf.Max(0, bit.Count - 5); i < bit.Count; i++) sb.Append("• ").Append(bit[i]).Append("\n");
            textoBitacora.text = sb.ToString();
        }

        if (arbolJugadores.root == null)
        {
            Text vacio = UIFabrica.CrearTexto(lienzoVista, "Vacio", "El árbol está vacío. Registre un comandante.", 16, TextAnchor.MiddleCenter, new Color(0.6f, 0.68f, 0.78f));
            UIFabrica.Estirar(vacio);
            return;
        }

        float ancho = 948f, alto = 480f;
        float radio = total > 24 ? 15f : (total > 12 ? 20f : 26f);
        float pasoX = total > 1 ? (ancho - 2f * (radio + 14f)) / (total - 1) : 0f;
        float pasoY = altura > 1 ? Mathf.Min(86f, (alto - 2f * (radio + 22f)) / (altura - 1)) : 0f;

        int contador = 0;
        Dictionary<Nodo, Vector2> posiciones = new Dictionary<Nodo, Vector2>();
        CalcularPosiciones(arbolJugadores.root, 0, ref contador, posiciones, radio + 14f, alto - radio - 22f, pasoX, pasoY, total == 1 ? ancho * 0.5f : -1f);

        // Primero las líneas (quedan detrás de los nodos)
        foreach (KeyValuePair<Nodo, Vector2> kv in posiciones)
        {
            if (kv.Key.left != null) UIFabrica.CrearLineaUI(lienzoVista, kv.Value, posiciones[kv.Key.left], new Color(0.35f, 0.75f, 1f, 0.8f), 2.5f);
            if (kv.Key.right != null) UIFabrica.CrearLineaUI(lienzoVista, kv.Value, posiciones[kv.Key.right], new Color(1f, 0.7f, 0.35f, 0.8f), 2.5f);
        }

        Jugador activo = GameManager.Instance != null ? GameManager.Instance.jugadorActivo : null;
        Jugador apoyo = GameManager.Instance != null ? GameManager.Instance.jugadorApoyo : null;

        foreach (KeyValuePair<Nodo, Vector2> kv in posiciones)
        {
            Nodo n = kv.Key;
            int fe = arbolJugadores.FactorEquilibrio(n);
            Color color = new Color(0.15f, 0.4f, 0.65f);
            if (n == arbolJugadores.root) color = new Color(0.75f, 0.58f, 0.08f);
            if (apoyo != null && apoyo.Id == n.data.Id) color = new Color(0.1f, 0.6f, 0.7f);
            if (activo != null && activo.Id == n.data.Id) color = new Color(0.12f, 0.62f, 0.3f);
            if (Mathf.Abs(fe) > 1) color = new Color(0.75f, 0.2f, 0.2f);

            Image bordeImg = UIFabrica.CrearImagen(lienzoVista, "Borde", GraficosProc.Circulo, n.data.Id == idResaltado ? Color.white : new Color(0.02f, 0.03f, 0.05f));
            RectTransform rb = bordeImg.rectTransform;
            rb.anchorMin = rb.anchorMax = Vector2.zero;
            rb.pivot = new Vector2(0.5f, 0.5f);
            rb.anchoredPosition = kv.Value;
            rb.sizeDelta = Vector2.one * (radio * 2f + (n.data.Id == idResaltado ? 10f : 4f));

            Image circulo = UIFabrica.CrearImagen(lienzoVista, "Nodo", GraficosProc.Circulo, color);
            RectTransform rc = circulo.rectTransform;
            rc.anchorMin = rc.anchorMax = Vector2.zero;
            rc.pivot = new Vector2(0.5f, 0.5f);
            rc.anchoredPosition = kv.Value;
            rc.sizeDelta = Vector2.one * radio * 2f;

            Text id = UIFabrica.CrearTexto(circulo.transform, "Id", n.data.Id.ToString(), radio > 20f ? 15 : 11, TextAnchor.MiddleCenter, Color.white, true);
            UIFabrica.Estirar(id);

            string nombre = n.data.nombre ?? "";
            if (nombre.Length > 11) nombre = nombre.Substring(0, 10) + "…";
            string rol = n == arbolJugadores.root ? "Raíz" : "";
            Text etiqueta = UIFabrica.CrearTexto(lienzoVista, "Etiqueta", $"{nombre}\n<color=#ffe08a>FE {(fe > 0 ? "+" : "")}{fe}</color> {rol}", 10, TextAnchor.UpperCenter, new Color(0.85f, 0.9f, 0.97f));
            RectTransform re = etiqueta.rectTransform;
            re.anchorMin = re.anchorMax = Vector2.zero;
            re.pivot = new Vector2(0.5f, 1f);
            re.anchoredPosition = kv.Value + new Vector2(0, -radio - 1f);
            re.sizeDelta = new Vector2(96, 28);
        }
    }

    private void CalcularPosiciones(Nodo n, int profundidad, ref int contador, Dictionary<Nodo, Vector2> pos,
        float margenX, float yTope, float pasoX, float pasoY, float xUnico)
    {
        if (n == null) return;
        CalcularPosiciones(n.left, profundidad + 1, ref contador, pos, margenX, yTope, pasoX, pasoY, xUnico);
        float x = xUnico >= 0f ? xUnico : margenX + contador * pasoX;
        pos[n] = new Vector2(x, yTope - profundidad * pasoY);
        contador++;
        CalcularPosiciones(n.right, profundidad + 1, ref contador, pos, margenX, yTope, pasoX, pasoY, xUnico);
    }

    #endregion

    #region Utilidades de UI

    private void SetRect(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private Text CrearTexto(Transform padre, string nombre, string contenido, int tamano, TextAnchor alineacion, Color color, Font fuente, bool negrita)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        Text t = go.AddComponent<Text>();
        t.text = contenido;
        t.font = fuente;
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
        return t;
    }

    private InputField CrearCampoTexto(Transform padre, string nombre, string placeholderTexto, Font fuente)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.11f, 0.15f, 0.22f, 1f);

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.35f, 0.5f, 0.5f);
        outline.effectDistance = new Vector2(1, -1);

        InputField input = go.AddComponent<InputField>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(go.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.font = fuente;
        text.fontSize = 13;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        RectTransform rtText = textObj.GetComponent<RectTransform>();
        rtText.anchorMin = Vector2.zero;
        rtText.anchorMax = Vector2.one;
        rtText.offsetMin = new Vector2(10, 2);
        rtText.offsetMax = new Vector2(-10, -2);

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(go.transform, false);
        Text ph = phObj.AddComponent<Text>();
        ph.font = fuente;
        ph.fontSize = 13;
        ph.color = new Color(0.55f, 0.65f, 0.78f, 0.5f);
        ph.text = placeholderTexto;
        ph.alignment = TextAnchor.MiddleLeft;

        RectTransform rtPh = phObj.GetComponent<RectTransform>();
        rtPh.anchorMin = Vector2.zero;
        rtPh.anchorMax = Vector2.one;
        rtPh.offsetMin = new Vector2(10, 2);
        rtPh.offsetMax = new Vector2(-10, -2);

        input.textComponent = text;
        input.placeholder = ph;

        return input;
    }

    private Button CrearBoton(Transform padre, string nombre, string textoBoton, Color colorFondo, Font fuente)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = colorFondo;

        Button btn = go.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.highlightedColor = colorFondo * 1.25f;
        cb.pressedColor = colorFondo * 0.80f;
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

    #endregion
}
