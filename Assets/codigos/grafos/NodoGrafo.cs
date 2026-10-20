using System;
using System.Collections.Generic;

namespace GrafosEST2
{
    /// <summary>
    /// Representa un vértice o nodo en un grafo G = (V, E).
    /// </summary>
    /// <typeparam name="T">Tipo del dato contenido en el nodo.</typeparam>
    [Serializable]
    public class NodoGrafo<T>
    {
        public string Id;
        public T Valor;
        public List<Arista<T>> Vecinos;

        public NodoGrafo(string id, T valor)
        {
            Id = id;
            Valor = valor;
            Vecinos = new List<Arista<T>>();
        }

        public override string ToString()
        {
            return $"[Nodo: {Id} | Valor: {Valor} | Vecinos: {Vecinos.Count}]";
        }
    }
}
