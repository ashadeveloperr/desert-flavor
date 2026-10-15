using System;
using UnityEngine;

namespace Study3D.Transforms
{
    public class Spinner : MonoBehaviour
    {
        [Header("Settings")] 
        public Vector3 axis;
        public float degreesPerSecond = 90f;
        public Space space = Space.Self;

        private void Update()
        {
            Vector3 rotVector = axis.normalized;
            transform.Rotate(rotVector * (degreesPerSecond * Time.deltaTime), space);
        }
    }
}