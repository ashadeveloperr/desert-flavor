using System.Collections.Generic;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 마우스가 타일·시설 위에 있으면 커서 옆에 정보 팝업을 띄우고, 벗어나면 숨긴다(임시 IMGUI).
    /// 무엇을 가리키는지는 <see cref="BuildInputController"/>가 계산한다(변 가까이면 수로·수문).
    /// 내용 문구는 <see cref="FacilityInfo"/>에 있다.
    /// </summary>
    [RequireComponent(typeof(GameSession), typeof(BuildInputController), typeof(SettlementView))]
    public sealed class HoverTooltip : MonoBehaviour
    {
        [SerializeField] private Vector2 cursorOffset = new Vector2(18f, 18f);
        [SerializeField] private float width = 330f;
        [Tooltip("마우스가 멈춘 뒤 팝업이 뜨기까지(초). 0이면 바로")]
        [SerializeField] private float delay = 0.15f;

        private GameSession _session;
        private BuildInputController _input;
        private SettlementView _view;
        private object _lastTarget;
        private float _targetSince;
        private GUIStyle _style;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            _input = GetComponent<BuildInputController>();
            _view = GetComponent<SettlementView>();
        }

        private void OnGUI()
        {
            if (!_session.IsReady || Mouse.current == null) return;
            if (!_input.HoverTile.HasValue && _input.HoveredFacility == null)
            {
                _lastTarget = null;
                return;
            }

            object target = (object)_input.HoveredFacility ?? _input.HoverTile;
            if (!Equals(target, _lastTarget))
            {
                _lastTarget = target;
                _targetSince = Time.unscaledTime;
            }
            if (Time.unscaledTime - _targetSince < delay) return;

            List<string> lines = BuildLines();
            if (lines.Count == 0) return;

            if (_style == null) _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = 12, padding = new RectOffset(8, 8, 6, 6) };
            var content = new GUIContent(string.Join("\n", lines));
            float height = _style.CalcHeight(content, width);

            Vector2 mouse = Mouse.current.position.ReadValue();
            var pos = new Vector2(mouse.x, Screen.height - mouse.y) + cursorOffset;
            if (pos.x + width > Screen.width) pos.x = Screen.width - width - 4;
            if (pos.y + height > Screen.height) pos.y = Screen.height - height - 4;
            GUI.Box(new Rect(pos.x, pos.y, width, height), content, _style);
        }

        private List<string> BuildLines()
        {
            Settlement s = _session.Settlement;
            var lines = new List<string>();
            Facility f = _input.HoveredFacility;
            if (f != null)
            {
                lines.AddRange(FacilityInfo.Describe(s, f, _view.JobProgress(s, f)));
            }
            if (_input.HoverTile.HasValue && (f == null || !f.Edge.HasValue))
            {
                HexCoord c = _input.HoverTile.Value;
                if (_session.Map.TryGetTile(c, out HexTile tile))
                {
                    if (lines.Count > 0) lines.Add("");
                    lines.Add($"타일 {c} · {FacilityInfo.TerrainName(tile.Terrain)}");
                }
            }
            if (_input.Tool != BuildTool.Select && !string.IsNullOrEmpty(_input.HoverMessage))
            {
                lines.Add($"✕ {BuildInputController.Label(_input.Tool)} 불가: {_input.HoverMessage}");
            }
            return lines;
        }
    }
}
