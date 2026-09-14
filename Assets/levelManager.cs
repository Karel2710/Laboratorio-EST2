using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class levelManager : MonoBehaviour{
    public static levelManager main;
    public Transform Start;
    public Transform[] path;
    private void Awake(){
        main = this;
    }
}
