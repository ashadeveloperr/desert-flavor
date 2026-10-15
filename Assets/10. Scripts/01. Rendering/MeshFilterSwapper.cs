using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Study3D.Rendering
{
    public class MeshFilterSwapper : MonoBehaviour
    {
        [SerializeField] private Mesh[] meshes;
        
        private MeshFilter meshFilter;
        private int index;
        
        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            index = 0;
            Apply();
        }

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Space))
            {
                index = (index + 1) % meshes.Length;
                Apply();
            }
        }

        private void Apply()
        {
            // 먼저 mesh 객체 가져옵니다.
            Mesh mesh = meshes[index];
            // meshFilter에 적용해 줍니다.
            meshFilter.sharedMesh = mesh;
        }
    }
}