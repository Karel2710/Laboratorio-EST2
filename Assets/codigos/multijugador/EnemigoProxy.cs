using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Representación de un enemigo en el CLIENTE LAN.
    /// Usa el mismo componente Enemigo del juego en modo réplica (cuerpo 2D/2.5D, barra de vida,
    /// animaciones), pero sin física ni lógica: se mueve suavemente hacia la última posición que
    /// envió el host y muestra vida, escudo, ralentización y aturdimiento.
    /// </summary>
    public class EnemigoProxy : MonoBehaviour
    {
        public static readonly List<EnemigoProxy> Activos = new List<EnemigoProxy>();

        public int idRed;
        public TipoEnemigo tipo;

        /// <summary>Enemigo réplica asociado (objetivo de los disparos visuales).</summary>
        public Enemigo Replica { get; private set; }

        private Vector3 posicionObjetivo;

        public static EnemigoProxy Crear(int id, TipoEnemigo tipo, Vector3 posicion)
        {
            GameObject go = new GameObject($"Proxy_{tipo}_{id}");
            go.transform.position = posicion;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpritesEnemigos.Obtener(tipo);
            sr.sortingOrder = 8;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            go.AddComponent<enemyMovement>();
            Enemigo e = go.AddComponent<Enemigo>();
            e.ConvertirEnReplica();
            int ronda = GameManager.Instance != null ? GameManager.Instance.oleadaActual : 1;
            e.ConfigurarTipo(tipo, ronda);

            EnemigoProxy p = go.AddComponent<EnemigoProxy>();
            p.idRed = id;
            p.tipo = tipo;
            p.posicionObjetivo = posicion;
            p.Replica = e;
            return p;
        }

        private void OnEnable()
        {
            if (!Activos.Contains(this)) Activos.Add(this);
        }

        private void OnDisable()
        {
            Activos.Remove(this);
        }

        public void ActualizarDesdeRed(Vector3 posicion, float vidaPct, byte flags)
        {
            posicionObjetivo = posicion;

            // Si el salto es muy grande (instantánea perdida o recién creado) se teletransporta
            if (Vector3.Distance(transform.position, posicion) > 3f)
            {
                transform.position = posicion;
            }

            if (Replica != null) Replica.AplicarEstadoReplica(vidaPct, flags);
        }

        private void Update()
        {
            // Interpolación exponencial: suave con 15 instantáneas por segundo
            float t = 1f - Mathf.Exp(-15f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, posicionObjetivo, t);
        }
    }
}
