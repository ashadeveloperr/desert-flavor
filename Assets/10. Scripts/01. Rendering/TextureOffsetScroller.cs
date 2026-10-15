using System;
using UnityEngine;

namespace Study3D.Rendering
{
    public class TextureOffsetScroller : MonoBehaviour
    {
        // Offset 좌표를 실시간으로 바꿔주면 어떻게 되는지 봅시다
        [Header("Settings")] 
        public Vector2 speed = new Vector2(0f, 0.5f);

        private string textureProperty = "_BaseMap";
        private int propertyId;
        private Material targetMaterial;
        private Vector2 offset;
        
        private void Awake()
        {
            targetMaterial = GetComponent<Renderer>().material;
            propertyId = Shader.PropertyToID(textureProperty);
            // Shader에게 특정 메테리얼이 갖고 있는 속성이나 함수에 접근하기 위해 
            // 문자열을 이용해서 Id를 가져옵니다.
            
            // targetMaterial에서 현재 _BaseMap을 적용하는 Offset 값을 가져옵니다.
            offset = targetMaterial.GetTextureOffset(propertyId);
        }

        private void Update()
        {
            offset += speed * Time.deltaTime;
            offset.x = Mathf.Repeat(offset.x, 1);
            offset.y = Mathf.Repeat(offset.y, 1);
            targetMaterial.SetTextureOffset(propertyId, offset);
        }

        private void OnDestroy()
        {
            Destroy(targetMaterial);
        }
    }
}
