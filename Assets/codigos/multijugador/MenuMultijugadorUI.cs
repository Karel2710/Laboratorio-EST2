using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Sala del multijugador LAN en el menú principal.
    ///  1) Cada jugador elige su comandante del árbol BST (o crea uno).
    ///  2) Uno crea la partida eligiendo su rol (atacante o defensor) → queda como host.
    ///  3) El otro la ve en la lista (búsqueda automática) o escribe la IP del host → cliente con el rol restante.
    ///  4) El host pulsa "Comenzar" y ambos cargan la escena de juego.
    /// </summary>
    public class MenuMultijugadorUI : MonoBehaviour
    {
        private enum EstadoSala
        {
            Inactivo,
            Hospedando,
            Conectando,
            EnSala
        }

        private const string EscenaJuego = "SampleScene";

        private EstadoSala estado = EstadoSala.Inactivo;
        private RedLAN red;
        private DescubrimientoLAN descubrimiento;

        private Jugador comandante;
        private RolJugador rolHost = RolJugador.Atacante;
        private RolJugador rolAsignadoCliente = RolJugador.Defensor;
        private bool soyHost = false;
        private string nombreRival = "";
        private int idRival = 0;

        /// <summary>Si es true al cargar el menú principal, abre directamente la sala de multijugador LAN.</summary>
        public static bool AbrirSalaDirectamenteAlCargar = false;

        // UI
        private GameObject panel;
        private GameObject panelInicio;
        private GameObject modalLeaderboard;
        private RectTransform listaComandantes;
        private InputField inputNuevoComandante;
        private Text txtComandante;
        private Button btnRolAtacante;
        private Button btnRolDefensor;
        private Button btnCrear;
        private Text txtIPs;
        private RectTransform listaPartidas;
        private Text txtSinPartidas;
        private InputField inputIP;
        private Button btnConectar;
        private Text txtEstado;
        private Button btnComenzar;
        private Button btnCancelar;

        private void Start()
        {
            descubrimiento = gameObject.AddComponent<DescubrimientoLAN>();
            descubrimiento.AlCambiarLista += RefrescarPartidas;

            ConstruirPantallaInicio();
            ConstruirPanel();
            panel.SetActive(false);

            if (AbrirSalaDirectamenteAlCargar)
            {
                AbrirSalaDirectamenteAlCargar = false;
                AbrirPanel();
            }
        }

        private void OnDestroy()
        {
            if (descubrimiento != null) descubrimiento.AlCambiarLista -= RefrescarPartidas;
            DesconectarEventosRed();
        }

        #region Pantalla de Inicio / Menú Principal

        private void ConstruirPantallaInicio()
        {
            // Ocultar botones heredados en la escena para presentar un menú unificado y limpio
            Button[] botonesEscena = FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var b in botonesEscena)
            {
                if (b.transform.root != transform)
                {
                    b.gameObject.SetActive(false);
                }
            }

            Canvas canvas = UIFabrica.CrearCanvas("Canvas_PantallaInicio", 20);
            canvas.transform.SetParent(transform, false);
            panelInicio = canvas.gameObject;

            RectTransform fondo = UIFabrica.CrearPanel(canvas.transform, "FondoOscuro", new Color(0.04f, 0.06f, 0.10f, 0.90f));
            UIFabrica.Estirar(fondo);

            RectTransform caja = UIFabrica.CrearPanel(fondo, "CajaInicio", new Color(0.08f, 0.11f, 0.16f, 0.98f));
            UIFabrica.Anclar(caja, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 710));
            Outline borde = caja.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(0.25f, 0.55f, 0.9f, 0.75f);
            borde.effectDistance = new Vector2(2, -2);

            VerticalLayoutGroup v = UIFabrica.Vertical(caja, 14, new RectOffset(36, 36, 26, 26));
            v.childForceExpandHeight = false;

            // Encabezado
            Text txtTitulo = UIFabrica.CrearTexto(caja, "Titulo", "🏰 TOWER DEFENSE", 40, new Color(1f, 0.86f, 0.35f), TextAnchor.MiddleCenter);
            UIFabrica.Tamano(txtTitulo, -1, 48);

            Text txtSubtitulo = UIFabrica.CrearTexto(caja, "Subtitulo", "ESTRUCTURAS DE DATOS II · LABORATORIO", 16, new Color(0.45f, 0.82f, 1f), TextAnchor.MiddleCenter);
            UIFabrica.Tamano(txtSubtitulo, -1, 24);

            RectTransform separador = UIFabrica.CrearPanel(caja, "Separador", new Color(0.25f, 0.35f, 0.50f, 0.7f));
            UIFabrica.Tamano(separador, -1, 2);

            // Tarjeta 1: Modo Historia (Un Jugador)
            RectTransform cardHistoria = UIFabrica.CrearPanel(caja, "CardHistoria", new Color(0.05f, 0.08f, 0.13f, 0.85f));
            UIFabrica.Tamano(cardHistoria, -1, 142);
            Outline outH = cardHistoria.gameObject.AddComponent<Outline>();
            outH.effectColor = new Color(0.2f, 0.45f, 0.7f, 0.5f);
            outH.effectDistance = new Vector2(1, -1);
            VerticalLayoutGroup vH = UIFabrica.Vertical(cardHistoria, 8, new RectOffset(16, 16, 12, 12));
            vH.childForceExpandHeight = false;

            Button btnHistoria = UIFabrica.CrearBoton(cardHistoria, "BtnHistoria", "🛡️ MODO HISTORIA (UN JUGADOR)", new Color(0.16f, 0.44f, 0.72f), 21);
            UIFabrica.Tamano(btnHistoria, -1, 56);
            btnHistoria.onClick.AddListener(() =>
            {
                ModoJuego.IniciadoDesdeMenu = true;
                ModoJuego.ConfigurarUnJugador();
                SceneManager.LoadScene(EscenaJuego);
            });

            Text descHistoria = UIFabrica.CrearTexto(cardHistoria, "DescHistoria",
                "Defiende la base en solitario contra 8 oleadas continuas. Gestiona tu oro, coloca defensas y potencia tus torres con el MST y el árbol de habilidades.",
                14, new Color(0.78f, 0.88f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFabrica.Tamano(descHistoria, -1, 44);

            // Tarjeta 2: Modo Multijugador (LAN 1v1)
            RectTransform cardMulti = UIFabrica.CrearPanel(caja, "CardMulti", new Color(0.05f, 0.08f, 0.13f, 0.85f));
            UIFabrica.Tamano(cardMulti, -1, 142);
            Outline outM = cardMulti.gameObject.AddComponent<Outline>();
            outM.effectColor = new Color(0.2f, 0.6f, 0.4f, 0.5f);
            outM.effectDistance = new Vector2(1, -1);
            VerticalLayoutGroup vM = UIFabrica.Vertical(cardMulti, 8, new RectOffset(16, 16, 12, 12));
            vM.childForceExpandHeight = false;

            Button btnMulti = UIFabrica.CrearBoton(cardMulti, "BtnMultiplayer", "🌐 MODO MULTIJUGADOR (LAN 1v1)", new Color(0.18f, 0.58f, 0.38f), 21);
            UIFabrica.Tamano(btnMulti, -1, 56);
            btnMulti.onClick.AddListener(AbrirPanel);

            Text descMulti = UIFabrica.CrearTexto(cardMulti, "DescMulti",
                "Enfréntate en red local: juega como Atacante (rutas Dijkstra y tropas) o como Defensor (torres y sobrecarga de red).",
                14, new Color(0.78f, 1f, 0.88f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFabrica.Tamano(descMulti, -1, 44);

            // Fila de botones secundarios
            RectTransform filaSecundaria = UIFabrica.CrearContenedor(caja, "FilaSecundaria");
            UIFabrica.Tamano(filaSecundaria, -1, 50);
            HorizontalLayoutGroup hSec = UIFabrica.Horizontal(filaSecundaria, 12, null);
            hSec.childForceExpandWidth = false;
            hSec.childAlignment = TextAnchor.MiddleCenter;

            Button btnCmds = UIFabrica.CrearBoton(filaSecundaria, "BtnComandantes", "👥 Comandantes (BST)", new Color(0.18f, 0.28f, 0.42f), 15);
            UIFabrica.Tamano(btnCmds, 240, -1);
            btnCmds.onClick.AddListener(AbrirPanel);

            Button btnLead = UIFabrica.CrearBoton(filaSecundaria, "BtnLeaderboard", "🏆 Leaderboard", new Color(0.18f, 0.28f, 0.42f), 15);
            UIFabrica.Tamano(btnLead, 240, -1);
            btnLead.onClick.AddListener(MostrarModalLeaderboard);

            Button btnSalir = UIFabrica.CrearBoton(filaSecundaria, "BtnSalir", "❌ Salir", new Color(0.55f, 0.2f, 0.2f), 15);
            UIFabrica.Tamano(btnSalir, 160, -1);
            btnSalir.onClick.AddListener(() => Application.Quit());

            // Tip pie de página
            Text tip = UIFabrica.CrearTexto(caja, "Tip", "💡 Ambos jugadores deben estar conectados a la misma red Wi-Fi o cable LAN para jugar multijugador.", 13, new Color(0.65f, 0.70f, 0.80f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFabrica.Tamano(tip, -1, 24);
        }

        private void MostrarModalLeaderboard()
        {
            if (modalLeaderboard != null)
            {
                Destroy(modalLeaderboard);
            }

            Canvas canvas = UIFabrica.CrearCanvas("Modal_Leaderboard", 210);
            modalLeaderboard = canvas.gameObject;

            RectTransform oscurecer = UIFabrica.CrearPanel(canvas.transform, "Oscurecer", new Color(0f, 0f, 0f, 0.75f));
            UIFabrica.Estirar(oscurecer);

            RectTransform caja = UIFabrica.CrearPanel(oscurecer, "Caja", new Color(0.08f, 0.11f, 0.17f, 0.98f));
            UIFabrica.Anclar(caja, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 620));
            Outline borde = caja.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(0.35f, 0.65f, 1f, 0.8f);
            borde.effectDistance = new Vector2(2, -2);

            VerticalLayoutGroup v = UIFabrica.Vertical(caja, 12, new RectOffset(24, 24, 20, 20));
            v.childForceExpandHeight = false;

            Text titulo = UIFabrica.CrearTexto(caja, "Titulo", "🏆 TABLA DE CLASIFICACIÓN (LEADERBOARD)", 22, new Color(1f, 0.88f, 0.35f), TextAnchor.MiddleCenter);
            UIFabrica.Tamano(titulo, -1, 40);

            Text subtitulo = UIFabrica.CrearTexto(caja, "Subtitulo", "Top 10 mejores puntajes históricos guardados en disco:", 14, new Color(0.75f, 0.85f, 0.95f), TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFabrica.Tamano(subtitulo, -1, 24);

            RectTransform lista = UIFabrica.CrearContenedor(caja, "Lista");
            UIFabrica.Tamano(lista, -1, 400);
            VerticalLayoutGroup vLista = UIFabrica.Vertical(lista, 6, null);
            vLista.childForceExpandHeight = false;

            List<EntradaLeaderboard> top = LeaderboardJSON.CargarTop();
            if (top == null || top.Count == 0)
            {
                Text vacio = UIFabrica.CrearTexto(lista, "Vacio", "No hay puntajes registrados aún.\n¡Juega y derrota enemigos para entrar al top!", 15, new Color(0.7f, 0.75f, 0.8f), TextAnchor.MiddleCenter, FontStyle.Italic);
                UIFabrica.Tamano(vacio, -1, 120);
            }
            else
            {
                for (int i = 0; i < top.Count && i < 10; i++)
                {
                    EntradaLeaderboard entrada = top[i];
                    string medalla = i == 0 ? "🥇" : (i == 1 ? "🥈" : (i == 2 ? "🥉" : $"#{i + 1}"));
                    RectTransform fila = UIFabrica.CrearPanel(lista, $"Fila_{i}", new Color(0.12f, 0.16f, 0.24f, 0.9f));
                    UIFabrica.Tamano(fila, -1, 34);
                    HorizontalLayoutGroup hFila = UIFabrica.Horizontal(fila, 8, new RectOffset(12, 12, 0, 0));
                    hFila.childForceExpandWidth = false;

                    Text txtPos = UIFabrica.CrearTexto(fila, "Pos", medalla, 16, Color.white);
                    UIFabrica.Tamano(txtPos, 50, -1);

                    Text txtNom = UIFabrica.CrearTexto(fila, "Nom", entrada.nombre, 15, new Color(0.9f, 0.95f, 1f));
                    UIFabrica.Tamano(txtNom, 320, -1, 1f);

                    Text txtPts = UIFabrica.CrearTexto(fila, "Pts", $"{entrada.puntaje} pts", 15, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleRight);
                    UIFabrica.Tamano(txtPts, 120, -1);
                }
            }

            Button btnCerrar = UIFabrica.CrearBoton(caja, "BtnCerrar", "✕ Cerrar", new Color(0.55f, 0.2f, 0.2f), 18);
            UIFabrica.Tamano(btnCerrar, 180, 44);
            btnCerrar.onClick.AddListener(() =>
            {
                if (modalLeaderboard != null) Destroy(modalLeaderboard);
            });
        }

        private void AbrirPanel()
        {
            if (panelInicio != null) panelInicio.SetActive(false);
            panel.SetActive(true);
            RefrescarComandantes();
            if (estado == EstadoSala.Inactivo) descubrimiento.IniciarBusqueda();
            RefrescarPartidas();
            RefrescarControles();
            if (estado == EstadoSala.Inactivo) MostrarEstado("Elige tu comandante y luego crea una partida o únete a una.");
        }

        private void CerrarPanel()
        {
            Cancelar();
            descubrimiento.DetenerBusqueda();
            panel.SetActive(false);
            if (panelInicio != null) panelInicio.SetActive(true);
        }

        #endregion

        #region Comandantes (BST)

        private void RefrescarComandantes()
        {
            foreach (Transform hijo in listaComandantes) Destroy(hijo.gameObject);

            List<Jugador> lista = RepositorioComandantes.Listar();
            if (comandante == null && lista.Count > 0) comandante = lista[0];

            int mostrados = 0;
            foreach (Jugador j in lista)
            {
                if (mostrados >= 8) break;
                mostrados++;
                Jugador capturado = j;
                bool sel = comandante != null && comandante.Id == j.Id;
                Button b = UIFabrica.CrearBoton(listaComandantes, "Cmd_" + j.Id,
                    $"{(sel ? "▶ " : "")}{j.nombre}  ·  ID {j.Id}  ·  {j.victoriasMultijugador}V/{j.derrotasMultijugador}D",
                    sel ? new Color(0.2f, 0.55f, 0.35f) : new Color(0.15f, 0.2f, 0.3f), 15);
                UIFabrica.Tamano(b, -1, 38);
                b.onClick.AddListener(() =>
                {
                    comandante = capturado;
                    RefrescarComandantes();
                });
            }

            if (lista.Count == 0)
            {
                Text vacio = UIFabrica.CrearTexto(listaComandantes, "SinComandantes",
                    "No hay comandantes en el BST de este PC. Escribe un nombre y pulsa Crear.", 15, new Color(0.8f, 0.8f, 0.85f),
                    TextAnchor.MiddleLeft, FontStyle.Italic);
                UIFabrica.Tamano(vacio, -1, 60);
            }

            txtComandante.text = comandante != null
                ? $"Jugarás como: <color=#66DDFF><b>{comandante.nombre}</b></color> (ID {comandante.Id})"
                : "<color=#FFAA66>Elige o crea un comandante.</color>";
        }

        private void CrearComandante()
        {
            string nombre = inputNuevoComandante.text.Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                MostrarEstado("Escribe un nombre para el comandante nuevo.");
                return;
            }
            comandante = RepositorioComandantes.Registrar(nombre);
            inputNuevoComandante.text = "";
            RefrescarComandantes();
            MostrarEstado($"Comandante '{comandante.nombre}' insertado en el BST con ID {comandante.Id}.");
        }

        #endregion

        #region Crear / unirse

        private bool ValidarComandante()
        {
            if (comandante != null) return true;
            MostrarEstado("<color=#FFAA66>Primero elige o crea tu comandante.</color>");
            return false;
        }

        private void PrepararRed()
        {
            red = RedLAN.Asegurar();
            DesconectarEventosRed();
            red.Suscribir(AlRecibir);
            red.AlConectarRival += AlConectarRival;
            red.AlDesconectar += AlDesconectar;
        }

        private void DesconectarEventosRed()
        {
            if (red == null) return;
            red.Desuscribir(AlRecibir);
            red.AlConectarRival -= AlConectarRival;
            red.AlDesconectar -= AlDesconectar;
        }

        private void CrearPartida()
        {
            if (estado != EstadoSala.Inactivo || !ValidarComandante()) return;

            PrepararRed();
            if (!red.IniciarHost())
            {
                MostrarEstado($"<color=#FF7777>No se pudo crear la partida. ¿El puerto {RedLAN.PuertoJuego} está ocupado por otro programa?</color>");
                return;
            }

            soyHost = true;
            estado = EstadoSala.Hospedando;
            descubrimiento.DetenerBusqueda();
            bool anunciada = descubrimiento.IniciarAnuncio(comandante.nombre, rolHost);

            string ips = string.Join("  ·  ", DescubrimientoLAN.IPsLocales());
            MostrarEstado($"Partida creada. Juegas como <b>{ModoJuego.NombreRol(rolHost)}</b>. Esperando al otro jugador...\n" +
                          $"Tu IP: <color=#FFDD44>{ips}</color> (puerto {RedLAN.PuertoJuego})" +
                          (anunciada ? "" : "\n<color=#FFAA66>No se pudo anunciar en la red: el otro jugador deberá escribir tu IP.</color>"));
            RefrescarControles();
        }

        private void Unirse(string ip)
        {
            if (estado != EstadoSala.Inactivo || !ValidarComandante()) return;

            ip = (ip ?? "").Trim();
            if (!System.Net.IPAddress.TryParse(ip, out _))
            {
                MostrarEstado("<color=#FFAA66>Escribe una IP válida, por ejemplo 192.168.1.20</color>");
                return;
            }

            PrepararRed();
            if (!red.IniciarCliente(ip))
            {
                MostrarEstado("<color=#FF7777>No se pudo iniciar la conexión.</color>");
                return;
            }

            soyHost = false;
            estado = EstadoSala.Conectando;
            descubrimiento.DetenerBusqueda();
            MostrarEstado($"Conectando a {ip}:{RedLAN.PuertoJuego}...");
            RefrescarControles();
        }

        private void Cancelar()
        {
            if (red != null && red.Activa) red.Detener();
            descubrimiento.DetenerAnuncio();
            estado = EstadoSala.Inactivo;
            soyHost = false;
            if (panel != null && panel.activeSelf) descubrimiento.IniciarBusqueda();
            RefrescarControles();
            MostrarEstado("Conexión cancelada.");
        }

        private void Comenzar()
        {
            if (!soyHost || estado != EstadoSala.EnSala) return;
            red.Enviar(new EscritorMensaje(TipoMensaje.IniciarPartida));
            CargarPartida(esHost: true);
        }

        private void CargarPartida(bool esHost)
        {
            RolJugador rolLocal = esHost ? rolHost : rolAsignadoCliente;

            if (rolLocal == RolJugador.Atacante)
            {
                ModoJuego.NombreAtacante = comandante.nombre;
                ModoJuego.IdAtacante = comandante.Id;
                ModoJuego.NombreDefensor = nombreRival;
                ModoJuego.IdDefensor = idRival;
            }
            else
            {
                ModoJuego.NombreDefensor = comandante.nombre;
                ModoJuego.IdDefensor = comandante.Id;
                ModoJuego.NombreAtacante = nombreRival;
                ModoJuego.IdAtacante = idRival;
            }

            ModoJuego.IniciadoDesdeMenu = true;
            ModoJuego.ConfigurarMultijugador(esHost, rolLocal);
            descubrimiento.DetenerAnuncio();
            descubrimiento.DetenerBusqueda();
            DesconectarEventosRed();
            SceneManager.LoadScene(EscenaJuego);
        }

        #endregion

        #region Eventos de red

        private void AlConectarRival()
        {
            if (soyHost) return; // el host espera el saludo del cliente

            EscritorMensaje hola = new EscritorMensaje(TipoMensaje.Hola);
            hola.W.Write(Protocolo.Version);
            hola.W.Write(comandante.nombre);
            hola.W.Write(comandante.Id);
            red.Enviar(hola);
            MostrarEstado("Conectado. Esperando respuesta del host...");
        }

        private void AlRecibir(byte[] datos)
        {
            LectorMensaje m = new LectorMensaje(datos);
            switch (m.Tipo)
            {
                case TipoMensaje.Hola:
                {
                    if (!soyHost) return;
                    int version = m.R.ReadInt32();
                    string nombre = m.R.ReadString();
                    int id = m.R.ReadInt32();

                    if (version != Protocolo.Version)
                    {
                        EscritorMensaje rechazo = new EscritorMensaje(TipoMensaje.Rechazo);
                        rechazo.W.Write($"Versión distinta del juego (host v{Protocolo.Version}, tú v{version}). Actualicen ambos al mismo commit.");
                        red.Enviar(rechazo);
                        return;
                    }

                    nombreRival = string.IsNullOrEmpty(nombre) ? "Rival" : nombre;
                    idRival = id;
                    RolJugador rolCliente = rolHost == RolJugador.Atacante ? RolJugador.Defensor : RolJugador.Atacante;

                    EscritorMensaje bienvenida = new EscritorMensaje(TipoMensaje.Bienvenida);
                    bienvenida.W.Write((byte)rolCliente);
                    bienvenida.W.Write(comandante.nombre);
                    bienvenida.W.Write(comandante.Id);
                    red.Enviar(bienvenida);

                    estado = EstadoSala.EnSala;
                    descubrimiento.DetenerAnuncio();
                    MostrarEstado($"<color=#88FF88>¡{nombreRival} se unió!</color> Será el <b>{ModoJuego.NombreRol(rolCliente)}</b>. " +
                                  "Pulsa <b>Comenzar partida</b> cuando estén listos.");
                    RefrescarControles();
                    break;
                }

                case TipoMensaje.Bienvenida:
                {
                    if (soyHost) return;
                    rolAsignadoCliente = (RolJugador)m.R.ReadByte();
                    nombreRival = m.R.ReadString();
                    idRival = m.R.ReadInt32();
                    estado = EstadoSala.EnSala;
                    MostrarEstado($"<color=#88FF88>Conectado con {nombreRival}.</color> Jugarás como <b>{ModoJuego.NombreRol(rolAsignadoCliente)}</b>. " +
                                  "Esperando a que el host comience la partida...");
                    RefrescarControles();
                    break;
                }

                case TipoMensaje.Rechazo:
                {
                    string motivo = m.R.ReadString();
                    if (red != null) red.Detener();
                    estado = EstadoSala.Inactivo;
                    descubrimiento.IniciarBusqueda();
                    MostrarEstado("<color=#FF7777>El host rechazó la conexión:</color> " + motivo);
                    RefrescarControles();
                    break;
                }

                case TipoMensaje.IniciarPartida:
                    if (!soyHost && estado == EstadoSala.EnSala) CargarPartida(esHost: false);
                    break;
            }
        }

        private void AlDesconectar(string motivo)
        {
            if (soyHost && estado == EstadoSala.EnSala)
            {
                // El cliente se fue: el host vuelve a esperar a otro jugador
                estado = EstadoSala.Hospedando;
                descubrimiento.IniciarAnuncio(comandante.nombre, rolHost);
                MostrarEstado("El otro jugador salió. Esperando a otro jugador...");
                RefrescarControles();
                return;
            }

            if (red != null) red.Detener();
            estado = EstadoSala.Inactivo;
            soyHost = false;
            if (panel.activeSelf) descubrimiento.IniciarBusqueda();
            MostrarEstado("<color=#FF7777>" + motivo + "</color>\nRevisa que ambos PCs estén en la misma red y que el firewall de Windows permita el juego.");
            RefrescarControles();
        }

        #endregion

        #region Interfaz

        private void MostrarEstado(string texto)
        {
            if (txtEstado != null) txtEstado.text = texto;
        }

        private void SeleccionarRol(RolJugador rol)
        {
            if (estado != EstadoSala.Inactivo) return;
            rolHost = rol;
            RefrescarControles();
        }

        private void RefrescarControles()
        {
            if (panel == null) return;
            bool libre = estado == EstadoSala.Inactivo;

            btnRolAtacante.image.color = rolHost == RolJugador.Atacante ? new Color(0.75f, 0.25f, 0.2f) : new Color(0.3f, 0.15f, 0.15f);
            btnRolDefensor.image.color = rolHost == RolJugador.Defensor ? new Color(0.2f, 0.45f, 0.8f) : new Color(0.12f, 0.18f, 0.3f);
            btnRolAtacante.interactable = libre;
            btnRolDefensor.interactable = libre;
            btnCrear.interactable = libre;
            btnConectar.interactable = libre;
            inputIP.interactable = libre;

            btnComenzar.gameObject.SetActive(soyHost && estado == EstadoSala.EnSala);
            btnCancelar.gameObject.SetActive(!libre);

            txtIPs.text = "IP de este PC: " + string.Join("  ·  ", DescubrimientoLAN.IPsLocales());
            RefrescarPartidas();
        }

        private void RefrescarPartidas()
        {
            if (listaPartidas == null) return;
            foreach (Transform hijo in listaPartidas)
            {
                if (hijo != txtSinPartidas.transform) Destroy(hijo.gameObject);
            }

            int n = 0;
            if (estado == EstadoSala.Inactivo)
            {
                foreach (PartidaEncontrada p in descubrimiento.Partidas.Values)
                {
                    if (n >= 5) break;
                    n++;
                    string ip = p.ip;
                    RolJugador rolTuyo = p.rolHost == RolJugador.Atacante ? RolJugador.Defensor : RolJugador.Atacante;
                    Button b = UIFabrica.CrearBoton(listaPartidas, "Partida_" + ip,
                        $"Unirse a {p.nombreHost}  ·  {ip}\n(tú serás {ModoJuego.NombreRol(rolTuyo)})",
                        new Color(0.18f, 0.4f, 0.25f), 14);
                    UIFabrica.Tamano(b, -1, 50);
                    b.onClick.AddListener(() => Unirse(ip));
                }
            }

            txtSinPartidas.gameObject.SetActive(n == 0);
            txtSinPartidas.text = estado == EstadoSala.Inactivo
                ? "Buscando partidas en la red local..."
                : "Búsqueda en pausa mientras estás conectado.";
            txtSinPartidas.transform.SetAsLastSibling();
        }

        private RectTransform CrearColumna(Transform padre, string nombre, string titulo, float ancho)
        {
            RectTransform col = UIFabrica.CrearPanel(padre, nombre, new Color(0.1f, 0.13f, 0.19f, 0.95f));
            UIFabrica.Tamano(col, ancho, -1, 1f);
            VerticalLayoutGroup v = UIFabrica.Vertical(col, 8, new RectOffset(14, 14, 12, 12));
            v.childForceExpandHeight = false;
            Text t = UIFabrica.CrearTexto(col, "Titulo", titulo, 19, new Color(1f, 0.9f, 0.55f));
            UIFabrica.Tamano(t, -1, 30);
            return col;
        }

        private void ConstruirPanel()
        {
            Canvas canvas = UIFabrica.CrearCanvas("Canvas_SalaLAN", 200);
            canvas.transform.SetParent(transform, false);
            panel = canvas.gameObject;

            RectTransform fondo = UIFabrica.CrearPanel(canvas.transform, "Oscurecer", new Color(0f, 0f, 0f, 0.75f));
            UIFabrica.Estirar(fondo);

            RectTransform caja = UIFabrica.CrearPanel(fondo, "Caja", new Color(0.06f, 0.08f, 0.12f, 0.98f));
            UIFabrica.Anclar(caja, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 800));
            Outline borde = caja.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(0.35f, 0.65f, 1f, 0.8f);
            borde.effectDistance = new Vector2(2, -2);
            VerticalLayoutGroup vCaja = UIFabrica.Vertical(caja, 12, new RectOffset(24, 24, 18, 18));
            vCaja.childForceExpandHeight = false;

            // Título + cerrar
            RectTransform barraTitulo = UIFabrica.CrearContenedor(caja, "BarraTitulo");
            UIFabrica.Tamano(barraTitulo, -1, 48);
            HorizontalLayoutGroup hTitulo = UIFabrica.Horizontal(barraTitulo, 10, null);
            hTitulo.childForceExpandWidth = false;
            Text titulo = UIFabrica.CrearTexto(barraTitulo, "Titulo", "🌐 MULTIJUGADOR LAN · ⚔️ Atacante vs 🛡️ Defensor", 26, Color.white);
            UIFabrica.Tamano(titulo, 1100, -1, 1f);
            Button cerrar = UIFabrica.CrearBoton(barraTitulo, "BtnCerrar", "✕", new Color(0.7f, 0.2f, 0.2f), 22);
            UIFabrica.Tamano(cerrar, 48, -1);
            cerrar.onClick.AddListener(CerrarPanel);

            // Cuerpo con tres columnas
            RectTransform cuerpo = UIFabrica.CrearContenedor(caja, "Cuerpo");
            UIFabrica.Tamano(cuerpo, -1, 520);
            HorizontalLayoutGroup hCuerpo = UIFabrica.Horizontal(cuerpo, 14, null);
            hCuerpo.childForceExpandWidth = false;

            // Columna 1: comandante
            RectTransform col1 = CrearColumna(cuerpo, "ColComandante", "1) Tu comandante (árbol BST)", 420);
            listaComandantes = UIFabrica.CrearContenedor(col1, "ListaComandantes");
            UIFabrica.Tamano(listaComandantes, -1, 340);
            VerticalLayoutGroup vLista = UIFabrica.Vertical(listaComandantes, 6, null);
            vLista.childForceExpandHeight = false;
            RectTransform filaNuevo = UIFabrica.CrearContenedor(col1, "FilaNuevo");
            UIFabrica.Tamano(filaNuevo, -1, 42);
            HorizontalLayoutGroup hNuevo = UIFabrica.Horizontal(filaNuevo, 8, null);
            hNuevo.childForceExpandWidth = false;
            inputNuevoComandante = UIFabrica.CrearInput(filaNuevo, "InputNuevo", "Nombre del comandante nuevo", 16);
            UIFabrica.Tamano(inputNuevoComandante, 250, -1, 1f);
            Button btnNuevo = UIFabrica.CrearBoton(filaNuevo, "BtnCrearComandante", "Crear", new Color(0.2f, 0.5f, 0.35f), 16);
            UIFabrica.Tamano(btnNuevo, 100, -1);
            btnNuevo.onClick.AddListener(CrearComandante);
            txtComandante = UIFabrica.CrearTexto(col1, "Seleccionado", "", 16, Color.white);
            UIFabrica.Tamano(txtComandante, -1, 40);

            // Columna 2: crear partida
            RectTransform col2 = CrearColumna(cuerpo, "ColCrear", "2) Crear partida (host)", 400);
            Text lblRol = UIFabrica.CrearTexto(col2, "LblRol", "Elige tu rol (el otro jugador toma el contrario):", 15, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFabrica.Tamano(lblRol, -1, 44);
            RectTransform filaRol = UIFabrica.CrearContenedor(col2, "FilaRol");
            UIFabrica.Tamano(filaRol, -1, 60);
            UIFabrica.Horizontal(filaRol, 10, null);
            btnRolAtacante = UIFabrica.CrearBoton(filaRol, "BtnRolAtacante", "⚔️ Atacante", new Color(0.75f, 0.25f, 0.2f), 18);
            btnRolDefensor = UIFabrica.CrearBoton(filaRol, "BtnRolDefensor", "🛡️ Defensor", new Color(0.2f, 0.45f, 0.8f), 18);
            btnRolAtacante.onClick.AddListener(() => SeleccionarRol(RolJugador.Atacante));
            btnRolDefensor.onClick.AddListener(() => SeleccionarRol(RolJugador.Defensor));
            btnCrear = UIFabrica.CrearBoton(col2, "BtnCrearPartida", "Crear partida", new Color(0.2f, 0.55f, 0.3f), 20);
            UIFabrica.Tamano(btnCrear, -1, 58);
            btnCrear.onClick.AddListener(CrearPartida);
            txtIPs = UIFabrica.CrearTexto(col2, "IPs", "", 14, new Color(0.75f, 0.85f, 1f), TextAnchor.UpperLeft, FontStyle.Normal);
            UIFabrica.Tamano(txtIPs, -1, 60);
            Text ayudaHost = UIFabrica.CrearTexto(col2, "AyudaHost",
                "El host simula la partida. Si Windows pregunta por el firewall, permite el acceso en redes privadas.",
                13, new Color(0.7f, 0.72f, 0.8f), TextAnchor.UpperLeft, FontStyle.Italic);
            UIFabrica.Tamano(ayudaHost, -1, 60);

            // Columna 3: unirse
            RectTransform col3 = CrearColumna(cuerpo, "ColUnirse", "3) Unirse a una partida", 420);
            listaPartidas = UIFabrica.CrearContenedor(col3, "ListaPartidas");
            UIFabrica.Tamano(listaPartidas, -1, 290);
            VerticalLayoutGroup vPartidas = UIFabrica.Vertical(listaPartidas, 6, null);
            vPartidas.childForceExpandHeight = false;
            txtSinPartidas = UIFabrica.CrearTexto(listaPartidas, "SinPartidas", "Buscando partidas en la red local...", 15, new Color(0.75f, 0.78f, 0.85f), TextAnchor.MiddleLeft, FontStyle.Italic);
            UIFabrica.Tamano(txtSinPartidas, -1, 40);
            Text lblIP = UIFabrica.CrearTexto(col3, "LblIP", "¿No aparece? Escribe la IP del host:", 15, new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFabrica.Tamano(lblIP, -1, 30);
            RectTransform filaIP = UIFabrica.CrearContenedor(col3, "FilaIP");
            UIFabrica.Tamano(filaIP, -1, 44);
            HorizontalLayoutGroup hIP = UIFabrica.Horizontal(filaIP, 8, null);
            hIP.childForceExpandWidth = false;
            inputIP = UIFabrica.CrearInput(filaIP, "InputIP", "192.168.1.20", 17);
            UIFabrica.Tamano(inputIP, 240, -1, 1f);
            btnConectar = UIFabrica.CrearBoton(filaIP, "BtnConectar", "Conectar", new Color(0.2f, 0.45f, 0.75f), 17);
            UIFabrica.Tamano(btnConectar, 120, -1);
            btnConectar.onClick.AddListener(() => Unirse(inputIP.text));

            // Estado
            RectTransform cajaEstado = UIFabrica.CrearPanel(caja, "CajaEstado", new Color(0.03f, 0.04f, 0.07f, 0.95f));
            UIFabrica.Tamano(cajaEstado, -1, 96);
            txtEstado = UIFabrica.CrearTexto(cajaEstado, "Estado", "", 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFabrica.Estirar(txtEstado.rectTransform);
            txtEstado.rectTransform.offsetMin = new Vector2(14, 6);
            txtEstado.rectTransform.offsetMax = new Vector2(-14, -6);

            // Acciones finales
            RectTransform filaAcciones = UIFabrica.CrearContenedor(caja, "FilaAcciones");
            UIFabrica.Tamano(filaAcciones, -1, 56);
            HorizontalLayoutGroup hAcc = UIFabrica.Horizontal(filaAcciones, 14, null);
            hAcc.childForceExpandWidth = false;
            hAcc.childAlignment = TextAnchor.MiddleCenter;
            btnComenzar = UIFabrica.CrearBoton(filaAcciones, "BtnComenzar", "▶ Comenzar partida", new Color(0.2f, 0.6f, 0.3f), 22);
            UIFabrica.Tamano(btnComenzar, 320, -1);
            btnComenzar.onClick.AddListener(Comenzar);
            btnCancelar = UIFabrica.CrearBoton(filaAcciones, "BtnCancelar", "✕ Cancelar conexión", new Color(0.6f, 0.25f, 0.2f), 20);
            UIFabrica.Tamano(btnCancelar, 280, -1);
            btnCancelar.onClick.AddListener(Cancelar);

            RefrescarControles();
        }

        #endregion
    }
}
