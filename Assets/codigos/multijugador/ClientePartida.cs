using System.Collections.Generic;
using GrafosEST2;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>
    /// Lado CLIENTE de la partida LAN. No simula nada:
    ///  - Al cargar la escena avisa al host (Listo).
    ///  - Aplica cada instantánea del host: HUD (GameManager y ControladorAtacante en modo espejo),
    ///    enemigos (EnemigoProxy), torres visuales, disparos y efectos.
    ///  - Muestra las notificaciones y la pantalla de fin que envía el host.
    /// </summary>
    public class ClientePartida : MonoBehaviour
    {
        public static ClientePartida Instancia { get; private set; }

        private RedLAN red;
        private bool partidaTerminada = false;
        private readonly Dictionary<int, EnemigoProxy> proxies = new Dictionary<int, EnemigoProxy>();
        private readonly HashSet<int> vistos = new HashSet<int>();
        private readonly Dictionary<int, TorreRed> torresRecibidas = new Dictionary<int, TorreRed>();

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
                PantallaFinPartida.MostrarDesconexion("No hay conexión con el host.");
                return;
            }
            red.Enviar(new EscritorMensaje(TipoMensaje.Listo));
        }

        private void AlDesconectar(string motivo)
        {
            if (partidaTerminada) return;
            partidaTerminada = true;
            PantallaFinPartida.MostrarDesconexion(motivo);
        }

        private void AlRecibir(byte[] datos)
        {
            LectorMensaje m = new LectorMensaje(datos);
            switch (m.Tipo)
            {
                case TipoMensaje.Estado:
                    AplicarEstado(EstadoRed.Leer(m.R));
                    break;

                case TipoMensaje.Notificacion:
                    GameManager.Instance?.MostrarNotificacionLocal(m.R.ReadString());
                    break;

                case TipoMensaje.FinPartida:
                    partidaTerminada = true;
                    PantallaFinPartida.Mostrar(ResumenPartida.Leer(m.R));
                    break;
            }
        }

        private static void CopiarLista(List<string> origen, List<string> destino)
        {
            destino.Clear();
            destino.AddRange(origen);
        }

        private void AplicarEstado(EstadoRed e)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            // ── HUD general y defensor (GameManager en modo espejo) ──
            gm.estado = (EstadoJuego)e.estado;
            gm.oleadaActual = e.ronda;
            gm.totalOleadas = e.totalRondas;
            gm.cuentaRegresivaOleada = e.cuentaRegresiva;
            gm.tiempoRondaRestante = e.tiempoRonda;
            gm.ventanaEnvioAbierta = e.ventanaAbierta;
            gm.enemigosRestantesEnOleada = e.enemigosRestantes;
            gm.vidaBaseActual = e.vidaBase;
            gm.vidaBaseMaxima = e.vidaBaseMax;
            gm.escudoBaseActual = e.escudoBase;
            gm.oroActual = e.oro;
            gm.sobrecargaActiva = e.sobrecargaActiva;
            gm.enfriamientoSobrecargaRestante = e.enfriamientoSobrecarga;

            Jugador def = gm.jugadorActivo;
            if (def != null)
            {
                def.nivel = e.defNivel;
                def.experiencia = e.defXP;
                def.experienciaSiguienteNivel = e.defXPSiguiente;
                def.puntosHabilidad = e.defPuntos;
                CopiarLista(e.defHabilidades, def.habilidadesDesbloqueadas);
            }
            if (ControladorHabilidadesUI.Instance != null) ControladorHabilidadesUI.Instance.RefrescarSiCambio();

            // ── Atacante (ControladorAtacante en modo espejo) ──
            ControladorAtacante atk = ControladorAtacante.Instance;
            if (atk != null)
            {
                atk.creditos = e.creditos;
                atk.ingresoPorSegundo = e.ingreso;
                atk.jugador.nivel = e.atkNivel;
                atk.jugador.puntosHabilidad = e.atkPuntos;
                CopiarLista(e.atkHabilidades, atk.jugador.habilidadesDesbloqueadas);
                for (int i = 0; i < atk.enfriamientos.Length && i < e.enfriamientos.Length; i++)
                {
                    atk.enfriamientos[i] = e.enfriamientos[i];
                }
                atk.asaltoActivo = e.asaltoActivo;
                atk.danioTotalBase = e.danioTotal;
                atk.enemigosEnviados = e.enemigosEnviados;
                atk.enemigosQueLlegaron = e.enemigosQueLlegaron;
                atk.enviosPendientesRemotos = e.enviosPendientes;

                if (atk.TieneHabilidad("ATK_ATAJO") && MapaRutas.Instancia != null && !MapaRutas.Instancia.RutaCAbierta)
                {
                    MapaRutas.Instancia.AbrirRutaC();
                }
            }
            if (ControladorHabilidadesUI.InstanciaAtaque != null) ControladorHabilidadesUI.InstanciaAtaque.RefrescarSiCambio();

            SincronizarEnemigos(e);
            SincronizarTorres(gm, e);
            ReproducirDisparos(gm, e);

            for (int i = 0; i < e.efectos.Count; i++)
            {
                EfectoRed ef = e.efectos[i];
                EfectoPulso.Crear(new Vector3(ef.x, ef.y, 0f), ef.radio, EfectoPulso.ColorDe((TipoEfectoVisual)ef.tipo));
            }

            gm.NotificarCambio();
        }

        private void SincronizarEnemigos(EstadoRed e)
        {
            vistos.Clear();
            for (int i = 0; i < e.enemigos.Count; i++)
            {
                EnemigoRed en = e.enemigos[i];
                vistos.Add(en.id);
                Vector3 pos = new Vector3(en.x, en.y, 0f);

                if (!proxies.TryGetValue(en.id, out EnemigoProxy proxy) || proxy == null)
                {
                    proxy = EnemigoProxy.Crear(en.id, (TipoEnemigo)en.tipo, pos);
                    proxies[en.id] = proxy;
                }
                proxy.ActualizarDesdeRed(pos, en.vida / 255f, en.flags);
            }

            // Los que ya no vienen en la instantánea murieron o llegaron a la base
            List<int> quitar = null;
            foreach (var kv in proxies)
            {
                if (vistos.Contains(kv.Key)) continue;
                if (quitar == null) quitar = new List<int>();
                quitar.Add(kv.Key);
            }
            if (quitar != null)
            {
                for (int i = 0; i < quitar.Count; i++)
                {
                    if (proxies.TryGetValue(quitar[i], out EnemigoProxy p) && p != null) Destroy(p.gameObject);
                    proxies.Remove(quitar[i]);
                }
            }
        }

        private void SincronizarTorres(GameManager gm, EstadoRed e)
        {
            torresRecibidas.Clear();
            for (int i = 0; i < e.torres.Count; i++) torresRecibidas[e.torres[i].plot] = e.torres[i];

            List<PlotTorre> plots = gm.plotsOrdenados;
            for (int i = 0; i < plots.Count; i++)
            {
                PlotTorre plot = plots[i];
                if (plot == null) continue;

                if (torresRecibidas.TryGetValue(plot.indiceRed, out TorreRed t))
                {
                    plot.ConstruirTorreVisual((TipoTorre)t.tipo);
                    if (plot.torreInstanciada != null) plot.torreInstanciada.EstablecerHackeadaVisual(t.hackeada);
                }
                else if (plot.TieneTorre)
                {
                    plot.QuitarTorreVisual();
                }
            }
        }

        private void ReproducirDisparos(GameManager gm, EstadoRed e)
        {
            for (int i = 0; i < e.disparos.Count; i++)
            {
                DisparoRed d = e.disparos[i];
                PlotTorre plot = gm.ObtenerPlotPorIndice(d.plot);
                if (plot == null || plot.torreInstanciada == null) continue;
                if (!proxies.TryGetValue(d.enemigo, out EnemigoProxy objetivo) || objetivo == null) continue;

                plot.torreInstanciada.DispararVisual(objetivo.transform,
                    (d.flags & DisparoRed.FlagCritico) != 0,
                    (d.flags & DisparoRed.FlagHielo) != 0,
                    (d.flags & DisparoRed.FlagSplash) != 0,
                    (d.flags & DisparoRed.FlagCongela) != 0);
            }
        }
    }
}
