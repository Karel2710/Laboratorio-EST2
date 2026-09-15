using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#else
using UnityEngine.EventSystems;
#endif

public class ControladorArbolUI : MonoBehaviour
{
    [Header("Instancia del Árbol")]
    public Arbol arbolJugadores = new Arbol();

    [Header("Campos de Entrada (InputFields)")]
    public InputField inputNombre;
    public InputField inputId;
    public InputField inputVida;
    public InputField inputBuscarId;

    [Header("Textos de Información")]
    public Text textoFeedback;
    public Text textoVisualizacionArbol;

    [Header("Panel Principal")]
    public GameObject panelRegistro;
    [SerializeField] private bool abrirPanelAlIniciar = false;

    [Header("Auto-Generación de UI si no está asignada")]
    [SerializeField] private bool autoGenerarUISiFalta = true;

    private void Awake()
    {
        if (autoGenerarUISiFalta && (panelRegistro == null || inputNombre == null || inputId == null))
        {
            ConstruirUIAutomaticamente();
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

        MostrarMensajeFeedback("Sistema de Árbol BST listo. Ingrese los datos del jugador.", new Color(0.2f, 0.9f, 0.4f));
        ActualizarVisualizacionArbol();
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
            MostrarMensajeFeedback("El nombre no puede estar vacío.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        if (!int.TryParse(inputId.text.Trim(), out int id))
        {
            MostrarMensajeFeedback("El ID debe ser un número entero válido.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        if (id <= 0)
        {
            MostrarMensajeFeedback("El ID debe ser mayor a 0.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        int vida = 100;
        if (inputVida != null && !string.IsNullOrEmpty(inputVida.text.Trim()))
        {
            if (!int.TryParse(inputVida.text.Trim(), out vida) || vida <= 0)
            {
                MostrarMensajeFeedback("La vida debe ser un número positivo.", new Color(1f, 0.75f, 0.2f));
                return;
            }
        }

        Jugador nuevoJugador = new Jugador(vida, id, nombre);
        bool insertado = arbolJugadores.Insertar(nuevoJugador);

        if (insertado)
        {
            MostrarMensajeFeedback($"¡Jugador '{nombre}' (ID: {id}, Vida: {vida}) insertado con éxito!", new Color(0.2f, 0.9f, 0.4f));
            LimpiarInputsRegistro();
            ActualizarVisualizacionArbol();
        }
        else
        {
            MostrarMensajeFeedback($"Error: Ya existe un jugador en el árbol con el ID {id}.", new Color(1f, 0.35f, 0.35f));
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
            MostrarMensajeFeedback("Ingrese un ID numérico válido para buscar.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        Jugador encontrado = arbolJugadores.Buscar(idABuscar);
        if (encontrado != null)
        {
            MostrarMensajeFeedback($"Encontrado: {encontrado}", new Color(0.3f, 0.8f, 1f));

            if (inputNombre != null) inputNombre.text = encontrado.nombre;
            if (inputId != null) inputId.text = encontrado.Id.ToString();
            if (inputVida != null) inputVida.text = encontrado.vida.ToString();
        }
        else
        {
            MostrarMensajeFeedback($"No se encontró ningún jugador con ID: {idABuscar} en el árbol.", new Color(1f, 0.35f, 0.35f));
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
            MostrarMensajeFeedback("Ingrese un ID numérico válido para eliminar.", new Color(1f, 0.75f, 0.2f));
            return;
        }

        bool eliminado = arbolJugadores.Eliminar(idAEliminar);
        if (eliminado)
        {
            MostrarMensajeFeedback($"Jugador con ID {idAEliminar} eliminado del árbol.", new Color(0.2f, 0.9f, 0.4f));
            LimpiarInputsRegistro();
            ActualizarVisualizacionArbol();
        }
        else
        {
            MostrarMensajeFeedback($"No se pudo eliminar: El ID {idAEliminar} no existe.", new Color(1f, 0.35f, 0.35f));
        }
    }

    /// <summary>
    /// Actualiza el cuadro de texto que muestra el recorrido In-Orden del árbol.
    /// </summary>
    public void ActualizarVisualizacionArbol()
    {
        if (textoVisualizacionArbol == null) return;

        List<Jugador> jugadoresEnOrden = arbolJugadores.InOrden();

        if (jugadoresEnOrden.Count == 0)
        {
            textoVisualizacionArbol.text = "El árbol de jugadores está vacío.";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- Árbol BST de Jugadores (Total: {jugadoresEnOrden.Count}) ---");
        for (int i = 0; i < jugadoresEnOrden.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {jugadoresEnOrden[i]}");
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
    /// Crea en tiempo de ejecución un Canvas interactivo con el panel de registro,
    /// campos de texto (InputFields), botones y visualizador del árbol si no están creados.
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
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Botón flotante para abrir/cerrar el panel
        Button botonToggle = CrearBoton(canvasObj.transform, "BotonTogglePanel", "👥 Árbol Jugadores", new Color(0.15f, 0.45f, 0.75f, 0.95f), fuente);
        RectTransform rtToggle = botonToggle.GetComponent<RectTransform>();
        rtToggle.anchorMin = new Vector2(1, 1);
        rtToggle.anchorMax = new Vector2(1, 1);
        rtToggle.pivot = new Vector2(1, 1);
        rtToggle.anchoredPosition = new Vector2(-20, -20);
        rtToggle.sizeDelta = new Vector2(170, 42);
        botonToggle.onClick.AddListener(AlternarVisibilidadPanel);

        // 4. Panel Principal
        panelRegistro = new GameObject("PanelRegistroJugadores");
        panelRegistro.transform.SetParent(canvasObj.transform, false);
        Image imgPanel = panelRegistro.AddComponent<Image>();
        imgPanel.color = new Color(0.10f, 0.12f, 0.18f, 0.94f);

        RectTransform rtPanel = panelRegistro.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0, 1);
        rtPanel.anchorMax = new Vector2(0, 1);
        rtPanel.pivot = new Vector2(0, 1);
        rtPanel.anchoredPosition = new Vector2(25, -25);
        rtPanel.sizeDelta = new Vector2(440, 680);

        // 5. Título y Botón Cerrar
        Text titulo = CrearTexto(panelRegistro.transform, "Titulo", "GESTIÓN DE JUGADORES (BST)", 16, TextAnchor.MiddleCenter, new Color(0.35f, 0.85f, 1f), fuente, true);
        RectTransform rtTitulo = titulo.GetComponent<RectTransform>();
        rtTitulo.anchorMin = new Vector2(0, 1);
        rtTitulo.anchorMax = new Vector2(1, 1);
        rtTitulo.pivot = new Vector2(0.5f, 1);
        rtTitulo.anchoredPosition = new Vector2(0, -15);
        rtTitulo.sizeDelta = new Vector2(0, 35);

        Button btnCerrar = CrearBoton(panelRegistro.transform, "BtnCerrar", "✕", new Color(0.75f, 0.25f, 0.25f, 0.95f), fuente);
        SetRect(btnCerrar.GetComponent<RectTransform>(), 395, -12, 32, 32);
        btnCerrar.onClick.AddListener(AlternarVisibilidadPanel);

        panelRegistro.SetActive(abrirPanelAlIniciar);

        float yPos = -60;

        // Fila Nombre
        Text lblNombre = CrearTexto(panelRegistro.transform, "LblNombre", "Nombre del Jugador:", 13, TextAnchor.MiddleLeft, Color.white, fuente, false);
        SetRect(lblNombre.GetComponent<RectTransform>(), 20, yPos, 400, 22);
        yPos -= 26;

        inputNombre = CrearCampoTexto(panelRegistro.transform, "InputNombre", "Ej: Carlos", fuente);
        SetRect(inputNombre.GetComponent<RectTransform>(), 20, yPos, 400, 36);
        yPos -= 45;

        // Fila ID
        Text lblId = CrearTexto(panelRegistro.transform, "LblId", "ID del Jugador (Entero positivo):", 13, TextAnchor.MiddleLeft, Color.white, fuente, false);
        SetRect(lblId.GetComponent<RectTransform>(), 20, yPos, 400, 22);
        yPos -= 26;

        inputId = CrearCampoTexto(panelRegistro.transform, "InputId", "Ej: 10", fuente);
        inputId.contentType = InputField.ContentType.IntegerNumber;
        SetRect(inputId.GetComponent<RectTransform>(), 20, yPos, 400, 36);
        yPos -= 45;

        // Fila Vida
        Text lblVida = CrearTexto(panelRegistro.transform, "LblVida", "Vida inicial (Opcional):", 13, TextAnchor.MiddleLeft, Color.white, fuente, false);
        SetRect(lblVida.GetComponent<RectTransform>(), 20, yPos, 400, 22);
        yPos -= 26;

        inputVida = CrearCampoTexto(panelRegistro.transform, "InputVida", "100", fuente);
        inputVida.contentType = InputField.ContentType.IntegerNumber;
        SetRect(inputVida.GetComponent<RectTransform>(), 20, yPos, 400, 36);
        yPos -= 48;

        // Botones de acción principales
        Button btnInsertar = CrearBoton(panelRegistro.transform, "BtnInsertar", "➕ Insertar en Árbol", new Color(0.18f, 0.65f, 0.35f), fuente);
        SetRect(btnInsertar.GetComponent<RectTransform>(), 20, yPos, 195, 38);
        btnInsertar.onClick.AddListener(RegistrarJugador);

        Button btnBuscar = CrearBoton(panelRegistro.transform, "BtnBuscar", "🔍 Buscar por ID", new Color(0.20f, 0.50f, 0.85f), fuente);
        SetRect(btnBuscar.GetComponent<RectTransform>(), 225, yPos, 195, 38);
        btnBuscar.onClick.AddListener(BuscarJugador);
        yPos -= 44;

        Button btnEliminar = CrearBoton(panelRegistro.transform, "BtnEliminar", "🗑️ Eliminar ID", new Color(0.75f, 0.25f, 0.25f), fuente);
        SetRect(btnEliminar.GetComponent<RectTransform>(), 20, yPos, 195, 34);
        btnEliminar.onClick.AddListener(EliminarJugador);

        Button btnLimpiar = CrearBoton(panelRegistro.transform, "BtnLimpiar", "🧹 Limpiar Campos", new Color(0.40f, 0.45f, 0.50f), fuente);
        SetRect(btnLimpiar.GetComponent<RectTransform>(), 225, yPos, 195, 34);
        btnLimpiar.onClick.AddListener(LimpiarInputsRegistro);
        yPos -= 44;

        // Mensaje de feedback
        textoFeedback = CrearTexto(panelRegistro.transform, "TextoFeedback", "", 13, TextAnchor.MiddleCenter, Color.green, fuente, false);
        SetRect(textoFeedback.GetComponent<RectTransform>(), 20, yPos, 400, 40);
        yPos -= 46;

        // Visualización del Árbol (In-Orden)
        Text lblArbol = CrearTexto(panelRegistro.transform, "LblArbol", "Contenido del Árbol (In-Orden):", 13, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.85f), fuente, true);
        SetRect(lblArbol.GetComponent<RectTransform>(), 20, yPos, 400, 22);
        yPos -= 24;

        GameObject fondoLista = new GameObject("FondoListaArbol");
        fondoLista.transform.SetParent(panelRegistro.transform, false);
        Image imgFondoLista = fondoLista.AddComponent<Image>();
        imgFondoLista.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
        SetRect(fondoLista.GetComponent<RectTransform>(), 20, yPos, 400, 165);

        textoVisualizacionArbol = CrearTexto(fondoLista.transform, "TextoVisualizacionArbol", "El árbol está vacío.", 12, TextAnchor.UpperLeft, new Color(0.9f, 0.9f, 0.9f), fuente, false);
        RectTransform rtVis = textoVisualizacionArbol.GetComponent<RectTransform>();
        rtVis.anchorMin = Vector2.zero;
        rtVis.anchorMax = Vector2.one;
        rtVis.offsetMin = new Vector2(10, 8);
        rtVis.offsetMax = new Vector2(-10, -8);
    }

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
        img.color = new Color(0.18f, 0.22f, 0.30f, 1f);

        InputField input = go.AddComponent<InputField>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(go.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.font = fuente;
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        RectTransform rtText = textObj.GetComponent<RectTransform>();
        rtText.anchorMin = Vector2.zero;
        rtText.anchorMax = Vector2.one;
        rtText.offsetMin = new Vector2(12, 2);
        rtText.offsetMax = new Vector2(-12, -2);

        GameObject phObj = new GameObject("Placeholder");
        phObj.transform.SetParent(go.transform, false);
        Text ph = phObj.AddComponent<Text>();
        ph.font = fuente;
        ph.fontSize = 14;
        ph.color = new Color(0.6f, 0.65f, 0.75f, 0.6f);
        ph.text = placeholderTexto;
        ph.alignment = TextAnchor.MiddleLeft;

        RectTransform rtPh = phObj.GetComponent<RectTransform>();
        rtPh.anchorMin = Vector2.zero;
        rtPh.anchorMax = Vector2.one;
        rtPh.offsetMin = new Vector2(12, 2);
        rtPh.offsetMax = new Vector2(-12, -2);

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
        cb.highlightedColor = colorFondo * 1.15f;
        cb.pressedColor = colorFondo * 0.85f;
        btn.colors = cb;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(go.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.font = fuente;
        text.fontSize = 13;
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
