namespace TowerDefenseGameplay
{
    public enum ModoPartida
    {
        UnJugador,
        MultijugadorLAN
    }

    /// <summary>Rol que cumple un jugador en una partida multijugador.</summary>
    public enum RolJugador : byte
    {
        Defensor = 0,
        Atacante = 1
    }

    /// <summary>
    /// Configuración global de la partida que sobrevive al cambio de escena (MainMenu → SampleScene).
    /// Define si se juega solo o en LAN, qué rol tiene este PC y si este PC simula la partida (host).
    ///
    /// Reglas de autoridad:
    ///  - Un jugador: este PC simula todo (como siempre).
    ///  - LAN host: este PC simula enemigos, torres, oro y rondas, y envía el estado al cliente.
    ///  - LAN cliente: este PC solo dibuja el estado que recibe y envía comandos al host.
    /// </summary>
    public static class ModoJuego
    {
        public static ModoPartida Modo = ModoPartida.UnJugador;
        public static RolJugador RolLocal = RolJugador.Defensor;
        public static bool EsHost = false;

        /// <summary>True cuando el juego pasó por el menú principal para seleccionar modo.</summary>
        public static bool IniciadoDesdeMenu = false;

        /// <summary>Índice de dificultad del modo un jugador (0 = Nivel 1, 1 = Nivel 2, 2 = Nivel 3).</summary>
        public static int NivelUnJugador = 0;

        // Comandantes (árbol BST) de cada rol en la partida multijugador
        public static string NombreAtacante = "Atacante";
        public static int IdAtacante = 0;
        public static string NombreDefensor = "Defensor";
        public static int IdDefensor = 0;

        public static bool EsMultijugador => Modo == ModoPartida.MultijugadorLAN;

        /// <summary>True si este PC ejecuta la simulación real (un jugador o host LAN).</summary>
        public static bool TieneAutoridad => !EsMultijugador || EsHost;

        /// <summary>True si este PC es el cliente LAN (solo vista + comandos).</summary>
        public static bool EsClienteRemoto => EsMultijugador && !EsHost;

        public static bool EsAtacanteLocal => EsMultijugador && RolLocal == RolJugador.Atacante;
        public static bool EsDefensorLocal => !EsMultijugador || RolLocal == RolJugador.Defensor;

        /// <summary>Rol del otro jugador (solo tiene sentido en LAN).</summary>
        public static RolJugador RolRemoto => RolLocal == RolJugador.Atacante ? RolJugador.Defensor : RolJugador.Atacante;

        public static string NombreLocal => RolLocal == RolJugador.Atacante ? NombreAtacante : NombreDefensor;
        public static int IdLocal => RolLocal == RolJugador.Atacante ? IdAtacante : IdDefensor;

        public static void ConfigurarUnJugador()
        {
            Modo = ModoPartida.UnJugador;
            RolLocal = RolJugador.Defensor;
            EsHost = false;
        }

        public static void ConfigurarMultijugador(bool esHost, RolJugador rolLocal)
        {
            Modo = ModoPartida.MultijugadorLAN;
            EsHost = esHost;
            RolLocal = rolLocal;
        }

        public static string NombreRol(RolJugador rol)
        {
            return rol == RolJugador.Atacante ? "Atacante" : "Defensor";
        }
    }
}
