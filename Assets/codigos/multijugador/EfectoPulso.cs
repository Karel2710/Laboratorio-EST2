using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>Efectos visuales que también se replican al cliente LAN.</summary>
    public enum TipoEfectoVisual : byte
    {
        Escudo = 0,
        Curacion = 1,
        Estampida = 2,
        Hackeo = 3,
        PulsoEMP = 4,
        AsaltoTotal = 5,
        Sobrecarga = 6,
        Barricada = 7,
        Bombardeo = 8,
        Construccion = 9
    }

    /// <summary>
    /// Anillo que se expande y se desvanece. Marca dónde se usó una habilidad (escudo, curación,
    /// hackeo, EMP...). En el host se registra para que el cliente dibuje el mismo efecto.
    /// </summary>
    public class EfectoPulso : MonoBehaviour
    {
        private static Sprite spriteAnillo;

        private SpriteRenderer sr;
        private float diametroFinal = 2f;
        private float duracion = 0.6f;
        private float tiempo = 0f;
        private Color color;

        /// <summary>Anillo blanco de 1 unidad de diámetro (se tiñe y escala según el uso).</summary>
        public static Sprite SpriteAnillo
        {
            get
            {
                if (spriteAnillo != null) return spriteAnillo;

                int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                Vector2 centro = new Vector2(size / 2f, size / 2f);
                float rExt = size / 2f - 1f;
                float rInt = rExt - 5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), centro);
                        float a = (d <= rExt && d >= rInt) ? 1f : 0f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                spriteAnillo = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return spriteAnillo;
            }
        }

        public static Color ColorDe(TipoEfectoVisual tipo)
        {
            switch (tipo)
            {
                case TipoEfectoVisual.Escudo: return new Color(0.35f, 0.9f, 1f, 0.9f);
                case TipoEfectoVisual.Curacion: return new Color(0.35f, 1f, 0.45f, 0.9f);
                case TipoEfectoVisual.Estampida: return new Color(1f, 0.8f, 0.2f, 0.9f);
                case TipoEfectoVisual.Hackeo: return new Color(0.55f, 1f, 0.35f, 0.95f);
                case TipoEfectoVisual.PulsoEMP: return new Color(0.6f, 0.75f, 1f, 0.95f);
                case TipoEfectoVisual.AsaltoTotal: return new Color(1f, 0.3f, 0.25f, 0.95f);
                case TipoEfectoVisual.Sobrecarga: return new Color(1f, 0.55f, 0.15f, 0.95f);
                case TipoEfectoVisual.Barricada: return new Color(1f, 0.85f, 0.25f, 0.95f);
                case TipoEfectoVisual.Bombardeo: return new Color(1f, 0.45f, 0.15f, 0.95f);
                case TipoEfectoVisual.Construccion: return new Color(0.5f, 0.9f, 1f, 0.9f);
                default: return Color.white;
            }
        }

        /// <summary>Crea el efecto localmente y, si este PC es el host LAN, lo envía al cliente.</summary>
        public static void Emitir(Vector3 posicion, float radio, TipoEfectoVisual tipo)
        {
            Crear(posicion, radio, ColorDe(tipo));
            if (ServidorPartida.Instancia != null)
            {
                ServidorPartida.Instancia.RegistrarEfecto(tipo, posicion, radio);
            }
        }

        public static void Crear(Vector3 posicion, float radio, Color color, float duracion = 0.6f)
        {
            // Con el gestor de efectos del juego la onda se adapta a la vista 2D y a la 2.5D
            EfectosVisualesManager fx = EfectosVisualesManager.Instance;
            if (fx != null)
            {
                fx.OndaExpansiva(new Vector3(posicion.x, posicion.y, 0f), Mathf.Max(0.1f, radio), color, Mathf.Max(0.1f, duracion));
                return;
            }

            GameObject go = new GameObject("EfectoPulso");
            go.transform.position = new Vector3(posicion.x, posicion.y, 0f);
            EfectoPulso e = go.AddComponent<EfectoPulso>();
            e.sr = go.AddComponent<SpriteRenderer>();
            e.sr.sprite = SpriteAnillo;
            e.sr.sortingOrder = 14;
            e.color = color;
            e.sr.color = color;
            e.diametroFinal = Mathf.Max(0.2f, radio * 2f);
            e.duracion = Mathf.Max(0.1f, duracion);
            go.transform.localScale = Vector3.one * e.diametroFinal * 0.2f;
        }

        private void Update()
        {
            tiempo += Time.deltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);
            transform.localScale = Vector3.one * Mathf.Lerp(diametroFinal * 0.2f, diametroFinal, t);
            if (sr != null)
            {
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
