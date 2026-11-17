using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Conexión LAN entre los dos PCs usando Netcode for GameObjects + Unity Transport.
    ///
    /// No se usan NetworkObjects: todo el juego crea sus objetos por código, así que la sincronización
    /// se hace con mensajes propios ("named messages" de NGO) que llevan bytes armados por Protocolo.cs.
    ///  - Host: escucha en el puerto 7777 de todas las interfaces y juega a la vez (StartHost).
    ///  - Cliente: se conecta a la IP del host (StartClient).
    /// El objeto persiste entre escenas (menú → juego).
    /// </summary>
    public class RedLAN : MonoBehaviour
    {
        public static RedLAN Instancia { get; private set; }

        public const ushort PuertoJuego = 7777;
        private const string Canal = "TD_LAN";

        private NetworkManager nm;
        private UnityTransport transporte;
        private ulong idRemoto = ulong.MaxValue;
        private bool canalRegistrado = false;

        // Receptores de mensajes. Si llega un mensaje sin nadie escuchando (por ejemplo, mientras
        // se carga la escena) se guarda y se entrega al primer receptor que se suscriba.
        private readonly List<Action<byte[]>> receptores = new List<Action<byte[]>>();
        private readonly Queue<byte[]> pendientes = new Queue<byte[]>();

        public event Action AlConectarRival;
        public event Action<string> AlDesconectar;

        public bool Activa => nm != null && (nm.IsListening || nm.IsClient || nm.IsServer);
        public bool EsHost => nm != null && nm.IsServer;
        public bool RivalConectado { get; private set; }

        /// <summary>Crea (una sola vez) el objeto de red persistente.</summary>
        public static RedLAN Asegurar()
        {
            if (Instancia != null) return Instancia;

            GameObject go = new GameObject("[RedLAN]");
            DontDestroyOnLoad(go);
            UnityTransport t = go.AddComponent<UnityTransport>();
            NetworkManager m = go.AddComponent<NetworkManager>();
            RedLAN red = go.AddComponent<RedLAN>();
            red.Inicializar(m, t);
            return red;
        }

        private void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            Instancia = this;
        }

        private void Inicializar(NetworkManager m, UnityTransport t)
        {
            nm = m;
            transporte = t;

            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transporte;
            nm.NetworkConfig.EnableSceneManagement = false; // cada PC carga sus escenas
            nm.NetworkConfig.ConnectionApproval = false;

            // Si la IP está mal, rendirse en ~5 s en vez de los 60 s por defecto
            transporte.MaxConnectAttempts = 10;
            transporte.ConnectTimeoutMS = 500;

            nm.OnClientConnectedCallback += AlConectarCliente;
            nm.OnClientDisconnectCallback += AlDesconectarCliente;
        }

        #region Iniciar / detener

        public bool IniciarHost()
        {
            Detener();
            transporte.SetConnectionData("127.0.0.1", PuertoJuego, "0.0.0.0");

            bool ok;
            try
            {
                ok = nm.StartHost();
            }
            catch (Exception ex)
            {
                Debug.LogError("[RedLAN] No se pudo crear la partida: " + ex.Message);
                ok = false;
            }

            if (ok) RegistrarCanal();
            return ok;
        }

        public bool IniciarCliente(string ipHost)
        {
            Detener();
            transporte.SetConnectionData(ipHost, PuertoJuego);

            bool ok;
            try
            {
                ok = nm.StartClient();
            }
            catch (Exception ex)
            {
                Debug.LogError("[RedLAN] No se pudo iniciar la conexión: " + ex.Message);
                ok = false;
            }

            if (ok) RegistrarCanal();
            return ok;
        }

        public void Detener()
        {
            if (nm != null && (nm.IsListening || nm.IsClient || nm.IsServer))
            {
                try
                {
                    if (canalRegistrado && nm.CustomMessagingManager != null)
                    {
                        nm.CustomMessagingManager.UnregisterNamedMessageHandler(Canal);
                    }
                }
                catch (Exception)
                {
                    // El manejador ya pudo haberse liberado al cerrar la conexión
                }
                nm.Shutdown();
            }

            canalRegistrado = false;
            RivalConectado = false;
            idRemoto = ulong.MaxValue;
            pendientes.Clear();
        }

        private void RegistrarCanal()
        {
            if (nm.CustomMessagingManager == null) return;
            nm.CustomMessagingManager.RegisterNamedMessageHandler(Canal, RecibirMensaje);
            canalRegistrado = true;
        }

        private void OnApplicationQuit()
        {
            Detener();
        }

        private void OnDestroy()
        {
            if (Instancia == this)
            {
                Detener();
                Instancia = null;
            }
        }

        #endregion

        #region Conexión

        private void AlConectarCliente(ulong idCliente)
        {
            if (nm.IsServer)
            {
                if (idCliente == NetworkManager.ServerClientId) return; // el propio host

                if (RivalConectado)
                {
                    // Solo se admiten dos jugadores
                    nm.DisconnectClient(idCliente);
                    return;
                }

                idRemoto = idCliente;
                RivalConectado = true;
                AlConectarRival?.Invoke();
            }
            else if (idCliente == nm.LocalClientId)
            {
                RivalConectado = true;
                AlConectarRival?.Invoke();
            }
        }

        private void AlDesconectarCliente(ulong idCliente)
        {
            if (nm.IsServer)
            {
                if (idCliente != idRemoto) return;
                RivalConectado = false;
                idRemoto = ulong.MaxValue;
                AlDesconectar?.Invoke("El otro jugador se desconectó.");
            }
            else
            {
                RivalConectado = false;
                string motivo = string.IsNullOrEmpty(nm.DisconnectReason)
                    ? "No se pudo conectar o se perdió la conexión con el host."
                    : nm.DisconnectReason;
                AlDesconectar?.Invoke(motivo);
            }
        }

        #endregion

        #region Mensajes

        public void Suscribir(Action<byte[]> receptor)
        {
            if (receptor == null || receptores.Contains(receptor)) return;
            receptores.Add(receptor);

            while (pendientes.Count > 0)
            {
                Entregar(pendientes.Dequeue());
            }
        }

        public void Desuscribir(Action<byte[]> receptor)
        {
            receptores.Remove(receptor);
        }

        private void RecibirMensaje(ulong emisor, FastBufferReader lector)
        {
            lector.ReadValueSafe(out int largo);
            if (largo <= 0 || largo > 1024 * 1024) return;

            byte[] datos = new byte[largo];
            lector.ReadBytesSafe(ref datos, largo);

            if (receptores.Count == 0)
            {
                pendientes.Enqueue(datos);
                return;
            }
            Entregar(datos);
        }

        private void Entregar(byte[] datos)
        {
            // Copia por si un receptor se desuscribe (cambio de escena) mientras se entrega
            Action<byte[]>[] copia = receptores.ToArray();
            for (int i = 0; i < copia.Length; i++)
            {
                try
                {
                    copia[i](datos);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>Envía un mensaje al otro PC (host → cliente o cliente → host).</summary>
        public void Enviar(byte[] datos)
        {
            if (datos == null || nm == null || nm.CustomMessagingManager == null || !Activa) return;

            ulong destino;
            if (nm.IsServer)
            {
                if (!RivalConectado) return;
                destino = idRemoto;
            }
            else
            {
                destino = NetworkManager.ServerClientId;
            }

            FastBufferWriter escritor = new FastBufferWriter(datos.Length + 8, Allocator.Temp);
            try
            {
                escritor.WriteValueSafe(datos.Length);
                escritor.WriteBytesSafe(datos, datos.Length);
                nm.CustomMessagingManager.SendNamedMessage(Canal, destino, escritor, NetworkDelivery.ReliableFragmentedSequenced);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RedLAN] No se pudo enviar el mensaje: " + ex.Message);
            }
            finally
            {
                escritor.Dispose();
            }
        }

        public void Enviar(EscritorMensaje mensaje)
        {
            if (mensaje != null) Enviar(mensaje.Datos());
        }

        #endregion
    }
}
