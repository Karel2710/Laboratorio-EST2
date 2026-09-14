using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class enemyMovement : MonoBehaviour{
    [header("References")]
    [SerializeField] private Rigidbody2D rb;
    [header("Attributes")]
    [SerializeField] private float moveSpeed=2f;

    private Transform target;
    private int pathIndex=0;
    private void Start()
    {
        target = levelManager.main.Path[pathIndex];
    }
    private void Update()
    {
        if (vector2.Distance(transform.position, target.position) <= 0.1f)
        {
            pathIndex++;
            if (pathIndex == levelManager.main.Path.Length)
            {
                Destroy(gameObject);
                return;
            }else{
                target = levelManager.main.Path[pathIndex];
            }
        }
    }
    private void FixedUpdate()
    {
        Vector2 dir = (target.position - transform.position).normalized;
        rb.velocity = dir * moveSpeed;
    }
}
