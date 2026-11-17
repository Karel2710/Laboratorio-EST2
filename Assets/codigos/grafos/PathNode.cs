using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tipo de terreno de una arista del grafo de caminos. Define el multiplicador de costo:
/// el peso de la arista es distancia euclidiana × multiplicador, y los enemigos
/// avanzan más lento en terreno costoso (el costo representa tiempo de viaje).
/// </summary>
public enum TipoTerreno
{
    Camino,   // ×1.0  Ruta principal
    Sendero,  // ×1.8  Ruta alternativa
    Pantano   // ×3.0  Zona pantanosa (atajo muy lento)
}

/// <summary>
/// Representa un nodo (vértice) en el grafo de caminos del mapa.
/// Cada nodo tiene una posición en el mundo y una lista de aristas hacia nodos vecinos.
/// </summary>
public class PathNode : MonoBehaviour
{
    [Header("Identificación del Nodo")]
    [Tooltip("ID único de este nodo dentro del grafo.")]
    public int nodeId = -1;

    [Tooltip("¿Es este nodo el punto de inicio (spawn) de los enemigos?")]
    public bool esInicio = false;

    [Tooltip("¿Es este nodo la base (objetivo final) de los enemigos?")]
    public bool esFinal = false;

    [Header("Conexiones (Lista de Adyacencia)")]
    [Tooltip("Nodos vecinos a los que este nodo está conectado.")]
    public List<PathNode> vecinos = new List<PathNode>();

    [Tooltip("Multiplicador de terreno de cada arista (paralelo a 'vecinos'). Vacío = 1.0")]
    public List<float> multiplicadores = new List<float>();

    // ─── Propiedades de apoyo para Dijkstra (no se serializan) ───────────────
    [System.NonSerialized] public float distancia = float.MaxValue;
    [System.NonSerialized] public PathNode previo = null;
    [System.NonSerialized] public bool visitado = false;

    // ─── Árbol de caminos mínimos hacia la base (se recalcula al cambiar el grafo) ──
    [System.NonSerialized] public float costoAFin = float.MaxValue;
    [System.NonSerialized] public PathNode siguienteHaciaFin = null;

    public static float MultiplicadorDeTerreno(TipoTerreno t)
    {
        switch (t)
        {
            case TipoTerreno.Sendero: return 1.8f;
            case TipoTerreno.Pantano: return 3.0f;
            default: return 1.0f;
        }
    }

    public static TipoTerreno TerrenoDeMultiplicador(float m)
    {
        if (m >= 2.6f) return TipoTerreno.Pantano;
        if (m > 1.05f) return TipoTerreno.Sendero;
        return TipoTerreno.Camino;
    }

    /// <summary>Multiplicador de terreno de la arista hacia el vecino (1.0 si no se definió).</summary>
    public float MultiplicadorHacia(PathNode vecino)
    {
        int i = vecinos.IndexOf(vecino);
        if (i >= 0 && i < multiplicadores.Count && multiplicadores[i] > 0f) return multiplicadores[i];
        return 1f;
    }

    /// <summary>
    /// Peso de la arista entre este nodo y el vecino indicado:
    /// distancia euclidiana × multiplicador de terreno.
    /// </summary>
    public float PesoHacia(PathNode vecino)
    {
        return Vector3.Distance(transform.position, vecino.transform.position) * MultiplicadorHacia(vecino);
    }

    /// <summary>
    /// Agrega un vecino (arista saliente) si aún no existe, con su multiplicador de terreno.
    /// </summary>
    public void ConectarCon(PathNode otro, float multiplicador = 1f)
    {
        if (otro == null || otro == this) return;
        while (multiplicadores.Count < vecinos.Count) multiplicadores.Add(1f);

        int i = vecinos.IndexOf(otro);
        if (i < 0)
        {
            vecinos.Add(otro);
            multiplicadores.Add(multiplicador);
        }
        else
        {
            multiplicadores[i] = multiplicador;
        }
    }

    /// <summary>
    /// Elimina la conexión con un vecino (por ejemplo, si una barricada bloquea el paso).
    /// </summary>
    public void DesconectarDe(PathNode otro)
    {
        int i = vecinos.IndexOf(otro);
        if (i < 0) return;
        vecinos.RemoveAt(i);
        if (i < multiplicadores.Count) multiplicadores.RemoveAt(i);
    }

    // ─── Dibujo en el editor ──────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (esInicio) Gizmos.color = new Color(0.2f, 1f, 0.2f);
        else if (esFinal) Gizmos.color = new Color(1f, 0.2f, 0.2f);
        else Gizmos.color = new Color(0.2f, 0.6f, 1f);

        Gizmos.DrawSphere(transform.position, 0.2f);

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        foreach (PathNode vecino in vecinos)
        {
            if (vecino == null) continue;
            Gizmos.DrawLine(transform.position, vecino.transform.position);
        }
    }
}
