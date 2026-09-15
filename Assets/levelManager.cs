using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class levelManager : MonoBehaviour
{
    public static levelManager main;

    [Header("Puntos de Ruta")]
    public Transform startPoint;
    public Transform[] path;

    // Propiedad para compatibilidad con código que use Path con mayúscula
    public Transform[] Path => path;
    public Transform Start => startPoint;

    private void Awake()
    {
        main = this;
    }
}
