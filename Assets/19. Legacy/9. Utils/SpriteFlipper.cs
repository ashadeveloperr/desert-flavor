using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study.Utilities
{
    public class SpriteFlipper : StateMachineBehaviour
    {
        private SpriteRenderer spriteRenderer;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            base.OnStateEnter(animator, stateInfo, layerIndex);
            if (spriteRenderer == null) spriteRenderer = animator.GetComponent<SpriteRenderer>();

            if (spriteRenderer)
            {
                spriteRenderer.flipX = true;
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            base.OnStateExit(animator, stateInfo, layerIndex);

            if (spriteRenderer)
            {
                spriteRenderer.flipX = false;
            }
        }
    }
}




