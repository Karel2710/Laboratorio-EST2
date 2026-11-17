using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using GrafosEST2;
using TowerDefenseGameplay;

namespace TowerDefenseOnline
{
    public enum ModoOnline { Cooperativo = 0, Competitivo = 1 }

    public enum EstadoConexion { Desconectado, Conectando, AnfitrionTCP, ClienteTCP, SalaSimulada }

    /// <summary>Tipos de comando del protocolo (viajan como texto dentro del JSON).</summary>
    public static class TipoPaquete
    {
        public const string CONECTAR_JUGADOR = "CONECTAR_JUGADOR";
        public const string BIENVENIDA = "BIENVENIDA";
        public const string RECHAZO = "RECHAZO";
        public const string DESCONECTAR = "DESCONECTAR";
        public const string CONSTRUIR_TORRE = "CONSTRUIR_TORRE";
        public const string MEJORAR_TORRE = "MEJORAR_TORRE";
        public const string VENDER_TORRE = "VENDER_TORRE";
        public const string USAR_HABILIDAD_DAG = "USAR_HABILIDAD_DAG";
        public const string ENVIAR_MERCENARIOS_PVP = "ENVIAR_MERCENARIOS_PVP";
        public const string SINCRONIZAR_ESTADO_OLEADA = "SINCRONIZAR_ESTADO_OLEADA";
        public const string PING = "PING";
        public const string PONG = "PONG";
    }

    /// <summary>
    /// Paquete de red serializable en JSON (una línea por paquete).
    /// Arquitectura basada en comandos: se transmiten las acciones del jugador, no el estado
    /// de cada enemigo; las oleadas se reproducen igual en todas las máquinas gracias a la semilla.
    /// </summary>
    [System.Serializable]
    public class PaqueteRedTD
    {
        public string tipo;
        public int secuencia;
        public int idJugador;
        public string nombreJugador;
        public string perfilJson;      // CONECTAR_JUGADOR: perfil completo del Jugador (BST local)
        public int slot = -1;
        public int modo;
        public int nivel;
        public int plotIndex = -1;     // CONSTRUIR / MEJORAR / VENDER
        public int tipoTorre;
        public int habilidad;          // USAR_HABILIDAD_DAG
        public int nodoA = -1;         // Barricada: arista del grafo de caminos
        public int nodoB = -1;
        public int tipoEnemigo;        // ENVIAR_MERCENARIOS_PVP
        public int cantidad;
        public int semilla;            // SINCRONIZAR_ESTADO_OLEADA
        public int oleada;
        public bool iniciarOleada;
        public float vidaBase;
        public int puntaje;
        public int oro;
        public string estadoGrafo;     // aristas bloqueadas "a-b;c-d"
        public double marcaTiempo;     // PING / PONG
        public string mensaje;
    }

    /// <summary>Estado en vivo de un integrante de la sala.</summary>
    public class JugadorSala
    {
        public Jugador perfil;
        public int slot;
        public Color color;
        public bool esLocal, esBot, esAnfitrion;
        public float ping;
        public int oro, puntaje, oleada;
        public float vidaBase = 100f;
        public bool eliminado;
        public int paquetes;
        public int bytesVentana;
        public float kbps;
        public int clienteId = -1;
        // IA
        public float temporizadorIA, temporizadorMerc, temporizadorHabilidad;
    }

    /// <summary>
    /// Gestor del modo multijugador. Doble motor de transporte:
    ///  • Sockets TCP cliente-servidor (LAN/Online): un anfitrión escucha en un puerto y retransmite
    ///    los paquetes de cada cliente al resto de la sala (topología en estrella).
    ///  • Sala simulada (sin configuración): comandantes del BST controlados por IA intercambian los
    ///    mismos paquetes por un canal en memoria con latencia simulada (topología P2P).
    /// </summary>
    public class GestorRedOnline : MonoBehaviour
    {
        public static GestorRedOnline Instance { get; private set; }

        public const int PUERTO_POR_DEFECTO = 7777;
        public const int MAX_JUGADORES = 4;

        public EstadoConexion estado = EstadoConexion.Desconectado;
        public ModoOnline modo = ModoOnline.Cooperativo;
        public int puerto = PUERTO_POR_DEFECTO;
        public string codigoSala = "";

        /// <summary>Integrantes de la sala indexados por ID en un árbol BST (misma estructura que los comandantes).</summary>
        public readonly Arbol salaJugadores = new Arbol();
        private readonly Dictionary<int, JugadorSala> datos = new Dictionary<int, JugadorSala>();

        public int paquetesEnviados, paquetesRecibidos;
        public readonly List<string> bitacora = new List<string>();
        public event System.Action OnSalaActualizada;

        // ── Transporte TCP ──
        private TcpListener servidor;
        private TcpClient cliente;
        private Thread hiloAceptar, hiloCliente;
        private volatile bool ejecutando;
        private int siguienteClienteId = 1;
        private readonly Dictionary<int, StreamWriter> escritores = new Dictionary<int, StreamWriter>();
        private readonly object candado = new object();
        private StreamWriter escritorCliente;
        private readonly ConcurrentQueue<KeyValuePair<int, string>> entrantes = new ConcurrentQueue<KeyValuePair<int, string>>();
        private readonly ConcurrentQueue<string> avisosHilo = new ConcurrentQueue<string>();

        // ── Transporte simulado ──
        private struct EnTransito { public float entrega; public PaqueteRedTD paquete; }
        private readonly List<EnTransito> canalSimulado = new List<EnTransito>();

        private int secuencia;
        private float temporizadorSync, temporizadorPing, temporizadorAncho;
        private bool suscrito;
        private int ultimaOleadaVista = 0;

        public bool Conectado => estado == EstadoConexion.AnfitrionTCP || estado == EstadoConexion.ClienteTCP || estado == EstadoConexion.SalaSimulada;
        public bool EsAutoridad => estado == EstadoConexion.AnfitrionTCP || estado == EstadoConexion.SalaSimulada;

        public static GestorRedOnline Asegurar()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("GestorRedOnline");
            return go.AddComponent<GestorRedOnline>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            CerrarTransporte();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            CerrarTransporte();
        }

        #region Sala (BST de integrantes)

        public List<JugadorSala> Integrantes()
        {
            List<JugadorSala> lista = new List<JugadorSala>();
            foreach (Jugador j in salaJugadores.InOrden())
            {
                if (datos.TryGetValue(j.Id, out JugadorSala js)) lista.Add(js);
            }
            return lista;
        }

        public JugadorSala Local()
        {
            foreach (JugadorSala js in datos.Values) if (js.esLocal) return js;
            return null;
        }

        public JugadorSala Buscar(int id)
        {
            return salaJugadores.Buscar(id) != null && datos.TryGetValue(id, out JugadorSala js) ? js : null;
        }

        private int SlotLibre()
        {
            for (int s = 0; s < MAX_JUGADORES; s++)
            {
                bool usado = false;
                foreach (JugadorSala js in datos.Values) if (js.slot == s) usado = true;
                if (!usado) return s;
            }
            return -1;
        }

        private JugadorSala AgregarIntegrante(Jugador perfil, int slot, bool local, bool bot, bool anfitrion)
        {
            if (perfil == null || slot < 0) return null;
            if (!salaJugadores.Insertar(perfil)) return null; // ID duplicado: la clave del BST es única
            JugadorSala js = new JugadorSala
            {
                perfil = perfil, slot = slot, esLocal = local, esBot = bot, esAnfitrion = anfitrion,
                color = Paleta.ColoresJugador[slot % Paleta.ColoresJugador.Length],
                ping = local ? 0f : Random.Range(25f, 70f),
                oro = 150,
                temporizadorIA = Random.Range(2f, 5f),
                temporizadorMerc = Random.Range(18f, 30f),
                temporizadorHabilidad = Random.Range(40f, 70f)
            };
            datos[perfil.Id] = js;
            return js;
        }

        private void QuitarIntegrante(int id)
        {
            if (!datos.ContainsKey(id)) return;
            salaJugadores.Eliminar(id);
            datos.Remove(id);
        }

        private void LimpiarSala()
        {
            salaJugadores.Limpiar();
            datos.Clear();
            canalSimulado.Clear();
            paquetesEnviados = 0;
            paquetesRecibidos = 0;
        }

        private void Log(string linea)
        {
            bitacora.Add($"[{System.DateTime.Now:HH:mm:ss}] {linea}");
            if (bitacora.Count > 60) bitacora.RemoveAt(0);
            Debug.Log("[Online] " + linea);
            OnSalaActualizada?.Invoke();
        }

        private static Jugador PerfilLocal()
        {
            GameManager gm = GameManager.Instance;
            return gm != null ? gm.jugadorActivo : null;
        }

        #endregion

        #region Crear / Unirse / Salir

        /// <summary>Abre un servidor TCP en el puerto indicado y entra a la sala como anfitrión.</summary>
        public bool CrearSalaTCP(int puertoDeseado, ModoOnline modoSala)
        {
            Salir(false);
            Jugador perfil = PerfilLocal();
            if (perfil == null) { Log("No hay comandante activo."); return false; }

            try
            {
                puerto = Mathf.Clamp(puertoDeseado, 1024, 65535);
                servidor = new TcpListener(IPAddress.Any, puerto);
                servidor.Start();
            }
            catch (System.Exception ex)
            {
                servidor = null;
                Log($"✕ No se pudo abrir el puerto {puertoDeseado}: {ex.Message}");
                return false;
            }

            modo = modoSala;
            estado = EstadoConexion.AnfitrionTCP;
            ejecutando = true;
            hiloAceptar = new Thread(BucleAceptar) { IsBackground = true, Name = "TD_Servidor" };
            hiloAceptar.Start();

            LimpiarSala();
            AgregarIntegrante(perfil, 0, true, false, true);
            string ip = ObtenerIPLocal();
            codigoSala = CodificarSala(ip, puerto);
            AlEntrarASala();
            Log($"🏠 Sala creada. Servidor TCP escuchando en {ip}:{puerto} · Código: {codigoSala} · Modo {modo}");
            return true;
        }

        /// <summary>Se conecta como cliente a un anfitrión por "IP", "IP:puerto" o código de sala.</summary>
        public bool UnirseSalaTCP(string destino)
        {
            Salir(false);
            if (PerfilLocal() == null) { Log("No hay comandante activo."); return false; }
            if (!ResolverDestino(destino, out string ip, out int puertoDestino))
            {
                Log("✕ Dirección inválida. Use una IP (192.168.1.10), IP:puerto o el código de sala.");
                return false;
            }

            estado = EstadoConexion.Conectando;
            ejecutando = true;
            puerto = puertoDestino;
            Log($"Conectando a {ip}:{puertoDestino}...");
            hiloCliente = new Thread(() => BucleCliente(ip, puertoDestino)) { IsBackground = true, Name = "TD_Cliente" };
            hiloCliente.Start();
            return true;
        }

        /// <summary>
        /// Sala simulada: los demás integrantes son comandantes del árbol BST controlados por IA.
        /// Permite demostrar el modo online con una sola PC y sin conexión.
        /// </summary>
        public bool CrearSalaSimulada(int totalJugadores, ModoOnline modoSala)
        {
            Salir(false);
            Jugador perfil = PerfilLocal();
            if (perfil == null) { Log("No hay comandante activo."); return false; }

            modo = modoSala;
            estado = EstadoConexion.SalaSimulada;
            codigoSala = "SIM-" + Random.Range(1000, 9999);
            LimpiarSala();
            AgregarIntegrante(perfil, 0, true, false, true);

            // Candidatos: otros comandantes registrados en el BST local
            List<Jugador> candidatos = new List<Jugador>();
            if (ControladorArbolUI.Instance != null)
            {
                foreach (Jugador j in ControladorArbolUI.Instance.arbolJugadores.InOrden())
                    if (j.Id != perfil.Id) candidatos.Add(j);
            }

            totalJugadores = Mathf.Clamp(totalJugadores, 2, MAX_JUGADORES);
            string[] nombresIA = { "Vega_IA", "Orión_IA", "Lyra_IA" };
            ClaseComandante[] clasesIA = { ClaseComandante.Artillero, ClaseComandante.Criomante, ClaseComandante.IngenieroMST };
            for (int i = 1; i < totalJugadores; i++)
            {
                Jugador botPerfil;
                if (candidatos.Count > 0)
                {
                    // Copia del perfil: la IA no modifica la ficha guardada del comandante
                    Jugador origen = candidatos[0];
                    candidatos.RemoveAt(0);
                    botPerfil = JsonUtility.FromJson<Jugador>(JsonUtility.ToJson(origen));
                }
                else
                {
                    int id = 9000 + i;
                    while (salaJugadores.Buscar(id) != null) id++;
                    botPerfil = new Jugador(100, id, nombresIA[(i - 1) % nombresIA.Length], 1 + i, 0) { clase = clasesIA[(i - 1) % clasesIA.Length] };
                }
                AgregarIntegrante(botPerfil, i, false, true, false);
                // El bot anuncia su conexión con el mismo paquete que usaría un cliente real
                EncolarSimulado(new PaqueteRedTD
                {
                    tipo = TipoPaquete.CONECTAR_JUGADOR, idJugador = botPerfil.Id, nombreJugador = botPerfil.nombre,
                    perfilJson = JsonUtility.ToJson(botPerfil), slot = i
                });
            }

            AlEntrarASala();
            Log($"🤖 Sala simulada {codigoSala}: {totalJugadores} comandantes · Modo {modo} · Canal en memoria con latencia simulada.");
            return true;
        }

        private void AlEntrarASala()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                JugadorSala yo = Local();
                gm.ColorJugadorLocal = yo != null ? yo.color : Color.clear;
                gm.oleadasControladasPorAnfitrion = estado == EstadoConexion.ClienteTCP;
                if (!suscrito)
                {
                    gm.OnTorreConstruidaLocal += EnviarConstruccion;
                    gm.OnTorreMejoradaLocal += EnviarMejora;
                    gm.OnTorreVendidaLocal += EnviarVenta;
                    gm.OnHabilidadUsadaLocal += EnviarHabilidad;
                    gm.OnOleadaIniciadaLocal += EnviarInicioOleada;
                    gm.OnFinPartida += AlFinalizarPartida;
                    suscrito = true;
                }
                ultimaOleadaVista = gm.oleadasCompletadas;
            }
            OnSalaActualizada?.Invoke();
        }

        public void Salir(bool avisar = true)
        {
            if (estado == EstadoConexion.Desconectado) return;
            if (Conectado && estado != EstadoConexion.SalaSimulada)
            {
                Jugador p = PerfilLocal();
                if (p != null) Enviar(new PaqueteRedTD { tipo = TipoPaquete.DESCONECTAR, idJugador = p.Id, nombreJugador = p.nombre });
            }
            CerrarTransporte();
            estado = EstadoConexion.Desconectado;
            codigoSala = "";
            LimpiarSala();

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.oleadasControladasPorAnfitrion = false;
                gm.ColorJugadorLocal = Color.clear;
                gm.bonoIngresoMercenarios = 0;
                if (suscrito)
                {
                    gm.OnTorreConstruidaLocal -= EnviarConstruccion;
                    gm.OnTorreMejoradaLocal -= EnviarMejora;
                    gm.OnTorreVendidaLocal -= EnviarVenta;
                    gm.OnHabilidadUsadaLocal -= EnviarHabilidad;
                    gm.OnOleadaIniciadaLocal -= EnviarInicioOleada;
                    gm.OnFinPartida -= AlFinalizarPartida;
                    suscrito = false;
                }
            }
            if (avisar) Log("⏏ Saliste de la sala.");
            else OnSalaActualizada?.Invoke();
        }

        #endregion

        #region Transporte TCP (hilos de red)

        private void BucleAceptar()
        {
            while (ejecutando)
            {
                try
                {
                    TcpClient entrante = servidor.AcceptTcpClient();
                    entrante.NoDelay = true;
                    int id = Interlocked.Increment(ref siguienteClienteId);
                    StreamWriter w = new StreamWriter(entrante.GetStream(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                    lock (candado) escritores[id] = w;
                    Thread t = new Thread(() => BucleLectura(entrante, id)) { IsBackground = true, Name = "TD_Lector" + id };
                    t.Start();
                }
                catch
                {
                    if (ejecutando) Thread.Sleep(50);
                }
            }
        }

        private void BucleLectura(TcpClient conexion, int id)
        {
            try
            {
                using (StreamReader r = new StreamReader(conexion.GetStream(), new UTF8Encoding(false)))
                {
                    string linea;
                    while (ejecutando && (linea = r.ReadLine()) != null)
                    {
                        if (linea.Length > 0) entrantes.Enqueue(new KeyValuePair<int, string>(id, linea));
                    }
                }
            }
            catch { /* conexión cerrada */ }
            finally
            {
                lock (candado) escritores.Remove(id);
                try { conexion.Close(); } catch { }
                // Aviso al hilo principal de que este cliente se desconectó
                entrantes.Enqueue(new KeyValuePair<int, string>(id, "#CERRADO"));
            }
        }

        private void BucleCliente(string ip, int puertoDestino)
        {
            try
            {
                cliente = new TcpClient();
                cliente.NoDelay = true;
                cliente.Connect(ip, puertoDestino);
                lock (candado) escritorCliente = new StreamWriter(cliente.GetStream(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                avisosHilo.Enqueue("#CONECTADO");

                using (StreamReader r = new StreamReader(cliente.GetStream(), new UTF8Encoding(false)))
                {
                    string linea;
                    while (ejecutando && (linea = r.ReadLine()) != null)
                    {
                        if (linea.Length > 0) entrantes.Enqueue(new KeyValuePair<int, string>(0, linea));
                    }
                }
                avisosHilo.Enqueue("#SERVIDOR_CERRADO");
            }
            catch (System.Exception ex)
            {
                avisosHilo.Enqueue("#ERROR " + ex.Message);
            }
        }

        private void CerrarTransporte()
        {
            ejecutando = false;
            try { if (servidor != null) servidor.Stop(); } catch { }
            servidor = null;
            lock (candado)
            {
                foreach (StreamWriter w in escritores.Values) { try { w.Close(); } catch { } }
                escritores.Clear();
                escritorCliente = null;
            }
            try { if (cliente != null) cliente.Close(); } catch { }
            cliente = null;
        }

        private void EscribirLinea(StreamWriter w, string json)
        {
            if (w == null) return;
            try { lock (w) w.WriteLine(json); }
            catch { /* el lector detectará el cierre */ }
        }

        /// <summary>Anfitrión: retransmite a todos los clientes excepto al de origen (topología en estrella).</summary>
        private void Difundir(string json, int exceptoCliente)
        {
            List<StreamWriter> destinos = new List<StreamWriter>();
            lock (candado)
            {
                foreach (KeyValuePair<int, StreamWriter> kv in escritores)
                    if (kv.Key != exceptoCliente) destinos.Add(kv.Value);
            }
            for (int i = 0; i < destinos.Count; i++) EscribirLinea(destinos[i], json);
        }

        private void EnviarACliente(int clienteId, PaqueteRedTD p)
        {
            StreamWriter w;
            lock (candado) escritores.TryGetValue(clienteId, out w);
            p.secuencia = ++secuencia;
            EscribirLinea(w, JsonUtility.ToJson(p));
            paquetesEnviados++;
        }

        public static string ObtenerIPLocal()
        {
            try
            {
                // Un socket UDP "conectado" revela la IP de la interfaz de salida sin enviar datos
                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Connect("10.255.255.255", 1);
                    IPEndPoint ep = s.LocalEndPoint as IPEndPoint;
                    if (ep != null) return ep.Address.ToString();
                }
            }
            catch { }
            try
            {
                foreach (IPAddress a in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) return a.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        private const string ALFABETO = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // sin I ni O

        private static string ABase(ulong n)
        {
            if (n == 0) return "0";
            StringBuilder sb = new StringBuilder();
            while (n > 0) { sb.Insert(0, ALFABETO[(int)(n % (ulong)ALFABETO.Length)]); n /= (ulong)ALFABETO.Length; }
            return sb.ToString();
        }

        private static bool DeBase(string s, out ulong n)
        {
            n = 0;
            foreach (char c in s)
            {
                int i = ALFABETO.IndexOf(c);
                if (i < 0) return false;
                n = n * (ulong)ALFABETO.Length + (ulong)i;
            }
            return s.Length > 0;
        }

        /// <summary>Código de sala: IP y puerto empaquetados en base 34.</summary>
        public static string CodificarSala(string ip, int puertoSala)
        {
            if (!IPAddress.TryParse(ip, out IPAddress dir)) return "";
            byte[] b = dir.GetAddressBytes();
            if (b.Length != 4) return "";
            ulong valor = ((ulong)b[0] << 24) | ((ulong)b[1] << 16) | ((ulong)b[2] << 8) | b[3];
            return ABase(valor * 65536UL + (ulong)puertoSala);
        }

        private static bool ResolverDestino(string texto, out string ip, out int puertoDestino)
        {
            ip = "";
            puertoDestino = PUERTO_POR_DEFECTO;
            if (string.IsNullOrWhiteSpace(texto)) return false;
            texto = texto.Trim();

            if (texto.ToLowerInvariant() == "localhost") { ip = "127.0.0.1"; return true; }

            string[] partes = texto.Split(':');
            if (IPAddress.TryParse(partes[0], out IPAddress dir) && dir.AddressFamily == AddressFamily.InterNetwork && partes[0].Contains("."))
            {
                ip = partes[0];
                if (partes.Length > 1 && (!int.TryParse(partes[1], out puertoDestino) || puertoDestino < 1 || puertoDestino > 65535)) return false;
                return true;
            }

            if (DeBase(texto.ToUpperInvariant().Replace("-", ""), out ulong valor))
            {
                puertoDestino = (int)(valor % 65536UL);
                ulong v = valor / 65536UL;
                if (v > 0xFFFFFFFFUL || puertoDestino < 1) return false;
                ip = $"{(v >> 24) & 255}.{(v >> 16) & 255}.{(v >> 8) & 255}.{v & 255}";
                return true;
            }
            return false;
        }

        #endregion

        #region Envío de Paquetes

        private void EncolarSimulado(PaqueteRedTD p)
        {
            JugadorSala origen = Buscar(p.idJugador);
            float latencia = origen != null ? origen.ping * 0.001f : 0.05f;
            canalSimulado.Add(new EnTransito { entrega = Time.unscaledTime + latencia, paquete = p });
        }

        /// <summary>Envía un comando del jugador local al resto de la sala.</summary>
        public void Enviar(PaqueteRedTD p)
        {
            if (!Conectado) return;
            p.secuencia = ++secuencia;
            string json = JsonUtility.ToJson(p);
            paquetesEnviados++;
            JugadorSala yo = Local();
            if (yo != null) { yo.paquetes++; yo.bytesVentana += json.Length; }

            switch (estado)
            {
                case EstadoConexion.AnfitrionTCP: Difundir(json, -1); break;
                case EstadoConexion.ClienteTCP:
                    StreamWriter w;
                    lock (candado) w = escritorCliente;
                    EscribirLinea(w, json);
                    break;
                case EstadoConexion.SalaSimulada:
                    // Los comandantes IA "reciben" el paquete: reaccionan en ProcesarComoBot
                    ProcesarComoBots(p);
                    break;
            }
        }

        private PaqueteRedTD Base(string tipo)
        {
            Jugador p = PerfilLocal();
            return new PaqueteRedTD { tipo = tipo, idJugador = p != null ? p.Id : 0, nombreJugador = p != null ? p.nombre : "?" };
        }

        private void EnviarConstruccion(int plot, TipoTorre tipo)
        {
            if (modo != ModoOnline.Cooperativo) return;
            PaqueteRedTD p = Base(TipoPaquete.CONSTRUIR_TORRE);
            p.plotIndex = plot;
            p.tipoTorre = (int)tipo;
            Enviar(p);
        }

        private void EnviarMejora(int plot)
        {
            if (modo != ModoOnline.Cooperativo) return;
            PaqueteRedTD p = Base(TipoPaquete.MEJORAR_TORRE);
            p.plotIndex = plot;
            Enviar(p);
        }

        private void EnviarVenta(int plot)
        {
            if (modo != ModoOnline.Cooperativo) return;
            PaqueteRedTD p = Base(TipoPaquete.VENDER_TORRE);
            p.plotIndex = plot;
            Enviar(p);
        }

        private void EnviarHabilidad(HabilidadActiva h, int a, int b)
        {
            PaqueteRedTD p = Base(TipoPaquete.USAR_HABILIDAD_DAG);
            p.habilidad = (int)h;
            p.nodoA = a;
            p.nodoB = b;
            Enviar(p);
        }

        private void EnviarInicioOleada(int oleada, int semilla)
        {
            if (!EsAutoridad) return;
            PaqueteRedTD p = PaqueteEstado();
            p.iniciarOleada = true;
            p.oleada = oleada;
            p.semilla = semilla;
            Enviar(p);
            Log($"📡 SINCRONIZAR_ESTADO_OLEADA: oleada {oleada}, semilla {semilla}");
        }

        private PaqueteRedTD PaqueteEstado()
        {
            GameManager gm = GameManager.Instance;
            PaqueteRedTD p = Base(TipoPaquete.SINCRONIZAR_ESTADO_OLEADA);
            if (gm == null) return p;
            p.oleada = gm.oleadaActual;
            p.semilla = gm.semillaPartida;
            p.vidaBase = gm.vidaBaseActual;
            p.puntaje = gm.puntaje;
            p.oro = gm.oroActual;
            p.nivel = gm.nivelActual;
            p.modo = (int)modo;
            if (PathGraph.main != null)
            {
                StringBuilder sb = new StringBuilder();
                foreach (PathGraph.Bloqueo b in PathGraph.main.bloqueos)
                    if (b.a != null && b.b != null) sb.Append(b.a.nodeId).Append('-').Append(b.b.nodeId).Append(';');
                p.estadoGrafo = sb.ToString();
            }
            return p;
        }

        public static int CostoMercenarios(TipoEnemigo tipo, out int cantidad, out int bonoIngreso)
        {
            switch (tipo)
            {
                case TipoEnemigo.Corredor: cantidad = 4; bonoIngreso = 5; return 45;
                case TipoEnemigo.Blindado: cantidad = 2; bonoIngreso = 10; return 95;
                default: cantidad = 1; bonoIngreso = 20; return 230;
            }
        }

        /// <summary>
        /// Modo competitivo: paga oro para enviar enemigos extra a los rivales.
        /// A cambio aumenta el ingreso propio por oleada.
        /// </summary>
        public bool EnviarMercenarios(TipoEnemigo tipo)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !Conectado) return false;
            if (modo != ModoOnline.Competitivo)
            {
                gm.MostrarNotificacion("⚔️ Los mercenarios solo están disponibles en el modo Competitivo.");
                return false;
            }
            int costo = CostoMercenarios(tipo, out int cantidad, out int bono);
            if (gm.oroActual < costo)
            {
                gm.MostrarNotificacion($"¡Oro insuficiente! Enviar {cantidad}× {SpritesEnemigos.Nombre(tipo)} cuesta {costo} de oro.");
                return false;
            }
            gm.GastarOro(costo);
            gm.bonoIngresoMercenarios += bono;

            PaqueteRedTD p = Base(TipoPaquete.ENVIAR_MERCENARIOS_PVP);
            p.tipoEnemigo = (int)tipo;
            p.cantidad = cantidad;
            Enviar(p);
            gm.MostrarNotificacion($"⚔️ {cantidad}× {SpritesEnemigos.Nombre(tipo)} enviados a los rivales (-{costo} Oro). Ingreso por oleada +{bono} (total +{gm.bonoIngresoMercenarios}).");
            Log($"➡ ENVIAR_MERCENARIOS_PVP: {cantidad}× {tipo}");
            return true;
        }

        #endregion

        #region Recepción y Procesamiento

        private void Update()
        {
            // Avisos de los hilos de red
            while (avisosHilo.TryDequeue(out string aviso))
            {
                if (aviso == "#CONECTADO")
                {
                    estado = EstadoConexion.ClienteTCP;
                    LimpiarSala();
                    Jugador perfil = PerfilLocal();
                    PaqueteRedTD p = Base(TipoPaquete.CONECTAR_JUGADOR);
                    p.perfilJson = JsonUtility.ToJson(perfil);
                    string json = JsonUtility.ToJson(p);
                    StreamWriter w;
                    lock (candado) w = escritorCliente;
                    EscribirLinea(w, json);
                    paquetesEnviados++;
                    Log("Conexión TCP establecida. Enviando CONECTAR_JUGADOR con el perfil del BST local...");
                }
                else if (aviso == "#SERVIDOR_CERRADO")
                {
                    if (estado != EstadoConexion.Desconectado) { Log("✕ El anfitrión cerró la sala."); Salir(false); }
                }
                else if (aviso.StartsWith("#ERROR"))
                {
                    if (estado != EstadoConexion.Desconectado) { Log("✕ No se pudo conectar: " + aviso.Substring(6).Trim()); Salir(false); }
                }
            }

            // Paquetes TCP entrantes (se procesan en el hilo principal de Unity)
            int limite = 200;
            while (limite-- > 0 && entrantes.TryDequeue(out KeyValuePair<int, string> msg))
            {
                if (msg.Value == "#CERRADO") { AlCerrarCliente(msg.Key); continue; }
                PaqueteRedTD p = null;
                try { p = JsonUtility.FromJson<PaqueteRedTD>(msg.Value); }
                catch { /* línea corrupta: se ignora */ }
                if (p == null || string.IsNullOrEmpty(p.tipo)) continue;

                if (estado == EstadoConexion.AnfitrionTCP && !ValidarYRetransmitir(p, msg.Key, msg.Value)) continue;
                Procesar(p, msg.Value.Length);
            }

            if (!Conectado) return;

            // Canal simulado: entrega cuando vence la latencia
            if (estado == EstadoConexion.SalaSimulada)
            {
                for (int i = canalSimulado.Count - 1; i >= 0; i--)
                {
                    if (canalSimulado[i].entrega > Time.unscaledTime) continue;
                    PaqueteRedTD p = canalSimulado[i].paquete;
                    canalSimulado.RemoveAt(i);
                    p.secuencia = ++secuencia;
                    Procesar(p, JsonUtility.ToJson(p).Length);
                }
                ActualizarBots();
            }

            // Estado periódico del jugador local
            temporizadorSync -= Time.unscaledDeltaTime;
            if (temporizadorSync <= 0f)
            {
                temporizadorSync = 1f;
                GameManager gm = GameManager.Instance;
                JugadorSala yo = Local();
                if (gm != null && yo != null)
                {
                    yo.oro = gm.oroActual;
                    yo.puntaje = gm.puntaje;
                    yo.vidaBase = gm.vidaBaseActual;
                    yo.oleada = gm.oleadaActual;
                    yo.eliminado = gm.estado == EstadoJuego.GameOver;
                    if (estado != EstadoConexion.SalaSimulada) Enviar(PaqueteEstado());
                    else { paquetesEnviados++; yo.paquetes++; yo.bytesVentana += 180; }
                }
                OnSalaActualizada?.Invoke();
            }

            // Medición de latencia (PING / PONG) en conexiones reales
            temporizadorPing -= Time.unscaledDeltaTime;
            if (temporizadorPing <= 0f)
            {
                temporizadorPing = 2f;
                if (estado == EstadoConexion.AnfitrionTCP || estado == EstadoConexion.ClienteTCP)
                {
                    PaqueteRedTD p = Base(TipoPaquete.PING);
                    p.marcaTiempo = Time.realtimeSinceStartupAsDouble;
                    Enviar(p);
                }
                else
                {
                    foreach (JugadorSala js in datos.Values)
                        if (!js.esLocal) js.ping = Mathf.Clamp(js.ping + Random.Range(-9f, 9f), 18f, 140f);
                }
            }

            // Ancho de banda por jugador (kbps en ventanas de 1 s)
            temporizadorAncho += Time.unscaledDeltaTime;
            if (temporizadorAncho >= 1f)
            {
                foreach (JugadorSala js in datos.Values)
                {
                    js.kbps = js.bytesVentana * 8f / 1000f / temporizadorAncho;
                    js.bytesVentana = 0;
                }
                temporizadorAncho = 0f;
            }
        }

        /// <summary>
        /// Anfitrión: valida la conexión de nuevos clientes y retransmite los comandos al resto.
        /// Devuelve false si el paquete no debe procesarse localmente.
        /// </summary>
        private bool ValidarYRetransmitir(PaqueteRedTD p, int clienteId, string json)
        {
            if (p.tipo == TipoPaquete.CONECTAR_JUGADOR)
            {
                Jugador perfil = null;
                try { perfil = JsonUtility.FromJson<Jugador>(p.perfilJson); } catch { }
                int slot = SlotLibre();
                string motivo = null;
                if (perfil == null) motivo = "Perfil de comandante ilegible.";
                else if (slot < 0) motivo = "La sala está llena (máximo 4 comandantes).";
                else if (salaJugadores.Buscar(perfil.Id) != null) motivo = $"Ya hay un comandante con el ID {perfil.Id} en la sala (clave BST duplicada). Use otro comandante.";

                if (motivo != null)
                {
                    EnviarACliente(clienteId, new PaqueteRedTD { tipo = TipoPaquete.RECHAZO, mensaje = motivo });
                    Log($"✕ Conexión rechazada: {motivo}");
                    return false;
                }

                // 1. Bienvenida con la configuración de la sala
                GameManager gm = GameManager.Instance;
                EnviarACliente(clienteId, new PaqueteRedTD
                {
                    tipo = TipoPaquete.BIENVENIDA, idJugador = perfil.Id, slot = slot, modo = (int)modo,
                    nivel = gm != null ? gm.nivelActual : 0, semilla = gm != null ? gm.semillaPartida : 0,
                    oleada = gm != null ? gm.oleadaActual : 1
                });
                // 2. Se le presentan los integrantes actuales
                foreach (JugadorSala js in Integrantes())
                {
                    EnviarACliente(clienteId, new PaqueteRedTD
                    {
                        tipo = TipoPaquete.CONECTAR_JUGADOR, idJugador = js.perfil.Id, nombreJugador = js.perfil.nombre,
                        perfilJson = JsonUtility.ToJson(js.perfil), slot = js.slot
                    });
                }
                // 3. En cooperativo recibe las torres ya construidas en el mapa compartido
                if (modo == ModoOnline.Cooperativo)
                {
                    foreach (Torre t in Torre.Activas)
                    {
                        if (t == null || t.plot == null) continue;
                        EnviarACliente(clienteId, new PaqueteRedTD
                        {
                            tipo = TipoPaquete.CONSTRUIR_TORRE, idJugador = t.propietarioId, nombreJugador = t.propietarioNombre,
                            plotIndex = t.plot.indice, tipoTorre = (int)t.tipo, cantidad = t.nivel
                        });
                    }
                }

                p.slot = slot;
                JugadorSala nuevo = AgregarIntegrante(perfil, slot, false, false, false);
                if (nuevo != null) nuevo.clienteId = clienteId;
                Difundir(JsonUtility.ToJson(p), clienteId);
                Log($"✓ {perfil.nombre} (ID {perfil.Id}, {perfil.NombreClaseActual}) se unió a la sala en el puesto {slot + 1}.");
                return false;
            }

            if (p.tipo == TipoPaquete.PING)
            {
                // El anfitrión responde directamente al emisor
                EnviarACliente(clienteId, new PaqueteRedTD { tipo = TipoPaquete.PONG, idJugador = p.idJugador, marcaTiempo = p.marcaTiempo });
                return false;
            }

            if (p.tipo == TipoPaquete.PONG)
            {
                JugadorSala js = Buscar(p.idJugador);
                if (js != null) js.ping = (float)((Time.realtimeSinceStartupAsDouble - p.marcaTiempo) * 1000.0);
                return false;
            }

            // Solo se aceptan comandos de integrantes registrados
            if (Buscar(p.idJugador) == null) return false;
            Difundir(json, clienteId);
            return true;
        }

        private void AlCerrarCliente(int clienteId)
        {
            if (estado != EstadoConexion.AnfitrionTCP) return;
            JugadorSala caido = null;
            foreach (JugadorSala js in datos.Values) if (js.clienteId == clienteId) caido = js;
            if (caido == null) return;
            Log($"⏏ {caido.perfil.nombre} se desconectó.");
            PaqueteRedTD p = new PaqueteRedTD { tipo = TipoPaquete.DESCONECTAR, idJugador = caido.perfil.Id, nombreJugador = caido.perfil.nombre };
            QuitarIntegrante(caido.perfil.Id);
            Difundir(JsonUtility.ToJson(p), -1);
            OnSalaActualizada?.Invoke();
        }

        /// <summary>Aplica un comando recibido de otro integrante de la sala.</summary>
        private void Procesar(PaqueteRedTD p, int bytes)
        {
            paquetesRecibidos++;
            GameManager gm = GameManager.Instance;
            JugadorSala autor = Buscar(p.idJugador);
            if (autor != null && !autor.esLocal) { autor.paquetes++; autor.bytesVentana += bytes; }

            switch (p.tipo)
            {
                case TipoPaquete.BIENVENIDA:
                {
                    modo = (ModoOnline)Mathf.Clamp(p.modo, 0, 1);
                    Jugador perfil = PerfilLocal();
                    AgregarIntegrante(perfil, p.slot, true, false, false);
                    if (gm != null)
                    {
                        gm.CargarNivel(p.nivel, false);
                        gm.semillaPartida = p.semilla;
                    }
                    estado = EstadoConexion.ClienteTCP;
                    AlEntrarASala();
                    Log($"✓ Bienvenido a la sala. Puesto {p.slot + 1} · Modo {modo} · Nivel {p.nivel + 1}. El anfitrión inicia las oleadas.");
                    break;
                }
                case TipoPaquete.RECHAZO:
                    Log("✕ El anfitrión rechazó la conexión: " + p.mensaje);
                    Salir(false);
                    break;

                case TipoPaquete.CONECTAR_JUGADOR:
                {
                    if (Buscar(p.idJugador) != null) break; // ya registrado (sala simulada)
                    Jugador perfil = null;
                    try { perfil = JsonUtility.FromJson<Jugador>(p.perfilJson); } catch { }
                    if (perfil == null) break;
                    JugadorSala js = AgregarIntegrante(perfil, p.slot >= 0 ? p.slot : SlotLibre(), false, false, p.slot == 0);
                    if (js != null) Log($"✓ {perfil.nombre} (ID {perfil.Id}) está en la sala.");
                    break;
                }
                case TipoPaquete.DESCONECTAR:
                    if (autor != null && !autor.esLocal)
                    {
                        Log($"⏏ {autor.perfil.nombre} salió de la sala.");
                        QuitarIntegrante(p.idJugador);
                    }
                    break;

                case TipoPaquete.CONSTRUIR_TORRE:
                    if (gm == null || modo != ModoOnline.Cooperativo) break;
                    if (autor != null && autor.esLocal) break;
                    Color col = autor != null ? autor.color : new Color(0.7f, 0.7f, 0.7f);
                    string nombre = autor != null ? autor.perfil.nombre : p.nombreJugador;
                    if (gm.ConstruirTorreRemota(p.plotIndex, (TipoTorre)Mathf.Clamp(p.tipoTorre, 0, Torre.TodosLosTipos.Length - 1), p.idJugador, nombre, col))
                    {
                        PlotTorre plot = gm.ObtenerPlot(p.plotIndex);
                        // 'cantidad' transporta el nivel al sincronizar torres ya existentes
                        for (int n = 1; n < p.cantidad && plot != null && plot.TieneTorre; n++) gm.MejorarTorre(plot.torreInstanciada, true);
                        Log($"⬅ CONSTRUIR_TORRE: {nombre} colocó {Torre.Nombre((TipoTorre)p.tipoTorre)} en la casilla {p.plotIndex}.");
                    }
                    break;

                case TipoPaquete.MEJORAR_TORRE:
                case TipoPaquete.VENDER_TORRE:
                {
                    if (gm == null || modo != ModoOnline.Cooperativo || (autor != null && autor.esLocal)) break;
                    PlotTorre plot = gm.ObtenerPlot(p.plotIndex);
                    if (plot == null || !plot.TieneTorre) break;
                    if (p.tipo == TipoPaquete.MEJORAR_TORRE) gm.MejorarTorre(plot.torreInstanciada, true);
                    else gm.VenderTorre(plot.torreInstanciada, true);
                    Log($"⬅ {p.tipo}: {p.nombreJugador} en la casilla {p.plotIndex}.");
                    break;
                }
                case TipoPaquete.USAR_HABILIDAD_DAG:
                    if (gm == null || (autor != null && autor.esLocal)) break;
                    if (modo == ModoOnline.Cooperativo)
                        gm.EjecutarHabilidadRemota((HabilidadActiva)Mathf.Clamp(p.habilidad, 0, 3), p.nodoA, p.nodoB, p.nombreJugador);
                    Log($"⬅ USAR_HABILIDAD_DAG: {p.nombreJugador} activó {GameManager.NombreHabilidad((HabilidadActiva)Mathf.Clamp(p.habilidad, 0, 3))}.");
                    break;

                case TipoPaquete.ENVIAR_MERCENARIOS_PVP:
                    if (gm == null || modo != ModoOnline.Competitivo || (autor != null && autor.esLocal)) break;
                    gm.RecibirMercenarios((TipoEnemigo)Mathf.Clamp(p.tipoEnemigo, 0, 6), Mathf.Clamp(p.cantidad, 1, 8), p.nombreJugador);
                    Log($"⬅ ENVIAR_MERCENARIOS_PVP: {p.nombreJugador} envió {p.cantidad}× {(TipoEnemigo)p.tipoEnemigo}.");
                    break;

                case TipoPaquete.SINCRONIZAR_ESTADO_OLEADA:
                    if (autor == null || autor.esLocal) break;
                    autor.oro = p.oro;
                    autor.puntaje = p.puntaje;
                    autor.vidaBase = p.vidaBase;
                    autor.oleada = p.oleada;
                    autor.eliminado = p.vidaBase <= 0f;
                    if (gm != null && autor.esAnfitrion && estado == EstadoConexion.ClienteTCP)
                    {
                        // El anfitrión es la autoridad de la sala
                        if (p.iniciarOleada)
                        {
                            gm.IniciarOleadaRemota(p.oleada, p.semilla);
                            Log($"⬅ SINCRONIZAR_ESTADO_OLEADA: inicia la oleada {p.oleada} con semilla {p.semilla}.");
                        }
                        else if (modo == ModoOnline.Cooperativo) gm.AplicarEstadoRemoto(p.oleada, p.vidaBase);
                    }
                    break;

                case TipoPaquete.PING:
                    if (estado == EstadoConexion.ClienteTCP && autor != null && autor.esAnfitrion)
                    {
                        PaqueteRedTD r = Base(TipoPaquete.PONG);
                        r.marcaTiempo = p.marcaTiempo;
                        Enviar(r);
                    }
                    break;

                case TipoPaquete.PONG:
                {
                    // Cliente: el PONG del anfitrión mide la latencia de ida y vuelta
                    float ms = (float)((Time.realtimeSinceStartupAsDouble - p.marcaTiempo) * 1000.0);
                    JugadorSala yo = Local();
                    if (estado == EstadoConexion.ClienteTCP && yo != null && p.idJugador == yo.perfil.Id)
                    {
                        foreach (JugadorSala js in datos.Values) if (js.esAnfitrion) js.ping = ms;
                    }
                    else if (autor != null) autor.ping = ms;
                    break;
                }
            }
        }

        private void AlFinalizarPartida(bool victoria, int total)
        {
            if (!Conectado) return;
            JugadorSala yo = Local();
            if (yo != null) { yo.puntaje = total; yo.eliminado = !victoria; }
            if (estado != EstadoConexion.SalaSimulada) Enviar(PaqueteEstado());

            // Los puntajes de los demás integrantes también quedan en el Ranking
            foreach (JugadorSala js in Integrantes())
            {
                if (js.esLocal || js.puntaje <= 0) continue;
                LeaderboardJSON.Asegurar().RegistrarResultado(js.perfil.nombre, js.puntaje,
                    GameManager.Instance != null ? GameManager.Instance.nivelActual + 1 : 1, js.oleada, !js.eliminado && victoria,
                    js.perfil.NombreClaseActual, js.esBot ? "Sala IA" : "Online");
            }
            Log($"🏁 Partida finalizada. Tu puntaje: {total}.");
        }

        #endregion

        #region Comandantes IA (sala simulada)

        /// <summary>Reacción de los comandantes IA a un paquete del jugador humano.</summary>
        private void ProcesarComoBots(PaqueteRedTD p)
        {
            if (p.tipo != TipoPaquete.ENVIAR_MERCENARIOS_PVP || modo != ModoOnline.Competitivo) return;
            foreach (JugadorSala js in datos.Values)
            {
                if (!js.esBot || js.eliminado) continue;
                // La defensa simulada del rival sufre según la amenaza enviada
                float amenaza = p.tipoEnemigo == (int)TipoEnemigo.Jefe ? 22f : (p.tipoEnemigo == (int)TipoEnemigo.Blindado ? 11f : 6f);
                float danio = amenaza * Random.Range(0.4f, 1.3f);
                js.vidaBase = Mathf.Max(0f, js.vidaBase - danio);
                Log($"⚔️ La defensa de {js.perfil.nombre} recibió {danio:0} de daño por tus mercenarios (base {js.vidaBase:0}).");
                if (js.vidaBase <= 0f)
                {
                    js.eliminado = true;
                    Log($"💥 ¡{js.perfil.nombre} fue eliminado!");
                    if (GameManager.Instance != null) GameManager.Instance.MostrarNotificacion($"💥 ¡Eliminaste a {js.perfil.nombre}!");
                }
            }
        }

        private void ActualizarBots()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.PartidaTerminada) return;
            float dt = Time.deltaTime;                // la IA se detiene con la pausa
            bool enCombate = gm.estado == EstadoJuego.GenerandoOleada || gm.estado == EstadoJuego.OleadaEnCurso;

            // Recompensa por oleada superada para cada IA
            if (gm.oleadasCompletadas > ultimaOleadaVista)
            {
                ultimaOleadaVista = gm.oleadasCompletadas;
                foreach (JugadorSala js in datos.Values)
                {
                    if (!js.esBot || js.eliminado) continue;
                    js.oro += 60 + gm.oleadaActual * 18;
                    js.puntaje += Mathf.RoundToInt(80 * gm.oleadaActual * gm.Nivel.multPuntaje * Random.Range(0.7f, 1.15f));
                    js.oleada = gm.oleadaActual;
                }
            }

            foreach (JugadorSala js in datos.Values)
            {
                if (!js.esBot || js.eliminado) continue;
                if (enCombate)
                {
                    js.oro += Mathf.RoundToInt(Random.value < dt * 2.5f ? 6 : 0);
                    if (Random.value < dt * 0.9f) js.puntaje += Mathf.RoundToInt(12 * gm.Nivel.multPuntaje);
                }

                js.temporizadorIA -= dt;
                if (js.temporizadorIA <= 0f)
                {
                    js.temporizadorIA = Random.Range(2.5f, 6f);
                    if (modo == ModoOnline.Cooperativo) DecidirConstruccionIA(js, gm);
                    else if (enCombate && Random.value < 0.12f + gm.oleadaActual * 0.012f)
                    {
                        // Competitivo: la defensa simulada del rival deja pasar enemigos de vez en cuando
                        js.vidaBase = Mathf.Max(0f, js.vidaBase - Random.Range(4f, 12f));
                        if (js.vidaBase <= 0f) { js.eliminado = true; Log($"💥 {js.perfil.nombre} perdió su base."); }
                    }
                }

                if (modo == ModoOnline.Competitivo && enCombate && gm.oleadaActual >= 2)
                {
                    js.temporizadorMerc -= dt;
                    if (js.temporizadorMerc <= 0f)
                    {
                        js.temporizadorMerc = Random.Range(22f, 40f);
                        TipoEnemigo tipo = gm.oleadaActual >= 6 && Random.value < 0.25f ? TipoEnemigo.Jefe
                            : (gm.oleadaActual >= 3 && Random.value < 0.45f ? TipoEnemigo.Blindado : TipoEnemigo.Corredor);
                        int costo = CostoMercenarios(tipo, out int cantidad, out _);
                        if (js.oro >= costo)
                        {
                            js.oro -= costo;
                            EncolarSimulado(new PaqueteRedTD
                            {
                                tipo = TipoPaquete.ENVIAR_MERCENARIOS_PVP, idJugador = js.perfil.Id, nombreJugador = js.perfil.nombre,
                                tipoEnemigo = (int)tipo, cantidad = cantidad
                            });
                        }
                    }
                }

                if (modo == ModoOnline.Cooperativo && enCombate)
                {
                    js.temporizadorHabilidad -= dt;
                    if (js.temporizadorHabilidad <= 0f && Enemigo.Activos.Count >= 10)
                    {
                        js.temporizadorHabilidad = Random.Range(55f, 90f);
                        EncolarSimulado(new PaqueteRedTD
                        {
                            tipo = TipoPaquete.USAR_HABILIDAD_DAG, idJugador = js.perfil.Id, nombreJugador = js.perfil.nombre,
                            habilidad = (int)(Random.value < 0.5f ? HabilidadActiva.PulsoEMP : HabilidadActiva.Bombardeo)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// IA cooperativa: mejora una torre propia o construye en la casilla libre mejor puntuada
        /// (cerca de la ruta óptima de Dijkstra y dentro del radio de enlace MST de otra torre).
        /// </summary>
        private void DecidirConstruccionIA(JugadorSala js, GameManager gm)
        {
            // 1. ¿Mejorar una torre propia?
            if (Random.value < 0.35f)
            {
                foreach (Torre t in Torre.Activas)
                {
                    if (t == null || t.propietarioId != js.perfil.Id || t.nivel >= Torre.NIVEL_MAXIMO || t.plot == null) continue;
                    if (js.oro < t.CostoMejora) continue;
                    js.oro -= t.CostoMejora;
                    EncolarSimulado(new PaqueteRedTD { tipo = TipoPaquete.MEJORAR_TORRE, idJugador = js.perfil.Id, nombreJugador = js.perfil.nombre, plotIndex = t.plot.indice });
                    return;
                }
            }

            // 2. Elegir tipo de torre asequible según la clase del comandante IA
            List<TipoTorre> opciones = new List<TipoTorre>();
            foreach (TipoTorre tipo in Torre.TodosLosTipos)
            {
                if (Torre.CostoBase(tipo) > js.oro) continue;
                opciones.Add(tipo);
                if (js.perfil.clase == ClaseComandante.Criomante && tipo == TipoTorre.Hielo) opciones.Add(tipo);
                if (js.perfil.clase == ClaseComandante.Artillero && tipo == TipoTorre.Canon) opciones.Add(tipo);
                if (js.perfil.clase == ClaseComandante.IngenieroMST && (tipo == TipoTorre.Radar || tipo == TipoTorre.Tesla)) opciones.Add(tipo);
            }
            if (opciones.Count == 0) return;
            TipoTorre elegido = opciones[Random.Range(0, opciones.Count)];

            // 3. Mejor casilla libre
            List<PathNode> ruta = PathGraph.main != null ? PathGraph.main.ObtenerRutaActual() : null;
            PlotTorre mejor = null;
            float mejorPuntos = float.MinValue;
            for (int i = 0; i < gm.CantidadPlots; i++)
            {
                PlotTorre plot = gm.ObtenerPlot(i);
                if (plot == null || plot.TieneTorre || plot.deshabilitado) continue;
                Vector2 pos = plot.transform.position;
                if (Mathf.Abs(pos.y) > 4.9f || Mathf.Abs(pos.x) > 8.5f) continue; // fuera del área visible

                float distRuta = float.MaxValue;
                if (ruta != null)
                {
                    for (int k = 0; k < ruta.Count - 1; k++)
                        distRuta = Mathf.Min(distRuta, PathGraph.DistanciaPuntoSegmento(pos, ruta[k].transform.position, ruta[k + 1].transform.position));
                }
                float puntos = -distRuta * 2f + Random.Range(0f, 1.5f);
                foreach (Torre t in Torre.Activas)
                {
                    if (t == null) continue;
                    if (Vector2.Distance(pos, t.transform.position) <= RedTorresMST.RADIO_BASE) { puntos += 1.2f; break; }
                }
                if (puntos > mejorPuntos) { mejorPuntos = puntos; mejor = plot; }
            }
            if (mejor == null) return;

            js.oro -= Torre.CostoBase(elegido);
            EncolarSimulado(new PaqueteRedTD
            {
                tipo = TipoPaquete.CONSTRUIR_TORRE, idJugador = js.perfil.Id, nombreJugador = js.perfil.nombre,
                plotIndex = mejor.indice, tipoTorre = (int)elegido, cantidad = 1
            });
        }

        #endregion

        #region Grafo de Topología de Red

        /// <summary>
        /// Representa la sesión como un grafo: vértices = comandantes, aristas = enlaces de red
        /// con la latencia (ms) como peso. TCP cliente-servidor → estrella; sala simulada → malla P2P.
        /// </summary>
        public Grafo<JugadorSala> ConstruirGrafoTopologia()
        {
            Grafo<JugadorSala> g = new Grafo<JugadorSala>(false);
            List<JugadorSala> lista = Integrantes();
            foreach (JugadorSala js in lista) g.AgregarNodo(js.perfil.Id.ToString(), js);

            if (estado == EstadoConexion.SalaSimulada)
            {
                for (int i = 0; i < lista.Count - 1; i++)
                    for (int j = i + 1; j < lista.Count; j++)
                        g.AgregarArista(lista[i].perfil.Id.ToString(), lista[j].perfil.Id.ToString(), Mathf.Max(1f, (lista[i].ping + lista[j].ping) * (lista[i].esLocal || lista[j].esLocal ? 1f : 0.5f)));
            }
            else
            {
                JugadorSala centro = null;
                foreach (JugadorSala js in lista) if (js.esAnfitrion) centro = js;
                if (centro != null)
                {
                    foreach (JugadorSala js in lista)
                        if (js != centro) g.AgregarArista(centro.perfil.Id.ToString(), js.perfil.Id.ToString(), Mathf.Max(1f, js.esLocal ? centro.ping : js.ping));
                }
            }
            return g;
        }

        #endregion
    }
}
