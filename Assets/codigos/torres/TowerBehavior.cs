using UnityEngine;

public class TowerBehavior : MonoBehaviour
{
    [Header("Atributos de Defensa")]
    public string nombreTorre = "Torre Básica";
    public float rangoAtaque = 4.0f;
    public float danoPorDisparo = 25f;
    public float disparosPorSegundo = 1.2f;
    public Color colorTorre = new Color(0.2f, 0.6f, 1.0f);

    [Header("Referencia al Proyectil")]
    public GameObject projectilePrefab;

    private float contadorCooldown = 0f;
    private EnemyHealth objetivoActual;

    private void Start()
    {
        // Si tiene SpriteRenderer, aplicar color descriptivo
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = colorTorre;
        }
    }

    private void Update()
    {
        BuscarObjetivo();

        if (objetivoActual != null)
        {
            // Apuntar hacia el objetivo
            Vector3 dir = (objetivoActual.transform.position - transform.position).normalized;
            float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angulo);

            // Control de cadencia de fuego
            contadorCooldown -= Time.deltaTime;
            if (contadorCooldown <= 0f)
            {
                Disparar();
                contadorCooldown = 1.0f / disparosPorSegundo;
            }
        }
    }

    private void BuscarObjetivo()
    {
        // Limpiar objetivo si murió o salió de rango
        if (objetivoActual != null)
        {
            float dist = Vector3.Distance(transform.position, objetivoActual.transform.position);
            if (dist > rangoAtaque || objetivoActual.currentHealth <= 0)
            {
                objetivoActual = null;
            }
        }

        // Buscar nuevo objetivo más cercano
        if (objetivoActual == null)
        {
            float menorDistancia = float.MaxValue;
            EnemyHealth masCercano = null;

            foreach (EnemyHealth enemigo in EnemyHealth.ActiveEnemies)
            {
                if (enemigo == null || enemigo.currentHealth <= 0) continue;

                float dist = Vector3.Distance(transform.position, enemigo.transform.position);
                if (dist <= rangoAtaque && dist < menorDistancia)
                {
                    menorDistancia = dist;
                    masCercano = enemigo;
                }
            }

            objetivoActual = masCercano;
        }
    }

    private void Disparar()
    {
        if (objetivoActual == null) return;

        if (projectilePrefab != null)
        {
            GameObject projObj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            Projectile p = projObj.GetComponent<Projectile>();
            if (p == null) p = projObj.AddComponent<Projectile>();
            p.Inicializar(objetivoActual, danoPorDisparo);
        }
        else
        {
            // Generación dinámica de proyectil si no hay prefab asignado
            GameObject projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projObj.name = "BalasTorre";
            projObj.transform.position = transform.position;
            projObj.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            Renderer r = projObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = Color.yellow;
            }

            DestroyImmediate(projObj.GetComponent<Collider>()); // Remover collider 3D predeterminado de inmediato

            Projectile p = projObj.AddComponent<Projectile>();
            p.Inicializar(objetivoActual, danoPorDisparo, 12f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizar rango de ataque en el editor de Unity
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangoAtaque);
    }
}
