using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.Rendering
{
    public class MaterialSwapper : MonoBehaviour
    {
        public Material[] materials = new Material[0];
        private Renderer myRenderer;
        private int index = 0;

        private void Awake()
        {
            // Renderer 클래스는 모든 Renderer들의 부모클래스 입니다.
            // SpriteRenderer, MeshRenderer등이 해당 클래스를 상속받아서
            // 구현된 컴포넌트들 입니다.
            myRenderer = GetComponent<Renderer>();
            Apply();
        }

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Space))
            {
                index = (index + 1) % materials.Length;
                Apply();
            }
        }

        private void Apply()
        {
            // 먼저 메테리얼 객체를 가져옵니다.
            Material mat = materials[index];
            // myRenderer에 메테리얼을 변경합니다.
            myRenderer.sharedMaterial = mat;
        }
    }
}