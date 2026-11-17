using System.Collections.Generic;
using TowerDefenseGameplay;
using UnityEngine;

/// <summary>
/// Mapa de rutas del modo multijugador: convierte el camino del nivel en un GRAFO DIRIGIDO
/// (PathGraph + PathNode, lista de adyacencia) y agrega dos desvíos para que el atacante elija ruta.
///
///   Inicio → P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7 → C1 → P8 → Base     (camino original)
///                        P3 → B1 → P7                                     (desvío B)
///              P1 → C1                                                    (atajo C, se abre con el DAG)
///
/// Cada ruta se calcula con Dijkstra (PathGraph.Dijkstra):
///  - Ruta A: Dijkstra(Inicio, P5) + Dijkstra(P5, Base)  → obliga a pasar por el lazo superior.
///  - Ruta B: Dijkstra(Inicio, B1) + Dijkstra(B1, Base)  → atraviesa el centro del mapa.
///  - Ruta C: Dijkstra(Inicio, Base) sin restricciones   → el camino mínimo, solo existe cuando
///            el atacante desbloquea "Atajo Táctico" (se agrega la arista P1 → C1).
///
/// Las casillas que quedan encima de los desvíos se desactivan (igual en ambos PCs).
/// </summary>
public class MapaRutas : MonoBehaviour
{
    public static MapaRutas Instancia { get; private set; }

    public const int RutaA = 0;
    public const int RutaB = 1;
    public const int RutaC = 2;
    public const int TotalRutas = 3;

    private static readonly Color[] coloresRuta =
    {
        new Color(0.30f, 0.76f, 0.97f), // A: azul
        new Color(1.00f, 0.72f, 0.30f), // B: naranja
        new Color(0.95f, 0.36f, 0.36f)  // C: rojo
    };

    private PathGraph grafo;
    private PathNode nodoInicio;
    private PathNode nodoBase;
    private PathNode nodoControlA;
    private PathNode nodoB1;
    private PathNode nodoDesvioC; // P1
    private PathNode nodoC1;
    private readonly List<PathNode> nodosCamino = new List<PathNode>();

    private readonly List<PathNode>[] rutas = new List<PathNode>[TotalRutas];
    private readonly LineRenderer[] lineasRuta = new LineRenderer[TotalRutas];
    private readonly TextMesh[] etiquetas = new TextMesh[TotalRutas];
    private Material materialLineas;

    private bool rutaCAbierta = false;
    private int rutaResaltada = -1;

    /// <summary>False si el nivel no tiene la forma esperada; entonces solo existe la Ruta A.</summary>
    public bool MapaCompleto { get; private set; }
    public bool RutaCAbierta => rutaCAbierta;

    public static string NombreRuta(int ruta)
    {
        switch (ruta)
        {
            case RutaB: return "B";
            case RutaC: return "C";
            default: return "A";
        }
    }

    public static Color ColorRuta(int ruta)
    {
        return coloresRuta[Mathf.Clamp(ruta, 0, TotalRutas - 1)];
    }

    private void Awake()
    {
        Instancia = this;
        materialLineas = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader"));
        ConstruirGrafo();
        CalcularRutas(); // en Awake: el HUD del atacante puede consultarlas desde su Start
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private void Start()
    {
        DesactivarCasillasSobreDesvios();
        DibujarCaminos();
        DibujarRutas();
    }

    #region Construcción del grafo

    private PathNode CrearNodo(string nombre, Vector3 posicion)
    {
        GameObject go = new GameObject("Nodo_" + nombre);
        go.transform.SetParent(grafo.transform, false);
        go.transform.position = new Vector3(posicion.x, posicion.y, 0f);
        PathNode nodo = go.AddComponent<PathNode>();
        grafo.RegistrarNodo(nodo);
        return nodo;
    }

    private void ConstruirGrafo()
    {
        GameObject goGrafo = new GameObject("GrafoRutas_Dijkstra");
        goGrafo.transform.SetParent(transform, false);
        grafo = goGrafo.AddComponent<PathGraph>();
        grafo.nodos = new List<PathNode>();

        levelManager lm = levelManager.main != null ? levelManager.main : FindFirstObjectByType<levelManager>();
        Transform[] camino = (lm != null) ? lm.Path : null;

        if (camino == null || camino.Length < 2)
        {
            Debug.LogError("[MapaRutas] El levelManager no tiene puntos de ruta; no se puede crear el grafo.");
            MapaCompleto = false;
            return;
        }

        // Punto de aparición: el objeto "Start" junto a los puntos de ruta (o 9.8 unidades arriba del primero)
        Vector3 posInicio = camino[0].position + Vector3.up * 9.8f;
        if (lm.Start != null)
        {
            posInicio = lm.Start.position;
        }
        else if (camino[0].parent != null && camino[0].parent.Find("Start") != null)
        {
            posInicio = camino[0].parent.Find("Start").position;
        }

        nodoInicio = CrearNodo("Inicio", posInicio);
        nodoInicio.esInicio = true;

        nodosCamino.Clear();
        for (int i = 0; i < camino.Length; i++)
        {
            nodosCamino.Add(CrearNodo(i == camino.Length - 1 ? "Base" : "P" + i, camino[i].position));
        }
        nodoBase = nodosCamino[nodosCamino.Count - 1];
        nodoBase.esFinal = true;

        // El nivel actual tiene 9 puntos + la base. Si cambia, solo se ofrece la Ruta A (camino original).
        MapaCompleto = camino.Length == 10;

        if (!MapaCompleto)
        {
            Debug.LogWarning("[MapaRutas] El camino del nivel no tiene 10 puntos; solo estará disponible la Ruta A.");
            nodoInicio.ConectarCon(nodosCamino[0]);
            for (int i = 0; i < nodosCamino.Count - 1; i++) nodosCamino[i].ConectarCon(nodosCamino[i + 1]);
            nodoControlA = nodosCamino[nodosCamino.Count / 2];
            return;
        }

        PathNode p1 = nodosCamino[1], p3 = nodosCamino[3], p5 = nodosCamino[5];
        PathNode p6 = nodosCamino[6], p7 = nodosCamino[7], p8 = nodosCamino[8];

        // Nodos nuevos de los desvíos
        nodoB1 = CrearNodo("B1", new Vector3(p3.transform.position.x, p6.transform.position.y, 0f));
        nodoC1 = CrearNodo("C1", new Vector3(p7.transform.position.x, p1.transform.position.y, 0f));
        nodoControlA = p5;
        nodoDesvioC = p1;

        // Aristas dirigidas del camino original (P7 → P8 pasa por C1, que está sobre ese tramo)
        nodoInicio.ConectarCon(nodosCamino[0]);
        for (int i = 0; i < 7; i++) nodosCamino[i].ConectarCon(nodosCamino[i + 1]); // P0 → ... → P7
        p7.ConectarCon(nodoC1);
        nodoC1.ConectarCon(p8);
        p8.ConectarCon(nodoBase);

        // Desvío B
        p3.ConectarCon(nodoB1);
        nodoB1.ConectarCon(p7);

        // La arista del atajo C (P1 → C1) se agrega al desbloquear "Atajo Táctico"
    }

    /// <summary>Abre el atajo: agrega la arista P1 → C1 y recalcula las rutas con Dijkstra.</summary>
    public void AbrirRutaC()
    {
        if (rutaCAbierta || !MapaCompleto) return;
        rutaCAbierta = true;
        nodoDesvioC.ConectarCon(nodoC1);
        CalcularRutas();
        DibujarRutas();
    }

    #endregion

    #region Rutas (Dijkstra)

    private List<PathNode> DijkstraPasandoPor(PathNode control)
    {
        List<PathNode> tramo1 = grafo.Dijkstra(nodoInicio, control);
        List<PathNode> tramo2 = grafo.Dijkstra(control, nodoBase);
        if (tramo1 == null || tramo2 == null) return null;

        List<PathNode> ruta = new List<PathNode>(tramo1);
        for (int i = 1; i < tramo2.Count; i++) ruta.Add(tramo2[i]); // el nodo de control no se repite
        return ruta;
    }

    private void CalcularRutas()
    {
        if (grafo == null || nodoInicio == null || nodoBase == null) return;

        rutas[RutaA] = MapaCompleto ? DijkstraPasandoPor(nodoControlA) : grafo.Dijkstra(nodoInicio, nodoBase);
        rutas[RutaB] = MapaCompleto ? DijkstraPasandoPor(nodoB1) : null;
        rutas[RutaC] = (MapaCompleto && rutaCAbierta) ? grafo.Dijkstra(nodoInicio, nodoBase) : null;

        for (int r = 0; r < TotalRutas; r++)
        {
            if (rutas[r] == null) continue;
            Debug.Log($"[MapaRutas] Ruta {NombreRuta(r)} (Dijkstra): {rutas[r].Count} nodos, longitud {LongitudRuta(r):F1}");
        }
    }

    public bool RutaDisponible(int ruta)
    {
        if (ruta < 0 || ruta >= TotalRutas) return false;
        return rutas[ruta] != null && rutas[ruta].Count >= 2;
    }

    public List<PathNode> ObtenerRuta(int ruta)
    {
        return RutaDisponible(ruta) ? rutas[ruta] : null;
    }

    public float LongitudRuta(int ruta)
    {
        List<PathNode> r = (ruta >= 0 && ruta < TotalRutas) ? rutas[ruta] : null;
        if (r == null) return 0f;
        float total = 0f;
        for (int i = 0; i < r.Count - 1; i++) total += r[i].PesoHacia(r[i + 1]);
        return total;
    }

    #endregion

    #region Casillas y dibujo

    private static float DistanciaASegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>
    /// Desactiva las casillas que quedan sobre los desvíos B y C (se calcula igual en ambos PCs,
    /// así los índices de casilla de la red siguen coincidiendo).
    /// </summary>
    private void DesactivarCasillasSobreDesvios()
    {
        if (!MapaCompleto) return;

        GameObject plots = GameObject.Find("plots");
        if (plots == null) return;

        Vector2[,] segmentos =
        {
            { nodosCamino[3].transform.position, nodoB1.transform.position },
            { nodoB1.transform.position, nodosCamino[7].transform.position },
            { nodoDesvioC.transform.position, nodoC1.transform.position }
        };

        int desactivadas = 0;
        foreach (Transform casilla in plots.transform)
        {
            Vector2 p = casilla.position;
            for (int s = 0; s < segmentos.GetLength(0); s++)
            {
                if (DistanciaASegmento(p, segmentos[s, 0], segmentos[s, 1]) < 0.7f)
                {
                    casilla.gameObject.SetActive(false);
                    desactivadas++;
                    break;
                }
            }
        }
        Debug.Log($"[MapaRutas] {desactivadas} casillas despejadas para abrir los desvíos B y C.");
    }

    private LineRenderer CrearLinea(string nombre, Color color, float ancho, int orden)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = materialLineas;
        lr.useWorldSpace = true;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = ancho;
        lr.endWidth = ancho;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.sortingOrder = orden;
        return lr;
    }

    /// <summary>Dibuja el "suelo" de todos los caminos del grafo (incluido el atajo, aunque esté cerrado).</summary>
    private void DibujarCaminos()
    {
        if (grafo == null) return;
        Color tierra = new Color(0.30f, 0.23f, 0.14f, 0.55f);

        List<KeyValuePair<PathNode, PathNode>> aristas = new List<KeyValuePair<PathNode, PathNode>>();
        foreach (PathNode n in grafo.nodos)
        {
            if (n == null) continue;
            foreach (PathNode v in n.vecinos)
            {
                if (v != null) aristas.Add(new KeyValuePair<PathNode, PathNode>(n, v));
            }
        }
        if (MapaCompleto && !rutaCAbierta)
        {
            aristas.Add(new KeyValuePair<PathNode, PathNode>(nodoDesvioC, nodoC1));
        }

        for (int i = 0; i < aristas.Count; i++)
        {
            LineRenderer lr = CrearLinea("Camino_" + i, tierra, 0.7f, 1);
            lr.positionCount = 2;
            lr.SetPosition(0, aristas[i].Key.transform.position);
            lr.SetPosition(1, aristas[i].Value.transform.position);
        }
    }

    private void DibujarRutas()
    {
        // Desplazamiento diagonal por ruta para que los tramos compartidos no se tapen entre sí
        Vector3[] desplazamientos =
        {
            new Vector3(-0.12f, 0.12f, 0f),
            Vector3.zero,
            new Vector3(0.12f, -0.12f, 0f)
        };

        for (int r = 0; r < TotalRutas; r++)
        {
            if (lineasRuta[r] == null)
            {
                lineasRuta[r] = CrearLinea("Ruta_" + NombreRuta(r), ColorRuta(r), 0.1f, 3);
            }

            List<PathNode> ruta = rutas[r];
            if (ruta == null && r == RutaC && MapaCompleto)
            {
                // Atajo cerrado: se dibuja tenue solo el tramo bloqueado
                ruta = new List<PathNode> { nodoDesvioC, nodoC1 };
            }

            if (ruta == null)
            {
                lineasRuta[r].positionCount = 0;
            }
            else
            {
                lineasRuta[r].positionCount = ruta.Count;
                for (int i = 0; i < ruta.Count; i++)
                {
                    lineasRuta[r].SetPosition(i, ruta[i].transform.position + desplazamientos[r]);
                }
            }

            CrearOActualizarEtiqueta(r);
        }

        AplicarResaltado();
    }

    private void CrearOActualizarEtiqueta(int r)
    {
        if (!MapaCompleto && r != RutaA) return;

        Vector3 pos;
        if (r == RutaA) pos = nodoControlA.transform.position + new Vector3(0.45f, 0.45f, 0f);
        else if (r == RutaB) pos = Vector3.Lerp(nodosCamino[3].transform.position, nodoB1.transform.position, 0.5f) + new Vector3(0.45f, 0f, 0f);
        else pos = Vector3.Lerp(nodoDesvioC.transform.position, nodoC1.transform.position, 0.5f) + new Vector3(0f, 0.45f, 0f);

        if (etiquetas[r] == null)
        {
            GameObject go = new GameObject("Etiqueta_Ruta_" + NombreRuta(r));
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            // Fondo circular oscuro
            GameObject fondo = new GameObject("Fondo");
            fondo.transform.SetParent(go.transform, false);
            SpriteRenderer srFondo = fondo.AddComponent<SpriteRenderer>();
            srFondo.sprite = CrearSpriteCirculo();
            srFondo.color = new Color(0.05f, 0.07f, 0.1f, 0.85f);
            srFondo.sortingOrder = 4;
            fondo.transform.localScale = Vector3.one * 0.55f;

            GameObject txt = new GameObject("Texto");
            txt.transform.SetParent(go.transform, false);
            TextMesh tm = txt.AddComponent<TextMesh>();
            Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente != null)
            {
                tm.font = fuente;
                txt.GetComponent<MeshRenderer>().material = fuente.material;
            }
            tm.fontSize = 64;
            tm.characterSize = 0.07f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            txt.GetComponent<MeshRenderer>().sortingOrder = 5;
            etiquetas[r] = tm;
        }

        bool bloqueada = r == RutaC && !rutaCAbierta;
        etiquetas[r].text = bloqueada ? "C×" : NombreRuta(r); // "C×" = atajo cerrado
        Color c = ColorRuta(r);
        etiquetas[r].color = bloqueada ? new Color(c.r, c.g, c.b, 0.55f) : c;
    }

    /// <summary>Resalta la ruta seleccionada por el atacante (solo en su PC).</summary>
    public void ResaltarRuta(int ruta)
    {
        rutaResaltada = ruta;
        AplicarResaltado();
    }

    private void AplicarResaltado()
    {
        for (int r = 0; r < TotalRutas; r++)
        {
            if (lineasRuta[r] == null) continue;
            bool bloqueada = r == RutaC && !rutaCAbierta;
            bool sel = r == rutaResaltada && !bloqueada;
            float ancho = sel ? 0.22f : 0.1f;
            Color c = ColorRuta(r);
            float alfa = bloqueada ? 0.25f : (rutaResaltada < 0 || sel ? 0.95f : 0.45f);
            Color final = new Color(c.r, c.g, c.b, alfa);
            lineasRuta[r].startWidth = ancho;
            lineasRuta[r].endWidth = ancho;
            lineasRuta[r].startColor = final;
            lineasRuta[r].endColor = final;
            lineasRuta[r].sortingOrder = sel ? 4 : 3;
        }
    }

    private static Sprite spriteCirculo;

    private static Sprite CrearSpriteCirculo()
    {
        if (spriteCirculo != null) return spriteCirculo;
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Vector2 centro = new Vector2(size / 2f, size / 2f);
        float radio = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), centro) <= radio ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        spriteCirculo = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return spriteCirculo;
    }

    #endregion
}
