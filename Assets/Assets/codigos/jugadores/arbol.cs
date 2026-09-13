using UnityEngine;

class nodo
{
    public Player data;
    public nodo left;
    public nodo right;

    public nodo(Player data)
    {
        this.data = data;
        this.left = null;
        this.right = null;
    }
}

class arbol
{
    public nodo root = null;

    public arbol(nodo root)
    {
        this.root = root;
    }

    public void mayor(nodo node)
    {
        if (node != null)
        {
            Debug.Log(node.data.nombre);
            mayor(node.right);
            mayor(node.left);
        }
    }

    public void insertar(nodo newNode)
    {
        if (root == null)
        {
            root = newNode;
        }
        else
        {
            nodo current = root;

            while (true)
            {
                if (newNode.data.Id < current.data.Id)
                {
                    if (current.left == null)
                    {
                        current.left = newNode;
                        break;
                    }
                    else
                    {
                        current = current.left;
                    }
                }
                else
                {
                    if (current.right == null)
                    {
                        current.right = newNode;
                        break;
                    }
                    else
                    {
                        current = current.right;
                    }
                }
            }
        }
    }

    public nodo search(int data)
    {
        nodo p = root;

        while (p != null)
        {
            if (data == p.data.Id)
            {
                return p;
            }
            else
            {
                if (data < p.data.Id)
                {
                    p = p.left;
                }
                else
                {
                    p = p.right;
                }
            }
        }

        return null;
    }

    public void eliminar(int data)
    {
        nodo p = root;
        nodo padre = null;

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
            return;
        }

        // Nodo hoja
        if (p.left == null && p.right == null)
        {
            if (p == root)
            {
                root = null;
            }
            else
            {
                if (p == padre.left)
                {
                    padre.left = null;
                }
                else
                {
                    padre.right = null;
                }
            }
        }

        // Nodo con hijo izquierdo
        else if (p.left != null && p.right == null)
        {
            if (p == root)
            {
                root = p.left;
            }
            else
            {
                if (p == padre.left)
                {
                    padre.left = p.left;
                }
                else
                {
                    padre.right = p.left;
                }
            }
        }

        // Nodo con hijo derecho
        else if (p.left == null && p.right != null)
        {
            if (p == root)
            {
                root = p.right;
            }
            else
            {
                if (p == padre.left)
                {
                    padre.left = p.right;
                }
                else
                {
                    padre.right = p.right;
                }
            }
        }

        // Nodo con dos hijos
        else
        {
            nodo pad_pred = p;
            nodo p_pred = p.left;

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
    }
}
