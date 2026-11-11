using System;
using UnityEngine;

/// <summary>
/// Especialización del comandante. El principal aplica el 100% del bono
/// y el comandante de apoyo del escuadrón aplica el 50%.
/// </summary>
public enum ClaseComandante
{
    Recluta = 0,
    IngenieroMST = 1,
    Artillero = 2,
    Criomante = 3,
    EstrategaEconomico = 4
}

[System.Serializable]
public class Jugador
{
    public int Id;
    public string nombre;
    public int vida;

    public int nivel = 1;
    public int experiencia = 0;
    public int experienciaSiguienteNivel = 100;
    public int puntosHabilidad = 0;
    public int oro = 100;
    public System.Collections.Generic.List<string> habilidadesDesbloqueadas = new System.Collections.Generic.List<string>();

    // Especialización y estadísticas de carrera (campos nuevos: los JSON antiguos cargan con valores por defecto)
    public ClaseComandante clase = ClaseComandante.Recluta;
    public int mejorPuntaje = 0;
    public int partidasJugadas = 0;

    public static string NombreClase(ClaseComandante c)
    {
        switch (c)
        {
            case ClaseComandante.IngenieroMST: return "Ingeniero MST";
            case ClaseComandante.Artillero: return "Artillero";
            case ClaseComandante.Criomante: return "Criomante";
            case ClaseComandante.EstrategaEconomico: return "Estratega Económico";
            default: return "Recluta";
        }
    }

    public static string DescripcionClase(ClaseComandante c)
    {
        switch (c)
        {
            case ClaseComandante.IngenieroMST: return "+1.5 de radio de enlace MST y +5% de sinergia extra por conexión.";
            case ClaseComandante.Artillero: return "+15% de daño en todas las torres y +20% de radio de explosión.";
            case ClaseComandante.Criomante: return "Ralentización +15% más fuerte, +1s de duración y +25% de daño de hielo.";
            case ClaseComandante.EstrategaEconomico: return "+20% de oro por oleada y torres 10% más baratas.";
            default: return "Sin especialización.";
        }
    }

    public string NombreClaseActual => NombreClase(clase);

    public Jugador()
    {
        this.vida = 100;
        this.Id = 0;
        this.nombre = string.Empty;
        this.nivel = 1;
        this.experiencia = 0;
        this.experienciaSiguienteNivel = 100;
        this.puntosHabilidad = 0;
        this.oro = 100;
        this.habilidadesDesbloqueadas = new System.Collections.Generic.List<string>();
    }

    public Jugador(int vida, int Id, string nombre)
    {
        this.vida = vida;
        this.Id = Id;
        this.nombre = nombre;
        this.nivel = 1;
        this.experiencia = 0;
        this.experienciaSiguienteNivel = 100;
        this.puntosHabilidad = 0;
        this.oro = 100;
        this.habilidadesDesbloqueadas = new System.Collections.Generic.List<string>();
    }

    public Jugador(int vida, int Id, string nombre, int nivel, int puntosHabilidad)
    {
        this.vida = vida;
        this.Id = Id;
        this.nombre = nombre;
        this.nivel = nivel > 0 ? nivel : 1;
        this.experiencia = 0;
        this.experienciaSiguienteNivel = this.nivel * 100;
        this.puntosHabilidad = puntosHabilidad >= 0 ? puntosHabilidad : 0;
        this.oro = 100;
        this.habilidadesDesbloqueadas = new System.Collections.Generic.List<string>();
    }

    /// <summary>
    /// Suma experiencia ganada en combate. Retorna true si el jugador subió de nivel.
    /// </summary>
    public bool GanarExperiencia(int xpGanada, out int nivelesSubidos, out int puntosGanados)
    {
        nivelesSubidos = 0;
        puntosGanados = 0;
        experiencia += xpGanada;

        while (experiencia >= experienciaSiguienteNivel)
        {
            experiencia -= experienciaSiguienteNivel;
            nivel++;
            experienciaSiguienteNivel = nivel * 100;
            puntosHabilidad += 1;
            nivelesSubidos++;
            puntosGanados += 1;
        }

        return nivelesSubidos > 0;
    }

    public bool TieneHabilidad(string idHabilidad)
    {
        return habilidadesDesbloqueadas != null && habilidadesDesbloqueadas.Contains(idHabilidad);
    }

    public void AgregarPuntosHabilidad(int puntos)
    {
        puntosHabilidad += puntos;
    }

    public override string ToString()
    {
        return $"[ID: {Id}] {nombre} - Vida: {vida} | Nvl: {nivel} (XP: {experiencia}/{experienciaSiguienteNivel}) | Pts Hab: {puntosHabilidad} | Oro: {oro} | Hab: {habilidadesDesbloqueadas.Count}";
    }
}

[System.Serializable]
public class torres
{
    public int daño;

    public torres()
    {
        this.daño = 10;
    }

    public torres(int daño)
    {
        this.daño = daño;
    }
}