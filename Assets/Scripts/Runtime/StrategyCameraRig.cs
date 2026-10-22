using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 탑다운 전략 카메라(C8). 이 오브젝트가 시선 중심(피벗)이고, Cinemachine 카메라가 피벗을 따라가며 내려다본다.
    /// - WASD/방향키 또는 화면 가장자리: 이동 (카메라가 바라보는 방향 기준)
    /// - 마우스 휠: 줌 / Q·E: 60° 회전
    /// 속도·각도·거리 범위는 인스펙터에서 바꾼다. 맵 밖으로는 나가지 않는다.
    /// </summary>
    public sealed class StrategyCameraRig : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private CinemachineCamera virtualCamera;

        [Header("이동")]
        [SerializeField] private float moveSpeed = 6f;
        [Tooltip("이동 가속·감속의 부드러움(클수록 빠르게 붙고 멈춤)")]
        [SerializeField] private float moveSmoothing = 8f;
        [SerializeField] private bool edgeScroll = true;
        [SerializeField] private float edgeMarginPixels = 12f;

        [Header("줌")]
        [SerializeField] private float pitch = 50f;
        [SerializeField] private float distance = 11f;
        [SerializeField] private float minDistance = 4f;
        [SerializeField] private float maxDistance = 24f;
        [SerializeField] private float zoomStep = 1.2f;
        [SerializeField] private float zoomSmoothing = 10f;

        [Header("회전")]
        [SerializeField] private float rotateStep = 60f;
        [SerializeField] private float rotateSmoothing = 10f;

        private float _yaw;
        private float _targetYaw;
        private float _currentDistance = -1f;
        private Vector3 _velocity;
        private CinemachineFollow _follow;

        /// <summary>코드로 리그를 만들 때 연결한다.</summary>
        public void Bind(GameSession gameSession, CinemachineCamera cam)
        {
            session = gameSession;
            virtualCamera = cam;
            _follow = null;
        }

        private void LateUpdate()
        {
            if (virtualCamera == null) return;
            if (_follow == null)
            {
                _follow = virtualCamera.GetComponent<CinemachineFollow>();
                if (_follow != null)
                {
                    // 위치 감쇠가 있으면 카메라 위치는 늦게 따라오는데 시선(HardLookAt)은 즉시 피벗을 봐서
                    // 이동할 때 살짝 회전했다 돌아온다. 감쇠는 끄고 부드러움은 리그 이동 자체에서 만든다.
                    var tracker = _follow.TrackerSettings;
                    tracker.PositionDamping = Vector3.zero;
                    _follow.TrackerSettings = tracker;
                }
            }
            if (_currentDistance < 0f) _currentDistance = distance;

            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;

            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
                if (kb.qKey.wasPressedThisFrame) _targetYaw -= rotateStep;
                if (kb.eKey.wasPressedThisFrame) _targetYaw += rotateStep;
            }
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                bool insideWindow = p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height;
                if (edgeScroll && insideWindow && Application.isFocused && !HudInputBlocker.IsOverHud(p))
                {
                    if (p.x < edgeMarginPixels) move.x -= 1;
                    if (p.x > Screen.width - edgeMarginPixels) move.x += 1;
                    if (p.y < edgeMarginPixels) move.y -= 1;
                    if (p.y > Screen.height - edgeMarginPixels) move.y += 1;
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !HudInputBlocker.IsOverHud(p))
                {
                    distance = Mathf.Clamp(distance * (scroll > 0 ? 1f / zoomStep : zoomStep), minDistance, maxDistance);
                }
            }

            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, 1f - Mathf.Exp(-rotateSmoothing * dt));
            _currentDistance = Mathf.Lerp(_currentDistance, distance, 1f - Mathf.Exp(-zoomSmoothing * dt));
            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);

            // 목표 속도로 부드럽게 가속·감속(관성 스크롤). 줌이 멀수록 빠르게.
            Vector3 targetVelocity = move.sqrMagnitude > 0f
                ? yawRotation * new Vector3(move.x, 0f, move.y).normalized * moveSpeed * (_currentDistance / 11f)
                : Vector3.zero;
            _velocity = Vector3.Lerp(_velocity, targetVelocity, 1f - Mathf.Exp(-moveSmoothing * dt));
            if (_velocity.sqrMagnitude > 1e-6f) transform.position = ClampToMap(transform.position + _velocity * dt);

            if (_follow != null)
            {
                float rad = pitch * Mathf.Deg2Rad;
                _follow.FollowOffset = yawRotation * new Vector3(0f, Mathf.Sin(rad) * _currentDistance, -Mathf.Cos(rad) * _currentDistance);
            }
        }

        private Vector3 ClampToMap(Vector3 position)
        {
            if (session == null || !session.IsReady) return position;
            Vector3 center = session.transform.position;
            float limit = session.TileSize * (session.MapRadius + 1) * 1.8f;
            Vector3 offset = position - center;
            offset.y = 0f;
            if (offset.magnitude > limit) offset = offset.normalized * limit;
            return center + offset + Vector3.up * (position.y - center.y);
        }
    }
}
