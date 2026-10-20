using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Proyectil disparado por las torres hacia los enemigos.
    /// Soporta daño base, impactos críticos y ralentización criogénica (habilidad DAG).
    /// </summary>
    public class Proyectil : MonoBehaviour
    {
        public float velocidad = 12f;
        public float danio = 20f;
        public bool esCritico = false;
        public bool aplicaRalentizacion = false;
        public float porcentajeRalentizacion = 0.45f;
        public float duracionRalentizacion = 2.5f;
        public bool esAreaSplash = false;
        public float radioSplash = 1.6f;
        public bool congelacionTotal = false;

        private Enemigo objetivo;
        private Vector3 ultimaPosicionConocida;
        private SpriteRenderer spriteRenderer;

        public void Configurar(Enemigo target, float danioBase, bool critico, bool ralentizar, bool splash = false, bool congelar = false)
        {
            objetivo = target;
            danio = danioBase;
            esCritico = critico;
            aplicaRalentizacion = ralentizar;
            esAreaSplash = splash;
            congelacionTotal = congelar;

            if (objetivo != null)
            {
                ultimaPosicionConocida = objetivo.transform.position;
            }

            AsegurarSprite();
        }

        private void Awake()
        {
            AsegurarSprite();
        }

        private void AsegurarSprite()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = CrearSpriteProyectil();
                    spriteRenderer.sortingOrder = 15;
                }
            }

            // Colorear según tipo de proyectil
            if (congelacionTotal)
            {
                spriteRenderer.color = new Color(0.1f, 0.95f, 1f); // Cian helado intenso
                transform.localScale = Vector3.one * 0.45f;
            }
            else if (aplicaRalentizacion)
            {
                spriteRenderer.color = new Color(0.3f, 0.85f, 1f); // Azul frío
                transform.localScale = Vector3.one * 0.35f;
            }
            else if (esAreaSplash)
            {
                spriteRenderer.color = new Color(1f, 0.35f, 0.1f); // Bomba de cañón
                transform.localScale = Vector3.one * 0.55f;
            }
            else if (esCritico)
            {
                spriteRenderer.color = new Color(1f, 0.85f, 0.1f); // Dorado crítico
                transform.localScale = Vector3.one * 0.45f;
            }
            else
            {
                spriteRenderer.color = new Color(1f, 0.5f, 0.2f); // Naranja energía
                transform.localScale = Vector3.one * 0.3f;
            }
        }

        private void Update()
        {
            if (objetivo != null)
            {
                ultimaPosicionConocida = objetivo.transform.position;
            }

            Vector3 direccion = (ultimaPosicionConocida - transform.position);
            float distanciaPaso = velocidad * Time.deltaTime;

            if (direccion.magnitude <= distanciaPaso || direccion.magnitude <= 0.2f)
            {
                Impactar();
                return;
            }

            transform.position += direccion.normalized * distanciaPaso;

            // Orientar proyectil
            float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angulo);
        }

        private void Impactar()
        {
            float ralentizacionAplicar = congelacionTotal ? 1.0f : porcentajeRalentizacion;

            if (esAreaSplash)
            {
                // Daño en área (Splash de Artillería)
                Collider2D[] colisiones = Physics2D.OverlapCircleAll(transform.position, radioSplash);
                for (int i = 0; i < colisiones.Length; i++)
                {
                    Enemigo e = colisiones[i].GetComponent<Enemigo>();
                    if (e != null)
                    {
                        e.RecibirDanio(danio, esCritico);
                        if (aplicaRalentizacion || congelacionTotal)
                        {
                            e.AplicarRalentizacion(ralentizacionAplicar, duracionRalentizacion);
                        }
                    }
                }
            }
            else
            {
                if (objetivo != null)
                {
                    objetivo.RecibirDanio(danio, esCritico);
                    if (aplicaRalentizacion || congelacionTotal)
                    {
                        objetivo.AplicarRalentizacion(ralentizacionAplicar, duracionRalentizacion);
                    }
                }
            }

            Destroy(gameObject);
        }

        private Sprite CrearSpriteProyectil()
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size);
            Vector2 centro = new Vector2(size / 2f, size / 2f);
            float radio = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), centro);
                    tex.SetPixel(x, y, dist <= radio ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16);
        }
    }
}
