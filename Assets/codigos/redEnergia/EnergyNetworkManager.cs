using System.Collections.Generic;
using UnityEngine;

namespace RedEnergiaMST
{
    /// <summary>
    /// Director central de la Red de Energía entre Torres.
    ///
    /// Responsabilidades:
    ///   1. Mantener el registro de todos los <see cref="TowerNode"/> activos en la escena.
    ///   2. Construir el grafo completo ponderado G = (V, E) donde |E| = n(n-1)/2.
    ///   3. Ejecutar el algoritmo de Kruskal (usando <see cref="DisjointSet"/>) para obtener
    ///      el MST con peso mínimo total y sin ciclos.
    ///   4. Aplicar multiplicadores de sinergia (daño y rango) a cada <see cref="TowerNode"/>
    ///      en función de su grado en el árbol resultante.
    ///   5. Gestionar el pooling de <see cref="LineRenderer"/> para los rayos de energía visuales.
    ///
    /// Patrón de uso:
    ///   - Llama a <see cref="RegistrarTorre"/> al instanciar una nueva torre.
    ///   - Llama a <see cref="DesregistrarTorre"/> al destruir o vender una torre.
    ///   - Llama a <see cref="RecalcularRed"/> en cualquier momento para forzar un recálculo.
    ///
    /// Integración con PlotTorre (TowerDefenseGameplay):
    ///   Agrega en <c>PlotTorre.ConstruirTorre()</c> la línea:
    ///   <code>
    ///     EnergyNetworkManager.Instance?.RegistrarTorre(torreInstanciada.gameObject);
    ///   </code>
    ///   Y en la lógica de venta/destrucción:
    ///   <code>
    ///     EnergyNetworkManager.Instance?.DesregistrarTorre(torreInstanciada.gameObject);
    ///   </code>
    /// </summary>
    [AddComponentMenu("RedEnergia/Energy Network Manager")]
    public class EnergyNetworkManager : MonoBehaviour
    {
        // ─── Singleton ───────────────────────────────────────────────────────────
        public static EnergyNetworkManager Instance { get; private set; }

        // ─── Configuración Visual ────────────────────────────────────────────────
        [Header("Visual: Rayos de Energía")]
        [Tooltip("Color del rayo de energía en el nodo origen.")]
        public Color colorOrigenRayo = new Color(0.2f, 0.8f, 1f, 1f);   // Cian energético

        [Tooltip("Color del rayo de energía en el nodo destino.")]
        public Color colorDestinoRayo = new Color(0.5f, 0.2f, 1f, 1f);  // Púrpura

        [Tooltip("Anchura del rayo de energía (unidades de mundo).")]
        public float anchoRayo = 0.08f;

        [Tooltip("Orden de renderizado del LineRenderer (sorting order).")]
        public int sortingOrderRayo = 15;

        [Tooltip("Nombre del Sorting Layer para los rayos (dejar vacío para 'Default').")]
        public string sortingLayerRayo = "Default";

        [Tooltip("Material para el LineRenderer. Si es null se usará el material por defecto de Unity.")]
        public Material materialRayo;

        // ─── Configuración de Sinergia ───────────────────────────────────────────
        [Header("Sinergia")]
        [Tooltip("Distancia máxima entre torres para que se intente conectarlas. " +
                 "0 = sin límite (grafo completo siempre).")]
        public float distanciaMaximaConexion = 0f;

        // ─── Estado interno ──────────────────────────────────────────────────────
        /// <summary>Lista de nodos activos en la red (una entrada por torre viva).</summary>
        private readonly List<TowerNode> nodos = new List<TowerNode>();

        /// <summary>Aristas del MST calculadas en el último recálculo.</summary>
        private readonly List<TowerEdge> aristasMST = new List<TowerEdge>();

        /// <summary>Pool de LineRenderers reutilizables para los rayos visuales.</summary>
        private readonly List<LineRenderer> poolLineRenderers = new List<LineRenderer>();

        // ─── Unity Lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── API Pública ─────────────────────────────────────────────────────────

        /// <summary>
        /// Registra el <see cref="GameObject"/> de una torre recién colocada en la red.
        /// Agrega (o reutiliza) un <see cref="TowerNode"/> y lanza el recálculo del MST.
        /// </summary>
        /// <param name="torreGO">
        ///   GameObject que contiene o es la torre. Puede ser la instancia de
        ///   <c>TowerDefenseGameplay.Torre</c> o de <c>TowerBehavior</c>.
        /// </param>
        public void RegistrarTorre(GameObject torreGO)
        {
            if (torreGO == null) return;

            // Reutilizar TowerNode si ya existe (evita duplicados)
            TowerNode nodo = torreGO.GetComponent<TowerNode>();
            if (nodo == null)
            {
                nodo = torreGO.AddComponent<TowerNode>();
            }

            if (!nodos.Contains(nodo))
            {
                nodos.Add(nodo);
                Debug.Log($"[EnergyNetworkManager] Torre registrada: {torreGO.name}. Nodos activos: {nodos.Count}");
            }

            RecalcularRed();
        }

        /// <summary>
        /// Elimina la torre del registro de la red, quita sus buffs y recalcula el MST.
        /// Llama a este método antes de destruir el <see cref="GameObject"/> de la torre.
        /// </summary>
        public void DesregistrarTorre(GameObject torreGO)
        {
            if (torreGO == null) return;

            TowerNode nodo = torreGO.GetComponent<TowerNode>();
            if (nodo != null && nodos.Remove(nodo))
            {
                nodo.EliminarBuffsDeRed();
                Debug.Log($"[EnergyNetworkManager] Torre removida: {torreGO.name}. Nodos activos: {nodos.Count}");
            }

            RecalcularRed();
        }

        /// <summary>
        /// Fuerza un recálculo completo de la red de energía:
        /// 1. Limpia estado anterior.
        /// 2. Construye el grafo completo.
        /// 3. Ejecuta Kruskal para obtener el MST.
        /// 4. Aplica sinergia a los nodos.
        /// 5. Actualiza el render visual de los rayos.
        /// </summary>
        public void RecalcularRed()
        {
            // Limpiar referencias nulas (torres destruidas sin desregistrar)
            nodos.RemoveAll(n => n == null);

            aristasMST.Clear();
            ResetearBuffsNodos();

            if (nodos.Count < 2)
            {
                // Con menos de 2 torres no hay aristas posibles
                OcultarTodosLosRayos();
                return;
            }

            // 1. Construir grafo completo G = (V, E)
            List<TowerEdge> todasAristas = ConstruirGrafoCompleto();

            // 2. Ejecutar Kruskal → MST
            aristasMST.AddRange(EjecutarKruskal(todasAristas));

            // 3. Aplicar sinergia de red a cada nodo
            AplicarSinergias();

            // 4. Renderizar rayos de energía
            RenderizarRayos();

            Debug.Log($"[EnergyNetworkManager] Red recalculada. MST: {aristasMST.Count} aristas | " +
                      $"Peso total: {ObtenerPesoTotalMST():F2}");
        }

        // ─── Construcción del Grafo ──────────────────────────────────────────────

        /// <summary>
        /// Genera todas las combinaciones C(n,2) de pares de nodos, calculando la
        /// distancia euclidiana 3D como peso de cada arista.
        /// </summary>
        private List<TowerEdge> ConstruirGrafoCompleto()
        {
            int n = nodos.Count;
            // Capacidad exacta para el grafo completo: n*(n-1)/2
            List<TowerEdge> aristas = new List<TowerEdge>(n * (n - 1) / 2);

            for (int i = 0; i < n - 1; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    float distancia = Vector3.Distance(
                        nodos[i].transform.position,
                        nodos[j].transform.position
                    );

                    // Filtro opcional de distancia máxima
                    if (distanciaMaximaConexion > 0f && distancia > distanciaMaximaConexion)
                    {
                        continue;
                    }

                    aristas.Add(new TowerEdge(nodos[i], nodos[j], distancia));
                }
            }

            return aristas;
        }

        // ─── Algoritmo de Kruskal ────────────────────────────────────────────────

        /// <summary>
        /// Implementación del algoritmo de Kruskal para Árbol de Expansión Mínima (MST).
        ///
        /// Pasos:
        ///   1. Ordenar todas las aristas por peso ascendente  → O(E log E).
        ///   2. Iterar y añadir la arista al MST si no crea ciclo (Union-Find) → O(E α(V)).
        ///   3. Terminar cuando se tengan V-1 aristas o se agoten todas las candidatas.
        ///
        /// Complejidad total: O(E log E) donde E = n(n-1)/2.
        /// </summary>
        private List<TowerEdge> EjecutarKruskal(List<TowerEdge> aristas)
        {
            // Paso 1 – Ordenar aristas por peso (IComparable definido en TowerEdge)
            aristas.Sort();

            int n = nodos.Count;
            DisjointSet uf  = new DisjointSet(n);
            List<TowerEdge> mst = new List<TowerEdge>(n - 1);

            // Mapeo nodo → índice para la estructura Union-Find
            Dictionary<TowerNode, int> indiceNodo = new Dictionary<TowerNode, int>(n);
            for (int i = 0; i < n; i++)
            {
                indiceNodo[nodos[i]] = i;
            }

            // Paso 2 – Iterar sobre aristas ordenadas
            foreach (TowerEdge arista in aristas)
            {
                // Si ya tenemos V-1 aristas, el MST está completo
                if (mst.Count == n - 1) break;

                int idxA = indiceNodo[arista.source];
                int idxB = indiceNodo[arista.target];

                // Union retorna true solo si conecta componentes distintos (sin ciclo)
                if (uf.Union(idxA, idxB))
                {
                    mst.Add(arista);
                }
            }

            return mst;
        }

        // ─── Aplicación de Sinergia ──────────────────────────────────────────────

        /// <summary>
        /// Resetea los multiplicadores de todos los nodos a su estado neutro (1.0f)
        /// antes de aplicar la nueva ronda de buffs del MST recalculado.
        /// </summary>
        private void ResetearBuffsNodos()
        {
            foreach (TowerNode nodo in nodos)
            {
                if (nodo == null) continue;
                nodo.multiplicadorDano  = 1.0f;
                nodo.multiplicadorRango = 1.0f;
                nodo.grado              = 0;
            }
        }

        /// <summary>
        /// Calcula el grado de cada nodo en el MST y aplica los multiplicadores
        /// de daño y rango proporcionalmente al número de conexiones directas.
        ///
        /// Fórmula:
        ///   multiplicadorDaño  = 1.0 + grado × 0.15   (+15% por conexión)
        ///   multiplicadorRango = 1.0 + grado × 0.10   (+10% por conexión)
        /// </summary>
        private void AplicarSinergias()
        {
            // Contar grado de cada nodo en el MST
            foreach (TowerEdge arista in aristasMST)
            {
                if (arista.source != null) arista.source.grado++;
                if (arista.target != null) arista.target.grado++;
            }

            // Calcular multiplicadores y aplicar buffs
            foreach (TowerNode nodo in nodos)
            {
                if (nodo == null) continue;
                nodo.ActualizarMultiplicadores(nodo.grado);
                nodo.AplicarBuffsDeRed();

                Debug.Log($"[EnergyNetworkManager] {nodo.name} | Grado={nodo.grado} | " +
                          $"×Daño={nodo.multiplicadorDano:F2} | ×Rango={nodo.multiplicadorRango:F2}");
            }
        }

        // ─── Renderizado Visual (LineRenderer Pool) ──────────────────────────────

        /// <summary>
        /// Dibuja los rayos de energía para cada arista del MST usando el pool de
        /// <see cref="LineRenderer"/>. Los LineRenderers sobrantes se desactivan.
        /// </summary>
        private void RenderizarRayos()
        {
            int cantidadAristas = aristasMST.Count;

            // Asegurar que el pool tenga suficientes entradas
            while (poolLineRenderers.Count < cantidadAristas)
            {
                poolLineRenderers.Add(CrearLineRenderer());
            }

            // Actualizar los LineRenderers activos con las posiciones del MST
            for (int i = 0; i < cantidadAristas; i++)
            {
                TowerEdge arista = aristasMST[i];
                LineRenderer lr  = poolLineRenderers[i];

                if (arista.source == null || arista.target == null)
                {
                    lr.gameObject.SetActive(false);
                    continue;
                }

                lr.gameObject.SetActive(true);
                lr.SetPosition(0, arista.source.transform.position);
                lr.SetPosition(1, arista.target.transform.position);
            }

            // Desactivar los LineRenderers sobrantes del pool
            OcultarRayosDesde(cantidadAristas);
        }

        /// <summary>
        /// Fabrica un nuevo <see cref="LineRenderer"/> hijo de este objeto con la
        /// configuración visual estándar del sistema de energía.
        /// </summary>
        private LineRenderer CrearLineRenderer()
        {
            GameObject lrObj = new GameObject("RayoEnergia");
            lrObj.transform.SetParent(transform, false);

            LineRenderer lr = lrObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth    = anchoRayo;
            lr.endWidth      = anchoRayo;
            lr.startColor    = colorOrigenRayo;
            lr.endColor      = colorDestinoRayo;
            lr.sortingOrder  = sortingOrderRayo;
            lr.sortingLayerName = sortingLayerRayo;

            // Material: usar el asignado o el Legacy/Particles por defecto
            if (materialRayo != null)
            {
                lr.material = materialRayo;
            }
            else
            {
                // Fallback al shader más compatible (funciona sin URP/HDRP)
                lr.material = new Material(Shader.Find("Sprites/Default") ??
                                           Shader.Find("Hidden/InternalErrorShader"));
            }

            return lr;
        }

        /// <summary>Desactiva todos los LineRenderers del pool (red vacía o 1 sola torre).</summary>
        private void OcultarTodosLosRayos() => OcultarRayosDesde(0);

        /// <summary>Desactiva los LineRenderers del pool a partir del índice dado.</summary>
        private void OcultarRayosDesde(int desdeIndice)
        {
            for (int i = desdeIndice; i < poolLineRenderers.Count; i++)
            {
                if (poolLineRenderers[i] != null)
                {
                    poolLineRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        // ─── Utilidades ──────────────────────────────────────────────────────────

        /// <summary>Devuelve el peso total (suma de distancias) del MST calculado.</summary>
        public float ObtenerPesoTotalMST()
        {
            float total = 0f;
            foreach (TowerEdge a in aristasMST) total += a.weight;
            return total;
        }

        /// <summary>Devuelve una copia de sólo lectura de las aristas del MST actual.</summary>
        public IReadOnlyList<TowerEdge> ObtenerAristasMST() => aristasMST.AsReadOnly();

        /// <summary>Devuelve una copia de sólo lectura de los nodos activos en la red.</summary>
        public IReadOnlyList<TowerNode> ObtenerNodos() => nodos.AsReadOnly();

        // ─── Gizmos de Depuración ────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (aristasMST == null) return;

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            foreach (TowerEdge arista in aristasMST)
            {
                if (arista.source == null || arista.target == null) continue;
                Gizmos.DrawLine(arista.source.transform.position, arista.target.transform.position);
            }

            // Esferas en cada nodo de la red
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.9f);
            foreach (TowerNode nodo in nodos)
            {
                if (nodo == null) continue;
                Gizmos.DrawSphere(nodo.transform.position, 0.18f);
            }
        }
    }
}
