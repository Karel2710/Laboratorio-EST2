using UnityEngine;
using UnityEngine.SceneManagement;
using GrafosEST2;
using TowerDefenseOnline;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Auto-arranque total: al cargar una escena de juego (la que contiene levelManager o el
    /// contenedor "plots") crea por código todos los sistemas, sin arrastrar nada en el Inspector.
    /// Funciona tanto al dar Play directamente en SampleScene como al llegar desde MainMenu.
    /// </summary>
    public static class ArranqueJuego
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Registrar()
        {
            SceneManager.sceneLoaded -= AlCargarEscena;
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PrimeraEscena()
        {
            Inicializar();
        }

        private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            Inicializar();
        }

        private static void Inicializar()
        {
            bool esEscenaDeJuego = Object.FindFirstObjectByType<levelManager>() != null || GameObject.Find("plots") != null;
            if (!esEscenaDeJuego) return;
            if (GameManager.Instance != null && HUDJuego.Instance != null) return; // ya inicializado

            Time.timeScale = 1f;
            UIFabrica.AsegurarEventSystem();

            if (Object.FindFirstObjectByType<ControladorArbolUI>() == null)
                new GameObject("ControladorArbolUI").AddComponent<ControladorArbolUI>();

            LeaderboardJSON.Asegurar();
            ControladorHabilidadesUI.Asegurar();
            EfectosVisualesManager.Asegurar();
            PathGraph.Asegurar();
            RedTorresMST.Asegurar();
            GameManager.Asegurar();
            AmbientacionMapa2D.Asegurar();
            VisualizadorGrafoRutas.Asegurar();
            GestorRedOnline.Asegurar();
            HUDJuego.Asegurar();
        }
    }
}
