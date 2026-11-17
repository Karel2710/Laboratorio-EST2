using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Nodo
{
    public Jugador data;
    public Nodo left;
    public Nodo right;

    public Nodo(Jugador data)
    {
        this.data = data;
        this.left = null;
        this.right = null;
    }
}

// Alias para compatibilidad con código existente
public class nodo : Nodo
{
    public nodo(Jugador data) : base(data) { }
}

public class Arbol
{
    public Nodo root = null;

    public Arbol()
    {
        this.root = null;
    }

    public Arbol(Nodo root)
    {
        this.root = root;
    }

    /// <summary>
    /// Inserta un nuevo jugador en el árbol binario según su ID.
    /// Retorna false si el ID ya existe para evitar claves duplicadas.
    /// </summary>
    public bool Insertar(Jugador nuevoJugador)
    {
        if (nuevoJugador == null) return false;

        // Modo AVL: inserción recursiva con rebalanceo por rotaciones
        if (autoBalanceoAVL)
        {
            bool insertado = false;
            root = InsertarAVLRec(root, nuevoJugador, ref insertado);
            return insertado;
        }

        Nodo nuevoNodo = new Nodo(nuevoJugador);

        if (root == null)
        {
            root = nuevoNodo;
            return true;
        }

        Nodo actual = root;
        while (true)
        {
            if (nuevoJugador.Id == actual.data.Id)
            {
                // ID duplicado
                return false;
            }
            else if (nuevoJugador.Id < actual.data.Id)
            {
                if (actual.left == null)
                {
                    actual.left = nuevoNodo;
                    return true;
                }
                actual = actual.left;
            }
            else
            {
                if (actual.right == null)
                {
                    actual.right = nuevoNodo;
                    return true;
                }
                actual = actual.right;
            }
        }
    }

    /// <summary>
    /// Compatibilidad con método original
    /// </summary>
    public void insertar(Nodo newNode)
    {
        if (newNode != null && newNode.data != null)
        {
            Insertar(newNode.data);
        }
    }

    /// <summary>
    /// Busca un nodo en el árbol por el ID del jugador.
    /// </summary>
    public Nodo search(int data)
    {
        Nodo p = root;

        while (p != null)
        {
            if (data == p.data.Id)
            {
                return p;
            }
            else if (data < p.data.Id)
            {
                p = p.left;
            }
            else
            {
                p = p.right;
            }
        }

        return null;
    }

    /// <summary>
    /// Busca y retorna directamente el Jugador por su ID.
    /// </summary>
    public Jugador Buscar(int id)
    {
        Nodo resultado = search(id);
        return resultado != null ? resultado.data : null;
    }

    /// <summary>
    /// Elimina un nodo por su ID manteniendo las propiedades del BST.
    /// </summary>
    public bool Eliminar(int data)
    {
        // Modo AVL: eliminación recursiva con rebalanceo en el camino de regreso
        if (autoBalanceoAVL)
        {
            bool eliminado = false;
            root = EliminarAVLRec(root, data, ref eliminado);
            return eliminado;
        }

        Nodo p = root;
        Nodo padre = null;

        while (p != null && p.data.Id != data)
        {
            padre = p;

            if (data < p.data.Id)
            {
                p = p.left;
            }
            else
            {
                p = p.right;
            }
        }

        if (p == null)
        {
            return false;
        }

        // Caso 1: Nodo hoja (sin hijos)
        if (p.left == null && p.right == null)
        {
            if (p == root)
            {
                root = null;
            }
            else if (p == padre.left)
            {
                padre.left = null;
            }
            else
            {
                padre.right = null;
            }
        }
        // Caso 2: Solo tiene hijo izquierdo
        else if (p.left != null && p.right == null)
        {
            if (p == root)
            {
                root = p.left;
            }
            else if (p == padre.left)
            {
                padre.left = p.left;
            }
            else
            {
                padre.right = p.left;
            }
        }
        // Caso 3: Solo tiene hijo derecho
        else if (p.left == null && p.right != null)
        {
            if (p == root)
            {
                root = p.right;
            }
            else if (p == padre.left)
            {
                padre.left = p.right;
            }
            else
            {
                padre.right = p.right;
            }
        }
        // Caso 4: Tiene 2 hijos (reemplazo por predecesor)
        else
        {
            Nodo pad_pred = p;
            Nodo p_pred = p.left;

            while (p_pred.right != null)
            {
                pad_pred = p_pred;
                p_pred = p_pred.right;
            }

            p.data = p_pred.data;

            if (pad_pred == p)
            {
                p.left = p_pred.left;
            }
            else
            {
                pad_pred.right = p_pred.left;
            }
        }

        return true;
    }

    /// <summary>
    /// Compatibilidad con método original
    /// </summary>
    public void eliminar(int data)
    {
        Eliminar(data);
    }

    /// <summary>
    /// Retorna una lista con todos los jugadores ordenados ascendentemente por ID (Recorrido In-Orden).
    /// </summary>
    public List<Jugador> InOrden()
    {
        List<Jugador> lista = new List<Jugador>();
        InOrdenRecursivo(root, lista);
        return lista;
    }

    private void InOrdenRecursivo(Nodo actual, List<Jugador> lista)
    {
        if (actual != null)
        {
            InOrdenRecursivo(actual.left, lista);
            lista.Add(actual.data);
            InOrdenRecursivo(actual.right, lista);
        }
    }

    /// <summary>
    /// Recorrido Pre-Orden (Raíz, Izquierda, Derecha).
    /// </summary>
    public List<Jugador> PreOrden()
    {
        List<Jugador> lista = new List<Jugador>();
        PreOrdenRecursivo(root, lista);
        return lista;
    }

    private void PreOrdenRecursivo(Nodo actual, List<Jugador> lista)
    {
        if (actual != null)
        {
            lista.Add(actual.data);
            PreOrdenRecursivo(actual.left, lista);
            PreOrdenRecursivo(actual.right, lista);
        }
    }

    /// <summary>
    /// Recorrido Post-Orden (Izquierda, Derecha, Raíz).
    /// </summary>
    public List<Jugador> PostOrden()
    {
        List<Jugador> lista = new List<Jugador>();
        PostOrdenRecursivo(root, lista);
        return lista;
    }

    private void PostOrdenRecursivo(Nodo actual, List<Jugador> lista)
    {
        if (actual != null)
        {
            PostOrdenRecursivo(actual.left, lista);
            PostOrdenRecursivo(actual.right, lista);
            lista.Add(actual.data);
        }
    }

    /// <summary>
    /// Muestra en consola los nombres (compatibilidad con método original)
    /// </summary>
    public void mayor(Nodo node)
    {
        if (node != null)
        {
            Debug.Log(node.data.nombre);
            mayor(node.right);
            mayor(node.left);
        }
    }

    public int Contar()
    {
        return ContarRecursivo(root);
    }

    private int ContarRecursivo(Nodo actual)
    {
        if (actual == null) return 0;
        return 1 + ContarRecursivo(actual.left) + ContarRecursivo(actual.right);
    }

    /// <summary>
    /// Calcula la altura máxima del árbol (profundidad en niveles).
    /// </summary>
    public int Altura()
    {
        return AlturaRecursivo(root);
    }

    private int AlturaRecursivo(Nodo actual)
    {
        if (actual == null) return 0;
        return 1 + Mathf.Max(AlturaRecursivo(actual.left), AlturaRecursivo(actual.right));
    }

    #region Balanceo AVL (Factor de Equilibrio y Rotaciones)

    /// <summary>Si es true, Insertar/Eliminar mantienen el árbol balanceado (AVL).</summary>
    public bool autoBalanceoAVL = false;

    /// <summary>Contador de rotaciones aplicadas (para demostrar el costo del balanceo).</summary>
    public int rotacionesRealizadas = 0;

    /// <summary>Bitácora legible de las rotaciones aplicadas (LL, RR, LR, RL).</summary>
    public List<string> bitacoraRotaciones = new List<string>();

    public int AlturaNodo(Nodo n)
    {
        if (n == null) return 0;
        return 1 + Mathf.Max(AlturaNodo(n.left), AlturaNodo(n.right));
    }

    /// <summary>
    /// Factor de Equilibrio: FE = altura(izq) - altura(der).
    /// Un árbol es AVL si para todo nodo |FE| &lt;= 1.
    /// </summary>
    public int FactorEquilibrio(Nodo n)
    {
        if (n == null) return 0;
        return AlturaNodo(n.left) - AlturaNodo(n.right);
    }

    public bool EsAVL()
    {
        return EsAVLRec(root);
    }

    private bool EsAVLRec(Nodo n)
    {
        if (n == null) return true;
        if (Mathf.Abs(FactorEquilibrio(n)) > 1) return false;
        return EsAVLRec(n.left) && EsAVLRec(n.right);
    }

    // Rotación simple a la derecha (caso Izquierda-Izquierda)
    private Nodo RotarDerecha(Nodo y)
    {
        Nodo x = y.left;
        y.left = x.right;
        x.right = y;
        rotacionesRealizadas++;
        return x;
    }

    // Rotación simple a la izquierda (caso Derecha-Derecha)
    private Nodo RotarIzquierda(Nodo x)
    {
        Nodo y = x.right;
        x.right = y.left;
        y.left = x;
        rotacionesRealizadas++;
        return y;
    }

    private void Anotar(string texto)
    {
        bitacoraRotaciones.Add(texto);
        if (bitacoraRotaciones.Count > 40) bitacoraRotaciones.RemoveAt(0);
    }

    /// <summary>Restaura la propiedad AVL en el nodo n aplicando rotación simple o doble.</summary>
    private Nodo Rebalancear(Nodo n)
    {
        if (n == null) return null;
        int fe = FactorEquilibrio(n);

        if (fe > 1)
        {
            if (FactorEquilibrio(n.left) < 0)
            {
                // Caso Izquierda-Derecha: rotación doble
                Anotar($"Rotación doble LR en ID {n.data.Id} (FE=+{fe})");
                n.left = RotarIzquierda(n.left);
            }
            else
            {
                Anotar($"Rotación simple derecha (LL) en ID {n.data.Id} (FE=+{fe})");
            }
            return RotarDerecha(n);
        }

        if (fe < -1)
        {
            if (FactorEquilibrio(n.right) > 0)
            {
                // Caso Derecha-Izquierda: rotación doble
                Anotar($"Rotación doble RL en ID {n.data.Id} (FE={fe})");
                n.right = RotarDerecha(n.right);
            }
            else
            {
                Anotar($"Rotación simple izquierda (RR) en ID {n.data.Id} (FE={fe})");
            }
            return RotarIzquierda(n);
        }

        return n;
    }

    private Nodo InsertarAVLRec(Nodo n, Jugador j, ref bool insertado)
    {
        if (n == null)
        {
            insertado = true;
            return new Nodo(j);
        }

        if (j.Id == n.data.Id) return n; // clave duplicada
        if (j.Id < n.data.Id) n.left = InsertarAVLRec(n.left, j, ref insertado);
        else n.right = InsertarAVLRec(n.right, j, ref insertado);

        return insertado ? Rebalancear(n) : n;
    }

    private Nodo EliminarAVLRec(Nodo n, int id, ref bool eliminado)
    {
        if (n == null) return null;

        if (id < n.data.Id) n.left = EliminarAVLRec(n.left, id, ref eliminado);
        else if (id > n.data.Id) n.right = EliminarAVLRec(n.right, id, ref eliminado);
        else
        {
            eliminado = true;
            if (n.left == null) return n.right;
            if (n.right == null) return n.left;

            // Dos hijos: reemplazo por predecesor in-order (igual que el BST clásico)
            Nodo pred = n.left;
            while (pred.right != null) pred = pred.right;
            n.data = pred.data;
            bool aux = false;
            n.left = EliminarAVLRec(n.left, pred.data.Id, ref aux);
        }

        return Rebalancear(n);
    }

    /// <summary>
    /// Convierte el BST actual (posiblemente degenerado) en un AVL balanceado:
    /// reinserta todos los nodos en pre-orden aplicando rotaciones.
    /// Retorna la cantidad de rotaciones que fueron necesarias.
    /// </summary>
    public int BalancearComoAVL()
    {
        List<Jugador> datos = PreOrden();
        int antes = rotacionesRealizadas;
        bool previo = autoBalanceoAVL;
        root = null;
        autoBalanceoAVL = true;
        for (int i = 0; i < datos.Count; i++) Insertar(datos[i]);
        autoBalanceoAVL = previo;
        return rotacionesRealizadas - antes;
    }

    /// <summary>Profundidad (nivel) de un ID dentro del árbol; -1 si no existe. Mide el costo de búsqueda.</summary>
    public int ProfundidadDe(int id)
    {
        int nivel = 1;
        Nodo p = root;
        while (p != null)
        {
            if (id == p.data.Id) return nivel;
            p = id < p.data.Id ? p.left : p.right;
            nivel++;
        }
        return -1;
    }

    #endregion

    public void Limpiar()
    {
        root = null;
    }
}

// Alias para compatibilidad con código existente
public class arbol : Arbol
{
    public arbol() : base() { }
    public arbol(Nodo root) : base(root) { }
}
