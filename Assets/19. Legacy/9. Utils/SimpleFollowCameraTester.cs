using UnityEngine;
using UnityEngine.InputSystem;

namespace Study.Utilities
{
    [RequireComponent(typeof(SimpleFollowCamera))]
    public class SimpleFollowCameraTester : MonoBehaviour
    {
        public Transform Target;
        private SimpleFollowCamera followCam;

        private void Awake()
        {
            followCam = GetComponent<SimpleFollowCamera>();
        }

        private void Update()
        {
            if(SimpleInput.GetKeyDown(Key.F1))
            {
                followCam.ChangeState(SimpleFollowCamera.States.Stopped);
            }
            else if (SimpleInput.GetKeyDown(Key.F2))
            {
                followCam.ChangeState(SimpleFollowCamera.States.Holding);
            }
            else if (SimpleInput.GetKeyDown(Key.F3))
            {
                followCam.Target = Target;
                followCam.ChangeState(SimpleFollowCamera.States.Following);
            }
            else if (SimpleInput.GetKeyDown(Key.F4))
            {
                followCam.ChangeState(SimpleFollowCamera.States.Following);
            }
        }
    }

}

