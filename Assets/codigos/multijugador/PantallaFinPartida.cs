using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefenseGameplay
{
    /// <summary>Resultado de una partida LAN (lo arma el host y lo envía al cliente).</summary>
    public class ResumenPartida
    {
        public RolJugador ganador;
        public string nombreAtacante;
        public string nombreDefensor;
        public int rondasSuperadas;
        public int totalRondas;
        public float vidaBaseRestante;
        public int danioTotal;
        public int enemigosEnviados;
        public int enemigosQueLlegaron;
        public int torresConstruidas;

        public static ResumenPartida CrearDesdeJuego(RolJugador ganador)
        {
            GameManager gm = GameManager.Instance;
            ControladorAtacante atk = ControladorAtacante.Instance;

            ResumenPartida r = new ResumenPartida
            {
                ganador = ganador,
                nombreAtacante = ModoJuego.NombreAtacante,
                nombreDefensor = ModoJuego.NombreDefensor
            };

            if (gm != null)
            {
                r.totalRondas = gm.totalOleadas;
                r.rondasSuperadas = ganador == RolJugador.Defensor ? gm.totalOleadas : Mathf.Max(0, gm.oleadaActual - 1);
                r.vidaBaseRestante = gm.vidaBaseActual;
                foreach (PlotTorre p in gm.Plots)
                {
                    if (p != null && p.TieneTorre) r.torresConstruidas++;
                }
            }

            if (atk != null)
            {
                r.danioTotal = atk.danioTotalBase;
                r.enemigosEnviados = atk.enemigosEnviados;
                r.enemigosQueLlegaron = atk.enemigosQueLlegaron;
            }
            return r;
        }

        public void Escribir(BinaryWriter w)
        {
            w.Write((byte)ganador);
            w.Write(nombreAtacante ?? "");
            w.Write(nombreDefensor ?? "");
            w.Write(rondasSuperadas);
            w.Write(totalRondas);
            w.Write(vidaBaseRestante);
            w.Write(danioTotal);
            w.Write(enemigosEnviados);
            w.Write(enemigosQueLlegaron);
            w.Write(torresConstruidas);
        }

        public static ResumenPartida Leer(BinaryReader r)
        {
            return new ResumenPartida
            {
                ganador = (RolJugador)r.ReadByte(),
                nombreAtacante = r.ReadString(),
                nombreDefensor = r.ReadString(),
                rondasSuperadas = r.ReadInt32(),
                totalRondas = r.ReadInt32(),
                vidaBaseRestante = r.ReadSingle(),
                danioTotal = r.ReadInt32(),
                enemigosEnviados = r.ReadInt32(),
                enemigosQueLlegaron = r.ReadInt32(),
                torresConstruidas = r.ReadInt32()
            };
        }

        /// <summary>
        /// Puntaje para el leaderboard.
        /// Defensor: 100 por ronda superada + 5 por punto de vida restante + 500 si gana.
        /// Atacante: 10 por punto de daño a la base + 20 por enemigo que llegó + 500 si gana.
        /// </summary>
        public int PuntajePara(RolJugador rol)
        {
            bool gano = ganador == rol;
            if (rol == RolJugador.Defensor)
            {
                return rondasSuperadas * 100 + Mathf.RoundToInt(vidaBaseRestante) * 5 + (gano ? 500 : 0);
            }
            return danioTotal * 10 + enemigosQueLlegaron * 20 + (gano ? 500 : 0);
        }
    }

    /// <summary>
    /// Pantalla final de la partida LAN: ganador, estadísticas, historial del comandante (BST)
    /// y top del leaderboard. Al mostrarse guarda el resultado del comandante de este PC.
    /// </summary>
    public class PantallaFinPartida : MonoBehaviour
    {
        private static PantallaFinPartida instancia;

        public static void Mostrar(ResumenPartida resumen)
        {
            if (instancia != null || resumen == null) return;

            // Guardar el resultado del comandante de ESTE PC (cada PC guarda el suyo)
            RolJugador rolLocal = ModoJuego.RolLocal;
            bool gano = resumen.ganador == rolLocal;
            Jugador comandante = RepositorioComandantes.RegistrarResultado(ModoJuego.IdLocal, ModoJuego.NombreLocal, gano);
            int puntaje = resumen.PuntajePara(rolLocal);
            List<EntradaLeaderboard> top = LeaderboardJSON.AgregarPuntajeGlobal($"{ModoJuego.NombreLocal} ({ModoJuego.NombreRol(rolLocal)})", puntaje, gano, resumen.rondasSuperadas);

            string titulo = gano ? "<color=#FFDD44>🏆 ¡GANASTE!</color>" : "<color=#FF6666>💀 PERDISTE</color>";
            string nombreGanador = resumen.ganador == RolJugador.Atacante ? resumen.nombreAtacante : resumen.nombreDefensor;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Gana el <b>{ModoJuego.NombreRol(resumen.ganador)}</b>: {nombreGanador}");
            sb.AppendLine();
            sb.AppendLine($"🛡️ Defensor: {resumen.nombreDefensor}   ·   ⚔️ Atacante: {resumen.nombreAtacante}");
            sb.AppendLine($"Rondas superadas: {resumen.rondasSuperadas}/{resumen.totalRondas}   ·   Vida final de la base: {resumen.vidaBaseRestante:0}");
            sb.AppendLine($"Tropas enviadas: {resumen.enemigosEnviados}   ·   Llegaron a la base: {resumen.enemigosQueLlegaron}   ·   Daño total: {resumen.danioTotal}");
            sb.AppendLine($"Torres en el mapa: {resumen.torresConstruidas}");
            sb.AppendLine();
            sb.AppendLine($"Tu puntaje: <color=#FFDD44><b>{puntaje}</b></color>");
            if (comandante != null)
            {
                sb.AppendLine($"Historial de {comandante.nombre} (ID {comandante.Id}) en el BST: " +
                              $"<color=#66FF88>{comandante.victoriasMultijugador} victorias</color> / " +
                              $"<color=#FF8888>{comandante.derrotasMultijugador} derrotas</color>");
            }
            sb.AppendLine();
            sb.AppendLine("<b>🏅 Leaderboard</b>");
            for (int i = 0; i < top.Count && i < 5; i++)
            {
                sb.AppendLine($"{i + 1}. {top[i].nombre} — {top[i].puntaje}");
            }

            Crear(titulo, sb.ToString());
        }

        public static void MostrarDesconexion(string motivo)
        {
            if (instancia != null) return;
            Crear("<color=#FFAA44>🔌 Conexión perdida</color>",
                  (string.IsNullOrEmpty(motivo) ? "Se perdió la conexión con el otro jugador." : motivo) +
                  "\n\nLa partida no se registra en el historial.");
        }

        private static void Crear(string titulo, string cuerpo)
        {
            GameObject go = new GameObject("PantallaFinPartida");
            instancia = go.AddComponent<PantallaFinPartida>();

            Canvas canvas = UIFabricaLAN.CrearCanvas("Canvas_FinPartida", 300);
            canvas.transform.SetParent(go.transform, false);

            RectTransform fondo = UIFabricaLAN.CrearPanel(canvas.transform, "Oscurecer", new Color(0f, 0f, 0f, 0.7f));
            UIFabricaLAN.Estirar(fondo);

            RectTransform panel = UIFabricaLAN.CrearPanel(fondo, "Panel", new Color(0.07f, 0.09f, 0.14f, 0.98f));
            UIFabricaLAN.Anclar(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640));
            Outline borde = panel.gameObject.AddComponent<Outline>();
            borde.effectColor = new Color(0.4f, 0.7f, 1f, 0.8f);
            borde.effectDistance = new Vector2(2, -2);
            UIFabricaLAN.Vertical(panel, 12, new RectOffset(36, 36, 28, 28));

            Text txtTitulo = UIFabricaLAN.CrearTexto(panel, "Titulo", titulo, 40, Color.white, TextAnchor.MiddleCenter);
            UIFabricaLAN.Tamano(txtTitulo, -1, 60);

            Text txtCuerpo = UIFabricaLAN.CrearTexto(panel, "Cuerpo", cuerpo, 19, new Color(0.9f, 0.92f, 1f), TextAnchor.UpperCenter, FontStyle.Normal);
            UIFabricaLAN.Tamano(txtCuerpo, -1, 440);

            Button volver = UIFabricaLAN.CrearBoton(panel, "BtnVolver", "← Volver al menú", new Color(0.2f, 0.45f, 0.75f), 20);
            UIFabricaLAN.Tamano(volver, -1, 54);
            volver.onClick.AddListener(VolverAlMenu);
        }

        public static void VolverAlMenu()
        {
            if (RedLAN.Instancia != null) RedLAN.Instancia.Detener();
            ModoJuego.ConfigurarUnJugador();
            SceneManager.LoadScene("MainMenu");
        }

        private void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }
    }
}
