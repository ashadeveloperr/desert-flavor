using System.Collections.Generic;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Water;
using DesertSettlement.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 시설 머리 위 글자: 공사 진행률, 폐쇄, 물 부족·농장 적자 이유, 일손 부족·주민 떠남. 인스펙터 '글자'에서 끌 수 있다.
    /// 정식 UI 캔버스(sortingOrder 10)보다 아래 캔버스(<see cref="LabelSortingOrder"/>)에 그려서 위쪽 띠·패널·결과창을 가리지 않는다(결정 UV1).
    /// 위험한 상태(물·일손 부족, 떠나는 중)는 경고색 테두리.
    /// </summary>
    public sealed partial class SettlementView
    {
        [Header("글자")]
        [SerializeField] private bool showLabels = true;

        private const int LabelSortingOrder = 5;
        private Camera _camera;
        private RectTransform _labelRoot;
        private Canvas _labelCanvas;
        private readonly List<(RectTransform box, Image back, Text text)> _labels = new List<(RectTransform, Image, Text)>();

        private void EnsureLabelCanvas()
        {
            if (_labelRoot != null) return;
            var go = new GameObject("Facility Labels", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = LabelSortingOrder;
            canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>(); // 정식 UI와 같은 기준(1920×1080)으로 글자 크기를 맞춘다
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            _labelRoot = (RectTransform)go.transform;
            _labelCanvas = canvas;
        }

        /// <summary>시설 하나의 머리 위 글자와 위험 여부. 글자가 없으면 null.</summary>
        private string LabelText(Settlement s, Facility f, out bool danger)
        {
            danger = false;
            if (f.State == FacilityState.Constructing) return $"건설 {JobProgress(s, f):P0}";
            if (f.State == FacilityState.Upgrading) return $"개량 {JobProgress(s, f):P0}";
            if (f.State == FacilityState.Demolishing) return $"철거 {JobProgress(s, f):P0}";
            if (f.State == FacilityState.Closed) return "폐쇄";
            if (!f.IsOperating) return null;
            // 농장 자기 수지 적자와 물 부족은 원인이 다르므로 따로 적는다.
            var parts = new List<string>();
            FacilityOutput o = s.OutputOf(f);
            if (o.FarmBelowOwnUpkeep) parts.Add($"식량 생산 {o.ProducedPerHour:0.0} < 소비 {o.FoodUsePerHour:0.0}");
            if (s.LastWater != null && s.LastWater.Consumers.TryGetValue(WaterIds.Of(f), out WaterConsumerResult w) && w.Rate < 0.999)
                parts.Add($"물 {w.Rate:P0} · {FacilityInfo.ShortageText(w.Shortage)}");
            // 노동력(결정 LB1~): 일손이 모자란 시설, 주민이 떠나는 주택.
            if (o.WorkersNeeded > 0 && o.WorkersAssigned + 0.05 < o.WorkersNeeded)
                parts.Add($"일손 {FacilityInfo.People(o.WorkersAssigned)}/{FacilityInfo.People(o.WorkersNeeded)}");
            if (s.IsLeaving(f)) parts.Add($"주민 {FacilityInfo.People(f.Residents)}/{FacilityInfo.People(s.HouseCapacity(f))} 떠나는 중");
            danger = parts.Count > 0;
            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }

        /// <summary>매 프레임(LateUpdate 끝): 보이는 시설마다 글자 상자를 놓고, 남는 상자는 끈다.</summary>
        private void UpdateLabels(Settlement s)
        {
            EnsureLabelCanvas();
            if (_camera == null) _camera = Camera.main;
            int used = 0;
            if (showLabels && _camera != null)
            {
                float scale = _labelCanvas.scaleFactor > 0 ? _labelCanvas.scaleFactor : 1f; // 화면 픽셀 → 캔버스 단위
                foreach (Facility f in s.Facilities)
                {
                    if (f.Edge.HasValue) continue;
                    string text = LabelText(s, f, out bool danger);
                    if (text == null) continue;
                    Vector3 screen = _camera.WorldToScreenPoint(_session.ToWorld(_session.Layout.TileCenter(f.Tile), Size * 0.9f));
                    if (screen.z < 0) continue;
                    (RectTransform box, Image back, Text label) = LabelAt(used++);
                    box.anchoredPosition = new Vector2(Mathf.Round(screen.x / scale), Mathf.Round(screen.y / scale));
                    label.text = text;
                    label.color = danger ? UiKit.Theme.Warn : UiKit.Theme.Text;
                    back.color = danger ? new Color(0.25f, 0.10f, 0.06f, 0.88f) : UiKit.Theme.Panel;
                }
            }
            for (int i = used; i < _labels.Count; i++)
                if (_labels[i].box.gameObject.activeSelf) _labels[i].box.gameObject.SetActive(false);
        }

        private (RectTransform, Image, Text) LabelAt(int index)
        {
            while (_labels.Count <= index)
            {
                RectTransform box = UiKit.Panel("Label", _labelRoot);
                box.anchorMin = box.anchorMax = Vector2.zero;
                box.pivot = new Vector2(0.5f, 0.5f);
                Image back = box.GetComponent<Image>();
                back.raycastTarget = false;
                var fit = box.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var pad = box.gameObject.AddComponent<HorizontalLayoutGroup>();
                pad.padding = new RectOffset(6, 6, 2, 2);
                pad.childControlWidth = pad.childControlHeight = true;
                Text t = UiKit.Label(box, "", UiKit.Theme.Small, UiKit.Theme.Text, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                _labels.Add((box, back, t));
            }
            (RectTransform b, Image i, Text tx) = _labels[index];
            if (!b.gameObject.activeSelf) b.gameObject.SetActive(true);
            return (b, i, tx);
        }
    }
}
