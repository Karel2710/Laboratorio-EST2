using System.Collections.Generic;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public static List<EnemyHealth> ActiveEnemies = new List<EnemyHealth>();

    [Header("Salud")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Recompensa")]
    public int puntosRecompensa = 10;

    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;

    private void Awake()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveEnemies.Remove(this);
    }

    public void ConfigurarSalud(float nuevaVida)
    {
        maxHealth = nuevaVida;
        currentHealth = nuevaVida;
    }

    public void RecibirDano(float cantidad)
    {
        currentHealth -= cantidad;

        if (spriteRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(EfectoDanio());
        }

        if (currentHealth <= 0)
        {
            Morir();
        }
    }

    private System.Collections.IEnumerator EfectoDanio()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
        }
    }

    private void Morir()
    {
        ActiveEnemies.Remove(this);
        Destroy(gameObject);
    }
}
