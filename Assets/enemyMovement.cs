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

    private Transform target;
    private int pathIndex = 0;

    private void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        ObtenerSiguienteObjetivo();
    }

    private void Update()
    {
        if (target == null)
        {
            ObtenerSiguienteObjetivo();
            if (target == null) return;
        }

        if (Vector2.Distance(transform.position, target.position) <= 0.1f)
        {
            pathIndex++;
            ObtenerSiguienteObjetivo();
        }
    }

    private void FixedUpdate()
    {
        if (target == null || rb == null) return;

        Vector2 dir = ((Vector2)target.position - rb.position).normalized;
        rb.linearVelocity = dir * moveSpeed;
    }

    private void ObtenerSiguienteObjetivo()
    {
        if (levelManager.main != null && levelManager.main.Path != null && levelManager.main.Path.Length > 0)
        {
            if (pathIndex >= 0 && pathIndex < levelManager.main.Path.Length)
            {
                target = levelManager.main.Path[pathIndex];
            }
            else
            {
                target = null;
                Destroy(gameObject);
            }
        }
    }
}
