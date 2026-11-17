using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TowerDefenseGameplay
{
    /// <summary>Tipos de mensaje del protocolo LAN (primer byte de cada mensaje).</summary>
    public enum TipoMensaje : byte
    {
        // Sala (menú)
        Hola = 1,            // cliente → host: versión, nombre e ID del comandante
        Bienvenida = 2,      // host → cliente: rol asignado y comandante del host
        Rechazo = 3,         // host → cliente: motivo
        IniciarPartida = 4,  // host → cliente: cargar la escena de juego
        Listo = 5,           // cliente → host: escena cargada

        // Partida (host → cliente)
        Estado = 10,         // instantánea completa del juego (~15 por segundo)
        Notificacion = 11,   // texto para el banner del cliente
        FinPartida = 12,     // resumen y ganador

        // Comandos (cliente → host)
        CmdConstruir = 20,
        CmdIniciarOleada = 21,
        CmdSobrecarga = 22,
        CmdDesbloquear = 23,
        CmdEnviar = 24,
        CmdHabilidad = 25
    }

    public static class Protocolo
    {
        /// <summary>Si cambia el formato de los mensajes, subir este número (el host rechaza versiones distintas).</summary>
        public const int Version = 1;
    }

    /// <summary>Arma un mensaje binario: [tipo:byte][contenido...].</summary>
    public sealed class EscritorMensaje
    {
        private readonly MemoryStream flujo;
        public readonly BinaryWriter W;

        public EscritorMensaje(TipoMensaje tipo)
        {
            flujo = new MemoryStream(256);
            W = new BinaryWriter(flujo, Encoding.UTF8);
            W.Write((byte)tipo);
        }

        public byte[] Datos()
        {
            W.Flush();
            return flujo.ToArray();
        }
    }

    /// <summary>Lee un mensaje binario armado con EscritorMensaje.</summary>
    public sealed class LectorMensaje
    {
        public readonly BinaryReader R;
        public readonly TipoMensaje Tipo;

        public LectorMensaje(byte[] datos)
        {
            R = new BinaryReader(new MemoryStream(datos), Encoding.UTF8);
            Tipo = (TipoMensaje)R.ReadByte();
        }
    }

    public struct EnemigoRed
    {
        public int id;
        public byte tipo;
        public float x;
        public float y;
        public byte vida;  // 0..255 = porcentaje de vida
        public byte flags; // Enemigo.Flag*
    }

    public struct TorreRed
    {
        public short plot;
        public byte tipo;
        public bool hackeada;
    }

    public struct DisparoRed
    {
        public const byte FlagCritico = 1;
        public const byte FlagHielo = 2;
        public const byte FlagSplash = 4;
        public const byte FlagCongela = 8;

        public short plot;
        public int enemigo;
        public byte flags;
    }

    public struct EfectoRed
    {
        public byte tipo;
        public float x;
        public float y;
        public float radio;
    }

    /// <summary>
    /// Instantánea completa de la partida que el host envía al cliente.
    /// El cliente no simula nada: con esto dibuja enemigos, torres, disparos y llena su HUD.
    /// </summary>
    public class EstadoRed
    {
        // General
        public byte estado;
        public short ronda;
        public short totalRondas;
        public float cuentaRegresiva;
        public float tiempoRonda;
        public bool ventanaAbierta;
        public short enemigosRestantes;
        public float vidaBase;
        public float vidaBaseMax;
        public float escudoBase;

        // Defensor
        public int oro;
        public bool sobrecargaActiva;
        public float enfriamientoSobrecarga;
        public int defNivel;
        public int defXP;
        public int defXPSiguiente;
        public int defPuntos;
        public List<string> defHabilidades = new List<string>();

        // Atacante
        public int creditos;
        public float ingreso;
        public int atkNivel;
        public int atkPuntos;
        public List<string> atkHabilidades = new List<string>();
        public float[] enfriamientos = new float[ControladorAtacante.TotalHabilidades];
        public bool asaltoActivo;
        public int danioTotal;
        public int enemigosEnviados;
        public int enemigosQueLlegaron;
        public short enviosPendientes;

        // Mundo
        public List<EnemigoRed> enemigos = new List<EnemigoRed>();
        public List<TorreRed> torres = new List<TorreRed>();
        public List<DisparoRed> disparos = new List<DisparoRed>();
        public List<EfectoRed> efectos = new List<EfectoRed>();

        private static void EscribirLista(BinaryWriter w, List<string> lista)
        {
            w.Write((byte)lista.Count);
            for (int i = 0; i < lista.Count; i++) w.Write(lista[i] ?? string.Empty);
        }

        private static void LeerLista(BinaryReader r, List<string> lista)
        {
            lista.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) lista.Add(r.ReadString());
        }

        public void Escribir(BinaryWriter w)
        {
            w.Write(estado);
            w.Write(ronda);
            w.Write(totalRondas);
            w.Write(cuentaRegresiva);
            w.Write(tiempoRonda);
            w.Write(ventanaAbierta);
            w.Write(enemigosRestantes);
            w.Write(vidaBase);
            w.Write(vidaBaseMax);
            w.Write(escudoBase);

            w.Write(oro);
            w.Write(sobrecargaActiva);
            w.Write(enfriamientoSobrecarga);
            w.Write(defNivel);
            w.Write(defXP);
            w.Write(defXPSiguiente);
            w.Write(defPuntos);
            EscribirLista(w, defHabilidades);

            w.Write(creditos);
            w.Write(ingreso);
            w.Write(atkNivel);
            w.Write(atkPuntos);
            EscribirLista(w, atkHabilidades);
            w.Write((byte)enfriamientos.Length);
            for (int i = 0; i < enfriamientos.Length; i++) w.Write(enfriamientos[i]);
            w.Write(asaltoActivo);
            w.Write(danioTotal);
            w.Write(enemigosEnviados);
            w.Write(enemigosQueLlegaron);
            w.Write(enviosPendientes);

            w.Write((short)enemigos.Count);
            for (int i = 0; i < enemigos.Count; i++)
            {
                EnemigoRed e = enemigos[i];
                w.Write(e.id);
                w.Write(e.tipo);
                w.Write(e.x);
                w.Write(e.y);
                w.Write(e.vida);
                w.Write(e.flags);
            }

            w.Write((short)torres.Count);
            for (int i = 0; i < torres.Count; i++)
            {
                w.Write(torres[i].plot);
                w.Write(torres[i].tipo);
                w.Write(torres[i].hackeada);
            }

            w.Write((short)disparos.Count);
            for (int i = 0; i < disparos.Count; i++)
            {
                w.Write(disparos[i].plot);
                w.Write(disparos[i].enemigo);
                w.Write(disparos[i].flags);
            }

            w.Write((short)efectos.Count);
            for (int i = 0; i < efectos.Count; i++)
            {
                w.Write(efectos[i].tipo);
                w.Write(efectos[i].x);
                w.Write(efectos[i].y);
                w.Write(efectos[i].radio);
            }
        }

        public static EstadoRed Leer(BinaryReader r)
        {
            EstadoRed e = new EstadoRed();
            e.estado = r.ReadByte();
            e.ronda = r.ReadInt16();
            e.totalRondas = r.ReadInt16();
            e.cuentaRegresiva = r.ReadSingle();
            e.tiempoRonda = r.ReadSingle();
            e.ventanaAbierta = r.ReadBoolean();
            e.enemigosRestantes = r.ReadInt16();
            e.vidaBase = r.ReadSingle();
            e.vidaBaseMax = r.ReadSingle();
            e.escudoBase = r.ReadSingle();

            e.oro = r.ReadInt32();
            e.sobrecargaActiva = r.ReadBoolean();
            e.enfriamientoSobrecarga = r.ReadSingle();
            e.defNivel = r.ReadInt32();
            e.defXP = r.ReadInt32();
            e.defXPSiguiente = r.ReadInt32();
            e.defPuntos = r.ReadInt32();
            LeerLista(r, e.defHabilidades);

            e.creditos = r.ReadInt32();
            e.ingreso = r.ReadSingle();
            e.atkNivel = r.ReadInt32();
            e.atkPuntos = r.ReadInt32();
            LeerLista(r, e.atkHabilidades);
            int nEnf = r.ReadByte();
            e.enfriamientos = new float[System.Math.Max(nEnf, ControladorAtacante.TotalHabilidades)];
            for (int i = 0; i < nEnf; i++) e.enfriamientos[i] = r.ReadSingle();
            e.asaltoActivo = r.ReadBoolean();
            e.danioTotal = r.ReadInt32();
            e.enemigosEnviados = r.ReadInt32();
            e.enemigosQueLlegaron = r.ReadInt32();
            e.enviosPendientes = r.ReadInt16();

            int nEnemigos = r.ReadInt16();
            for (int i = 0; i < nEnemigos; i++)
            {
                EnemigoRed en;
                en.id = r.ReadInt32();
                en.tipo = r.ReadByte();
                en.x = r.ReadSingle();
                en.y = r.ReadSingle();
                en.vida = r.ReadByte();
                en.flags = r.ReadByte();
                e.enemigos.Add(en);
            }

            int nTorres = r.ReadInt16();
            for (int i = 0; i < nTorres; i++)
            {
                TorreRed t;
                t.plot = r.ReadInt16();
                t.tipo = r.ReadByte();
                t.hackeada = r.ReadBoolean();
                e.torres.Add(t);
            }

            int nDisparos = r.ReadInt16();
            for (int i = 0; i < nDisparos; i++)
            {
                DisparoRed d;
                d.plot = r.ReadInt16();
                d.enemigo = r.ReadInt32();
                d.flags = r.ReadByte();
                e.disparos.Add(d);
            }

            int nEfectos = r.ReadInt16();
            for (int i = 0; i < nEfectos; i++)
            {
                EfectoRed ef;
                ef.tipo = r.ReadByte();
                ef.x = r.ReadSingle();
                ef.y = r.ReadSingle();
                ef.radio = r.ReadSingle();
                e.efectos.Add(ef);
            }

            return e;
        }
    }
}
