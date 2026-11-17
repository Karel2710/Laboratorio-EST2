using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Acceso al árbol BST de comandantes guardado en disco (jugadores_arbol.json), el mismo archivo
    /// que usa ControladorArbolUI. Permite elegir comandante en el menú LAN (sin la escena de juego)
    /// y guardar las victorias/derrotas al terminar una partida.
    /// </summary>
    public static class RepositorioComandantes
    {
        [System.Serializable]
        private class DatosArbolJugadores
        {
            public List<Jugador> listaJugadores = new List<Jugador>();
        }

        private static string Ruta => Path.Combine(Application.persistentDataPath, "jugadores_arbol.json");

        /// <summary>Carga el archivo y reconstruye el BST insertando cada comandante por su ID.</summary>
        public static Arbol Cargar()
        {
            Arbol arbol = new Arbol();
            try
            {
                if (File.Exists(Ruta))
                {
                    string json = File.ReadAllText(Ruta);
                    DatosArbolJugadores datos = JsonUtility.FromJson<DatosArbolJugadores>(json);
                    if (datos != null && datos.listaJugadores != null)
                    {
                        foreach (Jugador j in datos.listaJugadores)
                        {
                            if (j != null) arbol.Insertar(j);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[RepositorioComandantes] No se pudo leer el BST: " + ex.Message);
            }
            return arbol;
        }

        public static void Guardar(Arbol arbol)
        {
            try
            {
                DatosArbolJugadores datos = new DatosArbolJugadores { listaJugadores = arbol.InOrden() };
                File.WriteAllText(Ruta, JsonUtility.ToJson(datos, true));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[RepositorioComandantes] No se pudo guardar el BST: " + ex.Message);
            }
        }

        /// <summary>Comandantes ordenados por ID (recorrido in-orden del BST).</summary>
        public static List<Jugador> Listar()
        {
            return Cargar().InOrden();
        }

        /// <summary>Crea un comandante con el siguiente ID libre y lo inserta en el BST.</summary>
        public static Jugador Registrar(string nombre)
        {
            Arbol arbol = Cargar();
            int id = 1;
            foreach (Jugador j in arbol.InOrden())
            {
                if (j.Id >= id) id = j.Id + 1;
            }

            Jugador nuevo = new Jugador(100, id, nombre, 1, 0);
            arbol.Insertar(nuevo);
            Guardar(arbol);
            return nuevo;
        }

        /// <summary>Suma una victoria o derrota al comandante (lo crea si no estaba en este PC).</summary>
        public static Jugador RegistrarResultado(int id, string nombre, bool gano)
        {
            Arbol arbol = Cargar();
            Jugador j = id > 0 ? arbol.Buscar(id) : null;
            if (j == null)
            {
                int nuevoId = id > 0 ? id : 1;
                foreach (Jugador x in arbol.InOrden())
                {
                    if (id <= 0 && x.Id >= nuevoId) nuevoId = x.Id + 1;
                }
                j = new Jugador(100, nuevoId, nombre, 1, 0);
                arbol.Insertar(j);
            }

            if (gano) j.victoriasMultijugador++;
            else j.derrotasMultijugador++;

            Guardar(arbol);
            return j;
        }
    }
}
