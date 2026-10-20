using System.Collections.Generic;

namespace RedEnergiaMST
{
    /// <summary>
    /// Estructura Disjoint Set (Union-Find) con compresión de ruta y unión por rango.
    /// Permite verificar en tiempo casi O(α(n)) si dos nodos pertenecen al mismo componente
    /// conexo, siendo el núcleo del algoritmo de Kruskal para detección de ciclos.
    ///
    /// Complejidad:
    ///   - Find  : O(α(n)) amortizado  (α = función inversa de Ackermann, prácticamente constante)
    ///   - Union : O(α(n)) amortizado
    ///   - Espacio: O(n)
    /// </summary>
    public class DisjointSet
    {
        // Índice del representante (raíz) de cada elemento
        private readonly int[] parent;

        // Rango aproximado del árbol de cada subconjunto (para union por rango)
        private readonly int[] rank;

        /// <summary>Crea la estructura con <paramref name="n"/> elementos, numerados 0..n-1.</summary>
        public DisjointSet(int n)
        {
            parent = new int[n];
            rank   = new int[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;  // Cada elemento es su propio representante al inicio
                rank[i]   = 0;
            }
        }

        /// <summary>
        /// Encuentra el representante (raíz) del subconjunto al que pertenece <paramref name="x"/>.
        /// Aplica compresión de ruta: todos los nodos visitados apuntan directamente a la raíz,
        /// acelerando futuras consultas.
        /// </summary>
        public int Find(int x)
        {
            if (parent[x] != x)
            {
                // Compresión de ruta: aplana el árbol recursivamente
                parent[x] = Find(parent[x]);
            }
            return parent[x];
        }

        /// <summary>
        /// Une los subconjuntos que contienen a <paramref name="a"/> y <paramref name="b"/>.
        /// Utiliza unión por rango para mantener los árboles lo más planos posible.
        /// </summary>
        /// <returns>
        ///   <c>true</c> si la unión fue exitosa (estaban en componentes distintos);
        ///   <c>false</c> si ya pertenecían al mismo componente (indica ciclo en Kruskal).
        /// </returns>
        public bool Union(int a, int b)
        {
            int raizA = Find(a);
            int raizB = Find(b);

            if (raizA == raizB)
            {
                return false; // Ya conectados → la arista formaría un ciclo
            }

            // Unión por rango: el árbol de menor rango se cuelga del de mayor rango
            if (rank[raizA] < rank[raizB])
            {
                parent[raizA] = raizB;
            }
            else if (rank[raizA] > rank[raizB])
            {
                parent[raizB] = raizA;
            }
            else
            {
                parent[raizB] = raizA;
                rank[raizA]++;
            }

            return true;
        }

        /// <summary>
        /// Verifica si <paramref name="a"/> y <paramref name="b"/> están en el mismo componente conexo.
        /// </summary>
        public bool Conectados(int a, int b) => Find(a) == Find(b);
    }
}
