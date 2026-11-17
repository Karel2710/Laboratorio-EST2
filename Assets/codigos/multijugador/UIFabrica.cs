using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Ayudas para crear la interfaz por código (mismo estilo que el resto del proyecto:
    /// UI clásica de Unity con la fuente integrada, sin prefabs).
    /// </summary>
    public static class UIFabrica
    {
        private static Font fuente;

        public static Font Fuente
        {
            get
            {
                if (fuente != null) return fuente;
                fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        public static Canvas CrearCanvas(string nombre, int orden)
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
            return canvas;
        }

        public static RectTransform CrearPanel(Transform padre, string nombre, Color color)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        /// <summary>Contenedor sin fondo (solo para agrupar con layouts).</summary>
        public static RectTransform CrearContenedor(Transform padre, string nombre)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            return go.AddComponent<RectTransform>();
        }

        public static void Anclar(RectTransform rt, Vector2 anclaMin, Vector2 anclaMax, Vector2 pivote, Vector2 posicion, Vector2 tamano)
        {
            rt.anchorMin = anclaMin;
            rt.anchorMax = anclaMax;
            rt.pivot = pivote;
            rt.anchoredPosition = posicion;
            rt.sizeDelta = tamano;
        }

        public static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Text CrearTexto(Transform padre, string nombre, string texto, int tamano, Color color,
                                      TextAnchor alineacion = TextAnchor.MiddleLeft, FontStyle estilo = FontStyle.Bold)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<RectTransform>();
            Text t = go.AddComponent<Text>();
            t.font = Fuente;
            t.fontSize = tamano;
            t.fontStyle = estilo;
            t.color = color;
            t.alignment = alineacion;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.text = texto;
            return t;
        }

        public static Button CrearBoton(Transform padre, string nombre, string texto, Color fondo, int tamanoTexto, out Text etiqueta)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = fondo;

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = cb;

            etiqueta = CrearTexto(go.transform, "Texto", texto, tamanoTexto, Color.white, TextAnchor.MiddleCenter);
            Estirar(etiqueta.rectTransform);
            return btn;
        }

        public static Button CrearBoton(Transform padre, string nombre, string texto, Color fondo, int tamanoTexto)
        {
            return CrearBoton(padre, nombre, texto, fondo, tamanoTexto, out _);
        }

        public static InputField CrearInput(Transform padre, string nombre, string placeholder, int tamano)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.13f, 0.18f, 1f);

            Text ph = CrearTexto(go.transform, "Placeholder", placeholder, tamano, new Color(0.6f, 0.65f, 0.75f, 0.7f), TextAnchor.MiddleLeft, FontStyle.Italic);
            RectTransform rtPh = ph.rectTransform;
            Estirar(rtPh);
            rtPh.offsetMin = new Vector2(10, 2);
            rtPh.offsetMax = new Vector2(-10, -2);

            Text txt = CrearTexto(go.transform, "Text", "", tamano, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal);
            txt.supportRichText = false;
            RectTransform rtTxt = txt.rectTransform;
            Estirar(rtTxt);
            rtTxt.offsetMin = new Vector2(10, 2);
            rtTxt.offsetMax = new Vector2(-10, -2);

            InputField input = go.AddComponent<InputField>();
            input.textComponent = txt;
            input.placeholder = ph;
            input.targetGraphic = img;
            return input;
        }

        public static LayoutElement Tamano(Component c, float anchoPreferido, float altoPreferido, float anchoFlexible = 0f)
        {
            LayoutElement le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (anchoPreferido >= 0) le.preferredWidth = anchoPreferido;
            if (altoPreferido >= 0)
            {
                le.preferredHeight = altoPreferido;
                le.minHeight = altoPreferido;
            }
            le.flexibleWidth = anchoFlexible;
            return le;
        }

        public static VerticalLayoutGroup Vertical(Component c, int espacio, RectOffset relleno)
        {
            VerticalLayoutGroup v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = espacio;
            v.padding = relleno ?? new RectOffset(0, 0, 0, 0);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup Horizontal(Component c, int espacio, RectOffset relleno)
        {
            HorizontalLayoutGroup h = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = espacio;
            h.padding = relleno ?? new RectOffset(0, 0, 0, 0);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }

        /// <summary>True si el usuario está escribiendo en un campo de texto (para no disparar atajos).</summary>
        public static bool EscribiendoEnCampo()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) return false;
            InputField input = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
            return input != null && input.isFocused;
        }
    }
}
