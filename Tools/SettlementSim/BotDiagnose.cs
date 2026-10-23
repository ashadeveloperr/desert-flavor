using System.Linq;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 로드맵 2-④ 봇 보완: dotnet run --project Tools/SettlementSim -- botdiag
    // `-- tune`과 같은 맵(D마다 24장)에서 다중 봇이 첫 경매에 못 닿는 맵을 골라, 실패 이유와 그때의 정착지 상태를 보여 준다.
    public static partial class Program
    {
        private static int BotDiagnose(string[] args)
        {
            var config = LoadConfig();
            var rules = LoadProceduralConfig();
            Header("봇 진단 — 첫 경매에 못 닿는 맵(-- tune과 같은 시드)");
            foreach (double d in TuneDs)
            {
                DifficultyParams p = DifficultyParams.For(d, rules.Difficulty);
                for (int i = 0; i < 24; i++)
                {
                    GeneratedMap g = ProceduralMapGenerator.Generate(p, rules.Generator, 50000 + (int)(d * 1000) + i);
                    if (g == null) continue;
                    var r = ReferenceBot.Run(config, g.Map, x => x.StartingCash = 2000, seed: g.Seed, strategy: BotStrategy.Multi);
                    if (r.AuctionHour >= 0) continue;
                    Settlement s = r.Settlement;
                    string state = s == null ? "정착지 없음" :
                        $"{s.CurrentHour}h 자금 {s.Cash:0} 석재 {s.Stone:0} 식량 {s.Food:0} F {s.FoodCoefficient:0.00} 경매장 {(s.AuctionHallCompletedHour?.ToString() ?? "없음")} " +
                        $"시설 {string.Join(",", s.Facilities.Where(f => !f.Edge.HasValue).GroupBy(f => f.Kind).Select(gr => gr.Key + gr.Count()))} " +
                        $"대기 작업 {s.Jobs.Count(j => j.State == JobState.Queued)}";
                    Line($"  D{d:0.##} 맵 {i}: {r.Failure} | 계획 수로 {r.CanalCount} 광산 {r.MineCount} 우물 {r.WellCount} | {state}");
                }
            }
            return 0;
        }
    }
}
