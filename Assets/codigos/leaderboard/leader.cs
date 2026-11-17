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

    // ── Acceso estático (lo usa la pantalla de fin de partida LAN sin necesitar el componente en escena) ──

    private static string RutaGlobal => Path.Combine(Application.persistentDataPath, "leaderboard.json");

    /// <summary>Lee el top 10 guardado en disco (lista vacía si no existe).</summary>
    public static List<EntradaLeaderboard> CargarTop()
    {
        try
        {
            if (File.Exists(RutaGlobal))
            {
                string json = File.ReadAllText(RutaGlobal);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    DatosLeaderboard datos = JsonUtility.FromJson<DatosLeaderboard>(json);
                    if (datos != null && datos.listaPuntajes != null) return datos.listaPuntajes;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Leaderboard] No se pudo leer el archivo: " + ex.Message);
        }
        return new List<EntradaLeaderboard>();
    }

    /// <summary>Agrega un puntaje, ordena de mayor a menor, conserva el top 10 y guarda.</summary>
    public static List<EntradaLeaderboard> AgregarPuntajeGlobal(string nombre, int puntaje)
    {
        List<EntradaLeaderboard> tabla = CargarTop();
        tabla.Add(new EntradaLeaderboard(nombre, puntaje));
        tabla.Sort((x, y) => y.puntaje.CompareTo(x.puntaje));
        while (tabla.Count > 10) tabla.RemoveAt(tabla.Count - 1);

        try
        {
            DatosLeaderboard datos = new DatosLeaderboard { listaPuntajes = tabla };
            File.WriteAllText(RutaGlobal, JsonUtility.ToJson(datos, true));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Leaderboard] No se pudo guardar el archivo: " + ex.Message);
        }
        return tabla;
    }

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
            
            if (!string.IsNullOrWhiteSpace(json))
            {
                DatosLeaderboard datos = JsonUtility.FromJson<DatosLeaderboard>(json);
                
                if (datos != null && datos.listaPuntajes != null)
                {
                    tablaPuntajes = datos.listaPuntajes;
                    Debug.Log("Leaderboard cargado correctamente.");
                }
            }
        }
        else
        {
            Debug.Log("No se encontró un archivo de leaderboard previo. Se iniciará vacío.");
        }
    }
}