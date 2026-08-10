using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class day_night_cycle : MonoBehaviour
{

    public float speed = 2f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Vector3.down * Time.deltaTime * speed);
    }
}
