namespace RedEnergiaMST
{
    /// <summary>
    /// Arista ponderada no dirigida para el grafo completo de torres.
    /// El peso representa la distancia euclidiana 3D entre las dos torres.
    /// Se usa como insumo del algoritmo de Kruskal para obtener el MST.
    /// </summary>
    public struct TowerEdge : System.IComparable<TowerEdge>
    {
        /// <summary>Nodo de origen de la arista.</summary>
        public TowerNode source;

        /// <summary>Nodo de destino de la arista.</summary>
        public TowerNode target;

        /// <summary>
        /// Peso de la arista: distancia euclidiana 3D entre source y target
        /// calculada con <c>Vector3.Distance</c>.
        /// </summary>
        public float weight;

        public TowerEdge(TowerNode source, TowerNode target, float weight)
        {
            this.source = source;
            this.target = target;
            this.weight = weight;
        }

        /// <summary>
        /// Permite ordenar las aristas por peso de forma ascendente, 
        /// requisito de Kruskal para procesar siempre la arista más liviana primero.
        /// </summary>
        public int CompareTo(TowerEdge other) => weight.CompareTo(other.weight);

        public override string ToString() =>
            $"[{source?.name} <-> {target?.name}  w={weight:F2}]";
    }
}
