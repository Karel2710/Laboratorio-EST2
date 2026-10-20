using System.Collections.Generic;
using UnityEngine;

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

    // ─── Propiedades de apoyo para Dijkstra (no se serializan) ───────────────
    [System.NonSerialized] public float distancia = float.MaxValue;
    [System.NonSerialized] public PathNode previo = null;
    [System.NonSerialized] public bool visitado = false;

    /// <summary>
    /// Peso de la arista entre este nodo y el vecino indicado.
    /// Se calcula como la distancia euclidiana entre ambas posiciones en el mundo.
    /// </summary>
    public float PesoHacia(PathNode vecino)
    {
        return Vector3.Distance(transform.position, vecino.transform.position);
    }

    /// <summary>
    /// Agrega un vecino (arista) de forma bidireccional si aún no existe.
    /// </summary>
    public void ConectarCon(PathNode otro)
    {
        if (otro == null || otro == this) return;
        if (!vecinos.Contains(otro))
        {
            vecinos.Add(otro);
            Debug.Log($"[PathNode] Nodo {nodeId} conectado con nodo {otro.nodeId}");
        }
    }

    /// <summary>
    /// Elimina la conexión con un vecino (por ejemplo, si una torre bloquea el paso).
    /// </summary>
    public void DesconectarDe(PathNode otro)
    {
        if (vecinos.Contains(otro))
        {
            vecinos.Remove(otro);
            Debug.Log($"[PathNode] Arista entre nodo {nodeId} y nodo {otro.nodeId} eliminada.");
        }
    }

    // ─── Dibujo en el editor ──────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        // Color del nodo
        if (esInicio)
            Gizmos.color = new Color(0.2f, 1f, 0.2f);       // verde = spawn
        else if (esFinal)
            Gizmos.color = new Color(1f, 0.2f, 0.2f);        // rojo  = base
        else
            Gizmos.color = new Color(0.2f, 0.6f, 1f);        // azul  = nodo intermedio

        Gizmos.DrawSphere(transform.position, 0.25f);

        // Etiqueta del ID
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, $"N{nodeId}");
#endif

        // Aristas hacia vecinos
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);    // amarillo = arista
        foreach (PathNode vecino in vecinos)
        {
            if (vecino == null) continue;
            Vector3 dir = vecino.transform.position - transform.position;
            Vector3 mid = transform.position + dir * 0.5f;

            Gizmos.DrawLine(transform.position, vecino.transform.position);

            // Peso de la arista en el punto medio
#if UNITY_EDITOR
            float peso = PesoHacia(vecino);
            UnityEditor.Handles.color = new Color(1f, 0.7f, 0.1f);
            UnityEditor.Handles.Label(mid + Vector3.up * 0.2f, $"{peso:F1}");
#endif
        }
    }
}
