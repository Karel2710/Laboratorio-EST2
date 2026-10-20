using UnityEngine;

public class TowerPlacementManager : MonoBehaviour
{
    private void Start()
    {
        AsegurarRaycaster2D();
        InicializarParcelas();
        AsegurarSpawner();
    }

    /// <summary>
    /// Evita que Physics2DRaycaster interfiera con la detección de clics en la interfaz y en el mapa.
    /// </summary>
    private void AsegurarRaycaster2D()
    {
        if (Camera.main != null)
        {
            var p2d = Camera.main.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
            if (p2d != null)
            {
                Destroy(p2d);
                Debug.Log("[TowerPlacementManager] Se retiró Physics2DRaycaster para permitir clics directos sobre el terreno.");
            }
        }
    }

    /// <summary>
    /// Busca el objeto 'plots' en la escena y adjunta PlotPlacement a cada parcela de forma complementaria.
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
            Debug.Log($"[TowerPlacementManager] Se verificaron {contador} parcelas para colocación de defensas.");
        }
    }

    /// <summary>
    /// Asegura que exista un EnemySpawner únicamente si GameManager no está activo en la escena.
    /// </summary>
    private void AsegurarSpawner()
    {
        if (TowerDefenseGameplay.GameManager.Instance != null)
        {
            // GameManager ya administra el ciclo de oleadas principal del juego
            return;
        }

        if (EnemySpawner.instance == null && FindFirstObjectByType<EnemySpawner>() == null)
        {
            GameObject spawnerObj = new GameObject("EnemySpawner");
            spawnerObj.AddComponent<EnemySpawner>();
            Debug.Log("[TowerPlacementManager] EnemySpawner creado dinámicamente como respaldo.");
        }
    }
}
