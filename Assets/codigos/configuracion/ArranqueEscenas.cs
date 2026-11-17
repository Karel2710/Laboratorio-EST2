using GrafosEST2;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Crea los sistemas del juego cada vez que se carga una escena.
    ///
    /// Antes, GameManager, HUD y el DAG usaban [RuntimeInitializeOnLoadMethod(AfterSceneLoad)],
    /// que Unity ejecuta UNA sola vez y solo para la primera escena. Como MainMenu es la escena 0
    /// del build, los sistemas se creaban en el menú y se destruían al pasar a SampleScene.
    /// Ahora nos suscribimos a SceneManager.sceneLoaded y montamos lo necesario según la escena:
    ///  - Escena de juego (tiene levelManager): GameManager, HUD, DAG, red MST y, en LAN, la capa de red.
    ///  - Menú principal (tiene MainMenu): botón y panel del multijugador LAN.
    /// </summary>
    public static class ArranqueEscenas
    {
        private static int ultimaEscenaPreparada = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Registrar()
        {
            ultimaEscenaPreparada = int.MinValue;
            SceneManager.sceneLoaded -= AlCargarEscena;
            SceneManager.sceneLoaded += AlCargarEscena;
        }

        /// <summary>
        /// Respaldo para la primera escena (por ejemplo, Play directo en SampleScene desde el editor).
        /// Si sceneLoaded ya la preparó, no se repite gracias a ultimaEscenaPreparada.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RespaldoPrimeraEscena()
        {
            AlCargarEscena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
        {
            if (modo == LoadSceneMode.Additive) return;
            if (escena.handle == ultimaEscenaPreparada) return;
            ultimaEscenaPreparada = escena.handle;

            if (Object.FindFirstObjectByType<levelManager>() != null)
            {
                PrepararEscenaJuego();
            }
            else if (Object.FindFirstObjectByType<MainMenu>() != null)
            {
                PrepararMenuPrincipal();
            }
        }

        private static void PrepararEscenaJuego()
        {
            if (Object.FindFirstObjectByType<RedEnergiaMST.EnergyNetworkManager>() == null)
            {
                new GameObject("[EnergyNetworkManager]").AddComponent<RedEnergiaMST.EnergyNetworkManager>();
            }

            // Orden importante: el DAG primero (crea el jugador activo), luego GameManager (lo vincula) y el HUD.
            if (Object.FindFirstObjectByType<ControladorHabilidadesUI>() == null)
            {
                new GameObject("SistemaHabilidadesDAG").AddComponent<ControladorHabilidadesUI>();
            }

            if (Object.FindFirstObjectByType<GameManager>() == null)
            {
                new GameObject("GameManager_TowerDefense").AddComponent<GameManager>();
            }

            if (Object.FindFirstObjectByType<HUDJuego>() == null)
            {
                new GameObject("HUDJuego_CanvasHost").AddComponent<HUDJuego>();
            }

            if (!ModoJuego.EsMultijugador) return;

            // ── Capa multijugador LAN ──────────────────────────────────────────
            new GameObject("MapaRutasAtaque").AddComponent<MapaRutas>();

            // DAG de ataque: existe en ambos PCs (el host lo usa para validar, el atacante para verlo)
            ControladorHabilidadesUI.CrearArbolAtaque();

            new GameObject("ControladorAtacante").AddComponent<ControladorAtacante>();

            if (ModoJuego.EsHost)
            {
                new GameObject("ServidorPartida").AddComponent<ServidorPartida>();
            }
            else
            {
                new GameObject("ClientePartida").AddComponent<ClientePartida>();
            }

            if (ModoJuego.EsAtacanteLocal)
            {
                new GameObject("HUDAtacante").AddComponent<HUDAtacante>();
            }
        }

        private static void PrepararMenuPrincipal()
        {
            // Al volver al menú siempre se parte del modo un jugador; el panel LAN cambia el modo si se usa.
            if (RedLAN.Instancia != null && RedLAN.Instancia.Activa)
            {
                RedLAN.Instancia.Detener();
            }
            ModoJuego.ConfigurarUnJugador();

            if (Object.FindFirstObjectByType<MenuMultijugadorUI>() == null)
            {
                new GameObject("MenuMultijugadorLAN").AddComponent<MenuMultijugadorUI>();
            }
        }
    }
}
