using System.Collections.Generic;
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
