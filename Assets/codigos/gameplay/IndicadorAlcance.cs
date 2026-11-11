using System.Collections.Generic;
using UnityEngine;
using GrafosEST2;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Previsualización de construcción: círculo de alcance exacto de la torre y posibles
    /// enlaces MST hacia las torres vecinas dentro del radio de conexión.
    /// </summary>
    public class IndicadorAlcance : MonoBehaviour
    {
        private LineRenderer circulo;
        private readonly List<LineRenderer> enlaces = new List<LineRenderer>();
        private readonly List<Torre> vecinas = new List<Torre>();

        private void Awake()
        {
            circulo = GraficosProc.CrearLinea(transform, "CirculoAlcance", Color.white, Color.white, 0.05f, 12);
            circulo.gameObject.SetActive(false);
        }

        public void Mostrar(Vector3 centro, float radio, Color color, bool esRadar, Torre excluir)
        {
            circulo.gameObject.SetActive(true);
            GraficosProc.TrazarCirculo(circulo, centro, radio, 64);
            Color c = color;
            c.a = 0.85f;
            circulo.startColor = c;
            circulo.endColor = c;

            if (RedTorresMST.Instance != null) RedTorresMST.Instance.VecinasEnRadio(centro, esRadar, excluir, vecinas);
            else vecinas.Clear();

            while (enlaces.Count < vecinas.Count)
                enlaces.Add(GraficosProc.CrearLinea(transform, "EnlacePosible", Paleta.Cian, Paleta.Cian, 0.035f, 11));

            for (int i = 0; i < enlaces.Count; i++)
            {
                bool activo = i < vecinas.Count && vecinas[i] != null;
                enlaces[i].gameObject.SetActive(activo);
                if (!activo) continue;
                enlaces[i].SetPosition(0, centro);
                enlaces[i].SetPosition(1, vecinas[i].transform.position);
                Color ce = Paleta.Cian;
                ce.a = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
                enlaces[i].startColor = ce;
                enlaces[i].endColor = ce;
            }
        }

        public void Ocultar()
        {
            if (circulo != null) circulo.gameObject.SetActive(false);
            for (int i = 0; i < enlaces.Count; i++) enlaces[i].gameObject.SetActive(false);
        }
    }
}
