using System.Collections.Generic;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Water;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>시설 머리 위 글자(IMGUI): 공사 진행률, 폐쇄, 물 부족·농장 적자 이유. 인스펙터 '글자'에서 끌 수 있다.</summary>
    public sealed partial class SettlementView
    {
        [Header("글자")]
        [SerializeField] private bool showLabels = true;

        private Camera _camera;

        private void OnGUI()
        {
            if (!showLabels || !_session.IsReady) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Settlement s = _session.Settlement;

            foreach (Facility f in s.Facilities)
            {
                if (f.Edge.HasValue) continue;
                string text = null;
                if (f.State == FacilityState.Constructing) text = $"건설 {JobProgress(s, f):P0}";
                else if (f.State == FacilityState.Upgrading) text = $"개량 {JobProgress(s, f):P0}";
                else if (f.State == FacilityState.Demolishing) text = $"철거 {JobProgress(s, f):P0}";
                else if (f.State == FacilityState.Closed) text = "폐쇄";
                else if (f.IsOperating)
                {
                    // 농장 자기 수지 적자와 물 부족은 원인이 다르므로 따로 적는다.
                    var parts = new List<string>();
                    FacilityOutput o = s.OutputOf(f);
                    if (o.FarmBelowOwnUpkeep) parts.Add($"식량 생산 {o.ProducedPerHour:0.0} < 소비 {o.FoodUsePerHour:0.0}");
                    if (s.LastWater != null && s.LastWater.Consumers.TryGetValue(WaterIds.Of(f), out WaterConsumerResult w) && w.Rate < 0.999)
                    {
                        parts.Add($"물 {w.Rate:P0} · {FacilityInfo.ShortageText(w.Shortage)}");
                    }
                    if (parts.Count > 0) text = string.Join("\n", parts);
                }
                if (text == null) continue;

                Vector3 screen = _camera.WorldToScreenPoint(_session.ToWorld(_session.Layout.TileCenter(f.Tile), Size * 0.9f));
                if (screen.z < 0) continue;
                int lineCount = text.Split('\n').Length;
                var rect = new Rect(screen.x - 90, Screen.height - screen.y - 11 * lineCount, 180, 20 * lineCount + 2);
                GUI.Label(rect, text, LabelStyle);
            }
        }

        private static GUIStyle _labelStyle;
        private static GUIStyle LabelStyle
        {
            get
            {
                if (_labelStyle != null) return _labelStyle;
                _labelStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
                return _labelStyle;
            }
        }
    }
}
