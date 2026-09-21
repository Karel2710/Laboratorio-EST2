using UnityEngine;

public class Projectile : MonoBehaviour
{
    private EnemyHealth targetEnemy;
    private float damage = 25f;
    private float speed = 10f;

    public void Inicializar(EnemyHealth objetivo, float danoProyectil, float velocidadProyectil = 10f)
    {
        this.targetEnemy = objetivo;
        this.damage = danoProyectil;
        this.speed = velocidadProyectil;
    }

    private void Update()
    {
        if (targetEnemy == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direccion = (targetEnemy.transform.position - transform.position).normalized;
        transform.position += direccion * speed * Time.deltaTime;

        // Rotar proyectil hacia la dirección del movimiento
        float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angulo);

        // Distancia para impacto
        if (Vector3.Distance(transform.position, targetEnemy.transform.position) <= 0.25f)
        {
            Impactar();
        }
    }

    private void Impactar()
    {
        if (targetEnemy != null)
        {
            targetEnemy.RecibirDano(damage);
        }
        Destroy(gameObject);
    }
}
