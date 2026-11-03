using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using DesertSettlement.Core.Water;

namespace SettlementSim
{
    // 병목·파산 측정: dotnet run --project Tools/SettlementSim -- bottleneck [campaigns=20] [n=40] [maps=5] [diet] [nodisaster] [wells=0] [diag=k] [d.키=값] [m.키=값]
    // 숙련 봇 캠페인(`-- mods campaign … smart`과 같은 규칙, 캠페인 시드 1~campaigns)을 정착지 n개까지 돌리며
    // 스테이지마다 본선이 고르지 않은 후보 맵 maps곳(기본 5)도 같은 자금·모디파이어로 플레이해 표본을 늘린다(본선 진행은 그대로).
    // 매 시간 어떤 병목에 걸려 있는지 세고, 정착지(스테이지)마다 순이익·최저 자금·광산 물 공급률·긴급 매각·파산을 기록한다.
    // 결과: Tools/balance_output/bottlenecks.txt(요약)·bottlenecks.csv(정착지 한 줄씩).
    public static partial class Program
    {
        /// <summary>한 시간에 걸린 병목 종류(한 시간에 여럿 가능).</summary>
        private static readonly string[] BottleneckNames = { "광산 물", "기타 물", "일손", "식량", "석재", "공사 슬롯", "창고", "재해", "자금<100" };
        private const int SegmentHours = 24;
        // diag=k : D 5 이상 정착지 k곳의 시설·광산 물·모디파이어를 매각 직전에 찍는다.
        private static int DiagLeft;

        private sealed class StageRecord
        {
            public int Campaign, N, Hours;
            /// <summary>본선이 아닌 곁가지 표본(같은 자금·모디파이어로 다른 후보 맵에 들어가 본 것).</summary>
            public bool Side;
            public bool Failed => End == "파산" || End == "진행 불가";
            public double D, StartCash, Profit, MinCash, StoneBought, FoodBought, Upkeep;
            // 운영 중인 광산의 물 공급률 합과 광산·시간 수(광산 물 부족이 얼마나 깊은지).
            public double MineWaterSum;
            public int MineWaterSamples;
            public double MineWater => MineWaterSamples == 0 ? 1 : MineWaterSum / MineWaterSamples;
            public long MinCashHour;
            public string End = "정기"; // 정기 · 보류 후 · 긴급 · 파산 · 진행 불가
            public readonly int[] Counts = new int[BottleneckNames.Length];
            // 시간 구간(24시간)별 병목 시간 수와 그 구간의 시간 수.
            public readonly Dictionary<int, int[]> BySegment = new Dictionary<int, int[]>();
            public readonly Dictionary<int, int> SegmentHoursSeen = new Dictionary<int, int>();
            public double Share(int i) => Hours == 0 ? 0 : (double)Counts[i] / Hours;
            public string Main()
            {
                // 재해·자금 부족은 결과라 주 병목 후보에서 뺀다.
                int best = Enumerable.Range(0, 7).OrderByDescending(Share).First();
                return Share(best) < 0.1 ? "없음" : BottleneckNames[best];
            }
        }

        private static bool[] HourBottlenecks(Settlement s)
        {
            HourRecord r = s.History[s.History.Count - 1];
            var hit = new bool[BottleneckNames.Length];
            bool Dry(Facility f) => f.IsOperating && (s.LevelOf(f).WaterDemand ?? 0) > 0 && s.WaterRateOf(f) < 0.99;
            hit[0] = s.Facilities.Any(f => f.Kind == BuildingKinds.Mine && Dry(f));
            hit[1] = s.Facilities.Any(f => f.Kind != BuildingKinds.Mine && f.Kind != BuildingKinds.CanalEdge && f.Kind != BuildingKinds.House && Dry(f));
            hit[2] = r.WorkersNeeded - r.WorkersAssigned > 0.5;
            hit[3] = r.FoodDeficit > 0;
            ConstructionJob queued = s.Jobs.FirstOrDefault(j => j.State == JobState.Queued);
            hit[4] = queued != null && queued.StoneCost > s.Stone;
            hit[5] = queued != null && !hit[4];
            hit[6] = r.Discarded > 0 || r.AutoSaleIncome > 0;
            hit[7] = s.Disasters != null && s.Disasters.Events.Any(e => e.State == DisasterState.Active);
            hit[8] = r.Cash < 100;
            return hit;
        }

        /// <summary>
        /// 후보 비교용 덮어쓰기: d.키=값은 난이도 곡선(<c>difficulty</c>), m.키=값은 지역 모디파이어(<c>modifiers</c>)의 숫자 속성.
        /// 키는 JSON 이름(예: d.minWellsPer37=0.6, m.weaknessStepD=1.5). 바꾼 것을 머리글에 붙일 글로 돌려준다.
        /// </summary>
        private static string OverrideProcedural(ProceduralMapConfig proc, string[] args)
        {
            var applied = new List<string>();
            foreach (string a in args.Where(a => a.StartsWith("d.") || a.StartsWith("m.")))
            {
                string[] kv = a.Substring(2).Split('=');
                object target = a[0] == 'd' ? proc.Difficulty : (object)proc.Modifiers;
                var prop = target.GetType().GetProperty(kv[0], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)
                           ?? throw new ArgumentException($"알 수 없는 키: {a}");
                prop.SetValue(target, Convert.ChangeType(double.Parse(kv[1], CultureInfo.InvariantCulture), prop.PropertyType, CultureInfo.InvariantCulture));
                applied.Add(a);
            }
            return applied.Count == 0 ? "" : ", " + string.Join(" ", applied);
        }

        /// <summary>
        /// 후보표 한 줄: 본선 파산 수·파산 스테이지 중앙값·처음 평균 적자 스테이지(곁가지 포함),
        /// D 구간(1 단위)마다 순이익·광산 물 공급률(실패 제외)과 긴급 매각·실패 비율(곁가지 포함).
        /// </summary>
        private static string Summary(List<StageRecord> stages, int campaigns)
        {
            List<StageRecord> fails = stages.Where(x => !x.Side && x.Failed).OrderBy(x => x.N).ToList();
            string median = fails.Count == 0 ? "-" : $"#{fails[fails.Count / 2].N}(D{fails[fails.Count / 2].D:0.0})";
            int firstLoss = stages.Where(x => !x.Failed).GroupBy(x => x.N).OrderBy(g => g.Key).Where(g => g.Key > 2 && g.Average(x => x.Profit) < 0).Select(g => g.Key).DefaultIfEmpty(0).First();
            string bands = string.Join(" ", Enumerable.Range(0, 7).Select(b =>
            {
                var all = stages.Where(x => x.D >= b && x.D < b + 1).ToList();
                var l = all.Where(x => !x.Failed).ToList();
                return l.Count == 0 ? $"D{b}:-" : $"D{b}:{l.Average(x => x.Profit):+0;-0;0}/{l.Sum(x => x.MineWaterSum) / Math.Max(1, l.Sum(x => x.MineWaterSamples)):P0}"
                    + $"/실패{(double)all.Count(x => x.Failed || x.End == "긴급") / all.Count:P0}({all.Count})";
            }));
            return $"■ 요약: 파산 {fails.Count}/{campaigns} · 파산 중앙값 {median} · 처음 평균 적자 #{firstLoss} · 끝까지 {stages.Count(x => !x.Side && x.N == stages.Max(y => y.N))}개 · D 구간 순이익/광산 물/긴급·실패율(표본 수) {bands}";
        }

        /// <summary>
        /// 지금 캠페인의 정착지 하나를 숙련 봇으로 끝까지(매각·파산) 플레이하며 시간마다 병목을 센다.
        /// 실패(파산·진행 불가)면 순이익 = −시작 자금.
        /// </summary>
        private static StageRecord PlayStage(Campaign c, int seed, int n, bool side)
        {
            var st = new StageRecord { Campaign = seed, N = n, Side = side, D = c.CurrentGenerated?.Params.D ?? 0, StartCash = c.Current.Cash };
            st.MinCash = st.StartCash;
            Settlement s = c.Current;
            SkilledBot.AfterHour = x =>
            {
                bool[] hit = HourBottlenecks(x);
                int seg = (int)((x.CurrentHour - 1) / SegmentHours);
                if (!st.BySegment.TryGetValue(seg, out int[] segCounts)) st.BySegment[seg] = segCounts = new int[BottleneckNames.Length];
                st.SegmentHoursSeen[seg] = st.SegmentHoursSeen.TryGetValue(seg, out int h) ? h + 1 : 1;
                for (int i = 0; i < hit.Length; i++)
                    if (hit[i]) { st.Counts[i]++; segCounts[i]++; }
                foreach (Facility m in x.OperatingOf(BuildingKinds.Mine)) { st.MineWaterSum += x.WaterRateOf(m); st.MineWaterSamples++; }
                st.Hours++;
                st.Upkeep += x.History[x.History.Count - 1].Upkeep;
                if (x.Cash < st.MinCash) { st.MinCash = x.Cash; st.MinCashHour = x.CurrentHour; }
            };
            int holdsBefore = SkilledBot.Holds, salesBefore = c.Sales.Count;
            bool ok = SkilledBot.PlayCampaignSettlement(c);
            SkilledBot.AfterHour = null;
            st.StoneBought = s.StoneSpent;
            st.FoodBought = s.FoodSpent;
            if (!ok)
            {
                st.End = c.State == CampaignState.Bankrupt ? "파산" : "진행 불가";
                st.Profit = -st.StartCash;
                return st;
            }
            st.Profit = c.Cash - st.StartCash;
            if (c.Sales.Count > salesBefore && c.Sales[c.Sales.Count - 1].Emergency) st.End = "긴급";
            else if (SkilledBot.Holds > holdsBefore) st.End = "보류 후";
            return st;
        }

        /// <summary>
        /// 곁가지 표본: 매각 직후(행선지 선택) 캠페인을 복제해, 본선이 고른 후보(pick) 말고 다른 후보 맵 count곳에 각각 들어가 플레이한다.
        /// 후보 수(regionChoices)를 잠시 늘려 맵을 더 받는다 — 앞쪽 후보는 늘려도 같으므로 본선은 바뀌지 않는다.
        /// </summary>
        private static List<StageRecord> PlaySideMaps(Campaign c, int pick, int count, int seed, int n)
        {
            var result = new List<StageRecord>();
            ProceduralMapConfig proc = (ProceduralMapConfig)typeof(Campaign).GetField("_procedural", PrivateField).GetValue(c);
            if (count <= 0 || proc == null || c.NextOffers().Count < 2) return result; // 고정 맵 차례는 후보가 하나뿐
            int choices = proc.RegionChoices;
            proc.RegionChoices = count + 1;
            try
            {
                foreach (int i in Enumerable.Range(0, count + 1).Where(i => i != pick).Take(count))
                {
                    Campaign fork = Fork(c);
                    fork.StartNextSettlement(i);
                    result.Add(PlayStage(fork, seed, n, side: true));
                }
            }
            finally { proc.RegionChoices = choices; }
            return result;
        }

        /// <summary>diag=k: 매각 직전 시설·광산 물·우물 물 흐름·걸린 효과.</summary>
        private static void Diagnose(Campaign c, StageRecord st)
        {
            Settlement s = c.Current;
            string Of(string k) => $"{s.Facilities.Count(f => f.Kind == k && f.IsOperating)}/{s.Facilities.Count(f => f.Kind == k)}";
            Line($"  [진단] 캠페인 {st.Campaign} #{st.N} D{st.D:0.00}: 광산 {Of(BuildingKinds.Mine)} 우물 {Of(BuildingKinds.Well)} 수로 {Of(BuildingKinds.CanalEdge)}"
                 + $" 광산 물 {string.Join(",", s.Facilities.Where(f => f.Kind == BuildingKinds.Mine).Select(f => $"{s.WaterRateOf(f):P0}"))}"
                 + $" 모디 {string.Join(",", c.CurrentModifiers.Select(m => m.Name))} 석재 {s.Stone:0} 자금 {s.Cash:0}");
            WaterAllocation w = s.LastWater;
            Line($"         우물 L{string.Join(",", s.OperatingOf(BuildingKinds.Well).Select(f => f.Level))} 공급 {w.TotalSupply:0} 방출 {w.TotalReleased:0} 전달 {w.TotalDelivered:0} 손실 {w.TotalLoss:0}"
                 + $" · 물 쓰는 시설 {string.Join(",", s.Facilities.Where(f => f.IsOperating && (s.LevelOf(f).WaterDemand ?? 0) > 0).GroupBy(f => f.Kind).Select(g => $"{g.Key}{g.Count()}({g.Sum(f => s.LevelOf(f).WaterDemand ?? 0):0})"))}"
                 + $" · 효과 {s.Rules.AppliedEffects.Describe()}");
        }

        private const System.Reflection.BindingFlags PrivateField = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        /// <summary>측정용 캠페인 복제: 얕은 복사 뒤 목록·사전은 새로 만들고 이벤트 구독은 끊는다(본선 상태를 건드리지 않게).</summary>
        private static Campaign Fork(Campaign c)
        {
            var fork = (Campaign)typeof(object).GetMethod("MemberwiseClone", PrivateField).Invoke(c, null);
            foreach (var f in typeof(Campaign).GetFields(PrivateField))
            {
                object v = f.GetValue(c);
                if (v == null) continue;
                if (typeof(Delegate).IsAssignableFrom(f.FieldType)) f.SetValue(fork, null);
                else if (f.FieldType.IsGenericType && (f.FieldType.GetGenericTypeDefinition() == typeof(List<>) || f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                    f.SetValue(fork, Activator.CreateInstance(f.FieldType, v));
            }
            return fork;
        }

        private static int Bottlenecks(string[] args)
        {
            var inv = CultureInfo.InvariantCulture;
            string Arg(string key) => args.FirstOrDefault(a => a.StartsWith(key + "="))?.Substring(key.Length + 1);
            int campaigns = int.Parse(Arg("campaigns") ?? "20", inv), maxN = int.Parse(Arg("n") ?? "40", inv);
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            if (args.Contains("nodisaster")) config.Disasters = null;
            SkilledBot.Diet = args.Contains("diet");
            DiagLeft = int.Parse(Arg("diag") ?? "0", inv);
            SkilledBot.UpgradeWells = Arg("wells") != "0";
            string overrides = OverrideProcedural(proc, args);
            int sideMaps = int.Parse(Arg("maps") ?? "5", inv);
            var maps = LoadMaps();

            Header($"병목·파산 — 숙련 봇 캠페인 {campaigns}개 × 정착지 최대 {maxN}개 (DPerSettlement {proc.Difficulty.DPerSettlement}, CashPerD {proc.Difficulty.CashPerD}"
                   + $"{(SkilledBot.Diet ? ", 식품 시설" : "")}{(SkilledBot.UpgradeWells ? "" : ", 우물 개량 안 함")}{(config.Disasters == null ? ", 재해 없음" : "")}{overrides}, 스테이지마다 본선 + 다른 후보 맵 {sideMaps}곳)");
            var stages = new List<StageRecord>();
            for (int seed = 1; seed <= campaigns; seed++)
            {
                var c = new Campaign(config, maps, seed, proc);
                c.StartNextSettlement();
                var line = new StringBuilder($"  캠페인 {seed,2}: ");
                for (int n = 1; n <= maxN; n++)
                {
                    StageRecord st = PlayStage(c, seed, n, side: false);
                    stages.Add(st);
                    bool ok = !st.Failed;
                    Settlement s = c.Current;
                    if (DiagLeft > 0 && st.D >= 5 && ok) { DiagLeft--; Diagnose(c, st); }
                    string label = $"#{n} D{st.D:0.0}";
                    if (!ok)
                    {
                        line.Append($"| {label} {st.End}({s.CurrentHour}h, {c.BankruptcyReason ?? "봇 계획 실패"}) ");
                        break;
                    }
                    line.Append($"| {label} {st.Profit:+0;-0;0}{(st.End == "긴급" ? "!긴급" : "")} {st.Main()} ");
                    if (n == maxN) line.Append($"| 끝 자금 {c.Cash:0}");
                    else
                    {
                        int pick = BidderChoice.PickOffer(c);
                        stages.AddRange(PlaySideMaps(c, pick, sideMaps, seed, n + 1));
                        c.StartNextSettlement(pick);
                    }
                }
                Line(line.ToString());
            }
            SkilledBot.AfterHour = null;
            Line($"  숙련 봇 행동: 우물 개량 {SkilledBot.WellUpgrades}번 · 광산 개량 {SkilledBot.MineUpgrades}번 · 추가 주택 {SkilledBot.Houses}채 · 보류 {SkilledBot.Holds}번");

            string Pct(double x) => $"{x * 100,3:0}%";
            string Shares(IEnumerable<StageRecord> list)
            {
                int hours = list.Sum(x => x.Hours);
                return string.Join(" ", Enumerable.Range(0, BottleneckNames.Length).Select(i => Pct(hours == 0 ? 0 : (double)list.Sum(x => x.Counts[i]) / hours)));
            }
            string head = string.Join(" ", BottleneckNames);

            Line("");
            Line("■ D 구간별(본선 + 곁가지 표본) — 정착지 수 · 순이익 평균 · 적자 · 긴급 매각 · 파산/진행 불가 · 광산 물 공급률 · 평균 시간 · 석재/식량 구매 · 운영비 합");
            Line($"  병목 = 그 시간에 걸려 있던 비율({head})");
            foreach (var band in stages.GroupBy(x => (int)Math.Floor(x.D * 2)).OrderBy(g => g.Key))
            {
                var l = band.ToList();
                Line($"  D{band.Key / 2.0:0.0}~{(band.Key + 1) / 2.0:0.0}: {l.Count,3}곳 · 순이익 {l.Average(x => x.Profit),6:+0;-0;0} · 적자 {l.Count(x => x.Profit < 0),2}"
                     + $" · 긴급 {l.Count(x => x.End == "긴급"),2} · 파산 {l.Count(x => x.End == "파산"),2}/불가 {l.Count(x => x.End == "진행 불가"),2}"
                     + $" · 광산 물 공급 {l.Sum(x => x.MineWaterSum) / Math.Max(1, l.Sum(x => x.MineWaterSamples)),4:P0} · {l.Average(x => x.Hours),4:0}h · 구매 {l.Average(x => x.StoneBought),4:0}/{l.Average(x => x.FoodBought),4:0} · 운영비 {l.Average(x => x.Upkeep),5:0}");
                Line($"           병목 {Shares(l)}");
                Line($"           주 병목: {string.Join(", ", l.GroupBy(x => x.Main()).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"))}");
            }

            Line("");
            Line($"■ 정착지 안 시간 구간별 병목(모든 정착지, {SegmentHours}시간 단위) — {head}");
            int maxSeg = stages.SelectMany(x => x.BySegment.Keys).DefaultIfEmpty(0).Max();
            for (int seg = 0; seg <= maxSeg; seg++)
            {
                int hours = stages.Sum(x => x.SegmentHoursSeen.TryGetValue(seg, out int h) ? h : 0);
                if (hours == 0) continue;
                string shares = string.Join(" ", Enumerable.Range(0, BottleneckNames.Length)
                    .Select(i => Pct((double)stages.Sum(x => x.BySegment.TryGetValue(seg, out int[] cs) ? cs[i] : 0) / hours)));
                Line($"  {seg * SegmentHours,3}~{(seg + 1) * SegmentHours,3}h ({stages.Count(x => x.SegmentHoursSeen.ContainsKey(seg)),3}곳): {shares}");
            }

            Line("");
            Line("■ 스테이지(n번째 정착지)별 본선 — 남은 캠페인 · D 평균 · 시작 자금 평균 · 순이익 평균 · 적자/긴급/실패 · 최저 자금 평균(시각) · 곁가지 포함 표본 순이익(실패 제외)·긴급·실패율");
            foreach (var g in stages.Where(x => !x.Side).GroupBy(x => x.N).OrderBy(g => g.Key))
            {
                var l = g.ToList();
                var sample = stages.Where(x => x.N == g.Key).ToList();
                var alive = sample.Where(x => !x.Failed).ToList();
                Line($"  #{g.Key,2}: {l.Count,2}개 · D{l.Average(x => x.D):0.00} · 시작 {l.Average(x => x.StartCash),6:0} · 순이익 {l.Average(x => x.Profit),6:+0;-0;0}"
                     + $" · 적자 {l.Count(x => x.Profit < 0),2} 긴급 {l.Count(x => x.End == "긴급"),2} 실패 {l.Count(x => x.End == "파산" || x.End == "진행 불가"),2}"
                     + $" · 최저 {l.Average(x => x.MinCash),6:0}({l.Average(x => x.MinCashHour):0}h)"
                     + $" · 표본 {sample.Count,3}곳 {(alive.Count == 0 ? 0 : alive.Average(x => x.Profit)),6:+0;-0;0} 긴급·실패 {(double)sample.Count(x => x.Failed || x.End == "긴급") / sample.Count,4:P0}");
            }

            Line("");
            Line(Summary(stages, campaigns));
            Line("");
            Line("■ 본선에서 긴급 매각·파산·진행 불가가 난 곳");
            foreach (StageRecord x in stages.Where(x => !x.Side && (x.End == "긴급" || x.Failed)))
                Line($"  캠페인 {x.Campaign} #{x.N} D{x.D:0.00} {x.End}: 시작 자금 {x.StartCash:0}, {x.Hours}h, 최저 자금 {x.MinCash:0}({x.MinCashHour}h), 주 병목 {x.Main()}");
            if (!stages.Any(x => !x.Side && (x.End == "긴급" || x.Failed))) Line("  없음");

            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "bottlenecks.txt"), Report.ToString(), new UTF8Encoding(false));
            var csv = new StringBuilder("campaign,n,side,D,startCash,profit,end,hours,minCash,minCashHour,stoneBought,foodBought,upkeep,mineWater,main," + string.Join(",", BottleneckNames) + "\n");
            foreach (StageRecord x in stages)
                csv.Append(string.Join(",", new object[] { x.Campaign, x.N, x.Side ? 1 : 0, x.D.ToString("0.000", inv), x.StartCash.ToString("0", inv), x.Profit.ToString("0", inv), x.End, x.Hours,
                    x.MinCash.ToString("0", inv), x.MinCashHour, x.StoneBought.ToString("0", inv), x.FoodBought.ToString("0", inv), x.Upkeep.ToString("0", inv), x.MineWater.ToString("0.000", inv), x.Main() }
                    .Concat(Enumerable.Range(0, BottleneckNames.Length).Select(i => (object)x.Share(i).ToString("0.000", inv))))).Append('\n');
            File.WriteAllText(Path.Combine(outDir, "bottlenecks.csv"), csv.ToString(), new UTF8Encoding(true));
            return 0;
        }
    }
}
