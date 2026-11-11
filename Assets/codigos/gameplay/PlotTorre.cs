using System.Collections.Generic;
using UnityEngine;
using GrafosEST2;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Casilla de terreno donde se construyen torres.
    /// La interacción (hover, clic) la centraliza GameManager para evitar entradas duplicadas.
    /// </summary>
    public class PlotTorre : MonoBehaviour
    {
        public static readonly List<PlotTorre> Todos = new List<PlotTorre>();

        public int indice = -1;
        public int costoTorre = 50;
        public Torre torreInstanciada = null;
        public bool deshabilitado = false;

        private SpriteRenderer spriteRenderer;
        private bool resaltado = false;

        private static readonly Color colorLibre = new Color(0.45f, 0.85f, 1f, 0.13f);
        private static readonly Color colorOcupado = new Color(0.45f, 0.85f, 1f, 0.05f);

        public bool TieneTorre => torreInstanciada != null;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            // Regla anti-atasco: todo Collider2D de casilla es trigger (jamás bloquea a los enemigos)
            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].isTrigger = true;

            RestaurarColor();
        }

        private void OnEnable()
        {
            if (!Todos.Contains(this)) Todos.Add(this);
        }

        private void OnDisable()
        {
            Todos.Remove(this);
        }

        public void ResaltarHover(bool valido)
        {
            if (spriteRenderer == null) return;
            resaltado = true;
            Color c = valido ? Paleta.Positivo : Paleta.Negativo;
            c.a = 0.5f;
            spriteRenderer.color = c;
        }

        public void ResaltarSeleccion()
        {
            if (spriteRenderer == null) return;
            resaltado = true;
            Color c = Paleta.Dorado;
            c.a = 0.45f;
            spriteRenderer.color = c;
        }

        public void RestaurarColor()
        {
            resaltado = false;
            if (spriteRenderer != null) spriteRenderer.color = TieneTorre ? colorOcupado : colorLibre;
        }

        public bool EstaResaltado => resaltado;

        /// <summary>
        /// Instancia una torre en la casilla. No cobra: el pago lo valida GameManager.
        /// </summary>
        public Torre ColocarTorre(TipoTorre tipo, int inversion)
        {
            if (TieneTorre || deshabilitado) return null;

            GameObject torreObj = new GameObject($"Torre_{tipo}_{indice}");
            torreObj.transform.position = transform.position;
            torreObj.transform.SetParent(transform, true);

            Torre contenedor = torreObj.AddComponent<Torre>();
            // Compensar la escala de la casilla para que la torre conserve su tamaño de diseño
            Vector3 ls = transform.lossyScale;
            if (ls.x > 0.01f && ls.y > 0.01f) contenedor.escalaBase = new Vector3(1f / ls.x, 1f / ls.y, 1f);
            contenedor.ConfigurarTipo(tipo);
            contenedor.inversionTotal = inversion;
            contenedor.plot = this;
            torreInstanciada = contenedor;

            RestaurarColor();
            if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();
            return contenedor;
        }

        /// <summary>Retira la torre de la casilla y actualiza la red MST.</summary>
        public void QuitarTorre()
        {
            if (!TieneTorre) return;
            Torre t = torreInstanciada;
            torreInstanciada = null;
            Torre.Activas.Remove(t);
            Destroy(t.gameObject);
            RestaurarColor();
            if (RedTorresMST.Instance != null) RedTorresMST.Instance.Recalcular();
        }
    }
}
