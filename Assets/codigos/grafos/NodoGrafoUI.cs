using UnityEngine;
using UnityEngine.EventSystems;

namespace GrafosEST2
{
    /// <summary>Vértice interactivo del lienzo: clic para seleccionar y arrastre para moverlo.</summary>
    public class NodoGrafoUI : MonoBehaviour, IPointerClickHandler, IDragHandler
    {
        public string id;
        public System.Action<string> alClic;
        public System.Action<string, Vector2> alArrastrar;
        private bool arrastrado = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (arrastrado) { arrastrado = false; return; }
            alClic?.Invoke(id);
        }

        public void OnDrag(PointerEventData e)
        {
            arrastrado = true;
            RectTransform padre = (RectTransform)transform.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(padre, e.position, e.pressEventCamera, out Vector2 local))
                alArrastrar?.Invoke(id, local);
        }
    }
}
