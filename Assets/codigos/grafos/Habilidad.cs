using System;

namespace GrafosEST2
{
    /// <summary>
    /// Modelo de datos que representa un nodo en el Árbol/Grafo de Habilidades del Jugador (DAG).
    /// </summary>
    [Serializable]
    public class Habilidad
    {
        public string id;
        public string nombre;
        public string descripcion;
        public int costoPuntos;
        public int nivelRequerido;
        public string categoria;
        public bool desbloqueada;

        public Habilidad()
        {
            this.id = string.Empty;
            this.nombre = string.Empty;
            this.descripcion = string.Empty;
            this.costoPuntos = 5;
            this.nivelRequerido = 1;
            this.categoria = "General";
            this.desbloqueada = false;
        }

        public Habilidad(string id, string nombre, string descripcion, int costoPuntos, int nivelRequerido, string categoria)
        {
            this.id = id;
            this.nombre = nombre;
            this.descripcion = descripcion;
            this.costoPuntos = costoPuntos;
            this.nivelRequerido = nivelRequerido;
            this.categoria = categoria;
            this.desbloqueada = false;
        }

        public override string ToString()
        {
            string estado = desbloqueada ? "[DESBLOQUEADA]" : "[BLOQUEADA]";
            return $"{estado} {nombre} ({id}) | Costo: {costoPuntos} pts | Nvl req: {nivelRequerido} | Cat: {categoria}";
        }
    }
}
