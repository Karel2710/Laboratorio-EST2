using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    /// <summary>Botón original del menú: partida de un jugador (sin cambios en las reglas).</summary>
    public void EmpezarJuego()
    {
        TowerDefenseGameplay.ModoJuego.IniciadoDesdeMenu = true;
        TowerDefenseGameplay.ModoJuego.ConfigurarUnJugador();
        SceneManager.LoadScene("SampleScene");
    }
}
