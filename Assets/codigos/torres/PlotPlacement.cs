using UnityEngine;
using UnityEngine.EventSystems;

public class PlotPlacement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Configuración de Parcela")]
    public Color colorNormal = new Color(0.77f, 0.19f, 0.19f, 1f); // Color rojo por defecto del Square
    public Color colorHover = new Color(0.95f, 0.85f, 0.2f, 1f);   // Amarillo al pasar el cursor
    public Color colorOcupado = new Color(0.2f, 0.8f, 0.3f, 1f);  // Verde al tener torre

    [Header("Torre en esta parcela")]
    public GameObject torreActual;
    public GameObject towerPrefab;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            colorNormal = spriteRenderer.color;
        }

        if (GetComponent<Collider2D>() == null)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (torreActual == null && spriteRenderer != null)
        {
            spriteRenderer.color = colorHover;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (torreActual == null && spriteRenderer != null)
        {
            spriteRenderer.color = colorNormal;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ColocarOQuitarTorre();
    }

    public void ColocarOQuitarTorre()
    {
        var plotTorre = GetComponent<TowerDefenseGameplay.PlotTorre>();
        if (plotTorre != null)
        {
            plotTorre.ConstruirTorre();
            return;
        }

        if (torreActual == null)
        {
            ConstruirTorre();
        }
        else
        {
            Debug.Log("[PlotPlacement] Parcela ocupada. Torre ya instalada.");
        }
    }

    private void ConstruirTorre()
    {
        if (towerPrefab != null)
        {
            torreActual = Instantiate(towerPrefab, transform.position, Quaternion.identity, transform);
        }
        else
        {
            // Construcción dinámica de una torre de defensa si no hay prefab asignado
            torreActual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            torreActual.name = "TorreDefensa";
            torreActual.transform.position = transform.position + new Vector3(0, 0, -0.1f);
            torreActual.transform.localScale = new Vector3(0.5f, 0.2f, 0.5f);

            // Cambiar material/color
            Renderer r = torreActual.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.2f, 0.5f, 0.9f);
            }

            Destroy(torreActual.GetComponent<Collider>()); // Remover collider 3D

            TowerBehavior towerComp = torreActual.AddComponent<TowerBehavior>();
            towerComp.rangoAtaque = 4.5f;
            towerComp.danoPorDisparo = 30f;
            towerComp.disparosPorSegundo = 1.5f;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = colorOcupado;
        }

        Debug.Log($"[PlotPlacement] ¡Torre colocada con éxito en {gameObject.name}!");
    }
}
