using UnityEngine;

public class TowerPlacementManager : MonoBehaviour
{
    private void Start()
    {
        InicializarParcelas();
        AsegurarSpawner();
        AsegurarRaycaster2D();
    }

    /// <summary>
    /// Asegura que la cámara principal tenga un Physics2DRaycaster para que el EventSystem detecte clics 2D.
    /// </summary>
    private void AsegurarRaycaster2D()
    {
        if (Camera.main != null && Camera.main.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
        {
            Camera.main.gameObject.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
            Debug.Log("[TowerPlacementManager] Se añadió Physics2DRaycaster a la cámara principal.");
        }
    }

    /// <summary>
    /// Busca el objeto 'plots' en la escena y adjunta PlotPlacement a cada parcela.
    /// </summary>
    public void InicializarParcelas()
    {
        GameObject contenedorPlots = GameObject.Find("plots");
        if (contenedorPlots != null)
        {
            int contador = 0;
            foreach (Transform hijo in contenedorPlots.transform)
            {
                if (hijo.GetComponent<PlotPlacement>() == null)
                {
                    hijo.gameObject.AddComponent<PlotPlacement>();
                    contador++;
                }
            }
            Debug.Log($"[TowerPlacementManager] Se inicializaron {contador} parcelas para colocar defensas.");
        }
        else
        {
            // Si no se llama 'plots', buscar por tag o componentes de collider
            PlotPlacement[] parcelasExistentes = FindObjectsByType<PlotPlacement>(FindObjectsSortMode.None);
            Debug.Log($"[TowerPlacementManager] Parcelas registradas: {parcelasExistentes.Length}");
        }
    }

    /// <summary>
    /// Asegura que exista un EnemySpawner activo en la escena.
    /// </summary>
    private void AsegurarSpawner()
    {
        if (EnemySpawner.instance == null && FindFirstObjectByType<EnemySpawner>() == null)
        {
            GameObject spawnerObj = new GameObject("EnemySpawner");
            spawnerObj.AddComponent<EnemySpawner>();
            Debug.Log("[TowerPlacementManager] EnemySpawner creado dinámicamente en la escena.");
        }
    }
}
