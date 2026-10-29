using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 결정 완료(결정 BD1~BD6). 다시 잴 일이 있을 때만 쓴다(결정 SM1).
    // 로드맵 1-⑥ 입찰자 6종 측정: dotnet run --project Tools/SettlementSim -- bidders [맵 수]
    // 다중 봇이 첫 경매까지 운영한 정착지에서 입찰자마다 '현금 입찰 / 중립 가치'(시세 제외, 현금 비율 포함) 중앙값과,
    // 6명이 모두 왔을 때 현금이 가장 큰 입찰자가 되는 비율을 본다. 목표: 특정 입찰자가 늘 이기지 않고, 평균 현금 입찰이 기존 2종과 비슷.
    public static partial class Program
    {
        private static int BidderTuning(string[] args)
        {
            EconomyConfig config = LoadConfig();
            ProceduralMapConfig proc = LoadProceduralConfig();
            int maps = args.Length > 0 ? int.Parse(args[0]) : 20;
            List<BidderProfile> bidders = config.Auction.Bidders;
            Header($"입찰자 측정 — 다중 봇 첫 경매, D마다 {maps}장. 칸 = 현금 입찰 / 중립 가치 중앙값 (6명 중 최고 현금 입찰 비율)");
            Line("  D    | " + string.Join(" | ", bidders.Select(b => b.Name)));
            foreach (double d in new[] { 0.0, 0.5, 1.0, 2.0 })
            {
                var ratios = bidders.ToDictionary(b => b.Id, _ => new List<double>());
                var wins = bidders.ToDictionary(b => b.Id, _ => 0);
                int n = 0;
                for (int i = 0; i < maps; i++)
                {
                    GeneratedMap g = ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 81000 + (int)(d * 1000) + i);
                    if (g == null) continue;
                    var r = ReferenceBot.Run(config, g.Map, x => x.StartingCash = 2000, seed: g.Seed, strategy: BotStrategy.Multi);
                    if (r.AuctionHour < 0) continue;
                    double neutral = SettlementValuation.Evaluate(r.Settlement, r.AuctionHour).Value;
                    if (neutral <= 0) continue;
                    n++;
                    var cash = bidders.ToDictionary(b => b.Id, b => SettlementValuation.EvaluateFor(r.Settlement, r.AuctionHour, b).Value * b.CashShare);
                    foreach (BidderProfile b in bidders) ratios[b.Id].Add(cash[b.Id] / neutral);
                    wins[cash.OrderByDescending(kv => kv.Value).First().Key]++;
                }
                string Cell(BidderProfile b)
                {
                    List<double> xs = ratios[b.Id].OrderBy(x => x).ToList();
                    return xs.Count == 0 ? "-" : $"{xs[xs.Count / 2]:0.00} ({(double)wins[b.Id] / n:P0})";
                }
                Line($"  D{d:0.0} | " + string.Join(" | ", bidders.Select(Cell)) + $"   ({n}장)");
            }
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "bidder_tuning.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }
    }
}
