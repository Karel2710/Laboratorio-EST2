using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using GrafosEST2;
using TowerDefenseGameplay;

namespace TowerDefenseOnline
{
    /// <summary>
    /// Lobby multijugador: crear sala (servidor TCP), unirse por IP o código, sala simulada con IA,
    /// modos Cooperativo / Competitivo, lista de comandantes conectados, envío de mercenarios y
    /// visualizador del grafo de topología de red (estrella o P2P) con latencias en vivo.
    /// </summary>
    public class ControladorOnlineUI : MonoBehaviour
    {
        public static ControladorOnlineUI Instance { get; private set; }

        private GameObject panel;
        private Text txtEstado, txtJugadores, txtBitacora, txtTopologia, txtMarcador;
        private InputField inputDestino, inputPuerto;
        private Button btnCoop, btnComp, btnCantidad;
        private readonly Button[] btnMerc = new Button[3];
        private readonly Button[] btnMercRapido = new Button[3];
        private RectTransform lienzoRed;
        private GameObject panelMarcador;
        private GameObject filaMercRapido;
        private ModoOnline modoElegido = ModoOnline.Cooperativo;
        private int cantidadSimulada = 3;
        private float temporizadorRefresco = 0f;

        private static readonly TipoEnemigo[] tiposMerc = { TipoEnemigo.Corredor, TipoEnemigo.Blindado, TipoEnemigo.Jefe };

        public static ControladorOnlineUI Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("ControladorOnlineUI");
            return go.AddComponent<ControladorOnlineUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ConstruirUI();
            GestorRedOnline.Asegurar().OnSalaActualizada += Refrescar;
        }

        private void OnDestroy()
        {
            if (GestorRedOnline.Instance != null) GestorRedOnline.Instance.OnSalaActualizada -= Refrescar;
            if (Instance == this) Instance = null;
        }

        public void AlternarPanel()
        {
            panel.SetActive(!panel.activeSelf);
            Refrescar();
        }

        private void Update()
        {
            temporizadorRefresco -= Time.unscaledDeltaTime;
            if (temporizadorRefresco <= 0f)
            {
                temporizadorRefresco = 0.5f;
                Refrescar();
            }
        }

        #region Construcción

        private Button Boton(Transform padre, string texto, Color color, float x, float y, float w, float h, UnityEngine.Events.UnityAction accion)
        {
            Button b = UIFabrica.CrearBoton(padre, "Btn", texto, color, 13);
            UIFabrica.Colocar(b, x, y, w, h);
            b.onClick.AddListener(accion);
            return b;
        }

        private Text Titulo(Transform padre, string texto, float x, float y, float w)
        {
            Text t = UIFabrica.CrearTexto(padre, "Titulo", texto, 13, TextAnchor.MiddleLeft, Paleta.Cian, true);
            UIFabrica.Colocar(t, x, y, w, 22);
            return t;
        }

        private void ConstruirUI()
        {
            GameObject canvas = UIFabrica.CrearCanvas("Canvas_Online", 107);
            canvas.transform.SetParent(transform, false);

            RectTransform rt = UIFabrica.CrearPanel(canvas.transform, "PanelOnline", Paleta.FondoPanel, new Color(0.3f, 0.65f, 1f, 0.7f));
            UIFabrica.Anclar(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1540, 880));
            panel = rt.gameObject;
            UIFabrica.CrearEncabezado(rt, "🌐 ONLINE / MULTIJUGADOR — Sala de Comandantes (cliente-servidor por sockets TCP)", new Color(0.55f, 0.8f, 1f), () => panel.SetActive(false));

            // ── Columna izquierda: conexión ──
            Titulo(rt, "1. MODO DE JUEGO", 18, -56, 480);
            btnCoop = Boton(rt, "🛡️ Cooperativo\n2-4 defienden juntos", new Color(0.12f, 0.5f, 0.35f), 18, -82, 236, 52, () => ElegirModo(ModoOnline.Cooperativo));
            btnComp = Boton(rt, "⚔️ Competitivo\nMercenarios + puntaje", new Color(0.3f, 0.3f, 0.35f), 262, -82, 236, 52, () => ElegirModo(ModoOnline.Competitivo));

            Titulo(rt, "2. CREAR SALA (esta PC será el servidor)", 18, -146, 480);
            Text lp = UIFabrica.CrearTexto(rt, "LblPuerto", "Puerto:", 13, TextAnchor.MiddleLeft, Color.white);
            UIFabrica.Colocar(lp, 18, -172, 60, 36);
            inputPuerto = UIFabrica.CrearInput(rt, "InputPuerto", "7777");
            inputPuerto.text = GestorRedOnline.PUERTO_POR_DEFECTO.ToString();
            inputPuerto.contentType = InputField.ContentType.IntegerNumber;
            UIFabrica.Colocar(inputPuerto, 80, -172, 90, 36);
            Boton(rt, "🏠 Crear Sala (Host TCP)", new Color(0.12f, 0.42f, 0.7f), 180, -172, 318, 36, CrearSala);

            Titulo(rt, "3. UNIRSE (IP, IP:puerto o código de sala)", 18, -220, 480);
            inputDestino = UIFabrica.CrearInput(rt, "InputDestino", "Ej: 192.168.1.10:7777  ·  localhost  ·  código");
            UIFabrica.Colocar(inputDestino, 18, -246, 330, 36);
            Boton(rt, "🔌 Unirse", new Color(0.5f, 0.35f, 0.7f), 356, -246, 142, 36, Unirse);

            Titulo(rt, "4. SALA SIMULADA (sin red: comandantes del BST con IA)", 18, -294, 480);
            btnCantidad = Boton(rt, "Jugadores: 3", new Color(0.25f, 0.3f, 0.4f), 18, -320, 150, 36, () =>
            {
                cantidadSimulada = cantidadSimulada >= GestorRedOnline.MAX_JUGADORES ? 2 : cantidadSimulada + 1;
                UIFabrica.Rotular(btnCantidad, "Jugadores: " + cantidadSimulada);
            });
            Boton(rt, "🤖 Crear Sala Simulada", new Color(0.6f, 0.42f, 0.1f), 176, -320, 322, 36,
                () => GestorRedOnline.Asegurar().CrearSalaSimulada(cantidadSimulada, modoElegido));

            Boton(rt, "⏏ Salir de la sala", new Color(0.55f, 0.2f, 0.2f), 18, -366, 480, 34, () => GestorRedOnline.Asegurar().Salir());

            RectTransform fondoEstado = UIFabrica.CrearPanel(rt, "FondoEstado", Paleta.FondoOscuro);
            UIFabrica.Colocar(fondoEstado, 18, -410, 480, 96);
            txtEstado = UIFabrica.CrearTexto(fondoEstado, "Estado", "", 13, TextAnchor.UpperLeft, Color.white);
            UIFabrica.Estirar(txtEstado, 10, 6, 10, 6);

            Titulo(rt, "BITÁCORA DE PAQUETES", 18, -514, 480);
            UIFabrica.CrearScroll(rt, "ScrollBitacora", Paleta.FondoOscuro, out RectTransform contBit);
            UIFabrica.Colocar(contBit.parent.parent, 18, -540, 480, 324);
            txtBitacora = UIFabrica.CrearTexto(contBit, "Bitacora", "", 12, TextAnchor.UpperLeft, new Color(0.75f, 0.92f, 0.85f));

            // ── Columna central: comandantes y mercenarios ──
            Titulo(rt, "COMANDANTES EN LA SALA (árbol BST ordenado por ID)", 516, -56, 500);
            RectTransform fondoJug = UIFabrica.CrearPanel(rt, "FondoJugadores", Paleta.FondoOscuro);
            UIFabrica.Colocar(fondoJug, 516, -82, 500, 420);
            txtJugadores = UIFabrica.CrearTexto(fondoJug, "Jugadores", "", 13, TextAnchor.UpperLeft, Color.white);
            UIFabrica.Estirar(txtJugadores, 12, 8, 12, 8);

            Titulo(rt, "ENVÍO DE MERCENARIOS (modo Competitivo)", 516, -514, 500);
            Text ayudaMerc = UIFabrica.CrearTexto(rt, "AyudaMerc",
                "Paga oro para enviar enemigos extra a la defensa rival. Cada envío aumenta tu ingreso de oro por oleada.",
                12, TextAnchor.UpperLeft, new Color(0.7f, 0.78f, 0.88f));
            UIFabrica.Colocar(ayudaMerc, 516, -538, 500, 36);
            for (int i = 0; i < 3; i++)
            {
                TipoEnemigo tipo = tiposMerc[i];
                int costo = GestorRedOnline.CostoMercenarios(tipo, out int cantidad, out int bono);
                btnMerc[i] = Boton(rt, $"{cantidad}× {SpritesEnemigos.Nombre(tipo)}\n{costo} Oro  ·  +{bono} ingreso", new Color(0.55f, 0.22f, 0.22f),
                    516 + i * 168, -580, 160, 60, () => GestorRedOnline.Asegurar().EnviarMercenarios(tipo));
            }
            Text nota = UIFabrica.CrearTexto(rt, "Nota",
                "<b>Protocolo:</b> paquetes JSON de una línea — CONECTAR_JUGADOR · CONSTRUIR_TORRE · MEJORAR_TORRE · VENDER_TORRE · USAR_HABILIDAD_DAG · ENVIAR_MERCENARIOS_PVP · SINCRONIZAR_ESTADO_OLEADA · PING/PONG.\n" +
                "Se sincronizan comandos, no enemigos: la semilla de la oleada hace que todas las máquinas generen la misma composición.",
                12, TextAnchor.UpperLeft, new Color(0.6f, 0.7f, 0.82f));
            UIFabrica.Colocar(nota, 516, -656, 500, 200);

            // ── Columna derecha: grafo de topología ──
            Titulo(rt, "GRAFO DE TOPOLOGÍA DE RED (vértices = comandantes, aristas = latencia)", 1034, -56, 490);
            RectTransform fondoRed = UIFabrica.CrearPanel(rt, "LienzoRed", new Color(0.03f, 0.05f, 0.08f, 1f), new Color(0.3f, 0.45f, 0.65f, 0.6f));
            UIFabrica.Colocar(fondoRed, 1034, -82, 490, 460);
            fondoRed.gameObject.AddComponent<RectMask2D>();
            lienzoRed = fondoRed;

            RectTransform fondoTop = UIFabrica.CrearPanel(rt, "FondoTopologia", Paleta.FondoOscuro);
            UIFabrica.Colocar(fondoTop, 1034, -552, 490, 312);
            txtTopologia = UIFabrica.CrearTexto(fondoTop, "Topologia", "", 12, TextAnchor.UpperLeft, new Color(0.88f, 0.92f, 0.98f));
            UIFabrica.Estirar(txtTopologia, 10, 6, 10, 6);

            panel.SetActive(false);

            // ── Marcador compacto en pantalla (visible durante la partida online) ──
            RectTransform marcador = UIFabrica.CrearPanel(canvas.transform, "MarcadorOnline", new Color(0.04f, 0.07f, 0.11f, 0.9f), new Color(0.3f, 0.65f, 1f, 0.55f));
            UIFabrica.Anclar(marcador, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -160), new Vector2(330, 196));
            marcador.GetComponent<Image>().raycastTarget = false;
            txtMarcador = UIFabrica.CrearTexto(marcador, "Texto", "", 12, TextAnchor.UpperLeft, Color.white);
            UIFabrica.Colocar(txtMarcador, 10, -6, 312, 130);

            RectTransform fila = UIFabrica.CrearContenedor(marcador, "MercRapido");
            UIFabrica.Colocar(fila, 8, -146, 314, 44);
            for (int i = 0; i < 3; i++)
            {
                TipoEnemigo tipo = tiposMerc[i];
                int costo = GestorRedOnline.CostoMercenarios(tipo, out int cantidad, out _);
                btnMercRapido[i] = Boton(fila, $"⚔ {cantidad}× {SpritesEnemigos.Nombre(tipo)}\n{costo} Oro", new Color(0.55f, 0.22f, 0.22f),
                    i * 106, 0, 100, 44, () => GestorRedOnline.Asegurar().EnviarMercenarios(tipo));
                UIFabrica.TextoDe(btnMercRapido[i]).fontSize = 11;
            }
            filaMercRapido = fila.gameObject;
            panelMarcador = marcador.gameObject;
            panelMarcador.SetActive(false);
        }

        #endregion

        #region Acciones

        private void ElegirModo(ModoOnline m)
        {
            GestorRedOnline red = GestorRedOnline.Asegurar();
            if (red.Conectado)
            {
                if (GameManager.Instance != null) GameManager.Instance.MostrarNotificacion("El modo se elige antes de crear la sala. Sal de la sala para cambiarlo.");
                return;
            }
            modoElegido = m;
            Refrescar();
        }

        private void CrearSala()
        {
            int puerto = GestorRedOnline.PUERTO_POR_DEFECTO;
            if (inputPuerto != null && (!int.TryParse(inputPuerto.text.Trim(), out puerto) || puerto < 1024 || puerto > 65535))
            {
                puerto = GestorRedOnline.PUERTO_POR_DEFECTO;
                inputPuerto.text = puerto.ToString();
            }
            GestorRedOnline.Asegurar().CrearSalaTCP(puerto, modoElegido);
        }

        private void Unirse()
        {
            GestorRedOnline.Asegurar().UnirseSalaTCP(inputDestino != null ? inputDestino.text : "");
        }

        #endregion

        #region Refresco en Vivo

        private static string NombreEstado(EstadoConexion e)
        {
            switch (e)
            {
                case EstadoConexion.AnfitrionTCP: return "<color=#44ff88>ANFITRIÓN (servidor TCP)</color>";
                case EstadoConexion.ClienteTCP: return "<color=#44ff88>CLIENTE (conectado por TCP)</color>";
                case EstadoConexion.SalaSimulada: return "<color=#ffd166>SALA SIMULADA (canal en memoria)</color>";
                case EstadoConexion.Conectando: return "<color=#ffd166>Conectando...</color>";
                default: return "<color=#94a3b8>Sin conexión</color>";
            }
        }

        private void Refrescar()
        {
            GestorRedOnline red = GestorRedOnline.Instance;
            if (red == null || txtEstado == null) return;

            ModoOnline modoVisible = red.Conectado ? red.modo : modoElegido;
            UIFabrica.Colorear(btnCoop, modoVisible == ModoOnline.Cooperativo ? new Color(0.12f, 0.6f, 0.4f) : new Color(0.25f, 0.28f, 0.33f));
            UIFabrica.Colorear(btnComp, modoVisible == ModoOnline.Competitivo ? new Color(0.7f, 0.25f, 0.25f) : new Color(0.25f, 0.28f, 0.33f));

            List<JugadorSala> lista = red.Integrantes();
            bool competitivo = red.Conectado && red.modo == ModoOnline.Competitivo;
            for (int i = 0; i < 3; i++)
            {
                btnMerc[i].interactable = competitivo;
                btnMercRapido[i].interactable = competitivo;
            }

            // Marcador en pantalla
            panelMarcador.SetActive(red.Conectado);
            if (red.Conectado)
            {
                filaMercRapido.SetActive(competitivo);
                StringBuilder sm = new StringBuilder();
                sm.AppendLine($"<b>🌐 {(red.modo == ModoOnline.Cooperativo ? "COOPERATIVO" : "COMPETITIVO")}</b>  <color=#94a3b8>{red.codigoSala}</color>");
                List<JugadorSala> orden = new List<JugadorSala>(lista);
                orden.Sort((a, b) => b.puntaje.CompareTo(a.puntaje));
                for (int i = 0; i < orden.Count; i++)
                {
                    JugadorSala js = orden[i];
                    string tachado = js.eliminado ? " <color=#ff6666>✕</color>" : "";
                    sm.AppendLine($"<color={Paleta.Hex(js.color)}>●</color> {(js.esLocal ? "<b>" : "")}{js.perfil.nombre}{(js.esLocal ? "</b>" : "")}{tachado}  <color=#ffd166>{js.puntaje} pts</color>  <color=#94a3b8>{js.ping:0} ms</color>");
                }
                txtMarcador.text = sm.ToString();
            }

            if (!panel.activeSelf) return;

            string ip = GestorRedOnline.ObtenerIPLocal();
            txtEstado.text =
                $"Estado: {NombreEstado(red.estado)}\n" +
                (red.Conectado ? $"Código de sala: <color=#ffd166><b>{red.codigoSala}</b></color>   Modo: <b>{red.modo}</b>\n" : "") +
                $"IP local: <b>{ip}</b>   Puerto: <b>{red.puerto}</b>\n" +
                $"Paquetes enviados: <b>{red.paquetesEnviados}</b>   recibidos: <b>{red.paquetesRecibidos}</b>";

            // Lista de comandantes (recorrido in-orden del BST de la sala)
            StringBuilder sb = new StringBuilder();
            if (lista.Count == 0)
            {
                sb.Append("<color=#94a3b8><i>No estás en ninguna sala.\n\nCrea una sala, únete a una o inicia una Sala Simulada para probar el modo online con una sola PC.</i></color>");
            }
            for (int i = 0; i < lista.Count; i++)
            {
                JugadorSala js = lista[i];
                string rol = js.esAnfitrion ? "Anfitrión" : (js.esBot ? "IA" : "Cliente");
                string tu = js.esLocal ? "  <color=#44ff88><b>(TÚ)</b></color>" : "";
                string estadoJ = js.eliminado ? "  <color=#ff6666><b>ELIMINADO</b></color>" : "";
                sb.AppendLine($"<color={Paleta.Hex(js.color)}><b>● {js.perfil.nombre}</b></color>  <color=#94a3b8>ID {js.perfil.Id} · {rol}</color>{tu}{estadoJ}");
                sb.AppendLine($"     🎖 {js.perfil.NombreClaseActual} · Nvl {js.perfil.nivel}   📶 Ping: <b>{js.ping:0} ms</b>");
                sb.AppendLine($"     💰 Oro: <color=#ffd166>{js.oro}</color>   🎯 Puntaje: <color=#ffb347><b>{js.puntaje}</b></color>   🏰 Base: {js.vidaBase:0}   📦 {js.paquetes} paq.");
                sb.AppendLine();
            }
            txtJugadores.text = sb.ToString();

            StringBuilder bit = new StringBuilder();
            for (int i = Mathf.Max(0, red.bitacora.Count - 40); i < red.bitacora.Count; i++) bit.AppendLine(red.bitacora[i]);
            txtBitacora.text = bit.Length > 0 ? bit.ToString() : "<color=#94a3b8><i>Aquí aparecerán los paquetes de red enviados y recibidos.</i></color>";

            DibujarTopologia(red, lista);
        }

        /// <summary>Dibuja el grafo de red: vértices en círculo y aristas etiquetadas con la latencia.</summary>
        private void DibujarTopologia(GestorRedOnline red, List<JugadorSala> lista)
        {
            for (int i = lienzoRed.childCount - 1; i >= 0; i--) Destroy(lienzoRed.GetChild(i).gameObject);

            if (lista.Count == 0)
            {
                Text vacio = UIFabrica.CrearTexto(lienzoRed, "Vacio", "Sin sesión activa", 15, TextAnchor.MiddleCenter, new Color(0.5f, 0.58f, 0.68f));
                UIFabrica.Estirar(vacio);
                txtTopologia.text = "";
                return;
            }

            Grafo<JugadorSala> g = red.ConstruirGrafoTopologia();
            Vector2 centro = new Vector2(245, 230);
            Dictionary<string, Vector2> pos = new Dictionary<string, Vector2>();
            bool estrella = red.estado != EstadoConexion.SalaSimulada;
            int perifericos = 0;
            for (int i = 0; i < lista.Count; i++) if (!(estrella && lista[i].esAnfitrion)) perifericos++;
            int k = 0;
            for (int i = 0; i < lista.Count; i++)
            {
                string id = lista[i].perfil.Id.ToString();
                if (estrella && lista[i].esAnfitrion && lista.Count > 1) pos[id] = centro;
                else
                {
                    float ang = Mathf.PI * 0.5f + k * Mathf.PI * 2f / Mathf.Max(1, perifericos);
                    pos[id] = lista.Count == 1 ? centro : centro + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 160f;
                    k++;
                }
            }

            float sumaLatencia = 0f;
            List<Grafo<JugadorSala>.AristaSimple> aristas = g.ObtenerAristas();
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < aristas.Count; i++)
            {
                Vector2 a = pos[aristas[i].U], b = pos[aristas[i].V];
                float ms = aristas[i].Peso;
                sumaLatencia += ms;
                // Verde = baja latencia, ámbar = media, rojo = alta
                Color c = ms < 60f ? Paleta.Positivo : (ms < 110f ? Paleta.Aviso : Paleta.Negativo);
                c.a = 0.55f + 0.35f * pulso;
                UIFabrica.CrearLineaUI(lienzoRed, a, b, c, 3f);

                Text t = UIFabrica.CrearTexto(lienzoRed, "Lat", $"{ms:0} ms", 12, TextAnchor.MiddleCenter, Color.white, true);
                RectTransform rtT = t.rectTransform;
                rtT.anchorMin = rtT.anchorMax = Vector2.zero;
                rtT.pivot = new Vector2(0.5f, 0.5f);
                Vector2 dir = (b - a).normalized;
                rtT.anchoredPosition = Vector2.Lerp(a, b, 0.5f) + new Vector2(-dir.y, dir.x) * 13f;
                rtT.sizeDelta = new Vector2(70, 18);
            }

            float anchoTotal = 0f;
            for (int i = 0; i < lista.Count; i++)
            {
                JugadorSala js = lista[i];
                anchoTotal += js.kbps;
                Vector2 p = pos[js.perfil.Id.ToString()];

                Image borde = UIFabrica.CrearImagen(lienzoRed, "Borde", GraficosProc.Circulo, js.esLocal ? Color.white : new Color(0.02f, 0.03f, 0.05f));
                RectTransform rb = borde.rectTransform;
                rb.anchorMin = rb.anchorMax = Vector2.zero;
                rb.pivot = new Vector2(0.5f, 0.5f);
                rb.anchoredPosition = p;
                rb.sizeDelta = Vector2.one * 62f;

                Image nodo = UIFabrica.CrearImagen(lienzoRed, "Nodo", GraficosProc.Circulo, js.eliminado ? new Color(0.3f, 0.3f, 0.3f) : js.color * 0.75f);
                RectTransform rn = nodo.rectTransform;
                rn.anchorMin = rn.anchorMax = Vector2.zero;
                rn.pivot = new Vector2(0.5f, 0.5f);
                rn.anchoredPosition = p;
                rn.sizeDelta = Vector2.one * 54f;

                Text idT = UIFabrica.CrearTexto(nodo.transform, "Id", (js.esAnfitrion ? "★\n" : "") + js.perfil.Id, 13, TextAnchor.MiddleCenter, Color.white, true);
                UIFabrica.Estirar(idT);

                Text nombre = UIFabrica.CrearTexto(lienzoRed, "Nombre", $"{js.perfil.nombre}\n<color=#94a3b8>{js.kbps:0.0} kbps</color>", 11, TextAnchor.UpperCenter, Color.white);
                RectTransform rnm = nombre.rectTransform;
                rnm.anchorMin = rnm.anchorMax = Vector2.zero;
                rnm.pivot = new Vector2(0.5f, 1f);
                rnm.anchoredPosition = p + new Vector2(0, -32f);
                rnm.sizeDelta = new Vector2(120, 30);
            }

            g.KruskalMST(out float pesoMST, out _);
            txtTopologia.text =
                $"<color=#66ddff><b>{(estrella ? "TOPOLOGÍA EN ESTRELLA (cliente-servidor)" : "TOPOLOGÍA EN MALLA (P2P simulado)")}</b></color>\n" +
                $"Vértices |V| = <b>{g.CantidadVertices}</b>   Aristas |E| = <b>{aristas.Count}</b>\n" +
                $"Latencia media por enlace: <b>{(aristas.Count > 0 ? sumaLatencia / aristas.Count : 0f):0} ms</b>\n" +
                $"Árbol de expansión mínima de la red (Kruskal): <b>{pesoMST:0} ms</b>\n" +
                $"Ancho de banda total: <b>{anchoTotal:0.0} kbps</b>\n\n" +
                "<b>Lista de adyacencia</b> (ID → vecino(ms)):\n" + g.ListaAdyacenciaToString() +
                (estrella
                    ? "\n<color=#94a3b8>★ = anfitrión. Todo paquete pasa por el servidor, que lo valida y lo retransmite.</color>"
                    : "\n<color=#94a3b8>Cada comandante intercambia paquetes con todos los demás.</color>");
        }

        #endregion
    }
}
