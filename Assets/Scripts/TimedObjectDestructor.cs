using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimedObjectDestructor : MonoBehaviour
{
    public float time = 3f;

    void Start()
    {
        Destroy(gameObject, time);
    }
}
