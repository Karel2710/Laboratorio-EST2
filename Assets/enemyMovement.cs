using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class enemyMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Attributes")]
    [SerializeField] private float moveSpeed = 2f;

    public void ConfigurarVelocidad(float nuevaVelocidad)
    {
        moveSpeed = nuevaVelocidad;
    }

    private Transform target;
    private int pathIndex = 0;
    private TowerDefenseGameplay.Enemigo componenteEnemigo;

    private void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.gravityScale = 0f; // Evitar que caiga en gravedad 2D
            rb.freezeRotation = true; // Evitar giros físicos que lo tranquen contra las esquinas
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true; // Trigger para que nunca colisione físicamente con las casillas de torres
        }

        componenteEnemigo = GetComponent<TowerDefenseGameplay.Enemigo>();
        if (componenteEnemigo == null)
        {
            componenteEnemigo = gameObject.AddComponent<TowerDefenseGameplay.Enemigo>();
        }

        if (levelManager.main != null && levelManager.main.Path != null && levelManager.main.Path.Length > 0)
        {
            target = levelManager.main.Path[pathIndex];
        }
    }

    private void Update()
    {
        if (target == null || levelManager.main == null) return;

        // Umbral de 0.35f para que la navegación entre puntos de ruta sea fluida y nunca se quede atascado
        if (Vector2.Distance(transform.position, target.position) <= 0.35f)
        {
            pathIndex++;
            if (pathIndex >= levelManager.main.Path.Length)
            {
                // El enemigo llegó a la base
                if (TowerDefenseGameplay.GameManager.Instance != null)
                {
                    int danio = componenteEnemigo != null ? componenteEnemigo.danioABase : 10;
                    TowerDefenseGameplay.GameManager.Instance.EnemigoLlegoALaBase(danio);
                }
                Destroy(gameObject);
                return;
            }
            else
            {
                target = levelManager.main.Path[pathIndex];
            }
        }
    }

    private void FixedUpdate()
    {
        if (target == null || rb == null) return;

        float multiplicador = componenteEnemigo != null ? componenteEnemigo.VelocidadEfectiva : 1f;
        Vector2 dir = ((Vector2)target.position - rb.position).normalized;
        rb.linearVelocity = dir * (moveSpeed * multiplicador);
    }
}
