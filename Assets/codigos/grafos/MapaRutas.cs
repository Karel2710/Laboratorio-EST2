using System.Collections.Generic;
using TowerDefenseGameplay;
using UnityEngine;

/// <summary>
/// Rutas del ATACANTE en el multijugador LAN, calculadas con Dijkstra sobre el grafo de caminos
/// del juego (PathGraph.main, lista de adyacencia ponderada). En LAN el grafo se construye con la
/// topología completa: camino principal + Sendero Este + Pantano Central + Sendero Norte.
///
///   Ruta A: Dijkstra(Inicio, N6) + Dijkstra(N6, Base)            → camino principal (gran bucle)
///   Ruta B: Dijkstra(Inicio, SenderoEste) + Dijkstra(.., Base)   → baja por el borde derecho
///   Ruta C: Dijkstra(Inicio, Pantano) + Dijkstra(.., Base)       → atajo por el centro; solo existe
///           cuando el atacante desbloquea "Atajo Táctico" (el pantano se drena: su arista pasa a
///           costar como un camino normal y se vuelve el camino mínimo hacia la base).
///
/// Cuando el grafo cambia (barricadas del defensor) las rutas se recalculan con Dijkstra.
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
    private PathNode nodoControlB;
    private PathNode nodoControlC;

    private readonly List<PathNode>[] rutas = new List<PathNode>[TotalRutas];
    private readonly LineRenderer[] lineasRuta = new LineRenderer[TotalRutas];
    private readonly TextMesh[] etiquetas = new TextMesh[TotalRutas];
    private Material materialLineas;

    private bool listo = false;
    private bool rutaCAbierta = false;
    private bool recalcularPendiente = false;
    private int rutaResaltada = -1;
    private int versionVista = -1;

    /// <summary>False si el grafo no tiene la forma esperada; entonces solo existe la Ruta A.</summary>
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
        materialLineas = GraficosProc.MaterialLinea;
    }

    private void OnDestroy()
    {
        if (grafo != null) grafo.OnGrafoCambiado -= AlCambiarGrafo;
        if (Instancia == this) Instancia = null;
    }

    private void Update()
    {
        // El GameManager construye el grafo en su Start: se espera a que exista (y se rehace si se reconstruye)
        if (listo && (nodoInicio == null || nodoBase == null)) listo = false;
        if (!listo) IntentarInicializar();
        if (!listo) return;

        if (recalcularPendiente)
        {
            recalcularPendiente = false;
            CalcularRutas();
            DibujarRutas();
        }
        if (versionVista != Vista25D.Version) DibujarRutas();
    }

    #region Grafo

    private static PathNode BuscarPorEtiqueta(PathGraph g, string etiqueta)
    {
        for (int i = 0; i < g.nodos.Count; i++)
        {
            PathNode n = g.nodos[i];
            if (n != null && n.name.EndsWith("_" + etiqueta)) return n;
        }
        return null;
    }

    private void IntentarInicializar()
    {
        grafo = PathGraph.main;
        if (grafo == null || grafo.nodos.Count < 2) return;
        if (ModoJuego.EsMultijugador && grafo.TopologiaActual != GameManager.TopologiaMultijugador) return;
        nodoInicio = grafo.ObtenerNodoInicio();
        nodoBase = grafo.ObtenerNodoFin();
        if (nodoInicio == null || nodoBase == null) return;

        // Los nodos del camino principal se crean primero (ids 0..n): N6 es el inicio del gran bucle
        nodoControlA = grafo.nodos.Count > 6 ? grafo.nodos[6] : null;
        nodoControlB = BuscarPorEtiqueta(grafo, "SenderoEste");
        nodoControlC = BuscarPorEtiqueta(grafo, "Pantano");
        MapaCompleto = nodoControlA != null && nodoControlB != null && nodoControlC != null;
        if (!MapaCompleto)
        {
            Debug.LogWarning("[MapaRutas] El grafo no tiene los desvíos esperados; solo estará disponible la Ruta A.");
        }

        listo = true;
        grafo.OnGrafoCambiado -= AlCambiarGrafo;
        grafo.OnGrafoCambiado += AlCambiarGrafo;
        if (rutaCAbierta)
        {
            rutaCAbierta = false;
            AbrirRutaC();
        }
        CalcularRutas();
        DibujarRutas();
        if (ModoJuego.EsAtacanteLocal && ControladorAtacante.Instance != null) ResaltarRuta(ControladorAtacante.Instance.rutaSeleccionada);
    }

    private void AlCambiarGrafo()
    {
        recalcularPendiente = true;
    }

    /// <summary>
    /// Abre el atajo: el Pantano Central se drena (sus aristas pasan a costar ×1.0) y Dijkstra
    /// recalcula las rutas. Se hace igual en ambos PCs para que vean el mismo grafo.
    /// </summary>
    public void AbrirRutaC()
    {
        if (!listo) IntentarInicializar();
        if (rutaCAbierta || !MapaCompleto) return;
        rutaCAbierta = true;

        for (int i = 0; i < grafo.nodos.Count; i++)
        {
            PathNode n = grafo.nodos[i];
            if (n == null || !n.name.EndsWith("_Pantano")) continue;
            for (int v = 0; v < n.vecinos.Count; v++)
            {
                PathNode vecino = n.vecinos[v];
                if (vecino == null) continue;
                n.ConectarCon(vecino, 1f);
                vecino.ConectarCon(n, 1f);
            }
        }
        grafo.CalcularRutaPrincipal();
        CalcularRutas();
        DibujarRutas();
    }

    #endregion

    #region Rutas (Dijkstra)

    private List<PathNode> DijkstraPasandoPor(PathNode control)
    {
        if (control == null) return null;
        List<PathNode> tramo1 = grafo.Dijkstra(nodoInicio, control);
        if (tramo1 == null) return null;
        tramo1 = new List<PathNode>(tramo1);
        List<PathNode> tramo2 = grafo.Dijkstra(control, nodoBase);
        if (tramo2 == null) return null;

        List<PathNode> ruta = new List<PathNode>(tramo1);
        for (int i = 1; i < tramo2.Count; i++) ruta.Add(tramo2[i]); // el nodo de control no se repite
        return ruta;
    }

    private void CalcularRutas()
    {
        if (grafo == null || nodoInicio == null || nodoBase == null) return;

        rutas[RutaA] = MapaCompleto ? DijkstraPasandoPor(nodoControlA) : grafo.Dijkstra(nodoInicio, nodoBase);
        if (rutas[RutaA] != null) rutas[RutaA] = new List<PathNode>(rutas[RutaA]);
        rutas[RutaB] = MapaCompleto ? DijkstraPasandoPor(nodoControlB) : null;
        rutas[RutaC] = (MapaCompleto && rutaCAbierta) ? DijkstraPasandoPor(nodoControlC) : null;

        for (int r = 0; r < TotalRutas; r++)
        {
            if (rutas[r] == null) continue;
            Debug.Log($"[MapaRutas] Ruta {NombreRuta(r)} (Dijkstra): {rutas[r].Count} nodos, costo {LongitudRuta(r):F1}");
        }
    }

    public bool RutaDisponible(int ruta)
    {
        if (!listo) IntentarInicializar();
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

    #region Dibujo

    /// <summary>Altura de dibujo: en 2.5D apenas por encima de las baldosas del camino (-Z = arriba).</summary>
    private static float AlturaZ(float extra) => Vista25D.Activa ? -(Vista25D.ALTURA_CAMINO + extra) : 0f;

    private LineRenderer CrearLinea(string nombre, Color color, float ancho, int orden)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = materialLineas;
        lr.useWorldSpace = true;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = ancho;
        lr.endWidth = ancho;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.sortingOrder = orden;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    private void DibujarRutas()
    {
        versionVista = Vista25D.Version;
        float z = AlturaZ(0.03f);

        // Desplazamiento diagonal por ruta para que los tramos compartidos no se tapen entre sí
        Vector3[] desplazamientos =
        {
            new Vector3(-0.12f, 0.12f, z),
            new Vector3(0f, 0f, z),
            new Vector3(0.12f, -0.12f, z)
        };

        for (int r = 0; r < TotalRutas; r++)
        {
            if (lineasRuta[r] == null) lineasRuta[r] = CrearLinea("Ruta_" + NombreRuta(r), ColorRuta(r), 0.1f, 16);

            List<PathNode> ruta = rutas[r];
            if (ruta == null && r == RutaC && MapaCompleto)
            {
                // Atajo cerrado: se dibuja tenue el tramo del pantano
                ruta = DijkstraPasandoPor(nodoControlC);
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

        PathNode control = r == RutaA ? nodoControlA : (r == RutaB ? nodoControlB : nodoControlC);
        if (control == null) control = nodoInicio;
        Vector3 pos = control.transform.position + new Vector3(0.45f, 0.45f, AlturaZ(0.5f));

        if (etiquetas[r] == null)
        {
            GameObject go = new GameObject("Etiqueta_Ruta_" + NombreRuta(r));
            go.transform.SetParent(transform, false);

            SpriteRenderer srFondo = GraficosProc.CrearSprite(go.transform, "Fondo", GraficosProc.Circulo, new Color(0.05f, 0.07f, 0.1f, 0.85f), 17, 0.55f);
            srFondo.transform.localPosition = new Vector3(0f, 0f, 0.01f);

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
            txt.GetComponent<MeshRenderer>().sortingOrder = 18;
            etiquetas[r] = tm;
        }

        Transform raiz = etiquetas[r].transform.parent;
        raiz.position = pos;
        raiz.rotation = Vista25D.Activa ? Vista25D.RotacionCartel : Quaternion.identity;

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
            lineasRuta[r].sortingOrder = sel ? 17 : 16;
        }
    }

    #endregion
}
