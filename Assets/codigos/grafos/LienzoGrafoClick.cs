using UnityEngine;
using UnityEngine.EventSystems;

namespace GrafosEST2
{
    /// <summary>Recibe clics sobre el lienzo vacío para crear vértices.</summary>
    public class LienzoGrafoClick : MonoBehaviour, IPointerClickHandler
    {
        public System.Action<Vector2> alClic;

        public void OnPointerClick(PointerEventData e)
        {
            RectTransform rt = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
                alClic?.Invoke(local);
        }
    }
}
