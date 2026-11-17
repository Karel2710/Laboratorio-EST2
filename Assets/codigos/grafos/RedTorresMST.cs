using System.Collections.Generic;
using UnityEngine;
using TowerDefenseGameplay;

namespace GrafosEST2
{
    /// <summary>
    /// Conjuntos Disjuntos (Union-Find) con compresión de caminos y unión por rango.
    /// Núcleo del algoritmo de Kruskal: permite saber en tiempo casi constante O(α(n))
    /// si dos vértices ya están en la misma componente (la arista formaría un ciclo).
    /// </summary>
    public class DisjointSet
    {
        private readonly Dictionary<int, int> padre = new Dictionary<int, int>();
        private readonly Dictionary<int, int> rango = new Dictionary<int, int>();

        public int Conjuntos { get; private set; }

        /// <summary>Crea un conjunto unitario {x}. Cada elemento empieza siendo su propio representante.</summary>
        public void MakeSet(int x)
        {
            if (padre.ContainsKey(x)) return;
            padre[x] = x;
            rango[x] = 0;
            Conjuntos++;
        }

        /// <summary>Representante del conjunto de x. Compresión de caminos: los nodos visitados apuntan a la raíz.</summary>
        public int Find(int x)
        {
            if (!padre.ContainsKey(x)) MakeSet(x);
            int raiz = x;
            while (padre[raiz] != raiz) raiz = padre[raiz];
            while (padre[x] != raiz)
            {
                int siguiente = padre[x];
                padre[x] = raiz;
                x = siguiente;
            }
            return raiz;
        }

        /// <summary>
        /// Une los conjuntos de a y b. Devuelve false si ya estaban unidos (arista que cerraría un ciclo).
        /// </summary>
        public bool Union(int a, int b)
        {
            int ra = Find(a);
            int rb = Find(b);
            if (ra == rb) return false;

            // Unión por rango: el árbol más bajo cuelga del más alto
            if (rango[ra] < rango[rb]) padre[ra] = rb;
            else if (rango[ra] > rango[rb]) padre[rb] = ra;
            else
            {
                padre[rb] = ra;
                rango[ra]++;
            }
            Conjuntos--;
            return true;
        }

        public bool Conectados(int a, int b) => Find(a) == Find(b);
    }

    /// <summary>
    /// Red de Energía y Sinergia entre Torres.
    /// Cada torre es un vértice de un grafo no dirigido ponderado; existe arista entre dos torres si
    /// están dentro del radio de enlace (5.5 u; más con Torre Radar) y su peso es la distancia euclidiana.
    /// Con Kruskal + Union-Find se obtiene el Árbol (bosque) de Expansión Mínima cada vez que se
    /// construye, mejora o vende una torre. Las torres enlazadas reciben un bono de sinergia.
    /// </summary>
    public class RedTorresMST : MonoBehaviour
    {
        public static RedTorresMST Instance { get; private set; }

        public const float RADIO_BASE = 5.5f;
        public const float RADIO_EXTRA_RADAR = 2.5f;
        public const float BONO_DANIO_POR_CONEXION = 0.15f;
        public const float BONO_CADENCIA_POR_CONEXION = 0.05f;
        public const float TOPE_BONO = 0.35f;

        public struct AristaTorre
        {
            public Torre a;
            public Torre b;
            public float peso;
        }

        public readonly List<AristaTorre> aristasMST = new List<AristaTorre>();
        public readonly List<string> bitacoraKruskal = new List<string>();

        public float PesoTotal { get; private set; }
        public int Componentes { get; private set; }
        public int AristasCandidatas { get; private set; }
        public int AristasDescartadas { get; private set; }

        public event System.Action OnRedActualizada;

        private readonly List<LineRenderer> lineas = new List<LineRenderer>();

        public static RedTorresMST Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("RedTorresMST");
            return go.AddComponent<RedTorresMST>();
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

        /// <summary>Radio máximo de enlace entre dos torres (ampliado por Radar, habilidades y clase).</summary>
        public static float RadioEnlace(bool algunRadar)
        {
            float radio = RADIO_BASE;
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                if (gm.TieneHabilidadDAG("HAB_RED_RADAR")) radio += 1.5f;
                radio += 1.5f * gm.FactorClase(ClaseComandante.IngenieroMST);
            }
            if (algunRadar) radio += RADIO_EXTRA_RADAR;
            return radio;
        }

        /// <summary>
        /// Recalcula el MST con Kruskal y aplica los bonos de sinergia a cada torre.
        /// </summary>
        public void Recalcular()
        {
            List<Torre> torres = new List<Torre>();
            for (int i = 0; i < Torre.Activas.Count; i++)
            {
                if (Torre.Activas[i] != null) torres.Add(Torre.Activas[i]);
            }

            aristasMST.Clear();
            bitacoraKruskal.Clear();
            PesoTotal = 0f;
            AristasDescartadas = 0;

            // 1. Vértices: MakeSet por cada torre
            DisjointSet uf = new DisjointSet();
            for (int i = 0; i < torres.Count; i++)
            {
                uf.MakeSet(i);
                torres[i].ReiniciarSinergiaRed();
            }

            // 2. Aristas candidatas: pares dentro del radio de enlace
            List<AristaTorre> candidatas = new List<AristaTorre>();
            List<int> ia = new List<int>();
            List<int> ib = new List<int>();
            for (int i = 0; i < torres.Count - 1; i++)
            {
                for (int j = i + 1; j < torres.Count; j++)
                {
                    float d = Vector2.Distance(torres[i].transform.position, torres[j].transform.position);
                    if (d <= RadioEnlace(torres[i].EsRadar || torres[j].EsRadar))
                    {
                        candidatas.Add(new AristaTorre { a = torres[i], b = torres[j], peso = d });
                        ia.Add(i);
                        ib.Add(j);
                    }
                }
            }
            AristasCandidatas = candidatas.Count;

            // 3. Kruskal: ordenar por peso ascendente y unir si no se forma ciclo
            List<int> orden = new List<int>();
            for (int i = 0; i < candidatas.Count; i++) orden.Add(i);
            orden.Sort((x, y) => candidatas[x].peso.CompareTo(candidatas[y].peso));

            for (int k = 0; k < orden.Count; k++)
            {
                int idx = orden[k];
                AristaTorre ar = candidatas[idx];
                if (uf.Union(ia[idx], ib[idx]))
                {
                    aristasMST.Add(ar);
                    PesoTotal += ar.peso;
                    ar.a.gradoMST++;
                    ar.b.gradoMST++;
                    bitacoraKruskal.Add($"✓ {ar.a.NombreCorto}—{ar.b.NombreCorto} ({ar.peso:F1}) aceptada");
                }
                else
                {
                    AristasDescartadas++;
                    bitacoraKruskal.Add($"✕ {ar.a.NombreCorto}—{ar.b.NombreCorto} ({ar.peso:F1}) descartada: formaría ciclo");
                }
            }
            Componentes = uf.Conjuntos;

            // 4. Sinergia: bono por conexiones + bono de Radar para toda su componente
            HashSet<int> componentesConRadar = new HashSet<int>();
            for (int i = 0; i < torres.Count; i++)
            {
                if (torres[i].EsRadar) componentesConRadar.Add(uf.Find(i));
            }

            GameManager gm = GameManager.Instance;
            float tope = TOPE_BONO + (gm != null && gm.TieneHabilidadDAG("HAB_RED_RADAR") ? 0.15f : 0f);
            float extraIngeniero = gm != null ? 0.05f * gm.FactorClase(ClaseComandante.IngenieroMST) : 0f;

            for (int i = 0; i < torres.Count; i++)
            {
                Torre t = torres[i];
                t.componenteMST = uf.Find(i);
                t.bonoDanioRed = Mathf.Min(tope, t.gradoMST * (BONO_DANIO_POR_CONEXION + extraIngeniero));
                t.bonoCadenciaRed = Mathf.Min(tope, t.gradoMST * BONO_CADENCIA_POR_CONEXION);
                t.enRedConRadar = t.gradoMST > 0 && componentesConRadar.Contains(t.componenteMST);
                t.RefrescarIndicadores();
            }

            DibujarRed();
            OnRedActualizada?.Invoke();
        }

        /// <summary>Torres con las que una nueva torre en 'pos' podría enlazarse (previsualización).</summary>
        public void VecinasEnRadio(Vector3 pos, bool esRadar, Torre excluir, List<Torre> salida)
        {
            salida.Clear();
            for (int i = 0; i < Torre.Activas.Count; i++)
            {
                Torre t = Torre.Activas[i];
                if (t == null || t == excluir) continue;
                if (Vector2.Distance(pos, t.transform.position) <= RadioEnlace(esRadar || t.EsRadar)) salida.Add(t);
            }
        }

        #region Visual: Rayos de Energía

        private void DibujarRed()
        {
            while (lineas.Count < aristasMST.Count)
            {
                lineas.Add(GraficosProc.CrearLinea(transform, "RayoMST", Paleta.Cian, Paleta.Dorado, 0.07f, 4));
            }
            for (int i = 0; i < lineas.Count; i++)
            {
                bool activa = i < aristasMST.Count && aristasMST[i].a != null && aristasMST[i].b != null;
                lineas[i].gameObject.SetActive(activa);
                if (activa)
                {
                    lineas[i].positionCount = 2;
                    // En 2.5D el rayo une los cabezales de las torres
                    lineas[i].SetPosition(0, Vista25D.Elevar(aristasMST[i].a.transform.position, aristasMST[i].a.AlturaVisual));
                    lineas[i].SetPosition(1, Vista25D.Elevar(aristasMST[i].b.transform.position, aristasMST[i].b.AlturaVisual));
                }
            }
        }

        private int versionVista = -1;

        private void Update()
        {
            if (versionVista != Vista25D.Version)
            {
                versionVista = Vista25D.Version;
                DibujarRed();
            }
            if (aristasMST.Count == 0) return;
            bool sobrecarga = GameManager.Instance != null && GameManager.Instance.SobrecargaActiva;
            float t = Time.unscaledTime;

            for (int i = 0; i < aristasMST.Count && i < lineas.Count; i++)
            {
                LineRenderer lr = lineas[i];
                if (!lr.gameObject.activeSelf) continue;
                float onda = 0.5f + 0.5f * Mathf.Sin(t * (sobrecarga ? 9f : 3f) + i * 1.3f);
                Color c1 = Color.Lerp(Paleta.Cian, Paleta.Dorado, sobrecarga ? 0.7f + 0.3f * onda : onda * 0.6f);
                Color c2 = Color.Lerp(Paleta.Dorado, Paleta.Cian, sobrecarga ? 0.1f : onda * 0.6f);
                c1.a = 0.55f + 0.4f * onda;
                c2.a = 0.55f + 0.4f * (1f - onda);
                lr.startColor = c1;
                lr.endColor = c2;
                float ancho = (sobrecarga ? 0.13f : 0.06f) + 0.03f * onda;
                lr.startWidth = ancho;
                lr.endWidth = ancho;
            }
        }

        #endregion
    }
}
