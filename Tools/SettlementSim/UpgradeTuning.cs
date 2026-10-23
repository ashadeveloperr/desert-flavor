using System;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Economy;

namespace SettlementSim
{
    // 로드맵 1-③ L3 개량 측정: dotnet run --project Tools/SettlementSim -- upgrades
    // 고정 맵 1 스크립트(우물·농장·채석장·광산·경매장)를 첫 경매(133h) 뒤 72h 보류하며 개량했을 때
    // 205h 시점 '경매 직전 현금 + 입찰가(×1.05)'를 비교한다. 지금 규칙(가동 숙성·채석장 능력 켬)과 레벨 상한 3.
    public static partial class Program
    {
        private static int UpgradeTuning(string[] args)
        {
            var config = LoadConfig();
            config.Prototype.InitialMvpMaxFacilityLevel = Math.Max(3, config.Prototype.InitialMvpMaxFacilityLevel);
            const int first = 133, hold = 205;
            Header("L3 개량 측정 — 고정 맵 1, 첫 경매 133h 뒤 72h 보류, 205h 매각 후 현금(경매 직전 현금 + 입찰가 ×1.05)");
            var scenarios = new (string label, int until, (string, string, int)[] actions)[]
            {
                ("133h 즉시 매각", first, Array.Empty<(string, string, int)>()),
                ("보류(투자 없음)", hold, Array.Empty<(string, string, int)>()),
                ("보류 + 광산 L2", hold, new[] { ("mine", "upgrade", first) }),
                ("보류 + 광산 L2 → L3", hold, new[] { ("mine", "upgrade", first), ("mine", "upgrade", first + 15) }),
                ("보류 + 농장 L2 → L3", hold, new[] { ("farm", "upgrade", first), ("farm", "upgrade", first + 9) }),
                ("보류 + 채석장 L2 → L3", hold, new[] { ("quarry", "upgrade", first), ("quarry", "upgrade", first + 10) }),
                ("보류 + 우물 L2 → L3", hold, new[] { ("well", "upgrade", first), ("well", "upgrade", first + 11) }),
            };
            foreach (var sc in scenarios)
            {
                Settlement s;
                try
                {
                    s = RunCoreScript(config, sc.actions, null, null, sc.until, null, currentRules: true);
                }
                catch (InvalidOperationException e)
                {
                    Line($"  {sc.label,-22} | 실행 실패: {e.Message}");
                    continue;
                }
                double cash = s.History[sc.until - 1].Cash;
                double bid = SettlementValuation.Evaluate(s, sc.until).Bid(1.05);
                string mine = string.Join("", s.Facilities.Where(f => f.Kind == "mine").Select(f => $"광산 L{f.Level}"));
                Line($"  {sc.label,-22} | 매각 후 현금 {cash + bid,8:0.0} (현금 {cash,7:0.0} + 입찰 {bid,7:0.0}) | {mine} 석재 {s.Stone:0}");
            }
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "upgrade_tuning.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }
    }
}
