using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>화면 한 구역(위쪽 띠, 도구 막대 등). 0.2초마다 Refresh, 새 정착지마다 Reset.</summary>
    public interface IUiView
    {
        void Refresh();
        void Reset();
    }

    /// <summary>
    /// 정식 UI의 뿌리(결정 UI1~UI5). 캔버스·이벤트 시스템을 만들고 각 구역을 붙인다. 키보드 단축키는 <see cref="Hotkeys"/>(오른쪽 아래 ? 버튼이 안내).
    /// </summary>
    [RequireComponent(typeof(GameSession), typeof(BuildInputController), typeof(SettlementView))]
    public sealed class GameUi : MonoBehaviour
    {
        [Tooltip("화면 값 갱신 간격(실제 초)")]
        [SerializeField] private float refreshSeconds = 0.2f;

        private GameSession _session;
        private Canvas _canvas;
        private readonly List<IUiView> _views = new List<IUiView>();
        private float _nextRefresh;
        private TooltipView _tooltip;
        private TopBarView _topBar;

        public GameSession Session => _session;
        public BuildInputController Input { get; private set; }
        public SettlementView View { get; private set; }

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            Input = GetComponent<BuildInputController>();
            View = GetComponent<SettlementView>();
            BuildCanvas();
            EnsureEventSystem();
            Transform root = _canvas.transform;
            _views.Add(_topBar = new TopBarView(this, root));
            _views.Add(new BuildBarView(this, root));
            _views.Add(new BuyPanelView(this, root));
            _views.Add(new AlertsView(this, root));
            _views.Add(new InspectorView(this, root));
            BuildHelpButton(root);
            _views.Add(_tooltip = new TooltipView(this, root));
            _views.Add(new ModalView(this, root)); // 맨 위에 그려지도록 마지막
            _session.Initialized += () => { foreach (IUiView v in _views) v.Reset(); };
        }

        private void BuildCanvas()
        {
            var go = new GameObject("Game UI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            // 결정 TX1: 글자·패널을 픽셀 격자에 맞춘다. 끄면 배율(1920×1080 기준 → 실제 화면)·가운데 정렬 때문에
            // 글자가 반 픽셀에 걸려 쌍선형 필터로 번진다.
            _canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>오른쪽 아래 ? 버튼: 마우스를 올리면 조작 안내(결정 HT2, 문구는 <see cref="Hotkeys.HelpText"/>).</summary>
        private static void BuildHelpButton(Transform root)
        {
            UnityEngine.UI.Button help = UiKit.Btn(root, "?", null, 40, 40, UiKit.Theme.Big);
            ((RectTransform)help.transform).Place(Vector2.right, Vector2.right, Vector2.right, new Vector2(-BuildBarView.Margin, BuildBarView.Margin), new Vector2(40, 40));
            help.Tip(Hotkeys.HelpText);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // 프로젝트는 새 Input System 전용
        }

        private void Update()
        {
            Hotkeys.Handle(this);
            bool show = _session.IsReady;
            if (_canvas.gameObject.activeSelf != show) _canvas.gameObject.SetActive(show);
            if (show)
            {
                _tooltip.Follow(); // 툴팁은 매 프레임 커서를 따라간다
                _topBar?.TickClock(); // 시계는 1분 단위로 보이게 매 프레임 확인(CK1)
                _topBar?.Pulse();     // 위험한 자원 카드 깜빡임(UV3)
            }
            if (!show || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshSeconds;
            foreach (IUiView v in _views) v.Refresh();
        }

        /// <summary>캔버스 폭(1920×1080 기준 단위). 16:9 = 1920, 16:10 ≈ 1822, 5:4 ≈ 1610. 좁은 화면 축소(LY3)에 쓴다.</summary>
        public float CanvasWidth => ((RectTransform)_canvas.transform).rect.width;

        /// <summary>위쪽 띠 아래에 잠깐 문구를 띄운다(F5·F9 결과 등).</summary>
        public void ShowMessage(string message) => _views.OfType<TopBarView>().FirstOrDefault()?.Show(message);

        /// <summary>버튼을 누른 직후처럼 바로 다시 그리고 싶을 때.</summary>
        public void RefreshNow() => _nextRefresh = 0;
    }
}
