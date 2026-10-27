using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 지역 모디파이어·장비 수치 측정: dotnet run --project Tools/SettlementSim -- mods
    // 목표(2026-10-23 확정 + 제안): ① 맞는 대응 장비가 약점 하나의 피해를 대략 절반 줄인다.
    // ② 약점이 쌓이면 숙련자(다중 봇)도 D 3~4 무렵 적자.
    public static partial class Program
    {
        /// <summary>약점 → 대응 장비(설정 JSON의 장비 id).</summary>
        internal static readonly Dictionary<string, string> CounterOf = new Dictionary<string, string>
        {
            ["softGround"] = "reinforcedMaterials", ["thinAir"] = "oxygenSupply", ["dryAquifer"] = "wellPump",
            ["sandWind"] = "windbreak", ["salineSoil"] = "irrigation",
        };

        /// <summary>
        /// Core Campaign 그대로(후보 2곳·장비 슬롯 5·모디파이어) 다중 봇이 정착지를 이어 간다.
        /// 봇 상점 규칙: 예비금 2500을 남기고 우선순위대로 장비 구매 → 대응 장비로 못 막는 약점이 적은 후보 선택
        /// → 그 지역 약점의 대응 장비부터 슬롯에 넣고 나머지는 일반 장비.
        /// </summary>
        // disasters: false면 재해 없이(설정 disasters를 비움). vaccine: 봇이 방역 장비를 가장 먼저 사서 늘 장착(5-1 재측정, 2026-10-23).
        private static int ModifierCampaign(EconomyConfig config, ProceduralMapConfig proc, int settlements = 15, bool disasters = true, bool vaccine = false,
            bool upgrade = false, bool smart = false)
        {
            if (!disasters) config.Disasters = null;
            Header($"모디파이어 캠페인 — Core Campaign + 다중 봇(장비 구매·후보 선택·슬롯), 정착지 최대 {settlements}개, DPerSettlement {proc.Difficulty.DPerSettlement}, CashPerD {proc.Difficulty.CashPerD}"
                + $", 후반 가속 D>{proc.Difficulty.LateStartD}: 운영비²×{proc.Difficulty.UpkeepLatePerD2} 약점×{proc.Difficulty.WeaknessLatePerD} 재해×{proc.Difficulty.DisasterLatePerD}"
                + $", 재해 {(disasters ? "있음" : "없음")}{(vaccine ? ", 방역 장비 우선" : "")}{(upgrade ? ", 장비 강화" : "")}");
            var reachD1 = new List<int>();
            var maps = LoadMaps();
            string[] buyOrder = { "wellPump", "miningGear", "windbreak", "reinforcedMaterials", "oxygenSupply", "irrigation", "extraCrew", "bigStorage" };
            string[] generalOrder = { "wellPump", "miningGear", "windbreak", "reinforcedMaterials", "extraCrew", "bigStorage", "oxygenSupply", "irrigation" };
            if (smart)
            {
                upgrade = true; // 숙련 봇(O5~O8)은 장비도 강화하고, 재해 장비도 산다
                buyOrder = buyOrder.Concat(new[] { "vaccination", "insectNet" }).ToArray();
                generalOrder = new[] { "wellPump", "miningGear", "windbreak", "vaccination", "insectNet", "reinforcedMaterials", "extraCrew", "bigStorage", "oxygenSupply", "irrigation" };
            }
            if (vaccine)
            {
                buyOrder = new[] { "vaccination" }.Concat(buyOrder).ToArray();
                generalOrder = new[] { "vaccination" }.Concat(generalOrder).ToArray();
            }
            var deficitAt = new List<string>();
            var profitsByBand = new SortedDictionary<int, List<double>>(); // D 구간(0 = D<1, 1 = 1≤D<2 …) → 순이익
            int epidemicSettlements = 0, playedSettlements = 0;
            foreach (int campaignSeed in new[] { 1, 2, 3, 4, 5 })
            {
                var c = new Campaign(config, maps, campaignSeed, proc);
                c.StartNextSettlement();
                var line = new StringBuilder($"  캠페인 {campaignSeed}: ");
                string firstDeficit = null;
                for (int n = 1; n <= settlements; n++)
                {
                    double before = c.Current.Cash;
                    if (c.CurrentGenerated != null && c.CurrentGenerated.Params.D >= 1 && !reachD1.Contains(-campaignSeed)) { reachD1.Add(n); reachD1.Add(-campaignSeed); }
                    string label = c.CurrentGenerated == null ? "고정" : $"D{c.CurrentGenerated.Params.D:0.0}";
                    string mods = string.Concat(c.CurrentModifiers.Select(m => m.Kind == RegionModifierKind.Weakness ? m.Name.Substring(0, 1) : "+" + m.Name.Substring(0, 1)));
                    if (!(smart ? SkilledBot.PlayCampaignSettlement(c) : ReferenceBot.PlayCampaignSettlement(c)))
                    {
                        line.Append($"| #{n} {label}{(mods.Length > 0 ? "[" + mods + "]" : "")} 실패({(c.State == CampaignState.Bankrupt ? "파산" : "진행 불가")}) ");
                        firstDeficit = firstDeficit ?? $"#{n} {label} {(c.State == CampaignState.Bankrupt ? "파산" : "진행 불가(봇 계획 실패)")}";
                        break;
                    }
                    double profit = c.Cash - before;
                    if (profit < 0 && firstDeficit == null) firstDeficit = $"#{n} {label}";
                    // 경매 전에 시작한 재해 수(☣ 하나 = 재해 한 번, 종류 무관).
                    int hits = c.Disasters?.Events.Count(e => e.State == DisasterState.Active || e.State == DisasterState.Ended) ?? 0;
                    playedSettlements++;
                    if (hits > 0) epidemicSettlements++;
                    double dNow = c.CurrentGenerated?.Params.D ?? 0;
                    int band = (int)Math.Floor(dNow);
                    if (!profitsByBand.ContainsKey(band)) profitsByBand[band] = new List<double>();
                    profitsByBand[band].Add(profit);
                    line.Append($"| #{n} {label}{(mods.Length > 0 ? "[" + mods + "]" : "")}{new string('☣', hits)} {profit:+0;-0} ");
                    foreach (string id in buyOrder)
                    {
                        var e = config.Equipment.Single(x => x.Id == id);
                        if (!c.Owns(id) && c.Cash - e.Price >= 2500) c.BuyEquipment(id);
                    }
                    // 장비 강화(2026-10-23 로드맵 1-④): 예비금 2500을 남기고 일반 우선순위대로 한 단계씩.
                    if (upgrade)
                        foreach (string id in generalOrder)
                            while (c.UpgradeCost(id) is double cost && c.Cash - cost >= 2500) c.UpgradeEquipment(id);
                    var offers = c.NextOffers();
                    RegionOffer pick = offers.OrderBy(o => o.Weaknesses.Count(w => !CounterOf.ContainsKey(w.Id) || !c.Owns(CounterOf[w.Id])))
                        .ThenByDescending(o => o.Strengths.Count()).First();
                    foreach (string id in c.Loadout.ToList()) c.SetEquipped(id, false);
                    var wanted = (vaccine ? new[] { "vaccination" } : Array.Empty<string>())
                        .Concat(pick.Weaknesses.Where(w => CounterOf.ContainsKey(w.Id)).Select(w => CounterOf[w.Id])).Concat(generalOrder).Distinct();
                    foreach (string id in wanted) if (c.Owns(id)) c.SetEquipped(id, true);
                    c.StartNextSettlement(pick.Index);
                }
                line.Append($"| 자금 {c.Cash:0} 장비 {c.OwnedEquipment.Count}");
                Line(line.ToString());
                deficitAt.Add(firstDeficit ?? "없음");
            }
            Line("  D=1 도달 정착지: " + string.Join(", ", reachD1.Where(x => x > 0)));
            Line("  처음 적자/파산: " + string.Join(", ", deficitAt));
            Line("  D 구간별 정착지 순이익 평균(정착지 수): " + string.Join(" · ", profitsByBand.Select(kv =>
                $"D{kv.Key}~{kv.Key + 1} {kv.Value.Average():+0;-0}({kv.Value.Count}, 적자 {kv.Value.Count(x => x < 0)})")));
            Line($"  재해를 겪은 정착지(종류 무관): {epidemicSettlements}/{playedSettlements}");
            if (smart) Line($"  숙련 봇 행동: 보류 {SkilledBot.Holds}번 · 광산 개량 {SkilledBot.MineUpgrades}번 · 병원 {SkilledBot.Hospitals}곳 (정착지 {playedSettlements}곳)");
            Line("  표기: [약점 첫 글자, +강점 첫 글자] 무=무른 지반 산=고지대 마=마른 대수층 모=모래바람 염=염분 외=외딴 / +풍=풍부한 광맥 +비=오아시스 +단=단단한 암반 / ☣ = 경매 전 재해(종류 무관)");
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            File.WriteAllText(Path.Combine(outDir, "modifier_campaign.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static int ModifierTuning(string[] args)
        {
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            const int maps = 30;
            if (args.Length > 0 && args[0] == "campaign")
            {
                // -- mods campaign [DPerSettlement] [CashPerD] [정착지 수] [nodisaster] [vaccine] [upgrade] [smart] [upkeep=k] [attendees=a-b] [upkeepmin=x] [cash=x] [halls=k] : 곡선 후보를 바꿔 가며 비교.
                // nodisaster = 재해 끔, vaccine = 봇이 방역 장비 우선 구매·장착. 위치와 상관없이 쓸 수 있다.
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                bool noDisaster = args.Contains("nodisaster"), vaccine = args.Contains("vaccine"), upgrade = args.Contains("upgrade"), smart = args.Contains("smart");
                // upkeep=k : 생성 맵 운영비 × (1 + k × D)의 k(H1, 2026-10-23 흐름 조정 측정용).
                string upkeepArg = args.FirstOrDefault(a => a.StartsWith("upkeep="));
                if (upkeepArg != null) proc.Difficulty.UpkeepPerD = double.Parse(upkeepArg.Substring(7), inv);
                // attendees=a-b(경매 참석 인원), upkeepmin=x(운영비 D 하한), cash=x(시작 자금) : 재미 보강 후보 측정(2026-10-24).
                string attArg = args.FirstOrDefault(a => a.StartsWith("attendees="));
                if (attArg != null)
                {
                    string[] mm = attArg.Substring(10).Split('-');
                    config.Auction.AttendeesMin = int.Parse(mm[0], inv);
                    config.Auction.AttendeesMax = int.Parse(mm[1], inv);
                }
                string minArg = args.FirstOrDefault(a => a.StartsWith("upkeepmin="));
                if (minArg != null) proc.Difficulty.UpkeepMinD = double.Parse(minArg.Substring(10), inv);
                // halls=k : 봇이 경매장을 모두 k개 짓는다(2026-10-26 AH1 입찰자 수가 경매장 수에 따라 늘어나는 규칙 측정용, 기본 1).
                string hallsArg = args.FirstOrDefault(a => a.StartsWith("halls="));
                if (hallsArg != null) ReferenceBot.ExtraAuctionHalls = Math.Max(0, int.Parse(hallsArg.Substring(6), inv) - 1);
                // 후반 가속 후보(2026-10-27 이슈 #12): late=D(시작 D), upkeep2=k(운영비 제곱항), weak=s(약점 세기 /D), dis=s(재해 세기 /D).
                foreach (string a in args.Where(a => a.Contains("=")))
                {
                    string[] kv = a.Split('=');
                    if (!new[] { "late", "upkeep2", "weak", "dis" }.Contains(kv[0])) continue;
                    double v = double.Parse(kv[1], inv);
                    if (kv[0] == "late") proc.Difficulty.LateStartD = v;
                    else if (kv[0] == "upkeep2") proc.Difficulty.UpkeepLatePerD2 = v;
                    else if (kv[0] == "weak") proc.Difficulty.WeaknessLatePerD = v;
                    else if (kv[0] == "dis") proc.Difficulty.DisasterLatePerD = v;
                }
                string cashArg = args.FirstOrDefault(a => a.StartsWith("cash="));
                if (cashArg != null) config.Prototype.StartingCash = double.Parse(cashArg.Substring(5), inv);
                args = args.Where(a => a != "nodisaster" && a != "vaccine" && a != "upgrade" && a != "smart" && !a.Contains("=")).ToArray();
                if (args.Length > 1) proc.Difficulty.DPerSettlement = double.Parse(args[1], inv);
                if (args.Length > 2) proc.Difficulty.CashPerD = double.Parse(args[2], inv);
                int count = args.Length > 3 ? int.Parse(args[3], inv) : 15;
                return ModifierCampaign(config, proc, count, !noDisaster, vaccine, upgrade, smart);
            }
            double d = args.Length > 0 ? double.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 1.0;

            Header($"모디파이어 하나씩 — D {d:0.00} 생성 맵 {maps}장, 다중 봇 첫 경매 매각 순이익 중앙값(시작 자금 2000)");
            var defs = proc.Modifiers.Definitions;
            var mapsList = Enumerable.Range(0, maps)
                .Select(i => ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 70000 + i))
                .Where(g => g != null).ToList();
            double Median(IEnumerable<EconomyEffects> effects)
            {
                var list = mapsList.Select(g =>
                {
                    var r = ReferenceBot.Run(config, g.Map, x => { x.StartingCash = 2000; foreach (var e in effects) x.ApplyEffects(e); },
                        seed: g.Seed, strategy: BotStrategy.Multi);
                    return r.AuctionHour >= 0 ? r.FinalCash - 2000 : -2000.0;
                }).OrderBy(x => x).ToList();
                return list[list.Count / 2];
            }
            EconomyEffects Equip(string id) => config.Equipment.Single(e => e.Id == id).Effects;

            double baseline = Median(Array.Empty<EconomyEffects>());
            Line($"  기준(모디파이어 없음): {baseline:+0;-0}");
            Line("  모디파이어 | 효과 | 순이익 | 피해 | 대응 장비 | 장비 포함 순이익 | 회복 비율");
            foreach (RegionModifierDefinition m in defs)
            {
                double with = Median(new[] { m.Effects });
                string counter = CounterOf.TryGetValue(m.Id, out string c) ? c : null;
                string tail = "";
                if (counter != null)
                {
                    double fixedProfit = Median(new[] { m.Effects, Equip(counter) });
                    double damage = baseline - with;
                    double recovered = damage > 1e-9 ? (fixedProfit - with) / damage : double.NaN;
                    tail = $" | {counter} | {fixedProfit:+0;-0} | {recovered:P0}";
                }
                Line($"  {m.Name} | {m.Effects.Describe()} | {with:+0;-0} | {baseline - with:+0;-0}{tail}");
            }
            Line("  장비 단독(모디파이어 없음):");
            foreach (EquipmentDefinition e in config.Equipment)
                Line($"    {e.Name} ({e.Price:0}) | {e.Effects.Describe()} | {Median(new[] { e.Effects }) - baseline:+0;-0}");

            Header($"약점 누적 — D {d:0.00} 맵, 무작위 약점 n개(장비 없음 / 대응 장비 모두)");
            var weak = defs.Where(m => m.Kind == RegionModifierKind.Weakness).ToList();
            for (int n = 0; n <= 4; n++)
            {
                var none = new List<double>();
                var countered = new List<double>();
                for (int k = 0; k < 6; k++)
                {
                    var rng = new DeterministicRandom(k * 7919 + n * 104729);
                    var pick = weak.OrderBy(_ => rng.Next()).Take(n).ToList();
                    none.Add(Median(pick.Select(m => m.Effects)));
                    countered.Add(Median(pick.Select(m => m.Effects).Concat(pick.Where(m => CounterOf.ContainsKey(m.Id)).Select(m => Equip(CounterOf[m.Id])))));
                }
                Line($"  약점 {n}개: 장비 없음 {none.Average():+0;-0} / 대응 장비 {countered.Average():+0;-0}");
            }

            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            File.WriteAllText(Path.Combine(outDir, "modifier_tuning.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }
    }
}
