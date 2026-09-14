using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class EntradaLeaderboard
{
    public string nombre;
    public int puntaje;

    public EntradaLeaderboard(string nombre, int puntaje)
    {
        this.nombre = nombre;
        this.puntaje = puntaje;
    }
}

[System.Serializable]
public class DatosLeaderboard
{
    public List<EntradaLeaderboard> listaPuntajes = new List<EntradaLeaderboard>();
}

public class LeaderboardJSON : MonoBehaviour
{
    public List<EntradaLeaderboard> tablaPuntajes = new List<EntradaLeaderboard>();
    private string rutaArchivo;

    void Start()
    {
        rutaArchivo = Path.Combine(Application.persistentDataPath, "leaderboard.json");
        CargarLeaderboard();
    }

    public void AgregarPuntaje(string nombre, int puntaje)
    {
        tablaPuntajes.Add(new EntradaLeaderboard(nombre, puntaje));
        
        tablaPuntajes.Sort((x, y) => y.puntaje.CompareTo(x.puntaje));
        if (tablaPuntajes.Count > 10)
        {
            tablaPuntajes.RemoveAt(tablaPuntajes.Count - 1);
        }

        GuardarLeaderboard();
    }

    public void GuardarLeaderboard()
    {
        DatosLeaderboard datos = new DatosLeaderboard();
        datos.listaPuntajes = tablaPuntajes;

        string json = JsonUtility.ToJson(datos, true);
        
        File.WriteAllText(rutaArchivo, json);
        Debug.Log("Leaderboard guardado en: " + rutaArchivo);
    }

    public void CargarLeaderboard()
    {
        if (File.Exists(rutaArchivo))
        {
            string json = File.ReadAllText(rutaArchivo);
            
            DatosLeaderboard datos = JsonUtility.FromJson<DatosLeaderboard>(json);
            
            if (datos != null && datos.listaPuntajes != null)
            {
                tablaPuntajes = datos.listaPuntajes;
                Debug.Log("Leaderboard cargado correctamente.");
            }
        }
        else
        {
            Debug.Log("No se encontró un archivo de leaderboard previo. Se iniciará vacío.");
        }
    }
}