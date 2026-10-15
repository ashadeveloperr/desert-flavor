using System;
using UnityEngine;

namespace Study3D.ThreeDPhysics
{
    public class TriggerCounterZone : MonoBehaviour
    {
        public Color idleColor;
        public Color occupiedColor;

        private Material material;
        private bool isInside = false;
        
        private void Awake()
        {
            material = GetComponent<Renderer>().material;
            Refresh();
        }

        private void Refresh()
        {
            material.SetColor("_BaseColor", isInside ? occupiedColor : idleColor);
        }

        private void OnTriggerEnter(Collider other)
        {
            isInside = true;
            Refresh();
        }

        private void OnTriggerExit(Collider other)
        {
            isInside = false;
            Refresh();
        }
    }
}