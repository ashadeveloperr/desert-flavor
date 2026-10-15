using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_OOP_And_Design_Pattern.Creational.Builder
{
    public class BlacksmithBuilderDemo : MonoBehaviour
    {
        private Blacksmith blacksmith;

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Space))
            {
                blacksmith = new Blacksmith();
                Sword sword = blacksmith.MakeSword();
                Debug.Log(sword.ToString());
            }
        }
    }
}