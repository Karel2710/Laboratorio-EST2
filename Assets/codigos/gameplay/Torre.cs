using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    public enum TipoTorre
    {
        Basica,
        Hielo,
        Canon,
        Laser
    }

    /// <summary>
    /// Torre de defensa que busca enemigos en rango, rota su cañón y dispara proyectiles.
    /// Modifica sus atributos en tiempo real según las habilidades desbloqueadas en el DAG.
    /// </summary>
    public class Torre : MonoBehaviour
    {
        [Header("Configuración de Torre")]
        public TipoTorre tipo = TipoTorre.Basica;
        public float alcance = 3.8f;
        public float cadenciaDisparoBase = 0.8f;
        public float danioBase = 22f;
        public int costoConstruccion = 50;

        [Header("Partes Visuales")]
        private Transform canonRotatorio;
        private SpriteRenderer srBase;
        private SpriteRenderer srAnillo;
        private SpriteRenderer srCanon;

        [Header("Multijugador LAN")]
        [Tooltip("Cliente LAN: la torre solo se dibuja (el host es quien dispara de verdad).")]
        public bool soloVisual = false;
        private float hackeadaHasta = 0f;
        private bool hackeadaVisual = false;
        private bool tinteHackeoAplicado = false;

        private float temporizadorDisparo = 0f;
        private Enemigo objetivoActual;

        /// <summary>True mientras la habilidad "Hackeo" del atacante tiene desactivada esta torre.</summary>
        public bool EstaHackeada => soloVisual ? hackeadaVisual : Time.time < hackeadaHasta;

        /// <summary>
        /// Alcance real: incluye el bono de la habilidad DAG "Ventisca Prolongada" para torres de hielo.
        /// (El alcance base lo modifica además la red de energía MST.)
        /// </summary>
        public float AlcanceEfectivo
        {
            get
            {
                float a = alcance;
                if (tipo == TipoTorre.Hielo && GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_VENTISCA"))
                {
                    a *= 1.25f;
                }
                return a;
            }
        }

        public void Hackear(float duracion)
        {
            hackeadaHasta = Mathf.Max(hackeadaHasta, Time.time + duracion);
            objetivoActual = null;
        }

        /// <summary>Cliente LAN: refleja el estado de hackeo recibido del host.</summary>
        public void EstablecerHackeadaVisual(bool hackeada)
        {
            hackeadaVisual = hackeada;
        }

        private void Awake()
        {
            ConstruirVisualesTorre();
            AplicarParametrosSegunTipo();
        }

        public void ConfigurarTipo(TipoTorre nuevoTipo)
        {
            tipo = nuevoTipo;
            AplicarParametrosSegunTipo();
            ActualizarColoresVisuales();
        }

        private void AplicarParametrosSegunTipo()
        {
            switch (tipo)
            {
                case TipoTorre.Basica:
                    alcance = 3.8f;
                    cadenciaDisparoBase = 0.8f;
                    danioBase = 22f;
                    costoConstruccion = 50;
                    break;

                case TipoTorre.Hielo:
                    alcance = 3.4f;
                    cadenciaDisparoBase = 1.1f;
                    danioBase = 16f;
                    costoConstruccion = 75;
                    break;

                case TipoTorre.Canon:
                    alcance = 4.2f;
                    cadenciaDisparoBase = 1.6f;
                    danioBase = 50f;
                    costoConstruccion = 90;
                    break;

                case TipoTorre.Laser:
                    alcance = 4.8f;
                    cadenciaDisparoBase = 0.45f;
                    danioBase = 32f;
                    costoConstruccion = 120;
                    break;
            }
        }

        private void Update()
        {
            ActualizarTinteHackeo();

            if (soloVisual)
            {
                ApuntarAProxyCercano();
                return;
            }

            if (EstaHackeada) return;

            BuscarObjetivo();

            if (objetivoActual != null)
            {
                ApuntarHaciaObjetivo();

                temporizadorDisparo -= Time.deltaTime;
                if (temporizadorDisparo <= 0f)
                {
                    Disparar();
                    temporizadorDisparo = CalcularCadenciaEfectiva();
                }
            }
        }

        private void BuscarObjetivo()
        {
            float alcanceReal = AlcanceEfectivo;

            if (objetivoActual != null)
            {
                float distActual = Vector2.Distance(transform.position, objetivoActual.transform.position);
                if (distActual <= alcanceReal)
                {
                    return;
                }
            }

            // Lista estática de enemigos vivos (evita FindObjectsByType en cada frame de cada torre)
            List<Enemigo> todos = Enemigo.Activos;
            float menorDistancia = float.MaxValue;
            Enemigo masCercano = null;

            for (int i = 0; i < todos.Count; i++)
            {
                if (todos[i] == null || todos[i].EstaMuerto) continue;
                float dist = Vector2.Distance(transform.position, todos[i].transform.position);
                if (dist <= alcanceReal && dist < menorDistancia)
                {
                    menorDistancia = dist;
                    masCercano = todos[i];
                }
            }

            objetivoActual = masCercano;
        }

        private void ApuntarHaciaObjetivo()
        {
            if (canonRotatorio == null || objetivoActual == null) return;

            Vector2 dir = (objetivoActual.transform.position - transform.position).normalized;
            float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            canonRotatorio.rotation = Quaternion.Euler(0, 0, angulo);
        }

        private void Disparar()
        {
            if (objetivoActual == null) return;

            // Sinergias del Grafo DAG
            bool tieneCalibrePesado = GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_CALIBRE_PESADO");
            bool tieneCritico = GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_DISPARO_CRITICO");
            bool tieneCanonArea = GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_CANON_AREA");
            bool tieneCeroAbsoluto = GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_CERO_ABSOLUTO");
            bool tieneSobrecarga = GameManager.Instance != null && GameManager.Instance.SobrecargaActiva;

            float danioFinal = danioBase;
            bool esCritico = false;
            bool aplicaHielo = (tipo == TipoTorre.Hielo) || (GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_TORRE_HIELO"));
            bool esSplash = (tipo == TipoTorre.Canon);
            bool congela = tieneCeroAbsoluto && aplicaHielo;

            // Multiplicadores por habilidades DAG
            if (tieneCalibrePesado)
            {
                danioFinal *= 1.30f; // +30% daño de impacto
            }

            if (tieneCritico && Random.value <= 0.25f)
            {
                danioFinal *= 2f; // Impacto crítico x2
                esCritico = true;
            }

            if (tieneSobrecarga)
            {
                danioFinal *= 1.50f; // +50% daño por Sobrecarga de Red
            }

            // DAG: Torre Láser Perforante -> el láser derrite blindajes (+50% contra Blindados y Jefes)
            if (tipo == TipoTorre.Laser && GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_TORRE_LASER") &&
                (objetivoActual.tipoEnemigo == TipoEnemigo.Blindado || objetivoActual.tipoEnemigo == TipoEnemigo.Jefe))
            {
                danioFinal *= 1.5f;
            }

            // Instanciar proyectil
            Vector3 spawnPos = (canonRotatorio != null) ? canonRotatorio.position + canonRotatorio.up * 0.45f : transform.position;
            GameObject projObj = new GameObject("ProyectilTorre");
            projObj.transform.position = spawnPos;

            Proyectil p = projObj.AddComponent<Proyectil>();
            if (esSplash && tieneCanonArea)
            {
                p.radioSplash = 2.4f; // Expansión de onda de choque DAG
            }

            // DAG: Ventisca Prolongada -> la ralentización dura 4 segundos
            if (aplicaHielo && GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_VENTISCA"))
            {
                p.duracionRalentizacion = 4f;
            }

            p.Configurar(objetivoActual, danioFinal, esCritico, aplicaHielo, esSplash, congela);

            // LAN: el host avisa al cliente para que dibuje el mismo disparo
            if (ServidorPartida.Instancia != null)
            {
                ServidorPartida.Instancia.RegistrarDisparo(this, objetivoActual, esCritico, aplicaHielo, esSplash, congela);
            }
        }

        /// <summary>Índice de la casilla donde está la torre (identificador en la red).</summary>
        public int IndicePlot
        {
            get
            {
                PlotTorre plot = GetComponentInParent<PlotTorre>();
                return plot != null ? plot.indiceRed : -1;
            }
        }

        /// <summary>Cliente LAN: dispara un proyectil solo visual hacia el enemigo indicado.</summary>
        public void DispararVisual(Transform objetivo, bool critico, bool hielo, bool splash, bool congela)
        {
            if (objetivo == null) return;

            if (canonRotatorio != null)
            {
                Vector2 dir = (objetivo.position - transform.position).normalized;
                float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                canonRotatorio.rotation = Quaternion.Euler(0, 0, angulo);
            }

            Vector3 spawnPos = (canonRotatorio != null) ? canonRotatorio.position + canonRotatorio.up * 0.45f : transform.position;
            GameObject projObj = new GameObject("ProyectilVisual");
            projObj.transform.position = spawnPos;
            Proyectil p = projObj.AddComponent<Proyectil>();
            p.ConfigurarVisual(objetivo, critico, hielo, splash, congela);
        }

        private void ApuntarAProxyCercano()
        {
            if (canonRotatorio == null) return;

            EnemigoProxy cercano = null;
            float menor = AlcanceEfectivo;
            List<EnemigoProxy> proxies = EnemigoProxy.Activos;
            for (int i = 0; i < proxies.Count; i++)
            {
                if (proxies[i] == null) continue;
                float d = Vector2.Distance(transform.position, proxies[i].transform.position);
                if (d <= menor)
                {
                    menor = d;
                    cercano = proxies[i];
                }
            }

            if (cercano != null)
            {
                Vector2 dir = (cercano.transform.position - transform.position).normalized;
                float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                canonRotatorio.rotation = Quaternion.Euler(0, 0, angulo);
            }
        }

        private void ActualizarTinteHackeo()
        {
            bool hackeada = EstaHackeada;
            if (hackeada == tinteHackeoAplicado) return;
            tinteHackeoAplicado = hackeada;

            if (hackeada)
            {
                if (srBase != null) srBase.color = new Color(0.25f, 0.25f, 0.25f);
                if (srAnillo != null) srAnillo.color = new Color(0.55f, 1f, 0.35f); // Verde "hackeo"
                if (srCanon != null) srCanon.color = new Color(0.15f, 0.15f, 0.15f);
            }
            else
            {
                ActualizarColoresVisuales();
            }
        }

        private float CalcularCadenciaEfectiva()
        {
            float cadencia = cadenciaDisparoBase;

            // DAG: Balas Rápidas -> +25% cadencia
            if (GameManager.Instance != null && GameManager.Instance.TieneHabilidadDAG("HAB_BALAS_RAPIDAS"))
            {
                cadencia *= 0.75f;
            }

            // DAG Definitiva: Sobrecarga de Red -> duplica velocidad de disparo
            if (GameManager.Instance != null && GameManager.Instance.SobrecargaActiva)
            {
                cadencia *= 0.5f;
            }

            return Mathf.Max(cadencia, 0.15f);
        }

        #region Construcción Visual Automática

        private void ConstruirVisualesTorre()
        {
            // 1. Base Circular de la Torre
            GameObject baseObj = new GameObject("BaseTorre");
            baseObj.transform.SetParent(transform, false);
            srBase = baseObj.AddComponent<SpriteRenderer>();
            srBase.sprite = CrearSpriteCirculo();
            srBase.sortingOrder = 5;
            baseObj.transform.localScale = Vector3.one * 0.9f;

            // 2. Anillo Decorativo
            GameObject anillo = new GameObject("Anillo");
            anillo.transform.SetParent(transform, false);
            srAnillo = anillo.AddComponent<SpriteRenderer>();
            srAnillo.sprite = CrearSpriteCirculo();
            srAnillo.sortingOrder = 6;
            anillo.transform.localScale = Vector3.one * 0.6f;

            // 3. Cañón Rotatorio
            GameObject canonObj = new GameObject("CanonRotatorio");
            canonObj.transform.SetParent(transform, false);
            canonRotatorio = canonObj.transform;

            GameObject barril = new GameObject("Barril");
            barril.transform.SetParent(canonRotatorio, false);
            srCanon = barril.AddComponent<SpriteRenderer>();
            srCanon.sprite = CrearSpriteCuadrado();
            srCanon.sortingOrder = 7;
            barril.transform.localPosition = new Vector3(0, 0.28f, 0);

            ActualizarColoresVisuales();
        }

        public void ActualizarColoresVisuales()
        {
            if (srBase == null || srAnillo == null || srCanon == null) return;

            switch (tipo)
            {
                case TipoTorre.Basica:
                    srBase.color = new Color(0.2f, 0.25f, 0.35f);
                    srAnillo.color = new Color(0.35f, 0.5f, 0.7f);
                    srCanon.color = new Color(0.12f, 0.15f, 0.2f);
                    srCanon.transform.localScale = new Vector3(0.22f, 0.55f, 1f);
                    break;

                case TipoTorre.Hielo:
                    srBase.color = new Color(0.15f, 0.3f, 0.45f);
                    srAnillo.color = new Color(0.3f, 0.85f, 1f); // Azul cian gélido
                    srCanon.color = new Color(0.15f, 0.55f, 0.75f);
                    srCanon.transform.localScale = new Vector3(0.26f, 0.5f, 1f);
                    break;

                case TipoTorre.Canon:
                    srBase.color = new Color(0.35f, 0.25f, 0.18f); // Bronce pesado
                    srAnillo.color = new Color(0.85f, 0.5f, 0.15f); // Naranja cobre
                    srCanon.color = new Color(0.2f, 0.15f, 0.1f);
                    srCanon.transform.localScale = new Vector3(0.38f, 0.65f, 1f); // Cañón más ancho y largo
                    break;

                case TipoTorre.Laser:
                    srBase.color = new Color(0.25f, 0.12f, 0.35f); // Púrpura alta tecnología
                    srAnillo.color = new Color(0.85f, 0.25f, 1f); // Neón magenta
                    srCanon.color = new Color(0.15f, 0.05f, 0.25f);
                    srCanon.transform.localScale = new Vector3(0.18f, 0.7f, 1f); // Haz largo y estilizado
                    break;
            }
        }

        private Sprite CrearSpriteCirculo()
        {
            int size = 32;
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32);
        }

        private Sprite CrearSpriteCuadrado()
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] cols = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, AlcanceEfectivo);
        }

        #endregion
    }
}
