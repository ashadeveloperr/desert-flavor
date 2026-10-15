using System;
using DG.Tweening;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_DoTween
{
    public class C_HitFeedbackExample : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Tween idleTween;

        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            idleTween = transform
                .DOMoveY(transform.position.y + 0.5f, 1.0f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutCirc)
                .SetLink(gameObject);
        }

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Space)) Hit();
        }

        private void Hit()
        {
            transform.DOShakePosition(0.3f, 0.3f);
            spriteRenderer.DOColor(Color.red, 0.1f)
                .SetLoops(2, LoopType.Yoyo);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}