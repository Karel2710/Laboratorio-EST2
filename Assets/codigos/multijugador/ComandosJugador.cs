using GrafosEST2;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Punto único por donde pasan TODAS las acciones de los jugadores.
    ///  - Un jugador / host LAN: la acción se ejecuta de inmediato (este PC tiene autoridad).
    ///  - Cliente LAN: la acción se envía al host como mensaje; el host la valida y la ejecuta.
    /// Así las reglas (costos, prerrequisitos del DAG, ventana de envío) se validan en un solo lugar.
    /// </summary>
    public static class ComandosJugador
    {
        #region Acciones del defensor

        public static void ConstruirTorre(PlotTorre plot, TipoTorre tipo)
        {
            if (plot == null || !ModoJuego.EsDefensorLocal) return;

            if (ModoJuego.TieneAutoridad)
            {
                plot.ConstruirTorre(tipo);
                return;
            }

            // Cliente: validación rápida para dar respuesta inmediata (el host vuelve a validar)
            if (plot.TieneTorre)
            {
                GameManager.Instance?.MostrarNotificacionLocal($"Esta casilla ya contiene una torre {plot.torreInstanciada.tipo}.");
                return;
            }
            EnviarAlHost(TipoMensaje.CmdConstruir, w =>
            {
                w.Write((short)plot.indiceRed);
                w.Write((byte)tipo);
            });
        }

        public static void IniciarOleadaYa()
        {
            if (!ModoJuego.EsDefensorLocal) return;
            if (ModoJuego.TieneAutoridad)
            {
                GameManager.Instance?.IniciarOleada();
                return;
            }
            EnviarAlHost(TipoMensaje.CmdIniciarOleada, null);
        }

        public static void ActivarSobrecarga()
        {
            if (!ModoJuego.EsDefensorLocal) return;
            if (ModoJuego.TieneAutoridad)
            {
                GameManager.Instance?.ActivarSobrecargaRed();
                return;
            }
            EnviarAlHost(TipoMensaje.CmdSobrecarga, null);
        }

        #endregion

        #region Acciones de ambos roles

        public static void DesbloquearHabilidad(RolJugador arbol, string idHabilidad)
        {
            if (ModoJuego.EsMultijugador && arbol != ModoJuego.RolLocal) return;

            if (ModoJuego.TieneAutoridad)
            {
                EjecutarDesbloqueo(arbol, idHabilidad);
                return;
            }
            EnviarAlHost(TipoMensaje.CmdDesbloquear, w =>
            {
                w.Write((byte)arbol);
                w.Write(idHabilidad ?? string.Empty);
            });
        }

        #endregion

        #region Acciones del atacante

        public static void EnviarEnemigos(TipoEnemigo tipo, int ruta, int cantidad)
        {
            if (!ModoJuego.EsAtacanteLocal) return;

            if (ModoJuego.TieneAutoridad)
            {
                ControladorAtacante.Instance?.SolicitarEnvio(tipo, ruta, cantidad);
                return;
            }
            EnviarAlHost(TipoMensaje.CmdEnviar, w =>
            {
                w.Write((byte)tipo);
                w.Write((byte)ruta);
                w.Write((byte)Mathf.Clamp(cantidad, 1, 10));
            });
        }

        public static void UsarHabilidadAtaque(int indice, Vector2 punto)
        {
            if (!ModoJuego.EsAtacanteLocal) return;

            if (ModoJuego.TieneAutoridad)
            {
                ControladorAtacante.Instance?.SolicitarHabilidad(indice, punto);
                return;
            }
            EnviarAlHost(TipoMensaje.CmdHabilidad, w =>
            {
                w.Write((byte)indice);
                w.Write(punto.x);
                w.Write(punto.y);
            });
        }

        #endregion

        #region Ejecución en el host

        private static void EnviarAlHost(TipoMensaje tipo, System.Action<System.IO.BinaryWriter> escribir)
        {
            if (RedLAN.Instancia == null)
            {
                GameManager.Instance?.MostrarNotificacionLocal("⚠️ Sin conexión con el host.");
                return;
            }
            EscritorMensaje m = new EscritorMensaje(tipo);
            escribir?.Invoke(m.W);
            RedLAN.Instancia.Enviar(m);
        }

        private static void EjecutarDesbloqueo(RolJugador arbol, string idHabilidad)
        {
            ControladorHabilidadesUI ctrl = arbol == RolJugador.Atacante
                ? ControladorHabilidadesUI.InstanciaAtaque
                : ControladorHabilidadesUI.Instance;
            if (ctrl == null) return;
            ctrl.DesbloquearAutoritativo(idHabilidad, out _);
        }

        /// <summary>
        /// Host: ejecuta un comando que llegó del cliente. El rol del remitente es fijo
        /// (el que se asignó en la sala), así el cliente no puede actuar por el otro jugador.
        /// </summary>
        public static void EjecutarComandoRemoto(LectorMensaje m, RolJugador rolRemitente)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            switch (m.Tipo)
            {
                case TipoMensaje.CmdConstruir:
                {
                    int indice = m.R.ReadInt16();
                    TipoTorre tipo = (TipoTorre)m.R.ReadByte();
                    if (rolRemitente != RolJugador.Defensor) return;
                    PlotTorre plot = gm.ObtenerPlotPorIndice(indice);
                    if (plot != null && plot.gameObject.activeInHierarchy)
                    {
                        plot.ConstruirTorre(tipo);
                    }
                    break;
                }

                case TipoMensaje.CmdIniciarOleada:
                    if (rolRemitente == RolJugador.Defensor) gm.IniciarOleada();
                    break;

                case TipoMensaje.CmdSobrecarga:
                    if (rolRemitente == RolJugador.Defensor) gm.ActivarSobrecargaRed();
                    break;

                case TipoMensaje.CmdDesbloquear:
                {
                    RolJugador arbol = (RolJugador)m.R.ReadByte();
                    string id = m.R.ReadString();
                    if (arbol == rolRemitente) EjecutarDesbloqueo(arbol, id);
                    break;
                }

                case TipoMensaje.CmdEnviar:
                {
                    TipoEnemigo tipo = (TipoEnemigo)m.R.ReadByte();
                    int ruta = m.R.ReadByte();
                    int cantidad = m.R.ReadByte();
                    if (rolRemitente == RolJugador.Atacante && ControladorAtacante.Instance != null)
                    {
                        ControladorAtacante.Instance.SolicitarEnvio(tipo, ruta, cantidad);
                    }
                    break;
                }

                case TipoMensaje.CmdHabilidad:
                {
                    int indice = m.R.ReadByte();
                    float x = m.R.ReadSingle();
                    float y = m.R.ReadSingle();
                    if (rolRemitente == RolJugador.Atacante && ControladorAtacante.Instance != null)
                    {
                        ControladorAtacante.Instance.SolicitarHabilidad(indice, new Vector2(x, y));
                    }
                    break;
                }
            }
        }

        #endregion
    }
}
