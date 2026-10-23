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
    // 게임성 검토 위험 측정(2026-10-23): dotnet run --project Tools/SettlementSim -- funcheck [맵 수]
    // 1) 보류: 첫 경매에서 팔기 vs 72h 보류(투자 없음 / 광산 전부 한 단계 개량) — 보류 한 번마다 시세 −p 후보 비교
    // 2) 초반 긴장감: 캠페인 첫 4정착지의 순이익·적자·파산을 시작 자금 후보별로
    // 3) 입찰자 차이: 정착지마다 '가장 잘 쳐주는 입찰자 / 가장 덜 쳐주는 입찰자' 비율, 가중치 차이를 k배로 넓힌 후보 비교
    public static partial class Program
    {
        private static int FunCheck(string[] args)
        {
            int maps = args.Length > 0 ? int.Parse(args[0]) : 20;
            HoldCheck(maps);
            EarlyCheck();
            BidderSpreadCheck(maps);
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "fun_check.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static void HoldCheck(int maps)
        {
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            Header("보류 측정 — 첫 경매 즉시 매각 대비, 72h 보류 뒤 매각 후 현금 차이(중앙값). p = 보류 한 번마다 시세 감소");
            Line("  D    | 보류(투자 없음) p=0 | 보류+광산 개량 p=0 | 더 나은 보류 p=5% | p=10% | 보류가 이기는 맵(p=0 / 5% / 10%)");
            foreach (double d in new[] { 0.5, 1.0, 2.0 })
            {
                var plain = new List<double>(); var invest = new List<double>(); var p5 = new List<double>(); var p10 = new List<double>();
                int win0 = 0, win5 = 0, win10 = 0, n = 0;
                for (int i = 0; i < maps; i++)
                {
                    GeneratedMap g = ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 61000 + (int)(d * 1000) + i);
                    if (g == null) continue;
                    // 매각 후 현금 = 정착지 현금 + 입찰가. 시세 감소 p는 입찰가에만 걸린다.
                    (double cash, double bid)? now = null, holdPlain = null, holdInvest = null;
                    foreach (bool investing in new[] { false, true })
                    {
                        var r = ReferenceBot.Run(config, g.Map, x => x.StartingCash = 2000, seed: g.Seed, strategy: BotStrategy.Multi, disasters: config.Disasters);
                        if (r.AuctionHour < 0) break;
                        Settlement s = r.Settlement;
                        now = (s.Cash, SettlementValuation.Evaluate(s, r.AuctionHour).Bid(1.05));
                        r.Auctions.Hold();
                        if (investing)
                            foreach (Facility m in s.Facilities.Where(f => f.Kind == BuildingKinds.Mine && f.IsOperating).ToList()) s.RequestUpgrade(m.Id);
                        for (int h = 0; h < 200 && !r.Auctions.IsOpen && !r.Auctions.IsFinished; h++) { ReferenceBot.UnblockStone(s); s.AdvanceHour(); }
                        if (!r.Auctions.IsOpen) break;
                        var later = (s.Cash, SettlementValuation.Evaluate(s, r.Auctions.CurrentAuction.Hour).Bid(1.05));
                        if (investing) holdInvest = later; else holdPlain = later;
                    }
                    if (now == null || holdPlain == null || holdInvest == null) continue;
                    n++;
                    double Total((double cash, double bid) x, double p) => x.cash + x.bid * (1 - p);
                    double nowTotal = Total(now.Value, 0);
                    double Best(double p) => Math.Max(Total(holdPlain.Value, p), Total(holdInvest.Value, p));
                    plain.Add(Total(holdPlain.Value, 0) - nowTotal);
                    invest.Add(Total(holdInvest.Value, 0) - nowTotal);
                    p5.Add(Best(0.05) - nowTotal);
                    p10.Add(Best(0.10) - nowTotal);
                    if (Best(0) > nowTotal) win0++;
                    if (Best(0.05) > nowTotal) win5++;
                    if (Best(0.10) > nowTotal) win10++;
                }
                Line($"  D{d:0.0} | {Median(plain),18:+0;-0} | {Median(invest),17:+0;-0} | {Median(p5),4:+0;-0} | {Median(p10),5:+0;-0} | {win0}/{n} / {win5}/{n} / {win10}/{n}");
            }
        }

        private static void EarlyCheck()
        {
            Header("초반 긴장감 — 캠페인(5시드) 첫 4정착지: 정착지별 순이익 평균, 적자·파산 수");
            foreach (double startCash in new[] { 1000.0, 700.0, 500.0 })
            {
                var config = LoadConfig();
                config.Prototype.StartingCash = startCash;
                var proc = LoadProceduralConfig();
                var profits = new double[4]; int deficits = 0, fails = 0;
                foreach (int seed in new[] { 1, 2, 3, 4, 5 })
                {
                    var c = new Campaign(config, LoadMaps(), seed, proc);
                    c.StartNextSettlement();
                    for (int k = 0; k < 4; k++)
                    {
                        double before = c.Current.Cash;
                        if (!ReferenceBot.PlayCampaignSettlement(c)) { fails++; break; }
                        double profit = c.Cash - before;
                        profits[k] += profit / 5;
                        if (profit < 0) deficits++;
                        c.StartNextSettlement();
                    }
                }
                Line($"  시작 자금 {startCash,5:0} | 정착지 1~4 순이익 {string.Join(" / ", profits.Select(p => p.ToString("+0;-0")))} | 적자 {deficits} · 실패(파산·진행 불가) {fails}");
            }
        }

        private static void BidderSpreadCheck(int maps)
        {
            Header("입찰자 차이 — 정착지마다 현금 입찰 최고/최저 비율(중앙값)과 최고 입찰자 분포. k = 가중치의 1과의 차이를 k배");
            foreach (double k in new[] { 1.0, 2.0, 3.0 })
            {
                var config = LoadConfig();
                foreach (BidderProfile b in config.Auction.Bidders)
                {
                    double W(double w) => Math.Max(0.1, 1 + (w - 1) * k);
                    b.OreWeight = W(b.OreWeight); b.FoodMultiplierWeight = W(b.FoodMultiplierWeight);
                    b.StoneMultiplierWeight = W(b.StoneMultiplierWeight); b.InfraWeight = W(b.InfraWeight);
                    b.OpenLandWeight *= k;
                }
                var proc = LoadProceduralConfig();
                var spreads = new List<double>(); var avg = new List<double>();
                var wins = config.Auction.Bidders.ToDictionary(b => b.Name, _ => 0);
                foreach (double d in new[] { 0.5, 1.0, 2.0 })
                    for (int i = 0; i < maps; i++)
                    {
                        GeneratedMap g = ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 81000 + (int)(d * 1000) + i);
                        if (g == null) continue;
                        var r = ReferenceBot.Run(config, g.Map, x => x.StartingCash = 2000, seed: g.Seed, strategy: BotStrategy.Multi);
                        if (r.AuctionHour < 0) continue;
                        double neutral = SettlementValuation.Evaluate(r.Settlement, r.AuctionHour).Value;
                        var cash = config.Auction.Bidders.Select(b => (b.Name, v: SettlementValuation.EvaluateFor(r.Settlement, r.AuctionHour, b).Value * b.CashShare)).ToList();
                        spreads.Add(cash.Max(x => x.v) / Math.Max(1e-9, cash.Min(x => x.v)));
                        avg.Add(cash.Average(x => x.v) / neutral);
                        wins[cash.OrderByDescending(x => x.v).First().Name]++;
                    }
                Line($"  k={k:0} | 최고/최저 {Median(spreads):0.00} | 평균 입찰/중립 {Median(avg):0.00} | 최고 입찰자 {string.Join(", ", wins.Where(w => w.Value > 0).OrderByDescending(w => w.Value).Select(w => $"{w.Key} {w.Value}"))}");
            }
        }
    }
}
