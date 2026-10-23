using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 재미 보강 후보 측정(2026-10-24, Docs/review_2026-10-23.md 개선 제안 4): dotnet run --project Tools/SettlementSim -- funboost
    // 1) 초반 긴장: 후보 규칙별로 숙련 봇·기준 봇 캠페인(5시드) 첫 4정착지의 순이익·적자·'아슬아슬'(정착지 중 최저 자금 < 100)·
    //    그리고 25정착지까지의 처음 적자 D(후반이 같이 어려워지지 않는지)
    // 2) 매각 선택: 숙련 봇 캠페인의 경매마다 입찰자 수, 1·2위 현금 차이, 최고 입찰자 분포 — 참석 인원 후보(1~2명 / 2~3명) 비교
    public static partial class Program
    {
        private sealed class EarlyVariant
        {
            public string Name;
            public Action<EconomyConfig, ProceduralMapConfig> Apply;
        }

        private static int FunBoost(string[] args)
        {
            var variants = new List<EarlyVariant>
            {
                new EarlyVariant { Name = "현재", Apply = (c, p) => { } },
                new EarlyVariant { Name = "E1 초반 운영비 바닥(D 0.6만큼, ×1.3부터)", Apply = (c, p) => p.Difficulty.UpkeepMinD = 0.6 },
                new EarlyVariant { Name = "E2 재해 0회 없앰(1회 60%·2회 40%)", Apply = (c, p) => c.Disasters.CountWeights = new List<double> { 0, 0.6, 0.4 } },
                new EarlyVariant { Name = "E3 시작 자금 500", Apply = (c, p) => c.Prototype.StartingCash = 500 },
                new EarlyVariant { Name = "E1 + E2", Apply = (c, p) => { p.Difficulty.UpkeepMinD = 0.6; c.Disasters.CountWeights = new List<double> { 0, 0.6, 0.4 }; } },
            };
            Header("초반 긴장 후보 — 캠페인 5시드 첫 4정착지(숙련 봇 / 기준 봇). 아슬아슬 = 정착지 중 최저 자금 100 미만");
            Line("  후보                                   | 봇   | 정착지 1~4 순이익 평균          | 적자 | 아슬아슬 | 처음 적자 D(25정착지)");
            foreach (EarlyVariant v in variants)
                foreach (bool smart in new[] { true, false })
                    EarlyRun(v, smart);

            Header("매각 선택 — 숙련 봇 캠페인 5시드 × 8정착지의 경매. 접전 = 1·2위 현금 차이 10% 미만(혜택으로 고를 여지)");
            foreach ((int min, int max) in new[] { (1, 2), (2, 3) })
                AuctionChoiceRun(min, max);

            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "fun_boost.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static void EarlyRun(EarlyVariant v, bool smart)
        {
            var profits = new double[4];
            int deficits = 0, close = 0, fails = 0;
            var firstDeficitD = new List<string>();
            foreach (int seed in new[] { 1, 2, 3, 4, 5 })
            {
                EconomyConfig config = LoadConfig();
                ProceduralMapConfig proc = LoadProceduralConfig();
                v.Apply(config, proc);
                var c = new Campaign(config, LoadMaps(), seed, proc);
                c.StartNextSettlement();
                string first = "없음";
                for (int n = 1; n <= 25; n++)
                {
                    var played = c.Current;
                    double before = played.Cash;
                    double d = c.CurrentGenerated?.Params.D ?? 0;
                    bool ok = smart ? SkilledBot.PlayCampaignSettlement(c) : ReferenceBot.PlayCampaignSettlement(c);
                    double profit = ok ? c.Cash - before : -before;
                    if (n <= 4)
                    {
                        if (!ok) fails++;
                        profits[n - 1] += profit / 5;
                        if (profit < 0) deficits++;
                        double minCash = ok && played.History.Count > 0 ? played.History.Min(h => h.Cash) : 0;
                        if (minCash < 100) close++;
                    }
                    if ((profit < 0 || !ok) && first == "없음") first = $"D{d:0.0}";
                    if (!ok) break;
                    c.StartNextSettlement();
                }
                firstDeficitD.Add(first);
            }
            Line($"  {v.Name,-38} | {(smart ? "숙련" : "기준"),-4} | {string.Join(" / ", profits.Select(p => p.ToString("+0;-0"))),-30} | {deficits,4} | {close,4}/20 | {string.Join(", ", firstDeficitD)}{(fails > 0 ? $"  (실패 {fails})" : "")}");
        }

        private static void AuctionChoiceRun(int min, int max)
        {
            int auctions = 0, two = 0, closeCalls = 0;
            var wins = new Dictionary<string, int>();
            SkilledBot.BeforeSell = e =>
            {
                auctions++;
                var ordered = e.Bids.OrderByDescending(b => b.Cash).ToList();
                string top = ordered[0].Bidder.Name;
                wins[top] = wins.TryGetValue(top, out int w) ? w + 1 : 1;
                if (ordered.Count < 2) return;
                two++;
                if (ordered[1].Cash >= ordered[0].Cash * 0.9) closeCalls++;
            };
            foreach (int seed in new[] { 1, 2, 3, 4, 5 })
            {
                EconomyConfig config = LoadConfig();
                config.Auction.AttendeesMin = min;
                config.Auction.AttendeesMax = max;
                var c = new Campaign(config, LoadMaps(), seed, LoadProceduralConfig());
                c.StartNextSettlement();
                for (int n = 0; n < 8; n++)
                {
                    if (!SkilledBot.PlayCampaignSettlement(c)) break;
                    c.StartNextSettlement();
                }
            }
            SkilledBot.BeforeSell = null;
            Line($"  참석 {min}~{max}명 | 경매 {auctions} | 2명 이상 {two} | 접전 {closeCalls} ({(two > 0 ? closeCalls * 100 / two : 0)}%) | 최고 입찰자 " +
                 string.Join(", ", wins.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}")));
        }
    }
}
