using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Lado HOST de la partida LAN.
    ///  - Recibe los comandos del cliente y los ejecuta (ComandosJugador).
    ///  - Envía ~15 veces por segundo una instantánea completa del juego (EstadoRed).
    ///  - Reenvía notificaciones, disparos y efectos para que el cliente vea lo mismo.
    ///  - Anuncia el fin de la partida.
    /// </summary>
    public class ServidorPartida : MonoBehaviour
    {
        public static ServidorPartida Instancia { get; private set; }

        private const float IntervaloEstado = 1f / 15f;

        private RedLAN red;
        private bool clienteListo = false;
        private bool preparacionIniciada = false;
        private bool partidaTerminada = false;
        private float proximoEstado = 0f;

        private readonly List<DisparoRed> disparos = new List<DisparoRed>();
        private readonly List<EfectoRed> efectos = new List<EfectoRed>();

        public bool PartidaTerminada => partidaTerminada;

        private void Awake()
        {
            Instancia = this;
            red = RedLAN.Instancia;
        }

        private void OnEnable()
        {
            if (red == null) red = RedLAN.Instancia;
            if (red != null)
            {
                red.Suscribir(AlRecibir);
                red.AlDesconectar += AlDesconectar;
            }
        }

        private void OnDisable()
        {
            if (red != null)
            {
                red.Desuscribir(AlRecibir);
                red.AlDesconectar -= AlDesconectar;
            }
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        private void Start()
        {
            if (red == null || !red.RivalConectado)
            {
                PantallaFinPartida.MostrarDesconexion("No hay conexión con el otro jugador.");
            }
        }

        private void Update()
        {
            // Arranca la preparación cuando el cliente cargó la escena y el GameManager ya está esperando
            if (clienteListo && !preparacionIniciada && GameManager.Instance != null &&
                GameManager.Instance.estado == EstadoJuego.EsperandoJugadores)
            {
                preparacionIniciada = true;
                GameManager.Instance.IniciarPreparacionMultijugador();
            }
        }

        private void LateUpdate()
        {
            if (!clienteListo || red == null || !red.RivalConectado) return;
            if (Time.unscaledTime < proximoEstado) return;

            proximoEstado = Time.unscaledTime + IntervaloEstado;
            EnviarEstado();
        }

        private void AlRecibir(byte[] datos)
        {
            LectorMensaje m = new LectorMensaje(datos);
            switch (m.Tipo)
            {
                case TipoMensaje.Listo:
                    clienteListo = true;
                    break;

                case TipoMensaje.CmdConstruir:
                case TipoMensaje.CmdIniciarOleada:
                case TipoMensaje.CmdSobrecarga:
                case TipoMensaje.CmdDesbloquear:
                case TipoMensaje.CmdEnviar:
                case TipoMensaje.CmdHabilidad:
                    if (!partidaTerminada) ComandosJugador.EjecutarComandoRemoto(m, ModoJuego.RolRemoto);
                    break;
            }
        }

        private void AlDesconectar(string motivo)
        {
            if (partidaTerminada) return;
            partidaTerminada = true;
            PantallaFinPartida.MostrarDesconexion(motivo);
        }

        #region Registro de eventos para el cliente

        public void RegistrarDisparo(Torre torre, Enemigo objetivo, bool critico, bool hielo, bool splash, bool congela)
        {
            if (!clienteListo || torre == null || objetivo == null) return;

            byte flags = 0;
            if (critico) flags |= DisparoRed.FlagCritico;
            if (hielo) flags |= DisparoRed.FlagHielo;
            if (splash) flags |= DisparoRed.FlagSplash;
            if (congela) flags |= DisparoRed.FlagCongela;

            disparos.Add(new DisparoRed { plot = (short)torre.IndicePlot, enemigo = objetivo.idRed, flags = flags });
        }

        public void RegistrarEfecto(TipoEfectoVisual tipo, Vector3 posicion, float radio)
        {
            if (!clienteListo) return;
            efectos.Add(new EfectoRed { tipo = (byte)tipo, x = posicion.x, y = posicion.y, radio = radio });
        }

        public void EnviarNotificacion(string mensaje)
        {
            if (red == null) return;
            EscritorMensaje m = new EscritorMensaje(TipoMensaje.Notificacion);
            m.W.Write(mensaje ?? string.Empty);
            red.Enviar(m);
        }

        #endregion

        #region Instantánea del estado

        private EstadoRed ConstruirEstado()
        {
            GameManager gm = GameManager.Instance;
            ControladorAtacante atk = ControladorAtacante.Instance;
            EstadoRed e = new EstadoRed();
            if (gm == null) return e;

            e.estado = (byte)gm.estado;
            e.ronda = (short)gm.oleadaActual;
            e.totalRondas = (short)gm.totalOleadas;
            e.cuentaRegresiva = gm.cuentaRegresivaOleada;
            e.tiempoRonda = gm.tiempoRondaRestante;
            e.ventanaAbierta = gm.ventanaEnvioAbierta;
            e.enemigosRestantes = (short)gm.enemigosRestantesEnOleada;
            e.vidaBase = gm.vidaBaseActual;
            e.vidaBaseMax = gm.vidaBaseMaxima;
            e.escudoBase = gm.escudoBaseActual;

            e.oro = gm.oroActual;
            e.sobrecargaActiva = gm.sobrecargaActiva;
            e.enfriamientoSobrecarga = gm.enfriamientoSobrecargaRestante;
            Jugador def = gm.jugadorActivo;
            if (def != null)
            {
                e.defNivel = def.nivel;
                e.defXP = def.experiencia;
                e.defXPSiguiente = def.experienciaSiguienteNivel;
                e.defPuntos = def.puntosHabilidad;
                e.defHabilidades.AddRange(def.habilidadesDesbloqueadas);
            }

            if (atk != null)
            {
                e.creditos = atk.creditos;
                e.ingreso = gm.ventanaEnvioAbierta ? atk.ingresoPorSegundo : 0f;
                e.atkNivel = atk.jugador.nivel;
                e.atkPuntos = atk.jugador.puntosHabilidad;
                e.atkHabilidades.AddRange(atk.jugador.habilidadesDesbloqueadas);
                for (int i = 0; i < e.enfriamientos.Length && i < atk.enfriamientos.Length; i++)
                {
                    e.enfriamientos[i] = atk.enfriamientos[i];
                }
                e.asaltoActivo = atk.asaltoActivo;
                e.danioTotal = atk.danioTotalBase;
                e.enemigosEnviados = atk.enemigosEnviados;
                e.enemigosQueLlegaron = atk.enemigosQueLlegaron;
                e.enviosPendientes = (short)atk.EnviosPendientes;
            }

            List<Enemigo> enemigos = Enemigo.Activos;
            for (int i = 0; i < enemigos.Count; i++)
            {
                Enemigo en = enemigos[i];
                if (en == null || en.EstaMuerto) continue;
                Vector3 p = en.transform.position;
                e.enemigos.Add(new EnemigoRed
                {
                    id = en.idRed,
                    tipo = (byte)en.tipoEnemigo,
                    x = p.x,
                    y = p.y,
                    vida = (byte)Mathf.RoundToInt(Mathf.Clamp01(en.vidaActual / Mathf.Max(1f, en.vidaMaxima)) * 255f),
                    flags = en.FlagsRed
                });
            }

            List<PlotTorre> plots = gm.plotsOrdenados;
            for (int i = 0; i < plots.Count; i++)
            {
                if (plots[i] == null || !plots[i].TieneTorre) continue;
                e.torres.Add(new TorreRed
                {
                    plot = (short)plots[i].indiceRed,
                    tipo = (byte)plots[i].torreInstanciada.tipo,
                    hackeada = plots[i].torreInstanciada.EstaHackeada
                });
            }

            e.disparos.AddRange(disparos);
            e.efectos.AddRange(efectos);
            return e;
        }

        private void EnviarEstado()
        {
            EstadoRed estado = ConstruirEstado();
            EscritorMensaje m = new EscritorMensaje(TipoMensaje.Estado);
            estado.Escribir(m.W);
            red.Enviar(m);
            disparos.Clear();
            efectos.Clear();
        }

        #endregion

        /// <summary>Lo llama GameManager al terminar (base destruida o rondas superadas).</summary>
        public void FinalizarPartida(RolJugador ganador)
        {
            if (partidaTerminada) return;
            partidaTerminada = true;

            ResumenPartida resumen = ResumenPartida.CrearDesdeJuego(ganador);

            if (red != null && red.RivalConectado)
            {
                EnviarEstado();
                EscritorMensaje m = new EscritorMensaje(TipoMensaje.FinPartida);
                resumen.Escribir(m.W);
                red.Enviar(m);
            }

            PantallaFinPartida.Mostrar(resumen);
        }
    }
}
