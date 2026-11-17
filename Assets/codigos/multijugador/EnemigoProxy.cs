using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Representación visual de un enemigo en el CLIENTE LAN.
    /// No tiene física ni lógica: se mueve suavemente hacia la última posición que envió el host
    /// y muestra vida, escudo, ralentización y aturdimiento.
    /// </summary>
    public class EnemigoProxy : MonoBehaviour
    {
        public static readonly List<EnemigoProxy> Activos = new List<EnemigoProxy>();

        public int idRed;
        public TipoEnemigo tipo;

        private Vector3 posicionObjetivo;
        private SpriteRenderer sr;
        private SpriteRenderer anilloEscudo;
        private Transform barraVerde;
        private float anchoBarra = 0.9f;
        private float altoBarra = 0.12f;

        public static EnemigoProxy Crear(int id, TipoEnemigo tipo, Vector3 posicion)
        {
            GameObject go = new GameObject($"Proxy_{tipo}_{id}");
            go.transform.position = posicion;
            go.transform.localScale = Vector3.one * Enemigo.EscalaPorTipo(tipo);

            EnemigoProxy p = go.AddComponent<EnemigoProxy>();
            p.idRed = id;
            p.tipo = tipo;
            p.posicionObjetivo = posicion;

            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sprite = GameManager.CrearSpriteEnemigo(tipo);
            p.sr.sortingOrder = 8;

            p.CrearBarraVida();
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

            if (barraVerde != null)
            {
                float pct = Mathf.Clamp01(vidaPct);
                barraVerde.localScale = new Vector3(anchoBarra * pct, altoBarra, 1f);
                barraVerde.localPosition = new Vector3(-(anchoBarra * 0.5f) * (1f - pct), 0, 0);
            }

            if (sr != null)
            {
                if ((flags & Enemigo.FlagAturdido) != 0) sr.color = new Color(1f, 1f, 0.55f);
                else if ((flags & Enemigo.FlagRalentizado) != 0) sr.color = new Color(0.35f, 0.85f, 1f);
                else if ((flags & Enemigo.FlagAcelerado) != 0) sr.color = new Color(1f, 0.8f, 0.6f);
                else sr.color = Color.white;
            }

            if (anilloEscudo != null)
            {
                anilloEscudo.enabled = (flags & Enemigo.FlagEscudo) != 0;
            }
        }

        private void Update()
        {
            // Interpolación exponencial: suave con 15 instantáneas por segundo
            float t = 1f - Mathf.Exp(-15f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, posicionObjetivo, t);
        }

        private void CrearBarraVida()
        {
            float offsetY;
            switch (tipo)
            {
                case TipoEnemigo.Corredor: anchoBarra = 0.70f; altoBarra = 0.10f; offsetY = 0.65f; break;
                case TipoEnemigo.Blindado: anchoBarra = 1.05f; altoBarra = 0.13f; offsetY = 0.75f; break;
                case TipoEnemigo.Jefe: anchoBarra = 1.25f; altoBarra = 0.15f; offsetY = 0.85f; break;
                default: anchoBarra = 0.85f; altoBarra = 0.11f; offsetY = 0.70f; break;
            }

            GameObject contenedor = new GameObject("BarraVida");
            contenedor.transform.SetParent(transform, false);
            contenedor.transform.localPosition = new Vector3(0, offsetY, 0);

            GameObject fondo = new GameObject("FondoRojo");
            fondo.transform.SetParent(contenedor.transform, false);
            SpriteRenderer srFondo = fondo.AddComponent<SpriteRenderer>();
            srFondo.sprite = Enemigo.ObtenerSpriteCuadrado();
            srFondo.color = new Color(0.7f, 0.1f, 0.1f, 0.9f);
            srFondo.sortingOrder = 10;
            fondo.transform.localScale = new Vector3(anchoBarra, altoBarra, 1f);

            GameObject verde = new GameObject("BarraVerde");
            verde.transform.SetParent(contenedor.transform, false);
            SpriteRenderer srVerde = verde.AddComponent<SpriteRenderer>();
            srVerde.sprite = Enemigo.ObtenerSpriteCuadrado();
            srVerde.color = new Color(0.2f, 0.9f, 0.3f, 0.95f);
            srVerde.sortingOrder = 11;
            verde.transform.localScale = new Vector3(anchoBarra, altoBarra, 1f);
            barraVerde = verde.transform;

            GameObject anillo = new GameObject("AnilloEscudo");
            anillo.transform.SetParent(transform, false);
            anilloEscudo = anillo.AddComponent<SpriteRenderer>();
            anilloEscudo.sprite = EfectoPulso.SpriteAnillo;
            anilloEscudo.color = new Color(0.35f, 0.9f, 1f, 0.85f);
            anilloEscudo.sortingOrder = 9;
            anillo.transform.localScale = Vector3.one * 1.25f;
            anilloEscudo.enabled = false;
        }
    }
}
