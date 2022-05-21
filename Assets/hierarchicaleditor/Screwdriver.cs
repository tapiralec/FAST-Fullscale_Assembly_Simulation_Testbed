using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayStructure
{
    public class Screwdriver : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnCollisionEnter(Collision other)
        {
            //TODO: make sure this is held in a hand so users can't just tap the structure to the key.
            if (other.gameObject.CompareTag("Screw"))
            {
                var sp = other.gameObject.GetComponent<ScrewPiece>();
                sp.TryScrewIn(this);
            }
            else
            {
                //Debug.Log(other.gameObject.name);
            }
        }
    }

}