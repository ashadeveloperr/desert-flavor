using System;
using UnityEngine;

namespace Study3D.FpsGame.Player
{
    public partial class PlayerController
    {
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            LockCursor();
            initialCameraRotation = cameraTransform.localRotation;
        }

        private void OnEnable()
        {
            
        }

        private void OnDisable()
        {
            
        }
        
        private void Update()
        {
            UpdateMovement();
            UpdateCameraRotation();
            UpdatePlayerInput();
        }
        
        private void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}