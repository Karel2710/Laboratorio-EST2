using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Paleta global del juego. Incluye el modo de accesibilidad para daltonismo
/// (sustituye el par verde/rojo por azul/naranja, distinguible en deuteranopía y protanopía).
/// </summary>
public static class Paleta
{
    public static bool Daltonico = false;
    public static event System.Action OnCambio;

    public static Color Positivo => Daltonico ? new Color(0.25f, 0.62f, 1f) : new Color(0.25f, 0.92f, 0.45f);
    public static Color Negativo => Daltonico ? new Color(1f, 0.58f, 0.10f) : new Color(0.95f, 0.28f, 0.28f);
    public static Color Aviso => new Color(1f, 0.85f, 0.25f);
    public static Color Cian => new Color(0.25f, 0.9f, 1f);
    public static Color Dorado => new Color(1f, 0.82f, 0.25f);
    public static Color FondoPanel => new Color(0.07f, 0.09f, 0.14f, 0.97f);
    public static Color FondoOscuro => new Color(0.04f, 0.06f, 0.10f, 0.96f);

    public static readonly Color[] ColoresJugador =
    {
        new Color(0.25f, 0.9f, 1f), new Color(1f, 0.6f, 0.15f), new Color(0.8f, 0.45f, 1f), new Color(1f, 0.9f, 0.3f)
    };

    public static void AlternarDaltonico()
    {
        Daltonico = !Daltonico;
        OnCambio?.Invoke();
    }

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}

/// <summary>
/// Entrada unificada: New Input System + Legacy Input (compatibilidad dual).
/// </summary>
public static class EntradaJuego
{
    public static Vector2 PosicionCursor
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed)
                return UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
            if (UnityEngine.InputSystem.Mouse.current != null)
                return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }
    }

    public static bool ClickIzquierdo
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) return true;
#endif
            return false;
        }
    }

    public static bool ClickDerecho
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(1)) return true;
#endif
            return false;
        }
    }

    public static bool Escape
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }
    }

    private static readonly List<RaycastResult> resultadosUI = new List<RaycastResult>();

    /// <summary>True si el cursor está sobre un elemento gráfico de UI (botones, modales).</summary>
    public static bool EsCursorSobreUI()
    {
        if (EventSystem.current == null) return false;
        PointerEventData ped = new PointerEventData(EventSystem.current) { position = PosicionCursor };
        resultadosUI.Clear();
        EventSystem.current.RaycastAll(ped, resultadosUI);
        for (int i = 0; i < resultadosUI.Count; i++)
        {
            if (resultadosUI[i].module is GraphicRaycaster) return true;
        }
        return false;
    }

    /// <summary>Posición del cursor proyectada al plano 2D del mundo (Z = 0).</summary>
    public static bool CursorEnMundo(out Vector2 punto)
    {
        punto = Vector2.zero;
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) return false;
        Vector2 p = PosicionCursor;
        Vector3 m = cam.ScreenToWorldPoint(new Vector3(p.x, p.y, Mathf.Abs(cam.transform.position.z)));
        punto = new Vector2(m.x, m.y);
        return true;
    }
}

/// <summary>
/// Sprites, materiales y líneas generados por código (cero dependencias de prefabs o Inspector).
/// </summary>
public static class GraficosProc
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static Material materialLinea;

    public static Material MaterialLinea
    {
        get
        {
            if (materialLinea == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("UI/Default");
                materialLinea = new Material(sh);
            }
            return materialLinea;
        }
    }

    private delegate float FormaFn(float nx, float ny);

    private static Sprite Generar(string clave, int size, FormaFn alfa)
    {
        if (cache.TryGetValue(clave, out Sprite sp) && sp != null) return sp;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[size * size];
        float h = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float a = Mathf.Clamp01(alfa((x - h) / h, (y - h) / h));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        cache[clave] = sp;
        return sp;
    }

    private static float Borde(float d, float limite) => (limite - d) * 24f;

    public static Sprite Cuadrado => Generar("cuadrado", 4, (x, y) => 1f);
    public static Sprite Circulo => Generar("circulo", 64, (x, y) => Borde(Mathf.Sqrt(x * x + y * y), 0.96f));
    public static Sprite Brillo => Generar("brillo", 64, (x, y) => { float d = Mathf.Sqrt(x * x + y * y); return Mathf.Pow(Mathf.Clamp01(1f - d), 2f); });
    public static Sprite Diamante => Generar("diamante", 64, (x, y) => Borde(Mathf.Abs(x) + Mathf.Abs(y), 0.96f));
    public static Sprite Triangulo => Generar("triangulo", 64, (x, y) => Mathf.Min(Borde(Mathf.Abs(x) * 1.9f + y, 0.9f), Borde(-y, 0.9f)));

    public static Sprite Anillo => Generar("anillo", 96, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        return Mathf.Min(Borde(d, 0.96f), Borde(-d, -0.80f));
    });

    public static Sprite Hexagono => Generar("hexagono", 64, (x, y) =>
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        return Borde(Mathf.Max(ax * 0.866f + ay * 0.5f, ay), 0.90f);
    });

    public static Sprite Estrella => Generar("estrella", 64, (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        float ang = Mathf.Atan2(y, x);
        return Borde(d, 0.55f + 0.38f * Mathf.Abs(Mathf.Cos(ang * 2.5f)));
    });

    public static LineRenderer CrearLinea(Transform padre, string nombre, Color c1, Color c2, float ancho, int orden, int puntos = 2)
    {
        GameObject go = new GameObject(nombre);
        if (padre != null) go.transform.SetParent(padre, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = MaterialLinea;
        lr.useWorldSpace = true;
        lr.positionCount = puntos;
        lr.startWidth = ancho;
        lr.endWidth = ancho;
        lr.startColor = c1;
        lr.endColor = c2;
        lr.sortingOrder = orden;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    public static void TrazarCirculo(LineRenderer lr, Vector3 centro, float radio, int segmentos = 48)
    {
        if (lr == null) return;
        lr.loop = true;
        lr.positionCount = segmentos;
        for (int i = 0; i < segmentos; i++)
        {
            float a = i * Mathf.PI * 2f / segmentos;
            lr.SetPosition(i, centro + new Vector3(Mathf.Cos(a) * radio, Mathf.Sin(a) * radio, 0f));
        }
    }

    public static SpriteRenderer CrearSprite(Transform padre, string nombre, Sprite sp, Color color, int orden, float escala = 1f)
    {
        GameObject go = new GameObject(nombre);
        if (padre != null) go.transform.SetParent(padre, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.color = color;
        sr.sortingOrder = orden;
        go.transform.localScale = Vector3.one * escala;
        return sr;
    }

    /// <summary>Texto en espacio de mundo (TextMesh) con la fuente integrada.</summary>
    public static TextMesh CrearTextoMundo(Transform padre, string texto, Color color, float alto, int orden)
    {
        GameObject go = new GameObject("TextoMundo");
        if (padre != null) go.transform.SetParent(padre, false);
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.font = UIFabrica.Fuente;
        tm.fontSize = 64;
        tm.characterSize = alto * 0.16f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        tm.text = texto;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (tm.font != null) mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = orden;
        return tm;
    }
}

/// <summary>
/// Fábrica de interfaz uGUI creada 100% por código.
/// </summary>
public static class UIFabrica
{
    private static Font fuente;

    public static Font Fuente
    {
        get
        {
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fuente == null) fuente = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return fuente;
        }
    }

    public static void AsegurarEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    public static GameObject CrearCanvas(string nombre, int orden)
    {
        AsegurarEventSystem();
        GameObject go = new GameObject(nombre);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = orden;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    public static RectTransform CrearPanel(Transform padre, string nombre, Color color, Color? borde = null)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        if (borde.HasValue)
        {
            Outline o = go.AddComponent<Outline>();
            o.effectColor = borde.Value;
            o.effectDistance = new Vector2(2, -2);
        }
        return go.GetComponent<RectTransform>();
    }

    public static RectTransform CrearContenedor(Transform padre, string nombre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return go.GetComponent<RectTransform>();
    }

    public static Text CrearTexto(Transform padre, string nombre, string contenido, int tam, TextAnchor alineacion, Color color, bool negrita = false)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Text t = go.AddComponent<Text>();
        t.font = Fuente;
        t.fontSize = tam;
        t.alignment = alineacion;
        t.color = color;
        t.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = contenido;
        return t;
    }

    public static Button CrearBoton(Transform padre, string nombre, string texto, Color fondo, int tam = 12)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = fondo;
        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        Text t = CrearTexto(go.transform, "Text", texto, tam, TextAnchor.MiddleCenter, Color.white, true);
        Estirar(t, 3, 1, 3, 1);
        return btn;
    }

    public static Text TextoDe(Button b) => b != null ? b.GetComponentInChildren<Text>(true) : null;

    public static void Rotular(Button b, string texto)
    {
        Text t = TextoDe(b);
        if (t != null) t.text = texto;
    }

    public static void Colorear(Button b, Color c)
    {
        if (b == null) return;
        Image img = b.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    public static InputField CrearInput(Transform padre, string nombre, string placeholder, int tam = 13)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.03f, 0.05f, 0.09f, 1f);
        Outline o = go.AddComponent<Outline>();
        o.effectColor = new Color(0.2f, 0.45f, 0.65f, 0.7f);
        o.effectDistance = new Vector2(1, -1);
        InputField input = go.AddComponent<InputField>();

        Text txt = CrearTexto(go.transform, "Text", "", tam, TextAnchor.MiddleLeft, Color.white);
        txt.supportRichText = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        Estirar(txt, 8, 2, 8, 2);

        Text ph = CrearTexto(go.transform, "Placeholder", placeholder, tam, TextAnchor.MiddleLeft, new Color(0.5f, 0.58f, 0.68f, 0.8f));
        ph.fontStyle = FontStyle.Italic;
        Estirar(ph, 8, 2, 8, 2);

        input.textComponent = txt;
        input.placeholder = ph;
        return input;
    }

    public static Image CrearImagen(Transform padre, string nombre, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Coloca con ancla superior-izquierda (x hacia la derecha, y negativo hacia abajo).</summary>
    public static RectTransform Colocar(Component c, float x, float y, float ancho, float alto)
    {
        RectTransform rt = c.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(ancho, alto);
        return rt;
    }

    public static RectTransform Estirar(Component c, float izq = 0, float abajo = 0, float der = 0, float arriba = 0)
    {
        RectTransform rt = c.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(izq, abajo);
        rt.offsetMax = new Vector2(-der, -arriba);
        return rt;
    }

    public static RectTransform Anclar(Component c, Vector2 ancla, Vector2 pivote, Vector2 posicion, Vector2 tam)
    {
        RectTransform rt = c.GetComponent<RectTransform>();
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = pivote;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tam;
        return rt;
    }

    /// <summary>Línea de UI entre dos puntos (coordenadas locales con ancla inferior-izquierda del padre).</summary>
    public static Image CrearLineaUI(Transform padre, Vector2 a, Vector2 b, Color color, float grosor)
    {
        Image img = CrearImagen(padre, "Linea", null, color);
        ActualizarLineaUI(img, a, b, grosor);
        return img;
    }

    public static void ActualizarLineaUI(Image img, Vector2 a, Vector2 b, float grosor)
    {
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0.5f);
        Vector2 d = b - a;
        rt.anchoredPosition = a;
        rt.sizeDelta = new Vector2(d.magnitude, grosor);
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    /// <summary>Scroll vertical con contenido auto-ajustable. Devuelve el RectTransform del contenido.</summary>
    public static ScrollRect CrearScroll(Transform padre, string nombre, Color fondo, out RectTransform contenido, bool conLayout = true)
    {
        RectTransform raiz = CrearPanel(padre, nombre, fondo);
        ScrollRect sr = raiz.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 30f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = CrearPanel(raiz, "Viewport", Color.white);
        Estirar(viewport, 4, 4, 4, 4);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        contenido = CrearContenedor(viewport, "Content");
        contenido.anchorMin = new Vector2(0, 1);
        contenido.anchorMax = new Vector2(1, 1);
        contenido.pivot = new Vector2(0.5f, 1);
        contenido.sizeDelta = new Vector2(0, 100);

        if (conLayout)
        {
            VerticalLayoutGroup vlg = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            contenido.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        sr.viewport = viewport;
        sr.content = contenido;
        return sr;
    }

    /// <summary>Encabezado estándar de modal con título y botón de cierre.</summary>
    public static Text CrearEncabezado(RectTransform panel, string titulo, Color colorTitulo, UnityEngine.Events.UnityAction alCerrar, float alto = 46)
    {
        RectTransform header = CrearPanel(panel, "Encabezado", Paleta.FondoOscuro);
        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = new Vector2(1, 1);
        header.pivot = new Vector2(0.5f, 1);
        header.anchoredPosition = Vector2.zero;
        header.sizeDelta = new Vector2(0, alto);
        Text t = CrearTexto(header, "Titulo", titulo, 17, TextAnchor.MiddleLeft, colorTitulo, true);
        Estirar(t, 16, 0, 56, 0);
        Button cerrar = CrearBoton(header, "BtnCerrar", "✕", new Color(0.72f, 0.18f, 0.18f), 14);
        Anclar(cerrar, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(34, 30));
        cerrar.onClick.AddListener(alCerrar);
        return t;
    }
}
