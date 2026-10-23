using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 마우스를 올린 타일·시설의 툴팁(2026-10-23, Docs/decision_log_auto.md UI8). 커서를 따라다닌다(매 프레임 <see cref="Follow"/>).
    /// 위: 시설 이름·레벨·상태 / 가운데: 핵심 정보 몇 줄(전체는 선택하면 오른쪽 패널) / 아래: 지형, 건설 도구를 들었으면 가능 여부와 비용.
    /// </summary>
    public sealed class TooltipView : IUiView
    {
        private const int MaxInfoLines = 6;
        private const float Delay = 0.15f;
        private static readonly Vector2 CursorOffset = new Vector2(18, -18);

        private readonly GameUi _ui;
        private readonly Canvas _canvas;
        private readonly RectTransform _panel;
        private readonly Text _text;
        private object _target;
        private float _targetSince;
        private string _content;

        public TooltipView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            _canvas = canvas.GetComponent<Canvas>();
            _panel = Panel("Tooltip", canvas, new Color(0.07f, 0.06f, 0.05f, 0.94f)).Place(Vector2.zero, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(340, 0));
            VerticalLayoutGroup layout = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            FitHeight(_panel.gameObject);
            _text = Label(_panel, "", Theme.Small);
            _panel.GetComponent<Image>().raycastTarget = false; // 툴팁이 마우스를 가로채지 않게
            _text.raycastTarget = false;
            _panel.gameObject.SetActive(false);
        }

        public void Reset() => _target = null;

        /// <summary>내용(0.2초마다). 위치는 <see cref="Follow"/>.</summary>
        public void Refresh() => _content = BuildContent();

        /// <summary>매 프레임: 보이기·숨기기와 커서 따라가기.</summary>
        public void Follow()
        {
            BuildInputController input = _ui.Input;
            object target = (object)input.HoveredFacility ?? input.HoverTile;
            if (!Equals(target, _target))
            {
                _target = target;
                _targetSince = Time.unscaledTime;
                _content = BuildContent();
            }
            bool show = target != null && Mouse.current != null && Time.unscaledTime - _targetSince >= Delay && !string.IsNullOrEmpty(_content);
            if (_panel.gameObject.activeSelf != show) _panel.gameObject.SetActive(show);
            if (!show) return;
            if (_text.text != _content) _text.text = _content;

            float scale = _canvas.scaleFactor > 0 ? _canvas.scaleFactor : 1;
            Vector2 pos = Mouse.current.position.ReadValue() / scale + CursorOffset;
            Vector2 size = ((RectTransform)_canvas.transform).rect.size;
            float w = _panel.rect.width, h = _panel.rect.height;
            pos.x = Mathf.Min(pos.x, size.x - w - 4);
            pos.y = Mathf.Max(pos.y, h + 4); // 아래로 넘치면 위로
            _panel.anchoredPosition = pos;
        }

        private string BuildContent()
        {
            GameSession session = _ui.Session;
            BuildInputController input = _ui.Input;
            Settlement s = session.Settlement;
            if (s == null) return null;
            var lines = new List<string>();

            Facility f = input.HoveredFacility;
            if (f != null)
            {
                lines.Add(Paint($"{FacilityInfo.KindName(f.Kind)} L{f.Level}", Theme.Accent) + "  " + Paint(StateText(f.State), Theme.Muted));
                List<string> info = FacilityInfo.Describe(s, f, _ui.View.JobProgress(s, f));
                lines.AddRange(info.Skip(1).Take(MaxInfoLines)); // 첫 줄(이름)은 위 제목과 겹친다
                if (info.Count > MaxInfoLines + 1) lines.Add(Paint("… 클릭하면 오른쪽에 전체 정보", Theme.Muted));
            }
            if (input.HoverTile.HasValue && (f == null || !f.Edge.HasValue) && session.Map.TryGetTile(input.HoverTile.Value, out HexTile tile))
                lines.Add(Paint($"타일 {input.HoverTile.Value} · {FacilityInfo.TerrainName(tile.Terrain)}", Theme.Muted));

            string kind = BuildInputController.KindOf(input.Tool);
            if (input.Tool != BuildTool.Select)
            {
                if (!string.IsNullOrEmpty(input.HoverMessage))
                    lines.Add(Paint($"✕ {BuildInputController.Label(input.Tool)} 불가: {input.HoverMessage}", Theme.Danger));
                else if (kind != null)
                {
                    BuildingLevel lv = s.Rules.Config.GetLevel(kind, 1);
                    int free = s.FreeBuildsRemaining(kind);
                    string cost = free > 0 ? "무료 완공" : lv != null ? $"석재 {lv.Stone:0} · {lv.Hours:0}시간 · 유지비 {lv.Upkeep:0.00}/h" : "";
                    lines.Add(Paint($"✓ {BuildInputController.Label(input.Tool)} 건설 가능  {cost}", Theme.Good));
                }
            }
            return string.Join("\n", lines);
        }

        private static string StateText(FacilityState state) =>
            state == FacilityState.Operating ? "운영 중" : state == FacilityState.Closed ? "폐쇄" :
            state == FacilityState.Constructing ? "공사 중" : state == FacilityState.Upgrading ? "개량 중" : "철거 중";
    }
}
