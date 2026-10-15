using System;
using UnityEngine;

namespace Study3D.AirPlaneGame
{
    public partial class AirPlaneGameManager : MonoBehaviour
    {
        private void Awake()
        {
            InitFlyLog();
            InitGameFlow();
        }
    }
}