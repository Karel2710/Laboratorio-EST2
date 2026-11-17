using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace TowerDefenseGameplay
{
    public class PartidaEncontrada
    {
        public string ip;
        public string nombreHost;
        public RolJugador rolHost;
        public float ultimaVez;
    }

    /// <summary>
    /// Búsqueda automática de partidas en la red local por broadcast UDP (puerto 47777).
    ///  - El host responde a las preguntas "TDLAN?1" con su nombre y su rol.
    ///  - El cliente pregunta cada segundo y arma la lista de partidas abiertas.
    /// Si la red bloquea el broadcast (firewall, red de la universidad), queda la opción de escribir la IP.
    /// </summary>
    public class DescubrimientoLAN : MonoBehaviour
    {
        public const int PuertoDescubrimiento = 47777;
        private const string Pregunta = "TDLAN?" + "1";
        private const string PrefijoRespuesta = "TDLAN!1|";

        private UdpClient udpHost;
        private UdpClient udpBusqueda;
        private string nombreAnuncio = "";
        private RolJugador rolAnuncio = RolJugador.Defensor;
        private float proximaPregunta = 0f;

        public readonly Dictionary<string, PartidaEncontrada> Partidas = new Dictionary<string, PartidaEncontrada>();
        public event Action AlCambiarLista;

        public bool Anunciando => udpHost != null;
        public bool Buscando => udpBusqueda != null;

        #region Host: anunciar partida

        public bool IniciarAnuncio(string nombre, RolJugador rolHost)
        {
            DetenerAnuncio();
            try
            {
                udpHost = new UdpClient();
                udpHost.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpHost.Client.Bind(new IPEndPoint(IPAddress.Any, PuertoDescubrimiento));
                udpHost.EnableBroadcast = true;
                DesactivarResetUdp(udpHost);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DescubrimientoLAN] No se pudo abrir el puerto de anuncio: " + ex.Message);
                CerrarSeguro(ref udpHost);
                return false;
            }

            nombreAnuncio = (nombre ?? "Host").Replace("|", "/");
            rolAnuncio = rolHost;
            return true;
        }

        public void DetenerAnuncio()
        {
            CerrarSeguro(ref udpHost);
        }

        private void ResponderPreguntas()
        {
            if (udpHost == null) return;
            try
            {
                while (udpHost.Available > 0)
                {
                    IPEndPoint origen = new IPEndPoint(IPAddress.Any, 0);
                    byte[] datos = udpHost.Receive(ref origen);
                    if (Encoding.UTF8.GetString(datos) != Pregunta) continue;

                    byte[] respuesta = Encoding.UTF8.GetBytes(PrefijoRespuesta + nombreAnuncio + "|" + (int)rolAnuncio);
                    udpHost.Send(respuesta, respuesta.Length, origen);
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        #endregion

        #region Cliente: buscar partidas

        public void IniciarBusqueda()
        {
            DetenerBusqueda();
            try
            {
                udpBusqueda = new UdpClient(0);
                udpBusqueda.EnableBroadcast = true;
                DesactivarResetUdp(udpBusqueda);
                proximaPregunta = 0f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DescubrimientoLAN] No se pudo iniciar la búsqueda: " + ex.Message);
                CerrarSeguro(ref udpBusqueda);
            }
        }

        public void DetenerBusqueda()
        {
            CerrarSeguro(ref udpBusqueda);
            if (Partidas.Count > 0)
            {
                Partidas.Clear();
                AlCambiarLista?.Invoke();
            }
        }

        private void Buscar()
        {
            if (udpBusqueda == null) return;

            if (Time.unscaledTime >= proximaPregunta)
            {
                proximaPregunta = Time.unscaledTime + 1f;
                byte[] pregunta = Encoding.UTF8.GetBytes(Pregunta);
                foreach (IPAddress destino in DireccionesDeBroadcast())
                {
                    try
                    {
                        udpBusqueda.Send(pregunta, pregunta.Length, new IPEndPoint(destino, PuertoDescubrimiento));
                    }
                    catch (Exception) { }
                }
            }

            bool cambio = false;
            try
            {
                while (udpBusqueda.Available > 0)
                {
                    IPEndPoint origen = new IPEndPoint(IPAddress.Any, 0);
                    byte[] datos = udpBusqueda.Receive(ref origen);
                    string texto = Encoding.UTF8.GetString(datos);
                    if (!texto.StartsWith(PrefijoRespuesta)) continue;

                    string[] partes = texto.Substring(PrefijoRespuesta.Length).Split('|');
                    if (partes.Length < 2) continue;

                    string ip = origen.Address.ToString();
                    if (!Partidas.TryGetValue(ip, out PartidaEncontrada p))
                    {
                        p = new PartidaEncontrada { ip = ip };
                        Partidas[ip] = p;
                        cambio = true;
                    }
                    p.nombreHost = partes[0];
                    p.rolHost = partes[1] == "1" ? RolJugador.Atacante : RolJugador.Defensor;
                    p.ultimaVez = Time.unscaledTime;
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }

            // Quitar partidas que dejaron de responder y duplicados por loopback (mismo PC)
            List<string> quitar = new List<string>();
            foreach (var kv in Partidas)
            {
                if (Time.unscaledTime - kv.Value.ultimaVez > 3.5f) quitar.Add(kv.Key);
            }
            if (Partidas.ContainsKey("127.0.0.1") && Partidas.Count > 1)
            {
                foreach (var kv in Partidas)
                {
                    if (kv.Key != "127.0.0.1" && kv.Value.nombreHost == Partidas["127.0.0.1"].nombreHost)
                    {
                        quitar.Add("127.0.0.1");
                        break;
                    }
                }
            }
            for (int i = 0; i < quitar.Count; i++)
            {
                if (Partidas.Remove(quitar[i])) cambio = true;
            }

            if (cambio) AlCambiarLista?.Invoke();
        }

        #endregion

        private void Update()
        {
            ResponderPreguntas();
            Buscar();
        }

        private void OnDestroy()
        {
            DetenerAnuncio();
            CerrarSeguro(ref udpBusqueda);
        }

        #region Utilidades de red

        private static void CerrarSeguro(ref UdpClient udp)
        {
            if (udp == null) return;
            try { udp.Close(); } catch (Exception) { }
            udp = null;
        }

        /// <summary>
        /// En Windows, un "puerto inalcanzable" (ICMP) hace que el siguiente Receive lance excepción.
        /// Esta opción lo desactiva para que la búsqueda no se corte.
        /// </summary>
        private static void DesactivarResetUdp(UdpClient udp)
        {
            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch (Exception) { }
        }

        private static List<IPAddress> DireccionesDeBroadcast()
        {
            List<IPAddress> lista = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        try
                        {
                            if (ua.IPv4Mask == null) continue;
                            byte[] ip = ua.Address.GetAddressBytes();
                            byte[] mascara = ua.IPv4Mask.GetAddressBytes();
                            byte[] broadcast = new byte[4];
                            for (int i = 0; i < 4; i++) broadcast[i] = (byte)(ip[i] | ~mascara[i]);
                            IPAddress dir = new IPAddress(broadcast);
                            if (!lista.Contains(dir)) lista.Add(dir);
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }
            return lista;
        }

        /// <summary>IPv4 de este PC (para mostrarle al host qué IP debe dictar).</summary>
        public static List<string> IPsLocales()
        {
            List<string> ips = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254.")) continue; // sin DHCP
                        if (!ips.Contains(ip)) ips.Add(ip);
                    }
                }
            }
            catch (Exception) { }
            if (ips.Count == 0) ips.Add("127.0.0.1");
            return ips;
        }

        #endregion
    }
}
