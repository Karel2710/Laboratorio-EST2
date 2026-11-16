using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Gestor de la vista 2.5D: cámara en perspectiva inclinada sobre el mismo tablero 2D.
    /// Toda la lógica (Dijkstra, Kruskal, oleadas, red) sigue en el plano XY; solo cambia
    /// cómo se dibuja. Se puede alternar en vivo con la vista 2D original.
    /// Los componentes visuales consultan <see cref="Version"/> para saber cuándo actualizarse.
    /// </summary>
    public class Vista25D : MonoBehaviour
    {
        public static Vista25D Instance { get; private set; }

        /// <summary>True = vista 2.5D (perspectiva + modelos 3D); false = vista 2D original.</summary>
        public static bool Activa { get; private set; } = true;

        /// <summary>Aumenta cada vez que cambia la vista o el encuadre.</summary>
        public static int Version { get; private set; } = 1;

        public const float ANGULO = 40f;          // inclinación de la cámara respecto a la vista cenital
        public const float CAMPO_VISION = 34f;
        public const float ALTURA_CAMINO = 0.04f; // grosor de las baldosas del camino
        public const float ALTURA_PAD = 0.04f;    // grosor de las plataformas de construcción

        /// <summary>Fondo de la vista 2.5D: gris azulado oscuro sobre el que flota la isla.</summary>
        public static readonly Color COLOR_FONDO = new Color(0.12f, 0.13f, 0.17f);

        // Márgenes del encuadre (fracción de pantalla) para no quedar bajo el HUD
        private const float MARGEN_LATERAL = 0.015f;
        private const float MARGEN_INFERIOR = 0.095f;
        private const float MARGEN_SUPERIOR = 0.09f;

        private Camera cam;
        private bool originalGuardado;
        private bool ortoOriginal;
        private float tamOriginal, fovOriginal, nearOriginal, farOriginal;
        private Color fondoOriginal;
        private Vector3 posOriginal;
        private Quaternion rotOriginal;
        private int anchoPrevio, altoPrevio;
        private bool pendiente = true;
        private Rect limites;
        private bool limitesCalculados;

        public static Vista25D Asegurar()
        {
            if (Instance != null) return Instance;
            Vista25D existente = FindFirstObjectByType<Vista25D>();
            if (existente != null) return existente;
            GameObject go = new GameObject("Vista25D");
            return go.AddComponent<Vista25D>();
        }

        public static Camera Camara
        {
            get
            {
                if (Instance != null && Instance.cam != null) return Instance.cam;
                Camera c = Camera.main;
                return c != null ? c : FindFirstObjectByType<Camera>();
            }
        }

        /// <summary>Rotación para que un texto o sprite mire a la cámara (identidad en 2D).</summary>
        public static Quaternion RotacionCartel
        {
            get
            {
                if (!Activa) return Quaternion.identity;
                Camera c = Camara;
                return c != null ? c.transform.rotation : Quaternion.identity;
            }
        }

        /// <summary>Punto del plano del juego elevado a cierta altura (en 2D se queda en z = 0).</summary>
        public static Vector3 Elevar(Vector3 p, float altura)
        {
            return new Vector3(p.x, p.y, Activa ? -altura : 0f);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (pendiente || Screen.width != anchoPrevio || Screen.height != altoPrevio) Aplicar();
        }

        public void Alternar()
        {
            Activa = !Activa;
            Aplicar();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.MostrarNotificacion(Activa
                    ? "🧊 Vista 2.5D: cámara inclinada y modelos 3D (la lógica del juego no cambia)."
                    : "🗺️ Vista 2D clásica (cenital).");
                GameManager.Instance.NotificarCambio();
            }
        }

        /// <summary>Aplica la vista actual a la cámara y avisa a los componentes visuales.</summary>
        public void Aplicar()
        {
            cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam == null) return;

            if (!originalGuardado)
            {
                originalGuardado = true;
                ortoOriginal = cam.orthographic;
                tamOriginal = cam.orthographicSize;
                fovOriginal = cam.fieldOfView;
                nearOriginal = cam.nearClipPlane;
                farOriginal = cam.farClipPlane;
                posOriginal = cam.transform.position;
                rotOriginal = cam.transform.rotation;
                fondoOriginal = cam.backgroundColor;
            }

            if (EfectosVisualesManager.Instance != null) EfectosVisualesManager.Instance.DetenerSacudida();

            if (Activa)
            {
                cam.orthographic = false;
                cam.fieldOfView = CAMPO_VISION;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 300f;
                cam.backgroundColor = COLOR_FONDO;
                Encuadrar();
            }
            else
            {
                cam.orthographic = ortoOriginal;
                cam.orthographicSize = tamOriginal;
                cam.fieldOfView = fovOriginal;
                cam.nearClipPlane = nearOriginal;
                cam.farClipPlane = farOriginal;
                cam.backgroundColor = fondoOriginal;
                cam.transform.SetPositionAndRotation(posOriginal, rotOriginal);
            }

            anchoPrevio = Screen.width;
            altoPrevio = Screen.height;
            pendiente = false;
            Version++;
        }

        /// <summary>Rectángulo del tablero jugable: casillas y ruta (sin el punto de aparición, que queda fuera del mapa).</summary>
        public Rect LimitesTablero()
        {
            if (limitesCalculados) return limites;

            List<Vector2> puntos = new List<Vector2>();
            PlotTorre[] plots = FindObjectsByType<PlotTorre>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < plots.Length; i++) puntos.Add(plots[i].transform.position);

            levelManager lm = levelManager.main != null ? levelManager.main : FindFirstObjectByType<levelManager>();
            if (lm != null && lm.Path != null)
            {
                for (int i = 0; i < lm.Path.Length; i++) if (lm.Path[i] != null) puntos.Add(lm.Path[i].position);
            }

            if (puntos.Count < 2) return new Rect(-8f, -6f, 16f, 12f);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < puntos.Count; i++)
            {
                minX = Mathf.Min(minX, puntos[i].x); maxX = Mathf.Max(maxX, puntos[i].x);
                minY = Mathf.Min(minY, puntos[i].y); maxY = Mathf.Max(maxY, puntos[i].y);
            }
            const float borde = 0.75f;
            limites = Rect.MinMaxRect(minX - borde, minY - borde, maxX + borde, maxY + borde);
            limitesCalculados = plots.Length > 0;
            return limites;
        }

        /// <summary>
        /// Busca la distancia de la cámara (con inclinación fija) para que las cuatro esquinas del
        /// tablero quepan dentro de los márgenes, y la centra verticalmente entre las barras del HUD.
        /// </summary>
        private void Encuadrar()
        {
            Rect r = LimitesTablero();
            Quaternion rot = Quaternion.Euler(-ANGULO, 0f, 0f);
            Vector3 adelante = rot * Vector3.forward;
            cam.transform.rotation = rot;

            Vector3 objetivo = new Vector3(r.center.x, r.center.y, 0f);
            Vector3[] esquinas =
            {
                new Vector3(r.xMin, r.yMin, 0f), new Vector3(r.xMax, r.yMin, 0f),
                new Vector3(r.xMin, r.yMax, 0f), new Vector3(r.xMax, r.yMax, 0f)
            };
            float centroDeseado = (MARGEN_INFERIOR + 1f - MARGEN_SUPERIOR) * 0.5f;
            float distancia = 20f;

            for (int vuelta = 0; vuelta < 6; vuelta++)
            {
                float lo = 2f, hi = 150f;
                for (int k = 0; k < 32; k++)
                {
                    float mitad = (lo + hi) * 0.5f;
                    cam.transform.position = objetivo - adelante * mitad;
                    if (Cabe(esquinas)) hi = mitad; else lo = mitad;
                }
                distancia = hi;
                cam.transform.position = objetivo - adelante * distancia;

                float yInf = cam.WorldToViewportPoint(new Vector3(r.center.x, r.yMin, 0f)).y;
                float ySup = cam.WorldToViewportPoint(new Vector3(r.center.x, r.yMax, 0f)).y;
                objetivo.y += ((yInf + ySup) * 0.5f - centroDeseado) * r.height * 0.9f;
            }
            cam.transform.position = objetivo - adelante * distancia;
        }

        private bool Cabe(Vector3[] esquinas)
        {
            for (int i = 0; i < esquinas.Length; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(esquinas[i]);
                if (vp.z <= 0f) return false;
                if (vp.x < MARGEN_LATERAL || vp.x > 1f - MARGEN_LATERAL) return false;
                if (vp.y < MARGEN_INFERIOR || vp.y > 1f - MARGEN_SUPERIOR) return false;
            }
            return true;
        }
    }
}
