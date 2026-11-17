using System.Collections.Generic;
using GrafosEST2;
using UnityEngine;

namespace TowerDefenseGameplay
{
    /// <summary>Datos fijos de cada habilidad activa del atacante.</summary>
    public struct DefinicionHabilidadAtaque
    {
        public string nombre;
        public string tecla;
        public int costo;
        public float enfriamiento;
        public float radio;             // > 0: se lanza sobre un punto del mapa
        public bool apuntaATorre;       // Hackeo: se lanza sobre una torre
        public string habilidadDAG;     // vacío = disponible desde el inicio
        public string descripcion;
    }

    /// <summary>
    /// Estado y reglas del ATACANTE en la partida LAN.
    ///  - En el host: lleva los créditos, la cola de envíos, las recargas y aplica las habilidades.
    ///  - En el cliente: es un "espejo" cuyos campos llenan las instantáneas del host (para el HUD).
    ///
    /// Economía: presupuesto al inicio de cada ronda + ingreso por segundo mientras la ventana de envío
    /// está abierta + bono por cada golpe a la base. Puntos de DAG: 1 al empezar, 2 por ronda terminada
    /// y 1 por cada 40 de daño causado a la base.
    /// </summary>
    public class ControladorAtacante : MonoBehaviour
    {
        public static ControladorAtacante Instance { get; private set; }

        public const int TotalHabilidades = 5;
        public const int HabEscudo = 0;
        public const int HabCuracion = 1;
        public const int HabEstampida = 2;
        public const int HabHackeo = 3;
        public const int HabAsaltoTotal = 4;

        public static readonly DefinicionHabilidadAtaque[] Habilidades =
        {
            new DefinicionHabilidadAtaque { nombre = "Escudo", tecla = "Q", costo = 40, enfriamiento = 25f, radio = 2.5f,
                descripcion = "Escudo del 50% de la vida a tus tropas en el área (8 s)." },
            new DefinicionHabilidadAtaque { nombre = "Curación", tecla = "W", costo = 35, enfriamiento = 30f, radio = 2.5f,
                descripcion = "Cura un 30% de vida a tus tropas en el área." },
            new DefinicionHabilidadAtaque { nombre = "Estampida", tecla = "E", costo = 50, enfriamiento = 35f, radio = 0f,
                descripcion = "+60% de velocidad a todas tus tropas durante 4 s." },
            new DefinicionHabilidadAtaque { nombre = "Hackeo", tecla = "R", costo = 60, enfriamiento = 40f, radio = 0f, apuntaATorre = true,
                descripcion = "Desactiva una torre durante 5 s." },
            new DefinicionHabilidadAtaque { nombre = "Asalto Total", tecla = "T", costo = 0, enfriamiento = 45f, radio = 0f,
                habilidadDAG = "ATK_ASALTO_TOTAL", descripcion = "+50% de velocidad y escudo a todas tus tropas durante 10 s." }
        };

        [Header("Estado (sincronizado al cliente)")]
        public Jugador jugador;
        public int creditos = 0;
        public float ingresoPorSegundo = 0f;
        public float[] enfriamientos = new float[TotalHabilidades];
        public bool asaltoActivo = false;
        public int danioTotalBase = 0;
        public int enemigosEnviados = 0;
        public int enemigosQueLlegaron = 0;
        public int enviosPendientesRemotos = 0;

        [Header("Interfaz local")]
        public int rutaSeleccionada = MapaRutas.RutaA;

        private float asaltoHasta = 0f;
        private float acumuladorIngreso = 0f;
        private int danioAcumuladoParaPuntos = 0;
        private float proximoSpawn = 0f;

        private struct Envio
        {
            public TipoEnemigo tipo;
            public int ruta;
        }

        private readonly Queue<Envio> cola = new Queue<Envio>();

        /// <summary>Enemigos pagados que aún no salen al mapa.</summary>
        public int EnviosPendientes => ModoJuego.TieneAutoridad ? cola.Count : enviosPendientesRemotos;

        private void Awake()
        {
            Instance = this;
            jugador = new Jugador(100, ModoJuego.IdAtacante, ModoJuego.NombreAtacante, 1, 0);
        }

        private void Start()
        {
            if (ControladorHabilidadesUI.InstanciaAtaque != null)
            {
                ControladorHabilidadesUI.InstanciaAtaque.AsignarJugador(jugador);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region Reglas y consultas

        public bool TieneHabilidad(string id) => jugador != null && jugador.TieneHabilidad(id);

        public int CostoEnemigo(TipoEnemigo tipo)
        {
            switch (tipo)
            {
                case TipoEnemigo.Corredor: return TieneHabilidad("ATK_ENJAMBRE") ? 10 : 15;
                case TipoEnemigo.Infanteria: return 25;
                case TipoEnemigo.Blindado: return 50;
                case TipoEnemigo.Jefe: return 150;
                default: return 25;
            }
        }

        public bool TipoDisponible(TipoEnemigo tipo)
        {
            return tipo != TipoEnemigo.Jefe || TieneHabilidad("ATK_TITANES");
        }

        public bool HabilidadDisponible(int indice)
        {
            if (indice < 0 || indice >= TotalHabilidades) return false;
            string req = Habilidades[indice].habilidadDAG;
            return string.IsNullOrEmpty(req) || TieneHabilidad(req);
        }

        public static int PresupuestoRonda(int ronda)
        {
            return 30 + 30 * ronda + 6 * ronda * ronda;
        }

        private float CalcularIngreso(int ronda)
        {
            float ingreso = 1f + 0.5f * ronda + 0.1f * ronda * ronda;
            if (TieneHabilidad("ATK_LOGISTICA")) ingreso *= 1.2f;
            return ingreso;
        }

        private GameManager GM => GameManager.Instance;

        private void Notificar(string mensaje, DestinoNotificacion destino = DestinoNotificacion.Atacante)
        {
            if (GM != null) GM.Notificar(mensaje, destino);
        }

        private void RefrescarArbol()
        {
            if (ControladorHabilidadesUI.InstanciaAtaque != null)
            {
                ControladorHabilidadesUI.InstanciaAtaque.RefrescarSiCambio();
            }
            if (GM != null) GM.NotificarCambio();
        }

        #endregion

        #region Ciclo de la partida (host)

        public void AlIniciarPartida()
        {
            creditos = 60;
            jugador.nivel = 1;
            jugador.AgregarPuntosHabilidad(1);
            Notificar("⚔️ Eres el atacante: 60 créditos y 1 punto de asalto. La primera ronda empieza tras la preparación del defensor.");
            RefrescarArbol();
        }

        public void AlIniciarRonda(int ronda)
        {
            jugador.nivel = ronda; // el DAG de ataque usa la ronda como "nivel requerido"
            int presupuesto = PresupuestoRonda(ronda);
            creditos += presupuesto;
            ingresoPorSegundo = CalcularIngreso(ronda);
            acumuladorIngreso = 0f;
            Notificar($"💰 +{presupuesto} créditos de presupuesto. Ingreso: +{ingresoPorSegundo:0.0}/s mientras la ventana esté abierta.");
            RefrescarArbol();
        }

        public void AlCerrarVentana()
        {
            acumuladorIngreso = 0f;
        }

        public void AlCompletarRonda(int ronda)
        {
            jugador.AgregarPuntosHabilidad(2);
            Notificar($"🏁 Ronda {ronda} terminada: +2 puntos de asalto para el DAG de ataque.");
            RefrescarArbol();
        }

        public void AlGolpearBase(int danioOriginal, int danioReal)
        {
            enemigosQueLlegaron++;
            danioTotalBase += danioOriginal;

            int bono = danioOriginal * (TieneHabilidad("ATK_SAQUEO") ? 2 : 1);
            creditos += bono;

            danioAcumuladoParaPuntos += danioOriginal;
            int puntos = 0;
            while (danioAcumuladoParaPuntos >= 40)
            {
                danioAcumuladoParaPuntos -= 40;
                puntos++;
            }
            if (puntos > 0)
            {
                jugador.AgregarPuntosHabilidad(puntos);
                Notificar($"💥 ¡Golpe a la base! +{bono} créditos y +{puntos} punto(s) de asalto.");
            }
            RefrescarArbol();
        }

        public void AlDesbloquearHabilidad(string id)
        {
            if (id == "ATK_ATAJO" && MapaRutas.Instancia != null)
            {
                MapaRutas.Instancia.AbrirRutaC();
                Notificar("🗺️ ¡El atacante abrió la Ruta C (atajo)! Es el camino más corto hacia la base.", DestinoNotificacion.Ambos);
            }
            else if (id == "ATK_LOGISTICA" && GM != null && GM.ventanaEnvioAbierta)
            {
                ingresoPorSegundo = CalcularIngreso(GM.oleadaActual);
            }
            else if (id == "ATK_TITANES")
            {
                Notificar("👹 ¡El atacante ahora puede enviar Jefes!", DestinoNotificacion.Ambos);
            }
        }

        #endregion

        #region Comandos (host)

        public void SolicitarEnvio(TipoEnemigo tipo, int ruta, int cantidad)
        {
            if (!ModoJuego.TieneAutoridad || GM == null) return;

            if (GM.estado != EstadoJuego.OleadaEnCurso || !GM.ventanaEnvioAbierta)
            {
                Notificar("⏳ Solo puedes enviar tropas mientras la ventana de envío de la ronda está abierta.");
                return;
            }
            if (MapaRutas.Instancia == null || !MapaRutas.Instancia.RutaDisponible(ruta))
            {
                Notificar(ruta == MapaRutas.RutaC
                    ? "🔒 La Ruta C se abre con 'Atajo Táctico' en el DAG de ataque."
                    : "⚠️ Esa ruta no está disponible.");
                return;
            }
            if (!TipoDisponible(tipo))
            {
                Notificar("🔒 Para enviar Jefes necesitas 'Titanes' en el DAG de ataque.");
                return;
            }

            int costo = CostoEnemigo(tipo);
            cantidad = Mathf.Clamp(cantidad, 1, 10);
            int alcanza = costo > 0 ? creditos / costo : cantidad;
            if (alcanza <= 0)
            {
                Notificar($"💸 Créditos insuficientes: {tipo} cuesta {costo} (tienes {creditos}).");
                return;
            }
            cantidad = Mathf.Min(cantidad, alcanza);

            creditos -= costo * cantidad;
            for (int i = 0; i < cantidad; i++)
            {
                cola.Enqueue(new Envio { tipo = tipo, ruta = ruta });
            }
            enemigosEnviados += cantidad;
            GM.NotificarCambio();
        }

        public void SolicitarHabilidad(int indice, Vector2 punto)
        {
            if (!ModoJuego.TieneAutoridad || GM == null) return;
            if (indice < 0 || indice >= TotalHabilidades) return;

            DefinicionHabilidadAtaque def = Habilidades[indice];

            if (GM.estado == EstadoJuego.GameOver || GM.estado == EstadoJuego.Victoria || GM.estado == EstadoJuego.EsperandoJugadores)
            {
                return;
            }
            if (!HabilidadDisponible(indice))
            {
                Notificar($"🔒 '{def.nombre}' se desbloquea en el DAG de ataque.");
                return;
            }
            if (enfriamientos[indice] > 0f)
            {
                Notificar($"⏳ '{def.nombre}' en recarga: {enfriamientos[indice]:0} s.");
                return;
            }
            if (creditos < def.costo)
            {
                Notificar($"💸 '{def.nombre}' cuesta {def.costo} créditos (tienes {creditos}).");
                return;
            }

            bool usada = false;
            switch (indice)
            {
                case HabEscudo:
                    usada = AplicarEnArea(punto, def.radio, e => e.AplicarEscudo(e.vidaMaxima * 0.5f, 8f), TipoEfectoVisual.Escudo);
                    break;
                case HabCuracion:
                    usada = AplicarEnArea(punto, def.radio, e => e.Curar(e.vidaMaxima * 0.3f), TipoEfectoVisual.Curacion);
                    break;
                case HabEstampida:
                    usada = AplicarATodos(e => e.AplicarVelocidadTemporal(1.6f, 4f), TipoEfectoVisual.Estampida);
                    break;
                case HabHackeo:
                    usada = HackearTorre(punto);
                    break;
                case HabAsaltoTotal:
                    asaltoActivo = true;
                    asaltoHasta = Time.time + 10f;
                    AplicarATodos(AplicarEfectoAsalto, TipoEfectoVisual.AsaltoTotal);
                    usada = true;
                    break;
            }

            if (!usada) return;

            creditos -= def.costo;
            enfriamientos[indice] = def.enfriamiento;
            Notificar($"⚔️ El atacante usó {def.nombre}.", DestinoNotificacion.Ambos);
            GM.NotificarCambio();
        }

        private bool AplicarEnArea(Vector2 punto, float radio, System.Action<Enemigo> efecto, TipoEfectoVisual visual)
        {
            int afectados = 0;
            List<Enemigo> enemigos = new List<Enemigo>(Enemigo.Activos);
            for (int i = 0; i < enemigos.Count; i++)
            {
                Enemigo e = enemigos[i];
                if (e == null || e.EstaMuerto) continue;
                if (Vector2.Distance(punto, e.transform.position) > radio) continue;
                efecto(e);
                afectados++;
            }

            if (afectados == 0)
            {
                Notificar("⚠️ No hay tropas tuyas en esa zona.");
                return false;
            }
            EfectoPulso.Emitir(punto, radio, visual);
            return true;
        }

        private bool AplicarATodos(System.Action<Enemigo> efecto, TipoEfectoVisual visual)
        {
            int afectados = 0;
            List<Enemigo> enemigos = new List<Enemigo>(Enemigo.Activos);
            for (int i = 0; i < enemigos.Count; i++)
            {
                if (enemigos[i] == null || enemigos[i].EstaMuerto) continue;
                efecto(enemigos[i]);
                EfectoPulso.Emitir(enemigos[i].transform.position, 0.6f, visual);
                afectados++;
            }

            if (afectados == 0 && visual != TipoEfectoVisual.AsaltoTotal)
            {
                Notificar("⚠️ No tienes tropas en el mapa.");
                return false;
            }
            return true;
        }

        private void AplicarEfectoAsalto(Enemigo e)
        {
            float restante = Mathf.Max(0.5f, asaltoHasta - Time.time);
            e.AplicarVelocidadTemporal(1.5f, restante);
            e.AplicarEscudo(e.vidaMaxima * 0.3f, restante);
        }

        private bool HackearTorre(Vector2 punto)
        {
            Torre mejor = null;
            float menor = 1.0f;
            IReadOnlyList<PlotTorre> plots = GM.Plots;
            for (int i = 0; i < plots.Count; i++)
            {
                if (plots[i] == null || !plots[i].TieneTorre) continue;
                float d = Vector2.Distance(punto, plots[i].transform.position);
                if (d < menor)
                {
                    menor = d;
                    mejor = plots[i].torreInstanciada;
                }
            }

            if (mejor == null)
            {
                Notificar("⚠️ Haz clic sobre una torre para hackearla.");
                return false;
            }

            mejor.Hackear(5f);
            EfectoPulso.Emitir(mejor.transform.position, 0.8f, TipoEfectoVisual.Hackeo);
            return true;
        }

        /// <summary>Mejoras pasivas del DAG de ataque que recibe cada enemigo al aparecer.</summary>
        public void AplicarModificadoresEnemigo(Enemigo e)
        {
            if (e == null) return;
            if (TieneHabilidad("ATK_BLINDAJE")) e.MultiplicarVidaMaxima(1.2f);
            if (TieneHabilidad("ATK_BOTAS")) e.multiplicadorVelocidadPasivo = 1.15f;
            if (TieneHabilidad("ATK_REGENERACION")) e.regeneracionPorSegundo = 0.02f;
            if (TieneHabilidad("ATK_ANTICONGELANTE")) e.resistenciaHielo = 0.5f;
            if (TieneHabilidad("ATK_PLACAS")) e.multiplicadorDanioArea = 0.6f;
            if (asaltoActivo) AplicarEfectoAsalto(e);
        }

        #endregion

        private void Update()
        {
            if (!ModoJuego.TieneAutoridad || GM == null) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < enfriamientos.Length; i++)
            {
                if (enfriamientos[i] > 0f) enfriamientos[i] = Mathf.Max(0f, enfriamientos[i] - dt);
            }

            if (asaltoActivo && Time.time >= asaltoHasta)
            {
                asaltoActivo = false;
            }

            if (GM.estado == EstadoJuego.GameOver || GM.estado == EstadoJuego.Victoria)
            {
                cola.Clear();
                return;
            }

            // Ingreso por segundo durante la ventana de envío
            if (GM.ventanaEnvioAbierta && ingresoPorSegundo > 0f)
            {
                acumuladorIngreso += ingresoPorSegundo * dt;
                int entero = Mathf.FloorToInt(acumuladorIngreso);
                if (entero > 0)
                {
                    creditos += entero;
                    acumuladorIngreso -= entero;
                }
            }

            // Soltar la cola de envíos con separación para que no salgan encimados
            if (cola.Count > 0 && Time.time >= proximoSpawn)
            {
                Envio envio = cola.Dequeue();
                List<PathNode> ruta = MapaRutas.Instancia != null ? MapaRutas.Instancia.ObtenerRuta(envio.ruta) : null;
                if (ruta == null && MapaRutas.Instancia != null) ruta = MapaRutas.Instancia.ObtenerRuta(MapaRutas.RutaA);

                if (ruta != null)
                {
                    GM.SpawnearEnemigoEnRuta(envio.tipo, ruta);
                }
                proximoSpawn = Time.time + 0.45f;

                if (cola.Count == 0)
                {
                    GM.RevisarFinDeRonda();
                }
            }
        }
    }
}
