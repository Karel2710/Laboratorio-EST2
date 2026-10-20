using System;

namespace GrafosEST2
{
    /// <summary>
    /// Representa una arista dirigida y ponderada en un grafo G = (V, E).
    /// </summary>
    /// <typeparam name="T">Tipo del dato contenido en el nodo destino.</typeparam>
    [Serializable]
    public class Arista<T>
    {
        public NodoGrafo<T> Destino;
        public float Peso;

        public Arista(NodoGrafo<T> destino, float peso = 1f)
        {
            Destino = destino;
            Peso = peso;
        }

        public override string ToString()
        {
            return $"-> [Destino: {Destino?.Id}] (Peso: {Peso})";
        }
    }
}
