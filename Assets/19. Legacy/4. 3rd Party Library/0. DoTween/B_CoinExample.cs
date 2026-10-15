using System;
using DG.Tweening;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_DoTween
{
    public class B_CoinExample : MonoBehaviour
    {
        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Digit1))
            {
                SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

                Sequence seq = DOTween.Sequence();
                seq.Append(transform.DOMove(transform.position + Vector3.up, 0.3f).SetEase(Ease.OutQuad));
                seq.Join(transform.DOScale(1.4f, 0.3f));
                seq.Append(spriteRenderer.DOFade(0f, 0.3f));
                seq.AppendCallback(() => Debug.Log("코인 획득! 점수 +10"));
                seq.OnComplete(() => Destroy(gameObject));
                
                // Sequence seq = DOTween.Sequence()
                //     .Append(
                //         transform
                //             .DOMove(transform.position + Vector3.up, 0.3f)
                //             .SetEase(Ease.OutQuad))
                //     .Join(
                //         transform.DOScale(1.4f, 0.3f))
                //     .Append(
                //         spriteRenderer.DOFade(0f, 0.3f))
                //     .AppendCallback(() => Debug.Log("코인 획득! 점수 +10"))
                //     .OnComplete(() => Destroy(gameObject));
            }
        }
    }
}