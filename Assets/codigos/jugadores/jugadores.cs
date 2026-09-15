using System;
using UnityEngine;

[System.Serializable]
public class Jugador
{
    public int Id;
    public string nombre;
    public int vida;

    public Jugador()
    {
        this.vida = 100;
        this.Id = 0;
        this.nombre = string.Empty;
    }

    public Jugador(int vida, int Id, string nombre)
    {
        this.vida = vida;
        this.Id = Id;
        this.nombre = nombre;
    }

    public override string ToString()
    {
        return $"[ID: {Id}] {nombre} - Vida: {vida}";
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