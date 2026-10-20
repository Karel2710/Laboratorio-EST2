using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConfiguracionNivel
{
    public string nombreNivel = "Nivel 1";
    public int totalOleadas = 3;
    public int enemigosPorOleada = 5;
    public float tiempoEntreSpawns = 1.2f;
    public float tiempoEntreOleadas = 4.0f;
    public float vidaEnemigoBase = 50f;
    public float velocidadEnemigoBase = 2.5f;
}

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner instance;

    [Header("Prefab del Enemigo")]
    public GameObject enemyPrefab;

    [Header("Configuración del Nivel Actual")]
    public ConfiguracionNivel configActual = new ConfiguracionNivel();

    [Header("Niveles Preconfigurados (Fácil de modificar)")]
    public List<ConfiguracionNivel> listaNiveles = new List<ConfiguracionNivel>()
    {
        new ConfiguracionNivel { nombreNivel = "Nivel 1 (Fácil)", totalOleadas = 3, enemigosPorOleada = 5, tiempoEntreSpawns = 1.5f, tiempoEntreOleadas = 5f, vidaEnemigoBase = 40f, velocidadEnemigoBase = 2f },
        new ConfiguracionNivel { nombreNivel = "Nivel 2 (Medio)", totalOleadas = 5, enemigosPorOleada = 8, tiempoEntreSpawns = 1.0f, tiempoEntreOleadas = 4f, vidaEnemigoBase = 75f, velocidadEnemigoBase = 2.8f },
        new ConfiguracionNivel { nombreNivel = "Nivel 3 (Difícil)", totalOleadas = 8, enemigosPorOleada = 12, tiempoEntreSpawns = 0.7f, tiempoEntreOleadas = 3f, vidaEnemigoBase = 120f, velocidadEnemigoBase = 3.5f }
    };

    [Header("Estado de Oleadas")]
    public int oleadaActual = 0;
    public bool generandoOleada = false;
    public int enemigosVivos = 0;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        // Cargar primer nivel por defecto si existe en la lista
        if (listaNiveles.Count > 0)
        {
            CargarConfiguracionNivel(0);
        }

        // Iniciar spawning automático si no hay otro controlador
        StartCoroutine(RutinaBucleOleadas());
    }

    /// <summary>
    /// Cambia la configuración del spawner según el índice del nivel seleccionado.
    /// </summary>
    public void CargarConfiguracionNivel(int indiceNivel)
    {
        if (indiceNivel >= 0 && indiceNivel < listaNiveles.Count)
        {
            configActual = listaNiveles[indiceNivel];
            Debug.Log($"[EnemySpawner] Configuración cargada: {configActual.nombreNivel}");
        }
    }

    public void IniciarProximaOleada()
    {
        if (!generandoOleada && oleadaActual < configActual.totalOleadas)
        {
            StartCoroutine(GenerarOleadaCoroutine());
        }
    }

    private IEnumerator RutinaBucleOleadas()
    {
        yield return new WaitForSeconds(1.5f);

        while (oleadaActual < configActual.totalOleadas)
        {
            if (!generandoOleada && EnemyHealth.ActiveEnemies.Count == 0)
            {
                oleadaActual++;
                Debug.Log($"[EnemySpawner] Iniciando Oleada {oleadaActual}/{configActual.totalOleadas} de {configActual.nombreNivel}");
                yield return StartCoroutine(GenerarOleadaCoroutine());
                yield return new WaitForSeconds(configActual.tiempoEntreOleadas);
            }
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[EnemySpawner] ¡Todas las oleadas del nivel han sido completadas!");
    }

    private IEnumerator GenerarOleadaCoroutine()
    {
        generandoOleada = true;
        int enemigosAGenerar = configActual.enemigosPorOleada + ((oleadaActual - 1) * 2);

        for (int i = 0; i < enemigosAGenerar; i++)
        {
            SpawnEnemigo();
            yield return new WaitForSeconds(configActual.tiempoEntreSpawns);
        }

        generandoOleada = false;
    }

    private void SpawnEnemigo()
    {
        Vector3 spawnPosition = Vector3.zero;

        // 1ª prioridad: nodo de inicio del grafo (PathNode marcado como esInicio)
        if (PathGraph.main != null)
        {
            PathNode nodoInicio = PathGraph.main.ObtenerNodoInicio();
            if (nodoInicio != null)
            {
                spawnPosition = nodoInicio.transform.position;
            }
        }
        // 2ª prioridad: startPoint del levelManager
        else if (levelManager.main != null && levelManager.main.Start != null)
        {
            spawnPosition = levelManager.main.Start.position;
        }
        // 3ª prioridad: primer punto del path legacy
        else if (levelManager.main != null && levelManager.main.Path != null && levelManager.main.Path.Length > 0 && levelManager.main.Path[0] != null)
        {
            spawnPosition = levelManager.main.Path[0].position;
        }
        // Si nada existe, spawnea en (0,0,0) — señal de que falta configuración

        GameObject nuevoEnemigo = null;

        if (enemyPrefab != null)
        {
            nuevoEnemigo = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // Creación dinámica de un enemigo si no se asignó un prefab en el Inspector
            nuevoEnemigo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            nuevoEnemigo.name = "Enemy_Dynamic";
            nuevoEnemigo.transform.position = spawnPosition;
            nuevoEnemigo.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

            Renderer r = nuevoEnemigo.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.9f, 0.2f, 0.2f); // Rojo destacado
            }
            
            // Remover el MeshCollider 3D que viene por defecto de forma inmediata
            // para evitar conflictos al agregar componentes de físicas 2D (Rigidbody2D/Collider2D)
            DestroyImmediate(nuevoEnemigo.GetComponent<Collider>());

            Rigidbody2D rb = nuevoEnemigo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            CircleCollider2D col = nuevoEnemigo.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            nuevoEnemigo.AddComponent<enemyMovement>();
        }

        // Configurar atributos de vida y velocidad del enemigo instanciado
        EnemyHealth health = nuevoEnemigo.GetComponent<EnemyHealth>();
        if (health == null) health = nuevoEnemigo.AddComponent<EnemyHealth>();
        health.ConfigurarSalud(configActual.vidaEnemigoBase + (oleadaActual * 10f));

        // Ajustar velocidad si tiene enemyMovement
        enemyMovement movement = nuevoEnemigo.GetComponent<enemyMovement>();
        if (movement != null)
        {
            movement.MoveSpeed = configActual.velocidadEnemigoBase;
        }
    }
}
