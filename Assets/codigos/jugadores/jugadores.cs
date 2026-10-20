using System;
using UnityEngine;

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