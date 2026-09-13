using UnityEngine;

class nodo
{
    public Any data;
    public nodo left;
    public nodo right;
    public nodo(Any data)
    {
        this.data=data;
        this.left=null;
        this.right=null;
    }
}




class arbol
{
    public nodo root=null;
    public arbol(nodo root)
    {
        this.root=root;
    }
    public void mayor(nodo node)
    {
        if(node!=null)
        {
            Debug.Log(node.data.nombre);
            mayor(node.right);
            mayor(node.left);
        }
    }
    public void insertar(nodo newNode)
    {
        if(root==null)
        {
            root=newNode;
        }
        else
        {
            nodo current=root;
            while(true)
            {
                if(newNode.data.Id<current.data.Id)
                {
                    if(current.left==null)
                    {
                        current.left=newNode;
                        break;
                    }
                    else
                    {
                        current=current.left;
                    }
                }
                else
                {
                    if(current.right==null)
                    {
                        current.right=newNode;
                        break;
                    }
                    else
                    {
                        current=current.right;
                    }
                }
            }
        }
    }
    public void search(Any data)
    {
        nodo p; nodo padre=this.root;None;
        while(p!=null)
        {
            if(data==p.data.Id){
                return p;padre;
            }else if(data<p.data.Id){
                padre=p;
                p=p.left;
            }
            else{
                p=p.right;
            }
        }
        return p;padre;
    }
    public void eliminar(Any data)
    {
        nodo p; nodo padre = this.search(data);
        if (p == null)
        {
            return;
        }
        else if (p.left == null && p.right == null)
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
        else if (p.left != null && p.right == null)
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
        else if (p.left == null && p.right != null)
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
        else
        {
            // El nodo tiene 2 hijos (predecesor)
            nodo pad_pred = p;
            nodo p_pred = p.left;
            while (p_pred.right != null)
            {
                pad_pred = p_pred;
                p_pred = p_pred.right;
            }
            p.data = p_pred.data;
            if (p == pad_pred)
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