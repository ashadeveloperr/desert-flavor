using System;
using DG.Tweening;
using Study_OOP_And_Design_Pattern.Creational.Builder;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_DoTween
{
    // DoTween 에제 1 - 기본 트윈
    // - 이동, 회전, 크기, 페이드, 흔들림
    
    public class A_DoTweenBasicExample : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Vector3 startPosition;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            startPosition = transform.position;
        }

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Digit1))
            {
                // transform.DOMove | .DOMove
                transform.DOMove(startPosition + new Vector3(1, 1, 0), 1.0f)
                    .SetEase(Ease.InCirc);
            }
            if (SimpleInput.GetKeyDown(Key.Digit2))
            {
                // transform.DORotate | .DORotate
                transform.DORotate(new Vector3(0, 0, 180), 1.0f);
            }
            if (SimpleInput.GetKeyDown(Key.Digit3))
            {
                // transform.DOScale | .DOScale
                transform.localScale = Vector3.zero;
                transform.DOScale(new Vector3(1, 1, 1), 1.0f)
                    .SetEase(Ease.OutBack);
                // transform.DOScale(1, 1.0f); 이렇게 써도 됨
            }
            if (SimpleInput.GetKeyDown(Key.Digit4))
            {
                // .DOFade
                // : alpha 값을 변경하여 사라지거나 나타나게 한다
                spriteRenderer.DOFade(0f, 1.0f); // 사라짐
            }
            if (SimpleInput.GetKeyDown(Key.Digit5))
            {
                spriteRenderer.DOFade(1f, 1.0f); // 나타남
            }
            if (SimpleInput.GetKeyDown(Key.Digit6))
            {
                // .DOShakePosition
                // : 매개변수로 주어진 벡터 범위내에서 흔들리는 연출을 수행한다 
                transform.DOShakePosition(0.3f, new Vector3(0.4f, 0.4f, 0.4f));
            }
            if (SimpleInput.GetKeyDown(Key.Digit7))
            {
                // .DOPunchPosition
                // : 매개변수로 주어진 벡터를 축으로 삼아 흔들린다.
                transform.DOPunchPosition(new Vector3(1,1,0), 1f);
            }

            if (SimpleInput.GetKeyDown(Key.Digit0))
            {
                // .DOKill
                transform.DOKill();
            }
        }
    }
}