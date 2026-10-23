using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 임시 IMGUI HUD (C7). 정식 UI를 만들면 지워도 된다.
    /// - 왼쪽: 자원·경매 일정, 도구 버튼(비용 표시), 가리킨 곳의 불가 이유, 구매·샘플 배치
    /// - 오른쪽: 작업 대기열(순서 올리기·취소), 선택한 시설 정보와 버튼
    /// </summary>
    [RequireComponent(typeof(GameSession), typeof(BuildInputController), typeof(SettlementView))]
    public sealed class SettlementHud : MonoBehaviour
    {
        [SerializeField] private float leftTop = 110f;
        [SerializeField] private float panelWidth = 300f;
        [SerializeField] private int buyStep = 10;

        private GameSession _session;
        private BuildInputController _input;
        private SettlementView _view;
        private Rect _left, _right;
        private Vector2 _queueScroll;

        private static readonly BuildTool[] Tools =
        {
            BuildTool.Select, BuildTool.Well, BuildTool.Farm, BuildTool.Quarry, BuildTool.Mine, BuildTool.AuctionHall,
            BuildTool.Warehouse, BuildTool.Hospital, BuildTool.WaterTank, BuildTool.Canal, BuildTool.Upgrade, BuildTool.Demolish, BuildTool.ToggleClose,
        };

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            _input = GetComponent<BuildInputController>();
            _view = GetComponent<SettlementView>();
        }

        private void OnDisable() => HudInputBlocker.Clear(this);

        private void OnGUI()
        {
            if (!HudMode.Legacy) { HudInputBlocker.Clear(this); return; } // 새 화면(uGUI)을 쓰는 동안은 그리지 않는다(F1로 전환)
            if (!_session.IsReady) return;
            Settlement s = _session.Settlement;

            _left = new Rect(10, leftTop, panelWidth, Mathf.Min(560, Screen.height - leftTop - 10));
            _right = new Rect(Screen.width - panelWidth - 10, 10, panelWidth, Mathf.Min(620, Screen.height - 20));
            GUILayout.BeginArea(_left, GUI.skin.box);
            DrawResources(s);
            DrawTools(s);
            DrawPurchases(s);
            GUILayout.EndArea();

            GUILayout.BeginArea(_right, GUI.skin.box);
            DrawSelected(s);
            DrawQueue(s);
            GUILayout.EndArea();

            HudInputBlocker.SetRects(this, _left, _right);
        }

        private void DrawResources(Settlement s)
        {
            GUILayout.Label($"자금 {s.Cash:0.0}   식량 계수 F {s.FoodCoefficient:P0}");
            GUILayout.Label($"식량 {s.Food:0.0} / {s.FoodCapacity:0}   석재 {s.Stone:0.0} / {s.StoneCapacity:0}");
            HourRecord last = s.History.LastOrDefault();
            if (last != null)
            {
                GUILayout.Label($"지난 1시간: 식량 +{last.FoodProduced:0.00} −{last.FoodDemand:0.00}, 석재 +{last.StoneProduced:0.00}, 광물 +{last.OreProduced:0.00}, 유지비 −{last.Upkeep:0.00}");
            }
            var campaign = _session.Campaign;
            var auctions = _session.Auctions;
            GUILayout.Label($"정착지 {campaign.SettlementNumber}: {campaign.CurrentMap.Name}  · 장착 장비 {(campaign.Loadout.Count == 0 ? "없음" : string.Join(", ", campaign.Shop.Where(e => campaign.IsEquipped(e.Id)).Select(e => e.Name)))}");
            foreach (RegionModifierDefinition m in campaign.CurrentModifiers)
                GUILayout.Label($"  {(m.Kind == RegionModifierKind.Weakness ? "약점" : "강점")} {m.Name}: {m.Effects.Describe()}");
            if (auctions.NextAuctionHour.HasValue)
            {
                string bidders = string.Join(", ", auctions.AnnouncedBidders.Select(b => b.Name));
                GUILayout.Label($"다음 경매: {auctions.NextAuctionHour}시간째 (지금 {s.CurrentHour}, {auctions.NextAuctionHour - s.CurrentHour}시간 남음) · 예고 입찰자: {bidders}");
            }
            else
            {
                GUILayout.Label($"경매장 완공 후 {s.Rules.Config.ConfirmedRules.AuctionRepeatsGameHours}시간 뒤 첫 경매 · 경매장이나 광산이 없으면 자금 0이 되는 즉시 파산");
            }
            if (auctions.HoursUntilBankruptcy.HasValue) GUILayout.Label($"⚠ 자금 음수 — {auctions.HoursUntilBankruptcy}시간 뒤 파산");
            // 입찰자 혜택(무료 완공 배치). 2026-10-23 입찰자 6종: 농장 말고도 창고·채석장·물탱크·경매장이 올 수 있다.
            foreach (string kind in BuildingKinds.All)
            {
                int free = s.FreeBuildsRemaining(kind);
                if (free > 0) GUILayout.Label($"입찰자 혜택: 완공된 {FacilityInfo.KindName(kind)} {free}개를 무료로 배치할 수 있습니다({FacilityInfo.KindName(kind)} 도구로 클릭).");
            }
            DrawDisasters(s);
        }

        /// <summary>재해: 예고(시작 몇 시간 전), 진행 중 전염병의 심각도·저항력·피해, 끝난 뒤 남은 저항력.</summary>
        private static void DrawDisasters(Settlement s)
        {
            DisasterManager d = s.Disasters;
            if (d == null) return;
            foreach (DisasterEvent e in d.Visible)
            {
                if (e.State == DisasterState.Warned)
                    GUILayout.Label($"⚠ {e.Name} 예고 — {e.StartHour - s.CurrentHour}시간 뒤({e.StartHour}시간째) 시작");
                else if (e.State == DisasterState.Active && e.DurationHours > 0)
                    GUILayout.Label($"⚠ {e.Name} 진행 중 — {e.PlannedEndHour - s.CurrentHour}시간 남음 · {TimedEffectText(s, e)}");
            }
            EpidemicState ep = d.Epidemic;
            if (ep.Active)
            {
                GUILayout.Label($"☣ 전염병 진행 중 — 심각도 {ep.Severity:P0} · 저항력 {ep.Resistance:P0} · 생산 시설 효율 −{ep.Damage:P0} (병원 인근 −{ep.Damage * d.Settings.Epidemic.HospitalDamageFactor:P0})");
            }
            else if (ep.Resistance > s.Rules.EpidemicResistanceFloor + 0.005)
            {
                GUILayout.Label($"전염병 저항력 {ep.Resistance:P0} (서서히 줄어듦)");
            }
        }

        /// <summary>모래폭풍·메뚜기떼·탄저병의 지금 효과(장비 반영). 2026-10-23 D1~D6.</summary>
        internal static string TimedEffectText(Settlement s, DisasterEvent e)
        {
            DisasterSettings c = s.Disasters.Settings;
            SettlementRules r = s.Rules;
            switch (e.TypeId)
            {
                case DisasterManager.Sandstorm:
                    return $"수로 손실 ×{s.Disasters.CanalLossFactor:0.##}, 공사 속도 {s.Disasters.ConstructionSpeed:P0}";
                case DisasterManager.Locusts:
                    return $"농장 생산 {1 - (1 - c.Locusts.FarmProduction) * r.LocustDamageMultiplier:P0} (시작 때 식량 재고 −{c.Locusts.FoodStockLoss * r.LocustDamageMultiplier:P0})";
                case DisasterManager.Blight:
                    return $"농장 생산 {1 - (1 - c.Blight.FarmProduction) * r.BlightDamageMultiplier:P0}";
                default:
                    return "";
            }
        }

        private void DrawTools(Settlement s)
        {
            GUILayout.Space(6);
            GUILayout.Label("도구 (우클릭·Esc = 선택)");
            for (int i = 0; i < Tools.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < Mathf.Min(i + 3, Tools.Length); k++)
                {
                    BuildTool tool = Tools[k];
                    string kind = BuildInputController.KindOf(tool);
                    string label = BuildInputController.Label(tool);
                    BuildingLevel lv = kind != null ? s.Rules.Config.GetLevel(kind, 1) : null;
                    int free = kind != null ? s.FreeBuildsRemaining(kind) : 0;
                    if (free > 0) label += $"\n무료 완공 {free}";
                    else if (lv != null) label += $"\n석{lv.Stone:0}·{lv.Hours:0}h";
                    bool selected = _input.Tool == tool;
                    if (GUILayout.Toggle(selected, label, GUI.skin.button, GUILayout.Height(lv != null ? 38 : 24)) && !selected)
                    {
                        _input.Tool = tool;
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(_input.HoverMessage)) GUILayout.Label("✕ " + _input.HoverMessage);
            if (!string.IsNullOrEmpty(_input.LastResult)) GUILayout.Label(_input.LastResult);
        }

        private void DrawPurchases(Settlement s)
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"식량 +{buyStep}\n(다음 {s.NextPrice(true):0.00})")) s.BuyFood(buyStep);
            if (GUILayout.Button($"석재 +{buyStep}\n(다음 {s.NextPrice(false):0.00})")) s.BuyStone(buyStep);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("샘플 배치 예약 (이 맵의 예시)")) _session.RequestSampleLayout();
        }

        private void DrawSelected(Settlement s)
        {
            Facility f = s.GetFacility(_input.SelectedFacilityId);
            if (f == null)
            {
                GUILayout.Label("선택 도구로 시설을 클릭하면 정보가 나옵니다.");
                return;
            }

            GUILayout.Label($"선택 #{f.Id}");
            foreach (string line in FacilityInfo.Describe(s, f, _view.JobProgress(s, f))) GUILayout.Label(line);
            GUILayout.Label($"투입 석재 {f.StoneInvested:0} (철거 시 {f.StoneInvested * s.Rules.DemolishRefundRatio:0} 환급)");

            GUILayout.BeginHorizontal();
            if (Button("개량", s.CanUpgrade(f.Id))) s.RequestUpgrade(f.Id);
            if (Button("철거", s.CanDemolish(f.Id))) s.RequestDemolish(f.Id);
            if (!s.Rules.CannotClose.Contains(f.Kind) && (f.State == FacilityState.Operating || f.State == FacilityState.Closed))
            {
                if (GUILayout.Button(f.State == FacilityState.Closed ? (f.Kind == BuildingKinds.CanalEdge ? "수로 열기" : "재개") : (f.Kind == BuildingKinds.CanalEdge ? "수로 닫기" : "폐쇄"))) s.SetClosed(f.Id, f.State == FacilityState.Operating);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private static bool Button(string label, CommandResult check)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = check.Success;
            bool clicked = GUILayout.Button(new GUIContent(label, check.Message));
            GUI.enabled = enabled;
            return clicked;
        }

        private void DrawQueue(Settlement s)
        {
            GUILayout.Label($"작업 (진행 {s.JobsInProgress}/{s.Rules.ConstructionSlots}, FIFO)");
            _queueScroll = GUILayout.BeginScrollView(_queueScroll);
            foreach (ConstructionJob job in s.Jobs.ToList())
            {
                GUILayout.BeginHorizontal();
                string state = job.State == JobState.InProgress
                    ? $"진행 ~{job.DeadlineHour}h"
                    : (s.Stone + 1e-8 < job.StoneCost ? $"대기(석재 {job.StoneCost:0} 필요)" : "대기");
                GUILayout.Label($"{TypeText(job.Type)} {FacilityInfo.KindName(job.Kind)} L{job.TargetLevel} · {state}");
                if (job.State == JobState.Queued && GUILayout.Button("▲", GUILayout.Width(26)))
                {
                    int index = s.Jobs.Where(j => j.State == JobState.Queued).ToList().IndexOf(job);
                    s.MoveQueuedJob(job.Id, Mathf.Max(0, index - 1));
                }
                if (GUILayout.Button(job.State == JobState.InProgress ? "취소(50%)" : "취소", GUILayout.Width(job.State == JobState.InProgress ? 70 : 40)))
                {
                    s.CancelJob(job.Id);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private static string TypeText(JobType type) =>
            type == JobType.Build ? "건설" : type == JobType.Upgrade ? "개량" : "철거";
    }
}
