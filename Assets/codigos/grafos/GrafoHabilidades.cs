using System;
using System.Collections.Generic;
using System.Text;

namespace GrafosEST2
{
    public enum EstadoHabilidadEnum
    {
        Desbloqueada,
        DisponibleParaDesbloquear,
        FaltanPrerrequisitos,
        NivelInsuficiente,
        PuntosInsuficientes
    }

    /// <summary>
    /// Administrador del Árbol de Habilidades y Tecnologías implementado como
    /// un Grafo Dirigido Acíclico (DAG).
    /// Maneja dependencias, validación de ciclos y Algoritmo de Kahn.
    /// </summary>
    public class GrafoHabilidades
    {
        public Grafo<Habilidad> Grafo { get; private set; }

        public GrafoHabilidades()
        {
            Grafo = new Grafo<Habilidad>(esDirigido: true);
            InicializarArbolDefecto();
        }

        /// <summary>
        /// Inicializa el árbol de habilidades por defecto con ramas ofensivas, defensivas y de soporte.
        /// </summary>
        public void InicializarArbolDefecto()
        {
            Grafo.Limpiar();

            // 1. Vértices (22 Habilidades interconectadas en el DAG)
            // --- HABILIDAD RAÍZ ---
            Grafo.AgregarNodo("HAB_BASE", new Habilidad(
                "HAB_BASE", "Entrenamiento Básico",
                "Conocimientos esenciales de defensa de base. Habilidad inicial.",
                0, 1, "Básica"
            ));

            // --- NIVEL 1 (Prerrequisito: HAB_BASE) ---
            Grafo.AgregarNodo("HAB_BALAS_RAPIDAS", new Habilidad(
                "HAB_BALAS_RAPIDAS", "Balas Rápidas",
                "Incrementa la cadencia de tiro de todas las torres un 25%.",
                1, 1, "Ofensiva"
            ));

            Grafo.AgregarNodo("HAB_GENERADOR", new Habilidad(
                "HAB_GENERADOR", "Generador de Energía",
                "Aumenta un 25% el oro obtenido al completar cada oleada.",
                1, 1, "Economía"
            ));

            Grafo.AgregarNodo("HAB_ESCUDO_BASE", new Habilidad(
                "HAB_ESCUDO_BASE", "Escudo de Energía",
                "Genera un campo de fuerza que absorbe hasta 100 puntos de daño a la base.",
                1, 1, "Defensiva"
            ));

            // --- NIVEL 2 ---
            Grafo.AgregarNodo("HAB_CALIBRE_PESADO", new Habilidad(
                "HAB_CALIBRE_PESADO", "Calibre Pesado",
                "Aumenta el daño base de todas las torres en un 30%.",
                2, 2, "Ofensiva"
            ));

            Grafo.AgregarNodo("HAB_DISPARO_CRITICO", new Habilidad(
                "HAB_DISPARO_CRITICO", "Disparo Crítico",
                "25% de probabilidad de infligir daño crítico x2 en cada disparo.",
                2, 2, "Ofensiva"
            ));

            Grafo.AgregarNodo("HAB_CAZA_RECOMPENSAS", new Habilidad(
                "HAB_CAZA_RECOMPENSAS", "Cazador de Recompensas",
                "Cada enemigo derrotado otorga +35% de oro y +25% de experiencia.",
                2, 2, "Economía"
            ));

            Grafo.AgregarNodo("HAB_TORRE_HIELO", new Habilidad(
                "HAB_TORRE_HIELO", "Munición Criogénica",
                "Todas las torres ralentizan un 20% al impactar y las Criogénicas ganan +10% de frenado.",
                2, 2, "Control"
            ));

            Grafo.AgregarNodo("HAB_REPARACION_AUTO", new Habilidad(
                "HAB_REPARACION_AUTO", "Nanobots Reparadores",
                "Repara automáticamente +25 de salud a la base al superar cada oleada.",
                2, 2, "Defensiva"
            ));

            // --- NIVEL 3 ---
            Grafo.AgregarNodo("HAB_CANON_AREA", new Habilidad(
                "HAB_CANON_AREA", "Torre de Fragmentación",
                "Amplía el radio de explosión de los Cañones de Área de 1.6 a 2.4 unidades.",
                3, 3, "Ofensiva"
            ));

            Grafo.AgregarNodo("HAB_VENTISCA", new Habilidad(
                "HAB_VENTISCA", "Ventisca Prolongada",
                "+20% de alcance en torres Criogénicas y la ralentización dura 4 segundos.",
                3, 3, "Control"
            ));

            Grafo.AgregarNodo("HAB_TORRE_LASER", new Habilidad(
                "HAB_TORRE_LASER", "Torre Láser Perforante",
                "El Haz Láser inflige +30% de daño y derrite escudos enemigos.",
                3, 3, "Ofensiva"
            ));

            Grafo.AgregarNodo("HAB_PULSO_EMP", new Habilidad(
                "HAB_PULSO_EMP", "Pulso EMP Criogénico",
                "HABILIDAD ACTIVA: congela a todos los enemigos 3.5s y destruye sus escudos.",
                3, 3, "Defensiva"
            ));

            // --- NIVEL 4 ---
            Grafo.AgregarNodo("HAB_CERO_ABSOLUTO", new Habilidad(
                "HAB_CERO_ABSOLUTO", "Cero Absoluto",
                "Las torres Criogénicas tienen 20% de probabilidad de congelar al enemigo por 1.5s.",
                4, 4, "Control"
            ));

            // --- NIVEL 5 (Habilidad Definitiva - Cúspide del DAG) ---
            Grafo.AgregarNodo("HAB_SOBRECARGA", new Habilidad(
                "HAB_SOBRECARGA", "Sobrecarga de Red Definitiva",
                "HABILIDAD ACTIVA: +50% de daño y doble cadencia en toda la red MST durante 10s.",
                5, 5, "Definitiva"
            ));

            // --- EXPANSIÓN: 7 HABILIDADES ADICIONALES (22 en total) ---
            Grafo.AgregarNodo("HAB_BARRICADA", new Habilidad(
                "HAB_BARRICADA", "Barricada Táctica (Dijkstra)",
                "HABILIDAD ACTIVA: bloquea un tramo del camino 15s; Dijkstra recalcula y desvía a los enemigos.",
                2, 2, "Control"
            ));

            Grafo.AgregarNodo("HAB_INGENIERIA", new Habilidad(
                "HAB_INGENIERIA", "Ingeniería Avanzada",
                "Las mejoras de torre (Nivel 2 y 3) cuestan un 20% menos.",
                2, 2, "Economía"
            ));

            Grafo.AgregarNodo("HAB_RED_RADAR", new Habilidad(
                "HAB_RED_RADAR", "Enlace de Radar",
                "+1.5 de radio de enlace en la red MST y el tope de sinergia sube de 35% a 50%.",
                2, 3, "Economía"
            ));

            Grafo.AgregarNodo("HAB_RECICLAJE", new Habilidad(
                "HAB_RECICLAJE", "Reciclaje de Chatarra",
                "Vender una torre devuelve el 85% del oro invertido en lugar del 70%.",
                2, 3, "Economía"
            ));

            Grafo.AgregarNodo("HAB_ARCO_VOLTAICO", new Habilidad(
                "HAB_ARCO_VOLTAICO", "Arco Voltaico",
                "La Bobina Tesla encadena 2 rebotes adicionales (recorrido BFS más profundo).",
                3, 3, "Ofensiva"
            ));

            Grafo.AgregarNodo("HAB_BOMBARDEO", new Habilidad(
                "HAB_BOMBARDEO", "Bombardeo Orbital",
                "HABILIDAD ACTIVA: lluvia de proyectiles explosivos sobre todos los enemigos en pantalla.",
                3, 4, "Ofensiva"
            ));

            Grafo.AgregarNodo("HAB_FOCO_LASER", new Habilidad(
                "HAB_FOCO_LASER", "Foco Sostenido",
                "El daño del Haz Láser escala hasta x3 (en vez de x2) al mantener el mismo objetivo.",
                3, 4, "Ofensiva"
            ));

            // La habilidad básica siempre empieza desbloqueada
            Grafo.ObtenerNodo("HAB_BASE").Valor.desbloqueada = true;

            // 2. Aristas de Precedencia del DAG (u -> v significa: 'u' es prerrequisito de 'v')
            // Salidas de la Raíz (Nivel 1)
            Grafo.AgregarArista("HAB_BASE", "HAB_BALAS_RAPIDAS", 1f);
            Grafo.AgregarArista("HAB_BASE", "HAB_GENERADOR", 1f);
            Grafo.AgregarArista("HAB_BASE", "HAB_ESCUDO_BASE", 1f);

            // Nivel 2 (Dependencias de Nivel 1)
            Grafo.AgregarArista("HAB_BALAS_RAPIDAS", "HAB_CALIBRE_PESADO", 1f);
            Grafo.AgregarArista("HAB_BALAS_RAPIDAS", "HAB_DISPARO_CRITICO", 1f);
            Grafo.AgregarArista("HAB_GENERADOR", "HAB_CAZA_RECOMPENSAS", 1f);
            Grafo.AgregarArista("HAB_GENERADOR", "HAB_TORRE_HIELO", 1f);
            Grafo.AgregarArista("HAB_ESCUDO_BASE", "HAB_REPARACION_AUTO", 1f);

            // Nivel 3 (Dependencias cruzadas y avanzadas)
            Grafo.AgregarArista("HAB_CALIBRE_PESADO", "HAB_CANON_AREA", 1f);
            Grafo.AgregarArista("HAB_TORRE_HIELO", "HAB_VENTISCA", 1f);
            Grafo.AgregarArista("HAB_DISPARO_CRITICO", "HAB_TORRE_LASER", 1f);
            Grafo.AgregarArista("HAB_GENERADOR", "HAB_TORRE_LASER", 1f);
            Grafo.AgregarArista("HAB_ESCUDO_BASE", "HAB_PULSO_EMP", 1f);

            // Nivel 4 (Control supremo)
            Grafo.AgregarArista("HAB_VENTISCA", "HAB_CERO_ABSOLUTO", 1f);

            // Nivel 5: Habilidad Definitiva (convergencia de las 3 ramas principales)
            Grafo.AgregarArista("HAB_TORRE_LASER", "HAB_SOBRECARGA", 1f);
            Grafo.AgregarArista("HAB_CERO_ABSOLUTO", "HAB_SOBRECARGA", 1f);
            Grafo.AgregarArista("HAB_REPARACION_AUTO", "HAB_SOBRECARGA", 1f);

            // Aristas de la expansión (se mantiene la propiedad de DAG: sin ciclos)
            Grafo.AgregarArista("HAB_ESCUDO_BASE", "HAB_BARRICADA", 1f);
            Grafo.AgregarArista("HAB_GENERADOR", "HAB_INGENIERIA", 1f);
            Grafo.AgregarArista("HAB_INGENIERIA", "HAB_RED_RADAR", 1f);
            Grafo.AgregarArista("HAB_CAZA_RECOMPENSAS", "HAB_RECICLAJE", 1f);
            Grafo.AgregarArista("HAB_DISPARO_CRITICO", "HAB_ARCO_VOLTAICO", 1f);
            Grafo.AgregarArista("HAB_CANON_AREA", "HAB_BOMBARDEO", 1f);
            Grafo.AgregarArista("HAB_TORRE_LASER", "HAB_FOCO_LASER", 1f);
            Grafo.AgregarArista("HAB_TORRE_HIELO", "HAB_PULSO_EMP", 1f);
        }

        /// <summary>
        /// Obtiene la lista de habilidades que son prerrequisitos inmediatos de una habilidad.
        /// (Son los predecesores en el grafo dirigido).
        /// </summary>
        public List<Habilidad> ObtenerPrerrequisitos(string idHabilidad)
        {
            List<Habilidad> prereqs = new List<Habilidad>();
            List<NodoGrafo<Habilidad>> nodosPred = Grafo.ObtenerPredecesores(idHabilidad);

            for (int i = 0; i < nodosPred.Count; i++)
            {
                prereqs.Add(nodosPred[i].Valor);
            }

            return prereqs;
        }

        /// <summary>
        /// Obtiene la lista de habilidades que se habilitan o tienen como requisito directo a esta habilidad.
        /// (Son los vecinos salientes en el grafo dirigido).
        /// </summary>
        public List<Habilidad> ObtenerHabilidadesSiguientes(string idHabilidad)
        {
            List<Habilidad> siguientes = new List<Habilidad>();
            NodoGrafo<Habilidad> nodo = Grafo.ObtenerNodo(idHabilidad);

            if (nodo != null)
            {
                for (int i = 0; i < nodo.Vecinos.Count; i++)
                {
                    siguientes.Add(nodo.Vecinos[i].Destino.Valor);
                }
            }

            return siguientes;
        }

        /// <summary>
        /// Evalúa el estado de una habilidad para un jugador específico.
        /// </summary>
        public EstadoHabilidadEnum ObtenerEstadoHabilidad(string idHabilidad, Jugador jugador, out string detalle)
        {
            detalle = string.Empty;
            NodoGrafo<Habilidad> nodo = Grafo.ObtenerNodo(idHabilidad);

            if (nodo == null)
            {
                detalle = "La habilidad no existe en el grafo.";
                return EstadoHabilidadEnum.FaltanPrerrequisitos;
            }

            Habilidad hab = nodo.Valor;

            if (hab.desbloqueada || (jugador != null && jugador.TieneHabilidad(idHabilidad)))
            {
                detalle = "Habilidad ya desbloqueada y activa.";
                return EstadoHabilidadEnum.Desbloqueada;
            }

            // Verificar prerrequisitos (todos los predecesores deben estar desbloqueados)
            List<Habilidad> prereqs = ObtenerPrerrequisitos(idHabilidad);
            List<string> faltantes = new List<string>();

            for (int i = 0; i < prereqs.Count; i++)
            {
                if (!prereqs[i].desbloqueada && (jugador == null || !jugador.TieneHabilidad(prereqs[i].id)))
                {
                    faltantes.Add(prereqs[i].nombre);
                }
            }

            if (faltantes.Count > 0)
            {
                detalle = $"Requiere completar: {string.Join(", ", faltantes)}";
                return EstadoHabilidadEnum.FaltanPrerrequisitos;
            }

            // Verificar nivel
            if (jugador != null && jugador.nivel < hab.nivelRequerido)
            {
                detalle = $"Requiere Nivel {hab.nivelRequerido} (Nivel actual: {jugador.nivel})";
                return EstadoHabilidadEnum.NivelInsuficiente;
            }

            // Verificar puntos de habilidad
            if (jugador != null && jugador.puntosHabilidad < hab.costoPuntos)
            {
                detalle = $"Requiere {hab.costoPuntos} puntos (Puntos disponibles: {jugador.puntosHabilidad})";
                return EstadoHabilidadEnum.PuntosInsuficientes;
            }

            detalle = $"¡Lista para desbloquear! Costo: {hab.costoPuntos} pts";
            return EstadoHabilidadEnum.DisponibleParaDesbloquear;
        }

        /// <summary>
        /// Intenta desbloquear una habilidad para el jugador, validando el DAG y deduciendo recursos.
        /// </summary>
        public bool IntentarDesbloquear(string idHabilidad, Jugador jugador, out string mensaje)
        {
            if (jugador == null)
            {
                mensaje = "No hay ningún jugador activo para asignar la habilidad.";
                return false;
            }

            EstadoHabilidadEnum estado = ObtenerEstadoHabilidad(idHabilidad, jugador, out string detalle);

            if (estado == EstadoHabilidadEnum.Desbloqueada)
            {
                mensaje = $"La habilidad '{idHabilidad}' ya fue adquirida previamente.";
                return false;
            }

            if (estado != EstadoHabilidadEnum.DisponibleParaDesbloquear)
            {
                mensaje = $"No se puede desbloquear '{idHabilidad}': {detalle}";
                return false;
            }

            NodoGrafo<Habilidad> nodo = Grafo.ObtenerNodo(idHabilidad);
            Habilidad hab = nodo.Valor;

            // Deducir puntos y registrar
            jugador.puntosHabilidad -= hab.costoPuntos;
            hab.desbloqueada = true;

            if (!jugador.habilidadesDesbloqueadas.Contains(idHabilidad))
            {
                jugador.habilidadesDesbloqueadas.Add(idHabilidad);
            }

            mensaje = $"¡Habilidad '{hab.nombre}' desbloqueada con éxito! (-{hab.costoPuntos} pts | Restan: {jugador.puntosHabilidad} pts)";
            return true;
        }

        /// <summary>
        /// Ejecuta el Algoritmo de Kahn y construye una explicación pedagógica completa
        /// mostrando el grado de entrada inicial, la cola y el orden resultante.
        /// </summary>
        public string ObtenerOrdenKahnExplicado(out List<string> listaOrdenada)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=================================================");
            sb.AppendLine("📘 DEMOSTRACIÓN DEL ALGORITMO DE KAHN (ORDEN TOPOLÓGICO)");
            sb.AppendLine("=================================================");
            sb.AppendLine("Propósito: Calcular la secuencia válida de desbloqueo en un DAG.");
            sb.AppendLine();

            // 1. Grados de entrada iniciales
            Dictionary<string, int> inDegrees = Grafo.CalcularGradosEntrada();
            sb.AppendLine("--- 1. Grado de Entrada Inicial (In-Degree) ---");
            foreach (var kvp in inDegrees)
            {
                NodoGrafo<Habilidad> n = Grafo.ObtenerNodo(kvp.Key);
                string nombre = n != null ? n.Valor.nombre : kvp.Key;
                sb.AppendLine($" • [{kvp.Key}] '{nombre}': In-Degree = {kvp.Value}");
            }
            sb.AppendLine();

            // 2. Ejecutar Kahn
            bool esDAGValido = Grafo.OrdenamientoTopologicoKahn(out listaOrdenada, out bool tieneCiclo);

            if (!esDAGValido || tieneCiclo)
            {
                sb.AppendLine("❌ ERROR: El grafo contiene uno o más CICLOS.");
                sb.AppendLine("No existe un orden topológico válido porque hay dependencias circulares.");
                if (Grafo.DetectarCiclos(out List<string> ciclo))
                {
                    sb.AppendLine($"Ciclo detectado: {string.Join(" -> ", ciclo)}");
                }
                return sb.ToString();
            }

            sb.AppendLine("--- 2. Secuencia Topológica Resultante (Kahn) ---");
            for (int i = 0; i < listaOrdenada.Count; i++)
            {
                NodoGrafo<Habilidad> n = Grafo.ObtenerNodo(listaOrdenada[i]);
                string nom = n != null ? n.Valor.nombre : listaOrdenada[i];
                sb.AppendLine($"  {i + 1}. [{listaOrdenada[i]}] -> {nom}");
            }

            sb.AppendLine();
            sb.AppendLine("✅ Conclusión Teórica: Cada habilidad aparece estrictamente");
            sb.AppendLine("   DESPUÉS de todos sus prerrequisitos en la secuencia lineal.");
            sb.AppendLine("   Garantía de Grafo Acíclico Dirigido (DAG).");

            return sb.ToString();
        }

        /// <summary>
        /// Prueba agregar una nueva arista de precedencia (origen -> destino).
        /// Si la arista formaría un ciclo, la revierte inmediatamente y explica el error.
        /// Ideal para sustentación académica ante el docente.
        /// </summary>
        public bool ProbarAgregarAristaConValidacionDAG(string idOrigen, string idDestino, out string mensaje)
        {
            if (!Grafo.ExisteNodo(idOrigen) || !Grafo.ExisteNodo(idDestino))
            {
                mensaje = "Uno o ambos nodos no existen en el grafo.";
                return false;
            }

            if (idOrigen == idDestino)
            {
                mensaje = $"¡Error de Ciclo Inmediato! El nodo '{idOrigen}' no puede tener una arista reflexiva hacia sí mismo.";
                return false;
            }

            if (Grafo.ExisteArista(idOrigen, idDestino))
            {
                mensaje = $"La dependencia '{idOrigen}' -> '{idDestino}' ya existe actualmente en el grafo.";
                return false;
            }

            // Agregar temporalmente la arista
            Grafo.AgregarArista(idOrigen, idDestino, 1f);

            // Verificar si se introdujo un ciclo
            if (Grafo.DetectarCiclos(out List<string> ciclo))
            {
                // Revertir arista
                Grafo.EliminarArista(idOrigen, idDestino);

                mensaje = $"⛔ RECHAZADO POR DETECCIÓN DE CICLOS (DAG)\n" +
                          $"Agregar '{idOrigen}' -> '{idDestino}' crearía el siguiente ciclo:\n" +
                          $"{string.Join(" ➔ ", ciclo)}\n\n" +
                          $"Un Árbol de Habilidades debe ser un Grafo Dirigido Acíclico (DAG).\n" +
                          $"Una dependencia circular crearía un bloqueo mutuo donde ninguna habilidad podría desbloquearse.";
                return false;
            }

            mensaje = $"✅ ÉXITO: Dependencia '{idOrigen}' ➔ '{idDestino}' agregada correctamente.\n" +
                      $"El grafo continúa siendo un DAG acíclico válido.";
            return true;
        }

        /// <summary>
        /// Sincroniza el estado de las habilidades con la lista del jugador.
        /// </summary>
        public void SincronizarConJugador(Jugador jugador)
        {
            if (jugador == null) return;

            foreach (var kvp in Grafo.Nodos)
            {
                if (kvp.Key == "HAB_BASE")
                {
                    kvp.Value.Valor.desbloqueada = true;
                    if (!jugador.TieneHabilidad("HAB_BASE"))
                    {
                        jugador.habilidadesDesbloqueadas.Add("HAB_BASE");
                    }
                }
                else
                {
                    kvp.Value.Valor.desbloqueada = jugador.TieneHabilidad(kvp.Key);
                }
            }
        }

        /// <summary>
        /// Reinicia el progreso de habilidades para pruebas repetibles.
        /// </summary>
        public void ReiniciarHabilidades(Jugador jugador)
        {
            foreach (var kvp in Grafo.Nodos)
            {
                kvp.Value.Valor.desbloqueada = (kvp.Key == "HAB_BASE");
            }

            if (jugador != null)
            {
                jugador.habilidadesDesbloqueadas.Clear();
                jugador.habilidadesDesbloqueadas.Add("HAB_BASE");
                jugador.puntosHabilidad = 0; // Puntos obtenidos progresivamente por juego
                jugador.experiencia = 0;
            }
        }

        /// <summary>
        /// Retorna una lista con todas las habilidades en el grafo.
        /// </summary>
        public List<Habilidad> ObtenerTodasLasHabilidades()
        {
            List<Habilidad> lista = new List<Habilidad>();
            foreach (var kvp in Grafo.Nodos)
            {
                lista.Add(kvp.Value.Valor);
            }
            return lista;
        }
    }
}
