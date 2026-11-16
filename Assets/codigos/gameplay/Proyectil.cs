using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Proyectil disparado por las torres hacia los enemigos.
    /// Soporta impacto directo, daño crítico, ralentización, congelación total y daño en área.
    /// </summary>
    public class Proyectil : MonoBehaviour
    {
        public float velocidad = 13f;
        public float danio = 20f;
        public bool esCritico = false;
        public bool aplicaRalentizacion = false;
        public float porcentajeRalentizacion = 0.45f;
        public float duracionRalentizacion = 2.5f;
        public bool esAreaSplash = false;
        public float radioSplash = 1.6f;
        public bool congelacionTotal = false;
        public TipoDanio tipoDanio = TipoDanio.Cinetico;
        public Color colorEstela = new Color(1f, 0.6f, 0.2f);

        private Enemigo objetivo;
        private Vector3 ultimaPosicionConocida;
        private SpriteRenderer spriteRenderer;
        private TrailRenderer estela;
        private bool impacto = false;

        public void Configurar(Enemigo target, float danioBase, bool critico, bool ralentizar, bool splash = false, bool congelar = false)
        {
            objetivo = target;
            danio = danioBase;
            esCritico = critico;
            aplicaRalentizacion = ralentizar;
            esAreaSplash = splash;
            congelacionTotal = congelar;

            if (objetivo != null) ultimaPosicionConocida = objetivo.PuntoImpacto;
            else ultimaPosicionConocida = transform.position;

            AsegurarVisuales();
        }

        /// <summary>Proyectil dirigido a un punto fijo (Bombardeo Orbital).</summary>
        public void ConfigurarHaciaPunto(Vector3 destino, float danioBase, float radio)
        {
            objetivo = null;
            ultimaPosicionConocida = destino;
            danio = danioBase;
            esAreaSplash = true;
            radioSplash = radio;
            tipoDanio = TipoDanio.Explosivo;
            velocidad = 20f;
            AsegurarVisuales();
        }

        private void AsegurarVisuales()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = GraficosProc.Circulo;
                spriteRenderer.sortingOrder = 15;
            }

            Color color;
            float escala;
            if (congelacionTotal) { color = new Color(0.1f, 0.95f, 1f); escala = 0.24f; }
            else if (tipoDanio == TipoDanio.Hielo) { color = new Color(0.3f, 0.85f, 1f); escala = 0.2f; }
            else if (esAreaSplash) { color = new Color(1f, 0.35f, 0.1f); escala = 0.3f; }
            else if (esCritico) { color = new Color(1f, 0.85f, 0.1f); escala = 0.24f; }
            else { color = new Color(1f, 0.6f, 0.25f); escala = 0.17f; }

            spriteRenderer.color = color;
            transform.localScale = Vector3.one * escala;

            // Estela luminosa
            if (estela == null)
            {
                estela = gameObject.AddComponent<TrailRenderer>();
                estela.sharedMaterial = GraficosProc.MaterialLinea;
                estela.time = 0.16f;
                estela.minVertexDistance = 0.05f;
                estela.sortingOrder = 14;
                estela.numCapVertices = 2;
                estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            estela.startWidth = escala * 0.8f;
            estela.endWidth = 0f;
            estela.startColor = new Color(color.r, color.g, color.b, 0.85f);
            estela.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private void Update()
        {
            if (impacto) return;

            if (objetivo != null && !objetivo.EstaMuerto) ultimaPosicionConocida = objetivo.PuntoImpacto;

            Vector3 direccion = ultimaPosicionConocida - transform.position;
            float distanciaPaso = velocidad * Time.deltaTime;

            if (direccion.magnitude <= distanciaPaso || direccion.magnitude <= 0.15f)
            {
                transform.position = ultimaPosicionConocida;
                Impactar();
                return;
            }

            transform.position += direccion.normalized * distanciaPaso;
            transform.rotation = Vista25D.Activa ? Vista25D.RotacionCartel : Quaternion.Euler(0, 0, Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg);
        }

        private void AplicarEfectos(Enemigo e, float danioAplicado, bool critico)
        {
            if (e == null || e.EstaMuerto) return;
            // Los estados se aplican antes del daño: el enemigo puede ser destruido al recibirlo
            if (congelacionTotal) e.Congelar(1.5f);
            if (aplicaRalentizacion) e.AplicarRalentizacion(porcentajeRalentizacion, duracionRalentizacion);
            e.RecibirDanio(danioAplicado, critico, tipoDanio);
        }

        private void Impactar()
        {
            impacto = true;
            EfectosVisualesManager fx = EfectosVisualesManager.Instance;

            if (esAreaSplash)
            {
                // Daño en área: se copian los afectados porque la lista cambia cuando mueren
                List<Enemigo> afectados = new List<Enemigo>();
                List<Enemigo> lista = Enemigo.Activos;
                for (int i = 0; i < lista.Count; i++)
                {
                    if (lista[i] != null && Vector2.Distance(transform.position, lista[i].transform.position) <= radioSplash)
                        afectados.Add(lista[i]);
                }
                for (int i = 0; i < afectados.Count; i++)
                {
                    float d = Vector2.Distance(transform.position, afectados[i].transform.position);
                    float atenuacion = Mathf.Lerp(1f, 0.55f, d / Mathf.Max(0.01f, radioSplash));
                    AplicarEfectos(afectados[i], danio * atenuacion, esCritico);
                }

                if (fx != null)
                {
                    fx.OndaExpansiva(transform.position, radioSplash, new Color(1f, 0.55f, 0.15f), 0.3f);
                    fx.Explosion(transform.position, new Color(1f, 0.6f, 0.2f), 10, 3.5f);
                    fx.Sonido(TipoSonido.Explosion);
                }
            }
            else
            {
                AplicarEfectos(objetivo, danio, esCritico);
                if (fx != null) fx.Explosion(transform.position, spriteRenderer != null ? spriteRenderer.color : Color.white, 3, 1.6f);
            }

            Destroy(gameObject);
        }
    }
}
