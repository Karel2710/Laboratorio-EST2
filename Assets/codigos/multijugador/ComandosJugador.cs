using GrafosEST2;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Punto único por donde pasan las acciones de los jugadores en multijugador LAN.
    ///  - Un jugador / host LAN: la acción se ejecuta de inmediato (este PC tiene autoridad).
    ///  - Cliente LAN: la acción se envía al host como mensaje; el host la valida y la ejecuta.
    /// Así las reglas (costos, prerrequisitos del DAG, ventana de envío) se validan en un solo lugar.
    ///
    /// Las acciones del defensor (construir, mejorar, vender, prioridad, habilidades) las inicia
    /// GameManager: en el cliente remoto valida lo básico y delega aquí el envío al host.
    /// </summary>
    public static class ComandosJugador
    {
        #region Acciones del defensor (cliente → host)

        public static void ConstruirTorre(int indicePlot, TipoTorre tipo)
        {
            if (!ModoJuego.EsDefensorLocal) return;
            if (ModoJuego.TieneAutoridad)
            {
                GameManager gm = GameManager.Instance;
                if (gm != null) gm.IntentarConstruir(gm.ObtenerPlot(indicePlot), tipo);
                return;
            }
            EnviarAlHost(TipoMensaje.CmdConstruir, w =>
            {
                w.Write((short)indicePlot);
                w.Write((byte)tipo);
            });
        }

        public static void MejorarTorre(int indicePlot)
        {
            if (!ModoJuego.EsClienteRemoto || !ModoJuego.EsDefensorLocal) return;
            EnviarAlHost(TipoMensaje.CmdMejorar, w => w.Write((short)indicePlot));
        }

        public static void VenderTorre(int indicePlot)
        {
            if (!ModoJuego.EsClienteRemoto || !ModoJuego.EsDefensorLocal) return;
            EnviarAlHost(TipoMensaje.CmdVender, w => w.Write((short)indicePlot));
        }

        public static void CambiarPrioridad(int indicePlot, PrioridadObjetivo prioridad)
        {
            if (!ModoJuego.EsClienteRemoto || !ModoJuego.EsDefensorLocal) return;
            EnviarAlHost(TipoMensaje.CmdPrioridad, w =>
            {
                w.Write((short)indicePlot);
                w.Write((byte)prioridad);
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

        /// <summary>Habilidad activa del defensor (Sobrecarga, Barricada con su arista, Bombardeo, EMP).</summary>
        public static void UsarHabilidadDefensa(HabilidadActiva h, int nodoA, int nodoB)
        {
            if (!ModoJuego.EsDefensorLocal) return;
            if (ModoJuego.TieneAutoridad)
            {
                GameManager.Instance?.EjecutarHabilidadPorComando(h, nodoA, nodoB);
                return;
            }
            EnviarAlHost(TipoMensaje.CmdHabilidadDefensa, w =>
            {
                w.Write((byte)h);
                w.Write((short)nodoA);
                w.Write((short)nodoB);
            });
        }

        /// <summary>Compatibilidad con el botón de sobrecarga del HUD original.</summary>
        public static void ActivarSobrecarga()
        {
            UsarHabilidadDefensa(HabilidadActiva.Sobrecarga, -1, -1);
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
            bool defensor = rolRemitente == RolJugador.Defensor;

            switch (m.Tipo)
            {
                case TipoMensaje.CmdConstruir:
                {
                    int indice = m.R.ReadInt16();
                    TipoTorre tipo = (TipoTorre)m.R.ReadByte();
                    PlotTorre plot = gm.ObtenerPlot(indice);
                    if (defensor && plot != null && plot.gameObject.activeInHierarchy) gm.IntentarConstruir(plot, tipo);
                    break;
                }

                case TipoMensaje.CmdMejorar:
                {
                    PlotTorre plot = gm.ObtenerPlot(m.R.ReadInt16());
                    if (defensor && plot != null && plot.TieneTorre) gm.MejorarTorre(plot.torreInstanciada);
                    break;
                }

                case TipoMensaje.CmdVender:
                {
                    PlotTorre plot = gm.ObtenerPlot(m.R.ReadInt16());
                    if (defensor && plot != null && plot.TieneTorre) gm.VenderTorre(plot.torreInstanciada);
                    break;
                }

                case TipoMensaje.CmdPrioridad:
                {
                    PlotTorre plot = gm.ObtenerPlot(m.R.ReadInt16());
                    int prioridad = m.R.ReadByte();
                    int total = System.Enum.GetValues(typeof(PrioridadObjetivo)).Length;
                    if (defensor && plot != null && plot.TieneTorre && prioridad >= 0 && prioridad < total)
                        plot.torreInstanciada.prioridad = (PrioridadObjetivo)prioridad;
                    break;
                }

                case TipoMensaje.CmdIniciarOleada:
                    if (defensor) gm.IniciarOleada();
                    break;

                case TipoMensaje.CmdHabilidadDefensa:
                {
                    int h = m.R.ReadByte();
                    int a = m.R.ReadInt16();
                    int b = m.R.ReadInt16();
                    if (defensor && h >= 0 && h <= (int)HabilidadActiva.PulsoEMP)
                        gm.EjecutarHabilidadPorComando((HabilidadActiva)h, a, b);
                    break;
                }

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
