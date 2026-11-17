using UnityEngine;
using UnityEngine.SceneManagement;
using GrafosEST2;
using TowerDefenseOnline;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Auto-arranque total: cada vez que se carga una escena crea por código lo que necesita,
    /// sin arrastrar nada en el Inspector.
    ///  - Escena de juego (contiene levelManager o el contenedor "plots"): todos los sistemas del juego
    ///    y, en multijugador LAN, la capa de red (servidor o cliente, atacante, rutas y DAG de ataque).
    ///  - Menú principal (contiene MainMenu): selector Modo Historia / Multijugador LAN.
    /// Si se da Play directo en SampleScene sin pasar por el menú, se carga el menú principal.
    /// </summary>
    public static class ArranqueJuego
    {
        private static int ultimaEscenaPreparada = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Registrar()
        {
            ultimaEscenaPreparada = int.MinValue;
            SceneManager.sceneLoaded -= AlCargarEscena;
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        /// <summary>Respaldo para la primera escena; si sceneLoaded ya la preparó no se repite.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PrimeraEscena()
        {
            AlCargarEscena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            if (modo == LoadSceneMode.Additive) return;
            if (escena.handle == ultimaEscenaPreparada) return;
            ultimaEscenaPreparada = escena.handle;

            bool esEscenaDeJuego = Object.FindFirstObjectByType<levelManager>() != null || GameObject.Find("plots") != null;
            if (esEscenaDeJuego)
            {
                // Play directo en SampleScene desde el editor: ir al menú para elegir el modo
                if (!ModoJuego.IniciadoDesdeMenu && !ModoJuego.EsMultijugador && Application.CanStreamedLevelBeLoaded("MainMenu"))
                {
                    SceneManager.LoadScene("MainMenu");
                    return;
                }
                PrepararEscenaJuego();
            }
            else if (Object.FindFirstObjectByType<MainMenu>() != null)
            {
                PrepararMenuPrincipal();
            }
        }

        private static void PrepararEscenaJuego()
        {
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
            Vista25D.Asegurar();
            Escenario25D.Asegurar();
            AmbientacionMapa2D.Asegurar();
            VisualizadorGrafoRutas.Asegurar();
            if (!ModoJuego.EsMultijugador) GestorRedOnline.Asegurar(); // sala online de main (solo un jugador)

            if (ModoJuego.EsMultijugador) PrepararCapaLAN();

            HUDJuego.Asegurar();
            if (ModoJuego.EsAtacanteLocal) new GameObject("HUDAtacante").AddComponent<HUDAtacante>();
        }

        /// <summary>Capa multijugador LAN (atacante vs defensor).</summary>
        private static void PrepararCapaLAN()
        {
            new GameObject("MapaRutasAtaque").AddComponent<MapaRutas>();

            // DAG de ataque: existe en ambos PCs (el host lo usa para validar, el atacante para verlo)
            ControladorHabilidadesUI.CrearArbolAtaque();
            new GameObject("ControladorAtacante").AddComponent<ControladorAtacante>();

            if (ModoJuego.EsHost) new GameObject("ServidorPartida").AddComponent<ServidorPartida>();
            else new GameObject("ClientePartida").AddComponent<ClientePartida>();
        }

        private static void PrepararMenuPrincipal()
        {
            // Al volver al menú siempre se parte del modo un jugador; el panel LAN cambia el modo si se usa.
            Time.timeScale = 1f;
            if (RedLAN.Instancia != null && RedLAN.Instancia.Activa) RedLAN.Instancia.Detener();
            ModoJuego.ConfigurarUnJugador();

            if (Object.FindFirstObjectByType<MenuMultijugadorUI>() == null)
                new GameObject("MenuMultijugadorLAN").AddComponent<MenuMultijugadorUI>();
        }
    }
}
