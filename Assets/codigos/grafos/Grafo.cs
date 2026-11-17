using System;
using System.Collections.Generic;
using System.Text;

namespace GrafosEST2
{
    /// <summary>
    /// Estructura de datos de Grafo Genérico G = (V, E) basada en Lista de Adyacencia.
    /// Soporta grafos dirigidos y no dirigidos, algoritmos de recorrido (BFS, DFS),
    /// detección de ciclos y ordenamiento topológico mediante el Algoritmo de Kahn.
    /// </summary>
    /// <typeparam name="T">Tipo del dato almacenado en los nodos.</typeparam>
    [Serializable]
    public class Grafo<T>
    {
        public Dictionary<string, NodoGrafo<T>> Nodos { get; private set; }
        public bool EsDirigido { get; set; }

        public int CantidadVertices => Nodos.Count;

        public Grafo(bool esDirigido = true)
        {
            Nodos = new Dictionary<string, NodoGrafo<T>>();
            EsDirigido = esDirigido;
        }

        #region Operaciones Básicas

        /// <summary>
        /// Agrega un nuevo vértice al grafo.
        /// </summary>
        public bool AgregarNodo(string id, T valor)
        {
            if (string.IsNullOrEmpty(id) || Nodos.ContainsKey(id))
            {
                return false;
            }

            Nodos[id] = new NodoGrafo<T>(id, valor);
            return true;
        }

        /// <summary>
        /// Comprueba si existe un nodo con el ID dado.
        /// </summary>
        public bool ExisteNodo(string id)
        {
            return !string.IsNullOrEmpty(id) && Nodos.ContainsKey(id);
        }

        /// <summary>
        /// Obtiene el nodo con el ID especificado.
        /// </summary>
        public NodoGrafo<T> ObtenerNodo(string id)
        {
            if (ExisteNodo(id))
            {
                return Nodos[id];
            }
            return null;
        }

        /// <summary>
        /// Agrega una arista dirigida (o bidireccional si no es dirigido) entre dos nodos.
        /// </summary>
        public bool AgregarArista(string idOrigen, string idDestino, float peso = 1f)
        {
            if (!ExisteNodo(idOrigen) || !ExisteNodo(idDestino))
            {
                return false;
            }

            NodoGrafo<T> origen = Nodos[idOrigen];
            NodoGrafo<T> destino = Nodos[idDestino];

            // Evitar duplicar arista exacta
            if (!ExisteArista(idOrigen, idDestino))
            {
                origen.Vecinos.Add(new Arista<T>(destino, peso));
            }

            if (!EsDirigido && !ExisteArista(idDestino, idOrigen))
            {
                destino.Vecinos.Add(new Arista<T>(origen, peso));
            }

            return true;
        }

        /// <summary>
        /// Verifica si existe una arista directa desde origen hacia destino.
        /// </summary>
        public bool ExisteArista(string idOrigen, string idDestino)
        {
            if (!ExisteNodo(idOrigen) || !ExisteNodo(idDestino)) return false;

            NodoGrafo<T> origen = Nodos[idOrigen];
            for (int i = 0; i < origen.Vecinos.Count; i++)
            {
                if (origen.Vecinos[i].Destino.Id == idDestino)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Elimina una arista existente entre dos nodos.
        /// </summary>
        public bool EliminarArista(string idOrigen, string idDestino)
        {
            if (!ExisteNodo(idOrigen) || !ExisteNodo(idDestino)) return false;

            NodoGrafo<T> origen = Nodos[idOrigen];
            int removidos = origen.Vecinos.RemoveAll(a => a.Destino.Id == idDestino);

            if (!EsDirigido)
            {
                NodoGrafo<T> destino = Nodos[idDestino];
                destino.Vecinos.RemoveAll(a => a.Destino.Id == idOrigen);
            }

            return removidos > 0;
        }

        /// <summary>
        /// Elimina un nodo y todas las aristas entrantes y salientes asociadas.
        /// </summary>
        public bool EliminarNodo(string id)
        {
            if (!ExisteNodo(id)) return false;

            // Eliminar aristas entrantes desde otros nodos
            foreach (var kvp in Nodos)
            {
                if (kvp.Key != id)
                {
                    kvp.Value.Vecinos.RemoveAll(a => a.Destino.Id == id);
                }
            }

            return Nodos.Remove(id);
        }

        /// <summary>
        /// Obtiene la lista de nodos predecesores (aquellos u tales que existe la arista u -> v).
        /// Fundamental para consultar prerrequisitos en grafos DAG de dependencias.
        /// </summary>
        public List<NodoGrafo<T>> ObtenerPredecesores(string idNodo)
        {
            List<NodoGrafo<T>> predecesores = new List<NodoGrafo<T>>();
            if (!ExisteNodo(idNodo)) return predecesores;

            foreach (var kvp in Nodos)
            {
                for (int i = 0; i < kvp.Value.Vecinos.Count; i++)
                {
                    if (kvp.Value.Vecinos[i].Destino.Id == idNodo)
                    {
                        predecesores.Add(kvp.Value);
                        break;
                    }
                }
            }

            return predecesores;
        }

        /// <summary>
        /// Limpia todos los nodos y aristas del grafo.
        /// </summary>
        public void Limpiar()
        {
            Nodos.Clear();
        }

        #endregion

        #region Grados y Métricas de Vértices

        /// <summary>
        /// Calcula el grado de entrada (in-degree) para todos los vértices del grafo.
        /// Grado de entrada: número de aristas que apuntan hacia el nodo.
        /// </summary>
        public Dictionary<string, int> CalcularGradosEntrada()
        {
            Dictionary<string, int> grados = new Dictionary<string, int>();

            foreach (string id in Nodos.Keys)
            {
                grados[id] = 0;
            }

            foreach (var kvp in Nodos)
            {
                for (int i = 0; i < kvp.Value.Vecinos.Count; i++)
                {
                    string idDest = kvp.Value.Vecinos[i].Destino.Id;
                    if (grados.ContainsKey(idDest))
                    {
                        grados[idDest]++;
                    }
                }
            }

            return grados;
        }

        /// <summary>
        /// Calcula el grado de salida (out-degree) para todos los vértices del grafo.
        /// </summary>
        public Dictionary<string, int> CalcularGradosSalida()
        {
            Dictionary<string, int> grados = new Dictionary<string, int>();

            foreach (var kvp in Nodos)
            {
                grados[kvp.Key] = kvp.Value.Vecinos.Count;
            }

            return grados;
        }

        #endregion

        #region Algoritmos para Grafos Dirigidos Acíclicos (DAG)

        /// <summary>
        /// Implementación del Algoritmo de Kahn para Ordenamiento Topológico (Topological Sort).
        /// Complejidad: O(V + E) en tiempo y espacio.
        /// Retorna una lista con la secuencia topológica si es un DAG válido.
        /// Si el grafo contiene al menos un ciclo, 'tieneCiclo' será true y la lista estará incompleta.
        /// </summary>
        public bool OrdenamientoTopologicoKahn(out List<string> orden, out bool tieneCiclo)
        {
            orden = new List<string>();
            tieneCiclo = false;

            if (Nodos.Count == 0) return true;

            // 1. Calcular in-degree de cada vértice
            Dictionary<string, int> inDegree = CalcularGradosEntrada();

            // 2. Cola FIFO con todos los vértices cuyo in-degree es 0 (sin prerrequisitos)
            Queue<string> cola = new Queue<string>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    cola.Enqueue(kvp.Key);
                }
            }

            // 3. Procesar nodos
            int nodosProcesados = 0;
            while (cola.Count > 0)
            {
                string actual = cola.Dequeue();
                orden.Add(actual);
                nodosProcesados++;

                NodoGrafo<T> nodoActual = Nodos[actual];
                for (int i = 0; i < nodoActual.Vecinos.Count; i++)
                {
                    string vecinoId = nodoActual.Vecinos[i].Destino.Id;
                    inDegree[vecinoId]--;

                    // Si todos sus prerrequisitos fueron satisfechos, ingresa a la cola
                    if (inDegree[vecinoId] == 0)
                    {
                        cola.Enqueue(vecinoId);
                    }
                }
            }

            // 4. Si la cantidad de procesados es menor que el total de vértices, existe un ciclo
            if (nodosProcesados != Nodos.Count)
            {
                tieneCiclo = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Detecta ciclos en el grafo dirigido usando DFS con coloreo de 3 estados:
        /// 0 = Blanco (No visitado), 1 = Gris (Visitando en rama activa), 2 = Negro (Completamente procesado).
        /// Si detecta un ciclo, retorna true y llena 'caminoCiclo' con la secuencia de vértices que lo forman.
        /// </summary>
        public bool DetectarCiclos(out List<string> caminoCiclo)
        {
            caminoCiclo = new List<string>();
            Dictionary<string, int> colores = new Dictionary<string, int>(); // 0: Blanco, 1: Gris, 2: Negro
            Dictionary<string, string> predecesores = new Dictionary<string, string>();

            foreach (string id in Nodos.Keys)
            {
                colores[id] = 0;
            }

            foreach (string id in Nodos.Keys)
            {
                if (colores[id] == 0)
                {
                    if (DFSCiclo(id, colores, predecesores, out string inicioCiclo, out string finCiclo))
                    {
                        // Reconstruir el ciclo
                        caminoCiclo.Add(inicioCiclo);
                        string curr = finCiclo;
                        while (curr != inicioCiclo && !string.IsNullOrEmpty(curr))
                        {
                            caminoCiclo.Add(curr);
                            predecesores.TryGetValue(curr, out curr);
                        }
                        caminoCiclo.Add(inicioCiclo);
                        caminoCiclo.Reverse();
                        return true;
                    }
                }
            }

            return false;
        }

        private bool DFSCiclo(string u, Dictionary<string, int> colores, Dictionary<string, string> predecesores, out string inicioCiclo, out string finCiclo)
        {
            colores[u] = 1; // Gris (en la pila de recursión)

            NodoGrafo<T> nodoU = Nodos[u];
            for (int i = 0; i < nodoU.Vecinos.Count; i++)
            {
                string v = nodoU.Vecinos[i].Destino.Id;

                if (colores[v] == 1)
                {
                    // ¡Arista de retroceso detectada! Se ha encontrado un ciclo
                    inicioCiclo = v;
                    finCiclo = u;
                    return true;
                }

                if (colores[v] == 0)
                {
                    predecesores[v] = u;
                    if (DFSCiclo(v, colores, predecesores, out inicioCiclo, out finCiclo))
                    {
                        return true;
                    }
                }
            }

            colores[u] = 2; // Negro (terminado)
            inicioCiclo = null;
            finCiclo = null;
            return false;
        }

        #endregion

        #region Recorridos Clásicos: BFS y DFS

        /// <summary>
        /// Recorrido en Anchura (Breadth-First Search) desde un vértice inicial usando una Cola FIFO.
        /// Complejidad: O(V + E)
        /// </summary>
        public List<string> RecorridoBFS(string idInicio)
        {
            List<string> resultado = new List<string>();
            if (!ExisteNodo(idInicio)) return resultado;

            HashSet<string> visitados = new HashSet<string>();
            Queue<string> cola = new Queue<string>();

            visitados.Add(idInicio);
            cola.Enqueue(idInicio);

            while (cola.Count > 0)
            {
                string actual = cola.Dequeue();
                resultado.Add(actual);

                NodoGrafo<T> nodo = Nodos[actual];
                for (int i = 0; i < nodo.Vecinos.Count; i++)
                {
                    string vecinoId = nodo.Vecinos[i].Destino.Id;
                    if (!visitados.Contains(vecinoId))
                    {
                        visitados.Add(vecinoId);
                        cola.Enqueue(vecinoId);
                    }
                }
            }

            return resultado;
        }

        /// <summary>
        /// Recorrido en Profundidad (Depth-First Search) desde un vértice inicial usando una Pila LIFO.
        /// Complejidad: O(V + E)
        /// </summary>
        public List<string> RecorridoDFS(string idInicio)
        {
            List<string> resultado = new List<string>();
            if (!ExisteNodo(idInicio)) return resultado;

            HashSet<string> visitados = new HashSet<string>();
            Stack<string> pila = new Stack<string>();

            pila.Push(idInicio);

            while (pila.Count > 0)
            {
                string actual = pila.Pop();

                if (!visitados.Contains(actual))
                {
                    visitados.Add(actual);
                    resultado.Add(actual);

                    NodoGrafo<T> nodo = Nodos[actual];
                    // Recorrer en orden inverso para preservar orden intuitivo de adyacencia
                    for (int i = nodo.Vecinos.Count - 1; i >= 0; i--)
                    {
                        string vecinoId = nodo.Vecinos[i].Destino.Id;
                        if (!visitados.Contains(vecinoId))
                        {
                            pila.Push(vecinoId);
                        }
                    }
                }
            }

            return resultado;
        }

        #endregion

        #region Caminos Mínimos (Dijkstra) y Árbol de Expansión Mínima (Kruskal)

        /// <summary>
        /// Algoritmo de Dijkstra desde un vértice origen. Requiere pesos no negativos.
        /// Devuelve las distancias mínimas, los predecesores y el orden en que se fijaron los vértices.
        /// Complejidad: O(V^2) con extracción lineal del mínimo (adecuado para grafos pequeños).
        /// </summary>
        public List<string> Dijkstra(string idInicio, out Dictionary<string, float> distancias, out Dictionary<string, string> previos)
        {
            distancias = new Dictionary<string, float>();
            previos = new Dictionary<string, string>();
            List<string> ordenFijado = new List<string>();
            if (!ExisteNodo(idInicio)) return ordenFijado;

            HashSet<string> pendientes = new HashSet<string>();
            foreach (string id in Nodos.Keys)
            {
                distancias[id] = float.MaxValue;
                pendientes.Add(id);
            }
            distancias[idInicio] = 0f;

            while (pendientes.Count > 0)
            {
                // Extraer el vértice pendiente con menor distancia tentativa
                string actual = null;
                float mejor = float.MaxValue;
                foreach (string id in pendientes)
                {
                    if (distancias[id] < mejor)
                    {
                        mejor = distancias[id];
                        actual = id;
                    }
                }
                if (actual == null) break; // el resto es inalcanzable

                pendientes.Remove(actual);
                ordenFijado.Add(actual);

                // Relajación de aristas salientes
                NodoGrafo<T> nodo = Nodos[actual];
                for (int i = 0; i < nodo.Vecinos.Count; i++)
                {
                    Arista<T> arista = nodo.Vecinos[i];
                    string v = arista.Destino.Id;
                    if (!pendientes.Contains(v)) continue;
                    float nueva = distancias[actual] + Math.Max(0f, arista.Peso);
                    if (nueva < distancias[v])
                    {
                        distancias[v] = nueva;
                        previos[v] = actual;
                    }
                }
            }

            return ordenFijado;
        }

        /// <summary>
        /// Camino mínimo entre dos vértices. Retorna null si el destino es inalcanzable.
        /// </summary>
        public List<string> CaminoMinimo(string idInicio, string idFin, out float costo)
        {
            costo = float.MaxValue;
            if (!ExisteNodo(idInicio) || !ExisteNodo(idFin)) return null;

            Dijkstra(idInicio, out Dictionary<string, float> dist, out Dictionary<string, string> prev);
            if (dist[idFin] == float.MaxValue) return null;

            costo = dist[idFin];
            List<string> camino = new List<string>();
            string actual = idFin;
            camino.Add(actual);
            while (prev.ContainsKey(actual))
            {
                actual = prev[actual];
                camino.Insert(0, actual);
            }
            return camino;
        }

        /// <summary>
        /// Arista simple (u, v, peso) usada por Kruskal.
        /// </summary>
        public struct AristaSimple
        {
            public string U;
            public string V;
            public float Peso;
        }

        /// <summary>
        /// Lista las aristas del grafo. En grafos no dirigidos cada par aparece una sola vez.
        /// </summary>
        public List<AristaSimple> ObtenerAristas()
        {
            List<AristaSimple> lista = new List<AristaSimple>();
            HashSet<string> vistos = new HashSet<string>();
            foreach (var kvp in Nodos)
            {
                for (int i = 0; i < kvp.Value.Vecinos.Count; i++)
                {
                    string u = kvp.Key;
                    string v = kvp.Value.Vecinos[i].Destino.Id;
                    if (!EsDirigido)
                    {
                        string clave = string.CompareOrdinal(u, v) < 0 ? u + "|" + v : v + "|" + u;
                        if (!vistos.Add(clave)) continue;
                    }
                    lista.Add(new AristaSimple { U = u, V = v, Peso = kvp.Value.Vecinos[i].Peso });
                }
            }
            return lista;
        }

        /// <summary>
        /// Algoritmo de Kruskal con Union-Find. Trata las aristas como no dirigidas.
        /// Si el grafo no es conexo devuelve un bosque de expansión mínima.
        /// Complejidad: O(E log E).
        /// </summary>
        public List<AristaSimple> KruskalMST(out float pesoTotal, out List<AristaSimple> descartadas)
        {
            pesoTotal = 0f;
            descartadas = new List<AristaSimple>();
            List<AristaSimple> mst = new List<AristaSimple>();

            List<string> ids = new List<string>(Nodos.Keys);
            Dictionary<string, int> indice = new Dictionary<string, int>();
            DisjointSet uf = new DisjointSet();
            for (int i = 0; i < ids.Count; i++)
            {
                indice[ids[i]] = i;
                uf.MakeSet(i);
            }

            List<AristaSimple> aristas = ObtenerAristas();
            aristas.Sort((a, b) => a.Peso.CompareTo(b.Peso));

            for (int i = 0; i < aristas.Count; i++)
            {
                if (uf.Union(indice[aristas[i].U], indice[aristas[i].V]))
                {
                    mst.Add(aristas[i]);
                    pesoTotal += aristas[i].Peso;
                }
                else
                {
                    descartadas.Add(aristas[i]); // formaría ciclo
                }
            }
            return mst;
        }

        /// <summary>
        /// Representación textual de la Lista de Adyacencia.
        /// </summary>
        public string ListaAdyacenciaToString()
        {
            if (Nodos.Count == 0) return "El grafo no contiene vértices.";
            StringBuilder sb = new StringBuilder();
            foreach (var kvp in Nodos)
            {
                sb.Append(kvp.Key).Append(" → ");
                if (kvp.Value.Vecinos.Count == 0) sb.Append("∅");
                for (int i = 0; i < kvp.Value.Vecinos.Count; i++)
                {
                    Arista<T> a = kvp.Value.Vecinos[i];
                    sb.Append(a.Destino.Id).Append('(').Append(a.Peso.ToString("0.#")).Append(')');
                    if (i < kvp.Value.Vecinos.Count - 1) sb.Append(", ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        #endregion

        #region Representación en Matriz de Adyacencia

        /// <summary>
        /// Genera la Matriz de Adyacencia correspondiente al grafo actual.
        /// </summary>
        public void GenerarMatrizAdyacencia(out List<string> listaIds, out float[,] matriz)
        {
            listaIds = new List<string>(Nodos.Keys);
            int n = listaIds.Count;
            matriz = new float[n, n];

            Dictionary<string, int> indices = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                indices[listaIds[i]] = i;
            }

            for (int i = 0; i < n; i++)
            {
                string idU = listaIds[i];
                NodoGrafo<T> nodo = Nodos[idU];
                for (int k = 0; k < nodo.Vecinos.Count; k++)
                {
                    Arista<T> arista = nodo.Vecinos[k];
                    if (indices.TryGetValue(arista.Destino.Id, out int j))
                    {
                        matriz[i, j] = arista.Peso;
                    }
                }
            }
        }

        /// <summary>
        /// Genera un texto con formato de tabla mostrando la Matriz de Adyacencia.
        /// </summary>
        public string MatrizAdyacenciaToString()
        {
            GenerarMatrizAdyacencia(out List<string> ids, out float[,] matriz);
            int n = ids.Count;
            if (n == 0) return "El grafo no contiene vértices.";

            StringBuilder sb = new StringBuilder();
            sb.Append("      ");
            for (int j = 0; j < n; j++)
            {
                string cabecera = ids[j].Length > 5 ? ids[j].Substring(0, 5) : ids[j].PadRight(5);
                sb.Append(cabecera + " ");
            }
            sb.AppendLine();

            for (int i = 0; i < n; i++)
            {
                string filaId = ids[i].Length > 5 ? ids[i].Substring(0, 5) : ids[i].PadRight(5);
                sb.Append(filaId + " ");

                for (int j = 0; j < n; j++)
                {
                    float val = matriz[i, j];
                    string strVal = val == 0 ? "  .  " : val.ToString("0.0").PadLeft(5);
                    sb.Append(strVal + " ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion
    }
}
