using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    /// <summary>
    /// 주택·노동력 진단(결정 LB1~). `-- labor [D] [시간 간격]`:
    /// 기준 봇(단일·다중)으로 고정 맵 1과 난이도 D 생성 맵을 돌리며 몇 시간마다 자금·석재·식량·주민·배치를 찍는다.
    /// 봇이 어디서 막히는지(사람 부족·물 부족·식량 부족·공사 지연) 보는 용도.
    /// </summary>
    public static partial class Program
    {
        /// <summary>
        /// 노동력 수치 후보(결정 LB1~). `-- mods campaign 0.25 14000 30 smart lab=이름[+이름…]`으로 설정에 건다(여러 개는 +로 합침).
        /// 후보마다 따로 돌려 숙련 봇 D 구간별 순이익을 비교한다.
        /// </summary>
        private static readonly Dictionary<string, Action<EconomyConfig>> LaborCandidates = new Dictionary<string, Action<EconomyConfig>>
        {
            ["off"] = c => c.Labor = null,                                                // 노동력 없음(예전 규칙)
            ["crew2"] = c => c.Labor.CrewPerConstructionSlot = 2,                        // 공사 인원을 기본 인부로 다 채움
            ["leave2"] = c => c.Labor.LeavePerHour = 0.02,                               // 더 느리게 떠남
            ["return4"] = c => c.Labor.ReturnPerHour = 0.04,                             // 더 빨리 돌아옴
            ["water25"] = c => c.Labor.WaterPerResidentPerHour = 0.25,                   // 주민 물 절반
            ["food15"] = c => c.Labor.FoodPerResidentPerHour = 0.15,                     // 주민 식량 덜
            ["cheap"] = c => Houses(c, (lv, i) => { lv.Upkeep = new[] { 0.1, 0.2, 0.3 }[i]; lv.Stone = new[] { 4.0, 8, 12 }[i]; }), // 주택 싸게
            ["big"] = c => Houses(c, (lv, i) => lv.Residents = new[] { 8.0, 12, 16 }[i]),  // 주택 정원 크게
            ["free2"] = c => c.Labor.FreeHouses = 2,                                     // 무료 주택 2채
            ["lean"] = c =>                                                               // 필요 인원 더 적게(우물 1 · 농장·채석장 2/3/4 · 광산 4/6/8)
            {
                Levels(c, BuildingKinds.Well, (lv, i) => lv.Workers = 1);
                Levels(c, BuildingKinds.Farm, (lv, i) => lv.Workers = 2 + i);
                Levels(c, BuildingKinds.Quarry, (lv, i) => lv.Workers = 2 + i);
                Levels(c, BuildingKinds.Mine, (lv, i) => lv.Workers = 4 + 2 * i);
            },
        };

        private static void Levels(EconomyConfig c, string kind, Action<BuildingLevel, int> change)
        {
            List<BuildingLevel> levels = c.BuildingLevels[kind];
            for (int i = 0; i < levels.Count; i++) change(levels[i], i);
        }

        private static void Houses(EconomyConfig c, Action<BuildingLevel, int> change) => Levels(c, BuildingKinds.House, change);

        /// <summary>`lab=a+b` 인자를 설정에 건다. 모르는 이름이면 예외.</summary>
        private static void ApplyLaborCandidate(EconomyConfig config, string names)
        {
            foreach (string name in names.Split('+'))
            {
                if (!LaborCandidates.TryGetValue(name, out Action<EconomyConfig> apply)) throw new ArgumentException($"노동력 후보 '{name}'이 없습니다: {string.Join(", ", LaborCandidates.Keys)}");
                apply(config);
            }
        }

        /// <summary>
        /// `-- labor sample`: 고정 맵 1 샘플 배치의 경매장 완공·첫 경매 시각(게임 규칙, 그리고 EditMode 경매 테스트처럼 건설 시간 ×1).
        /// 샘플 배치나 노동력 수치를 바꾸면 테스트 기대값(AuctionCampaignTests·DebugSessionSmokeTests)을 이 값으로 고친다.
        /// </summary>
        private static int LaborSample()
        {
            var config = LoadConfig();
            var maps = LoadMaps();
            Header("고정 맵 1 샘플 배치 시각 — 테스트 기대값 확인용");
            foreach (double multiplier in new[] { config.ConfirmedRules.ConstructionHoursMultiplier, 1.0 })
            {
                SettlementRules rules = SettlementRules.FromConfig(config);
                rules.ConstructionHoursMultiplier = multiplier;
                var s = new Settlement(maps[0].CreateMap(), rules);
                var a = new DesertSettlement.Core.Auction.AuctionManager(s, 1);
                List<string> failures = maps[0].RequestSampleLayout(s);
                for (int h = 0; h < 400 && !a.IsOpen && !a.IsFinished; h++) s.AdvanceHour();
                Line($"  건설 시간 ×{multiplier}: 예약 실패 {failures.Count} · 경매장 완공 {s.AuctionHallCompletedHour}h · 첫 경매 {(a.IsOpen ? a.CurrentAuction.Hour.ToString() : a.Phase.ToString())}h" +
                     $" · 운영 시설 {s.Facilities.Count(f => f.IsOperating)}(수로 {s.Facilities.Count(f => f.IsOperating && f.Edge.HasValue)}) · 자금 {s.Cash:0}");
            }
            return 0;
        }

        private static int LaborTuning(string[] args)
        {
            if (args.Length > 0 && args[0] == "sample") return LaborSample();
            double d = args.Length > 0 && double.TryParse(args[0], out double x) ? x : 0.25;
            int every = args.Length > 1 && int.TryParse(args[1], out int e) ? e : 6;
            var config = LoadConfig();
            var procedural = LoadProceduralConfig();
            var maps = LoadMaps();

            Header($"노동력 진단 — 고정 맵 1과 D{d:0.##} 생성 맵, {every}시간마다");
            var generated = ProceduralMapGenerator.Generate(DifficultyParams.For(d, procedural.Difficulty), procedural.Generator, 50000 + (int)(d * 1000));
            foreach (MapDefinition map in new[] { maps[0], generated?.Map }.Where(m => m != null))
            {
                foreach (BotStrategy strategy in new[] { BotStrategy.Single, BotStrategy.Multi })
                {
                    Line($"  ── {map.Name ?? map.Id} · {strategy}");
                    List<MapBuildEntry> plan = ReferenceBot.Plan(map, strategy);
                    if (plan != null) Line("    계획: " + string.Join(" ", plan.Where(b => b.Kind != BuildingKinds.CanalEdge).Select(b => FacilityCatalog.NameOf(b.Kind))));
                    var r = ReferenceBot.Run(config, map, rules => rules.StartingCash = 500, strategy: strategy, maxHours: 200);
                    Settlement s = r.Settlement;
                    if (s == null) { Line("    " + r.Failure); continue; }
                    foreach (HourRecord h in s.History.Where(h => h.Hour % every == 0 || h == s.History.Last()))
                    {
                        Line($"    {h.Hour,4}h 자금 {h.Cash,6:0} 석재 {h.Stone,4:0} 식량 {h.Food,4:0} (+{h.FoodProduced,4:0.0} −{h.FoodDemand,4:0.0}) " +
                             $"주민 {h.Residents,5:0.0} 배치 {h.WorkersAssigned,5:0.0}/{h.WorkersNeeded,5:0.0} 광물 {h.OreProduced,5:0.0}");
                    }
                    Line($"    결과: {(r.AuctionHour >= 0 ? $"경매 {r.AuctionHour}h, 매각 {r.BestCash:0} + 현금 {r.SettlementCashAtAuction:0}" : r.Failure)}" +
                         $" · 주택 {s.Facilities.Count(f => f.Kind == BuildingKinds.House)}채, 남은 작업 {s.Jobs.Count}");
                }
            }
            return 0;
        }
    }
}
