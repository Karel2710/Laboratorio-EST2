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

        // Vista 2.5D: plataforma de piedra del kit Kenney que se tiñe al pasar el cursor
        private Transform pad3D;
        private Renderer[] rendPad;
        private Color tintePad = new Color(0f, 0f, 0f, 0f);
        private int versionVista = -1;
        private static readonly Color tinteLibre = new Color(0.55f, 0.82f, 1f, 0.24f);

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

        private void Update()
        {
            if (versionVista != Vista25D.Version) AplicarModoVista();
        }

        private void AplicarModoVista()
        {
            versionVista = Vista25D.Version;
            bool v3 = Vista25D.Activa;
            if (v3 && pad3D == null) CrearPad();
            if (pad3D != null) pad3D.gameObject.SetActive(v3);
            if (spriteRenderer != null) spriteRenderer.enabled = !v3;
        }

        private void CrearPad()
        {
            pad3D = Recursos25D.CrearRaizModelo(transform, "Plataforma3D");
            // floor mide 1 x 0.05 x 1: se escala al tamaño de la casilla y a ALTURA_PAD de grosor
            if (Recursos25D.CrearModelo("floor", pad3D, Vector3.zero, new Vector3(0.8f, Vista25D.ALTURA_PAD / 0.05f, 0.8f)) == null)
            {
                Recursos25D.Primitiva(PrimitiveType.Cube, pad3D, new Vector3(0f, Vista25D.ALTURA_PAD * 0.5f, 0f),
                    new Vector3(0.8f, Vista25D.ALTURA_PAD, 0.8f), Recursos25D.MaterialColor(new Color(0.5f, 0.52f, 0.56f)));
            }
            rendPad = pad3D.GetComponentsInChildren<Renderer>(true);
            Recursos25D.Tenir(rendPad, tintePad);
        }

        private void TenirPad(Color c)
        {
            if (c == tintePad) return;
            tintePad = c;
            if (rendPad != null) Recursos25D.Tenir(rendPad, c);
        }

        public void ResaltarHover(bool valido)
        {
            resaltado = true;
            Color c = valido ? Paleta.Positivo : Paleta.Negativo;
            TenirPad(new Color(c.r, c.g, c.b, 0.55f));
            if (spriteRenderer == null) return;
            c.a = 0.5f;
            spriteRenderer.color = c;
        }

        public void ResaltarSeleccion()
        {
            resaltado = true;
            Color c = Paleta.Dorado;
            TenirPad(new Color(c.r, c.g, c.b, 0.5f));
            if (spriteRenderer == null) return;
            c.a = 0.45f;
            spriteRenderer.color = c;
        }

        public void RestaurarColor()
        {
            resaltado = false;
            TenirPad(TieneTorre ? new Color(0f, 0f, 0f, 0f) : tinteLibre);
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
            if (ls.x > 0.01f && ls.y > 0.01f) contenedor.escalaBase = new Vector3(1f / ls.x, 1f / ls.y, ls.z > 0.01f ? 1f / ls.z : 1f);
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
