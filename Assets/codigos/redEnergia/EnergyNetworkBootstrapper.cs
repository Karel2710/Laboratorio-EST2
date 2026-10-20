using UnityEngine;

namespace RedEnergiaMST
{
    /// <summary>
    /// Inicializa automáticamente el <see cref="EnergyNetworkManager"/> al cargar cualquier escena,
    /// garantizando que el sistema de Red de Energía MST esté siempre disponible sin necesidad
    /// de colocar manualmente el prefab/objeto en la jerarquía del editor.
    ///
    /// Si el desarrollador prefiere configurar el Manager visualmente (para ajustar colores, materiales,
    /// etc.), basta con añadir un GameObject con <see cref="EnergyNetworkManager"/> a la escena en el
    /// Editor. Este inicializador detectará esa instancia existente y no creará un duplicado.
    /// </summary>
    public static class EnergyNetworkBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInicializar()
        {
            // Si ya existe una instancia (colocada en la escena manualmente), no crear otra
            if (EnergyNetworkManager.Instance != null) return;

            if (Object.FindFirstObjectByType<EnergyNetworkManager>() != null) return;

            GameObject go = new GameObject("[EnergyNetworkManager]");
            go.AddComponent<EnergyNetworkManager>();
            Object.DontDestroyOnLoad(go);

            Debug.Log("[EnergyNetworkBootstrapper] EnergyNetworkManager creado automáticamente.");
        }
    }
}
