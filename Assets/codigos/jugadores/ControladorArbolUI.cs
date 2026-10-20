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
    }

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

        CargarArbolDesdeDisco();
        AsegurarSistemasDefensa();
        ActualizarEstilosTabs();
    }

    private void AsegurarSistemasDefensa()
    {
        if (FindFirstObjectByType<TowerPlacementManager>() == null)
        {
            GameObject managerObj = new GameObject("TowerPlacementManager");
            managerObj.AddComponent<TowerPlacementManager>();
        }
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
            MostrarMensajeFeedback($"✓ Localizado en BST: {encontrado.nombre} (ID: {encontrado.Id}, Vida: {encontrado.vida})", new Color(0.3f, 0.85f, 1f));

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
            GameManager.Instance.jugadorActivo = j;
        }

        if (ControladorHabilidadesUI.Instance != null)
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
            datos.listaJugadores = arbolJugadores.InOrden();

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
                    foreach (Jugador j in datos.listaJugadores)
                    {
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
            textoMetricasArbol.text = $"📊 <b>Total:</b> <color=#00e5ff>{total}</color>  |  <b>Raíz BST:</b> <color=#ffd700>ID {raizId}</color>  |  <b>Niveles:</b> <color=#44ff88>{altura}</color>";
        }

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
            string tagActivo = esActivo ? "  <color=#44ff88><b>[★ EN MANDO TÁCTICO]</b></color>" : "";
            string numColor = esActivo ? "#44ff88" : "#00e5ff";

            sb.AppendLine($"<color={numColor}><b>#{i + 1:00}</b></color> <b>{j.nombre}</b> (ID: <color=#ffd700><b>{j.Id}</b></color>){tagActivo}");
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

        // 3. Banner Superior HUD de Nivel e Instrucciones (aportado por compañero de equipo)
        GameObject panelHud = new GameObject("PanelHUDInfo");
        panelHud.transform.SetParent(canvasObj.transform, false);
        Image imgHud = panelHud.AddComponent<Image>();
        imgHud.color = new Color(0.08f, 0.11f, 0.16f, 0.88f);

        RectTransform rtHud = panelHud.GetComponent<RectTransform>();
        rtHud.anchorMin = new Vector2(0.5f, 1);
        rtHud.anchorMax = new Vector2(0.5f, 1);
        rtHud.pivot = new Vector2(0.5f, 1);
        rtHud.anchoredPosition = new Vector2(0, -6);
        rtHud.sizeDelta = new Vector2(760, 44);

        Text txtInstruccion = CrearTexto(panelHud.transform, "TxtInstruccion", "🛡️ Haz clic en los cuadrantes del mapa para colocar defensas 🏹", 12, TextAnchor.MiddleLeft, new Color(0.95f, 0.9f, 0.3f), fuente, true);
        RectTransform rtTxtInst = txtInstruccion.GetComponent<RectTransform>();
        rtTxtInst.anchorMin = new Vector2(0, 0);
        rtTxtInst.anchorMax = new Vector2(0.58f, 1);
        rtTxtInst.offsetMin = new Vector2(14, 0);
        rtTxtInst.offsetMax = new Vector2(0, 0);

        // Botones de Nivel en el HUD
        Button btnNivel1 = CrearBoton(panelHud.transform, "BtnNivel1", "Nivel 1", new Color(0.2f, 0.6f, 0.3f), fuente);
        SetRect(btnNivel1.GetComponent<RectTransform>(), 450, -6, 90, 32);
        btnNivel1.onClick.AddListener(() => { if (EnemySpawner.instance != null) EnemySpawner.instance.CargarConfiguracionNivel(0); });

        Button btnNivel2 = CrearBoton(panelHud.transform, "BtnNivel2", "Nivel 2", new Color(0.8f, 0.5f, 0.1f), fuente);
        SetRect(btnNivel2.GetComponent<RectTransform>(), 550, -6, 90, 32);
        btnNivel2.onClick.AddListener(() => { if (EnemySpawner.instance != null) EnemySpawner.instance.CargarConfiguracionNivel(1); });

        Button btnNivel3 = CrearBoton(panelHud.transform, "BtnNivel3", "Nivel 3", new Color(0.8f, 0.2f, 0.2f), fuente);
        SetRect(btnNivel3.GetComponent<RectTransform>(), 650, -6, 90, 32);
        btnNivel3.onClick.AddListener(() => { if (EnemySpawner.instance != null) EnemySpawner.instance.CargarConfiguracionNivel(2); });

        // 4. Botón flotante para abrir/cerrar el panel (tactical pill)
        Button botonToggle = CrearBoton(canvasObj.transform, "BotonTogglePanel", "👥 Mando BST", new Color(0.10f, 0.25f, 0.40f, 0.95f), fuente);
        RectTransform rtToggle = botonToggle.GetComponent<RectTransform>();
        rtToggle.anchorMin = new Vector2(1, 1);
        rtToggle.anchorMax = new Vector2(1, 1);
        rtToggle.pivot = new Vector2(1, 1);
        rtToggle.anchoredPosition = new Vector2(-20, -18);
        rtToggle.sizeDelta = new Vector2(145, 40);
        botonToggle.onClick.AddListener(AlternarVisibilidadPanel);

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
        rtPanel.anchoredPosition = new Vector2(25, -20);
        rtPanel.sizeDelta = new Vector2(500, 725);

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
        GameObject scrollObj = new GameObject("ScrollArbolJugadores");
        scrollObj.transform.SetParent(panelRegistro.transform, false);
        SetRect(scrollObj.GetComponent<RectTransform>(), 20, yPos, 460, 185);

        Image imgFondoScroll = scrollObj.AddComponent<Image>();
        imgFondoScroll.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);

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
