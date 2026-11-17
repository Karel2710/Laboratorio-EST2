using GrafosEST2;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Interfaz del ATACANTE (barra inferior). Funciona igual si el atacante es host o cliente:
    /// lee el estado de ControladorAtacante/GameManager y manda todas las acciones por ComandosJugador.
    ///
    /// Teclas: 1-4 enviar tropa (Shift = x5) · A/B/C elegir ruta · Q W E R T habilidades · G abrir DAG · Esc cancelar.
    /// </summary>
    public class HUDAtacante : MonoBehaviour
    {
        private static readonly TipoEnemigo[] tiposEnemigo = { TipoEnemigo.Corredor, TipoEnemigo.Infanteria, TipoEnemigo.Blindado, TipoEnemigo.Jefe };
        private static readonly string[] nombresEnemigo = { "Corredor", "Infantería", "Blindado", "Jefe" };
        private static readonly KeyCode[] teclasEnemigo = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
        private static readonly KeyCode[] teclasRuta = { KeyCode.A, KeyCode.B, KeyCode.C };
        private static readonly KeyCode[] teclasHabilidad = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T };

        private Text txtCreditos;
        private Text txtPuntos;
        private Text txtEstado;
        private Text txtAyuda;
        private readonly Button[] botonesRuta = new Button[MapaRutas.TotalRutas];
        private readonly Text[] textosRuta = new Text[MapaRutas.TotalRutas];
        private readonly Button[] botonesEnemigo = new Button[4];
        private readonly Text[] textosEnemigo = new Text[4];
        private readonly Button[] botonesHabilidad = new Button[ControladorAtacante.TotalHabilidades];
        private readonly Text[] textosHabilidad = new Text[ControladorAtacante.TotalHabilidades];
        private Text txtBotonDAG;

        // Modo "apuntar" para habilidades con objetivo en el mapa
        private int habilidadApuntando = -1;
        private SpriteRenderer cursorObjetivo;

        private ControladorAtacante Atk => ControladorAtacante.Instance;

        public static HUDAtacante Instancia { get; private set; }

        /// <summary>True mientras se elige el objetivo de una habilidad (Esc cancela el apuntado, no abre la pausa).</summary>
        public bool EstaApuntando => habilidadApuntando >= 0 || frameCancelacion == Time.frameCount;
        private int frameCancelacion = -1;

        private void Awake()
        {
            Instancia = this;
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        private void Start()
        {
            ConstruirUI();
            CrearCursorObjetivo();
            SeleccionarRuta(MapaRutas.RutaA, silencioso: true);
        }

        private void Update()
        {
            if (Atk == null || GameManager.Instance == null) return;

            LeerTeclado();
            ActualizarApuntado();
            RefrescarTextos();
        }

        #region Acciones

        private void EnviarTropa(int indice, int cantidad)
        {
            TipoEnemigo tipo = tiposEnemigo[indice];
            if (!Atk.TipoDisponible(tipo))
            {
                Avisar("🔒 Para enviar Jefes necesitas 'Titanes' en el DAG de ataque (tecla G).");
                return;
            }
            if (Atk.creditos < Atk.CostoEnemigo(tipo))
            {
                Avisar($"💸 {nombresEnemigo[indice]} cuesta {Atk.CostoEnemigo(tipo)} créditos.");
                return;
            }
            ComandosJugador.EnviarEnemigos(tipo, Atk.rutaSeleccionada, cantidad);
        }

        private void SeleccionarRuta(int ruta, bool silencioso = false)
        {
            if (MapaRutas.Instancia == null) return;
            if (!MapaRutas.Instancia.RutaDisponible(ruta))
            {
                if (!silencioso)
                {
                    Avisar(ruta == MapaRutas.RutaC
                        ? "🔒 La Ruta C (atajo) se abre con 'Atajo Táctico' en el DAG de ataque."
                        : "⚠️ Esa ruta no existe en este mapa.");
                }
                return;
            }
            Atk.rutaSeleccionada = ruta;
            MapaRutas.Instancia.ResaltarRuta(ruta);
        }

        private void ActivarHabilidad(int indice)
        {
            DefinicionHabilidadAtaque def = ControladorAtacante.Habilidades[indice];
            if (!Atk.HabilidadDisponible(indice))
            {
                Avisar($"🔒 '{def.nombre}' se desbloquea en el DAG de ataque.");
                return;
            }
            if (Atk.enfriamientos[indice] > 0f)
            {
                Avisar($"⏳ '{def.nombre}' en recarga: {Atk.enfriamientos[indice]:0} s.");
                return;
            }
            if (Atk.creditos < def.costo)
            {
                Avisar($"💸 '{def.nombre}' cuesta {def.costo} créditos.");
                return;
            }

            if (def.radio > 0f || def.apuntaATorre)
            {
                habilidadApuntando = indice;
                Avisar(def.apuntaATorre
                    ? $"🎯 {def.nombre}: haz clic sobre una torre (clic derecho o Esc para cancelar)."
                    : $"🎯 {def.nombre}: haz clic en el mapa donde están tus tropas (clic derecho o Esc para cancelar).");
                return;
            }

            ComandosJugador.UsarHabilidadAtaque(indice, Vector2.zero);
        }

        private void AlternarDAG()
        {
            if (ControladorHabilidadesUI.InstanciaAtaque != null)
            {
                ControladorHabilidadesUI.InstanciaAtaque.AlternarVisibilidadPanel();
            }
        }

        private void Avisar(string mensaje)
        {
            GameManager.Instance?.MostrarNotificacionLocal(mensaje);
        }

        #endregion

        #region Entrada

        private void LeerTeclado()
        {
            if (UIFabricaLAN.EscribiendoEnCampo()) return;

            int cantidad = EntradaLAN.ShiftMantenido ? 5 : 1;
            for (int i = 0; i < teclasEnemigo.Length; i++)
            {
                if (EntradaLAN.Presionada(teclasEnemigo[i])) EnviarTropa(i, cantidad);
            }
            for (int r = 0; r < teclasRuta.Length; r++)
            {
                if (EntradaLAN.Presionada(teclasRuta[r])) SeleccionarRuta(r);
            }
            for (int h = 0; h < teclasHabilidad.Length; h++)
            {
                if (EntradaLAN.Presionada(teclasHabilidad[h])) ActivarHabilidad(h);
            }
            if (EntradaLAN.Presionada(KeyCode.G)) AlternarDAG();
        }

        private void ActualizarApuntado()
        {
            if (habilidadApuntando < 0)
            {
                if (cursorObjetivo != null && cursorObjetivo.enabled) cursorObjetivo.enabled = false;
                return;
            }

            DefinicionHabilidadAtaque def = ControladorAtacante.Habilidades[habilidadApuntando];
            Vector2 punto = EntradaLAN.PosicionMouseMundo();

            if (cursorObjetivo != null)
            {
                cursorObjetivo.enabled = true;
                // En 2.5D el aro se dibuja apenas por encima de las baldosas del camino (-Z = arriba)
                float z = Vista25D.Activa ? -(Vista25D.ALTURA_CAMINO + 0.02f) : 0f;
                cursorObjetivo.transform.position = new Vector3(punto.x, punto.y, z);
                float diametro = def.apuntaATorre ? 1.1f : def.radio * 2f;
                cursorObjetivo.transform.localScale = Vector3.one * diametro;
                cursorObjetivo.color = EfectoPulso.ColorDe((TipoEfectoVisual)Mathf.Min(habilidadApuntando, (int)TipoEfectoVisual.Hackeo));
            }

            if (EntradaLAN.Presionada(KeyCode.Escape) || EntradaLAN.ClicDerecho)
            {
                habilidadApuntando = -1;
                frameCancelacion = Time.frameCount;
                return;
            }

            if (EntradaLAN.ClicIzquierdo && !EntradaLAN.CursorSobreUI)
            {
                int indice = habilidadApuntando;
                habilidadApuntando = -1;
                ComandosJugador.UsarHabilidadAtaque(indice, punto);
            }
        }

        private void CrearCursorObjetivo()
        {
            GameObject go = new GameObject("CursorObjetivoAtacante");
            go.transform.SetParent(transform, false);
            cursorObjetivo = go.AddComponent<SpriteRenderer>();
            cursorObjetivo.sprite = EfectoPulso.SpriteAnillo;
            cursorObjetivo.sortingOrder = 20;
            cursorObjetivo.enabled = false;
        }

        #endregion

        #region Interfaz

        private void RefrescarTextos()
        {
            GameManager gm = GameManager.Instance;
            ControladorAtacante atk = Atk;

            string ingreso = atk.ingresoPorSegundo > 0f ? $"  <color=#88FF88>(+{atk.ingresoPorSegundo:0.0}/s)</color>" : "";
            txtCreditos.text = $"💰 Créditos: <color=#FFDD44><b>{atk.creditos}</b></color>{ingreso}";
            txtPuntos.text = $"💎 Puntos de asalto: <color=#44FF88><b>{atk.jugador.puntosHabilidad}</b></color>";

            string estado;
            if (gm.estado == EstadoJuego.OleadaEnCurso && gm.ventanaEnvioAbierta)
                estado = $"<color=#88FF88>Envío ABIERTO · {gm.tiempoRondaRestante:0}s</color>";
            else if (gm.estado == EstadoJuego.OleadaEnCurso)
                estado = "<color=#AAAAAA>Envío cerrado · esperando fin de ronda</color>";
            else if (gm.estado == EstadoJuego.EsperandoInicio || gm.estado == EstadoJuego.OleadaCompletada)
                estado = $"<color=#FFDD44>Próxima ronda en {gm.cuentaRegresivaOleada:0}s</color>";
            else
                estado = "";
            int pendientes = atk.EnviosPendientes;
            txtEstado.text = estado + (pendientes > 0 ? $"  ·  En cola: {pendientes}" : "") +
                             $"  ·  Daño a la base: {atk.danioTotalBase}";

            for (int r = 0; r < MapaRutas.TotalRutas; r++)
            {
                bool disponible = MapaRutas.Instancia != null && MapaRutas.Instancia.RutaDisponible(r);
                bool sel = atk.rutaSeleccionada == r;
                string nombre = MapaRutas.NombreRuta(r);
                textosRuta[r].text = disponible ? (sel ? $"▶ {nombre}" : nombre) : $"{nombre} 🔒";
                Color c = MapaRutas.ColorRuta(r);
                botonesRuta[r].image.color = disponible ? (sel ? c : c * 0.55f) : new Color(0.25f, 0.25f, 0.28f, 0.9f);
            }

            for (int i = 0; i < tiposEnemigo.Length; i++)
            {
                TipoEnemigo tipo = tiposEnemigo[i];
                int costo = atk.CostoEnemigo(tipo);
                bool desbloqueado = atk.TipoDisponible(tipo);
                bool alcanza = atk.creditos >= costo;
                textosEnemigo[i].text = desbloqueado
                    ? $"[{i + 1}] {nombresEnemigo[i]}\n{costo} créditos"
                    : $"[{i + 1}] {nombresEnemigo[i]}\n🔒 Titanes";
                botonesEnemigo[i].image.color = (desbloqueado && alcanza) ? new Color(0.55f, 0.18f, 0.18f) : new Color(0.25f, 0.18f, 0.18f, 0.85f);
            }

            for (int h = 0; h < ControladorAtacante.TotalHabilidades; h++)
            {
                DefinicionHabilidadAtaque def = ControladorAtacante.Habilidades[h];
                bool disponible = atk.HabilidadDisponible(h);
                float cd = atk.enfriamientos[h];
                string estadoHab = !disponible ? "🔒 DAG" : (cd > 0f ? $"{cd:0}s" : (def.costo > 0 ? $"{def.costo} cr." : "listo"));
                textosHabilidad[h].text = $"[{def.tecla}] {def.nombre}\n{estadoHab}";
                bool usable = disponible && cd <= 0f && atk.creditos >= def.costo;
                botonesHabilidad[h].image.color = habilidadApuntando == h
                    ? new Color(0.9f, 0.7f, 0.2f)
                    : (usable ? new Color(0.25f, 0.3f, 0.6f) : new Color(0.2f, 0.2f, 0.28f, 0.85f));
            }

            if (txtBotonDAG != null)
            {
                txtBotonDAG.text = $"[G] DAG de Ataque\n{atk.jugador.puntosHabilidad} pts";
            }
        }

        private void ConstruirUI()
        {
            Canvas canvas = UIFabricaLAN.CrearCanvas("Canvas_HUDAtacante", 92);
            canvas.transform.SetParent(transform, false);

            RectTransform panel = UIFabricaLAN.CrearPanel(canvas.transform, "BarraAtacante", new Color(0.09f, 0.05f, 0.06f, 0.95f));
            UIFabricaLAN.Anclar(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(1240, 214));
            Outline borde = panel.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(1f, 0.4f, 0.3f, 0.6f);
            borde.effectDistance = new Vector2(0, 2);
            UIFabricaLAN.Vertical(panel, 6, new RectOffset(14, 14, 8, 8));

            // Fila 0: información
            RectTransform fila0 = UIFabricaLAN.CrearContenedor(panel, "FilaInfo");
            UIFabricaLAN.Tamano(fila0, -1, 28);
            UIFabricaLAN.Horizontal(fila0, 12, null);
            txtCreditos = UIFabricaLAN.CrearTexto(fila0, "Creditos", "", 16, Color.white);
            txtPuntos = UIFabricaLAN.CrearTexto(fila0, "Puntos", "", 16, Color.white);
            txtEstado = UIFabricaLAN.CrearTexto(fila0, "Estado", "", 15, Color.white, TextAnchor.MiddleRight);
            UIFabricaLAN.Tamano(txtCreditos, 300, -1);
            UIFabricaLAN.Tamano(txtPuntos, 250, -1);
            UIFabricaLAN.Tamano(txtEstado, 600, -1, 1f);

            // Fila 1: rutas + tropas
            RectTransform fila1 = UIFabricaLAN.CrearContenedor(panel, "FilaEnvio");
            UIFabricaLAN.Tamano(fila1, -1, 58);
            HorizontalLayoutGroup h1 = UIFabricaLAN.Horizontal(fila1, 8, null);
            h1.childForceExpandWidth = false;

            Text lblRuta = UIFabricaLAN.CrearTexto(fila1, "LblRuta", "🗺️ Ruta:", 15, new Color(1f, 0.85f, 0.7f));
            UIFabricaLAN.Tamano(lblRuta, 80, -1);
            for (int r = 0; r < MapaRutas.TotalRutas; r++)
            {
                int ruta = r;
                botonesRuta[r] = UIFabricaLAN.CrearBoton(fila1, "BtnRuta" + MapaRutas.NombreRuta(r), MapaRutas.NombreRuta(r), MapaRutas.ColorRuta(r), 18, out textosRuta[r]);
                UIFabricaLAN.Tamano(botonesRuta[r], 78, -1);
                botonesRuta[r].onClick.AddListener(() => SeleccionarRuta(ruta));
            }

            Text separador = UIFabricaLAN.CrearTexto(fila1, "Sep", "   ⚔️ Enviar:", 15, new Color(1f, 0.85f, 0.7f));
            UIFabricaLAN.Tamano(separador, 110, -1);
            for (int i = 0; i < tiposEnemigo.Length; i++)
            {
                int indice = i;
                botonesEnemigo[i] = UIFabricaLAN.CrearBoton(fila1, "BtnEnviar" + i, nombresEnemigo[i], new Color(0.55f, 0.18f, 0.18f), 14, out textosEnemigo[i]);
                UIFabricaLAN.Tamano(botonesEnemigo[i], 170, -1, 1f);
                botonesEnemigo[i].onClick.AddListener(() => EnviarTropa(indice, EntradaLAN.ShiftMantenido ? 5 : 1));
            }

            // Fila 2: habilidades + DAG
            RectTransform fila2 = UIFabricaLAN.CrearContenedor(panel, "FilaHabilidades");
            UIFabricaLAN.Tamano(fila2, -1, 58);
            HorizontalLayoutGroup h2 = UIFabricaLAN.Horizontal(fila2, 8, null);
            h2.childForceExpandWidth = false;

            for (int hb = 0; hb < ControladorAtacante.TotalHabilidades; hb++)
            {
                int indice = hb;
                botonesHabilidad[hb] = UIFabricaLAN.CrearBoton(fila2, "BtnHab" + hb, ControladorAtacante.Habilidades[hb].nombre, new Color(0.25f, 0.3f, 0.6f), 14, out textosHabilidad[hb]);
                UIFabricaLAN.Tamano(botonesHabilidad[hb], 170, -1, 1f);
                botonesHabilidad[hb].onClick.AddListener(() => ActivarHabilidad(indice));
            }

            Button btnDAG = UIFabricaLAN.CrearBoton(fila2, "BtnDAGAtaque", "DAG de Ataque", new Color(0.6f, 0.25f, 0.15f), 14, out txtBotonDAG);
            UIFabricaLAN.Tamano(btnDAG, 190, -1);
            btnDAG.onClick.AddListener(AlternarDAG);

            // Fila 3: ayuda de teclas
            txtAyuda = UIFabricaLAN.CrearTexto(panel, "Ayuda",
                "Teclas: 1-4 enviar (Shift = x5) · A/B/C ruta · Q W E R T habilidades · G DAG · Esc cancelar",
                13, new Color(0.8f, 0.75f, 0.75f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFabricaLAN.Tamano(txtAyuda, -1, 20);
        }

        #endregion
    }
}
