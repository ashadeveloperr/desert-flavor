using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 매각 시점 측정: dotnet run --project Tools/SettlementSim -- selltime [n=2,4] [holds=4] [diet] [lab=이름…]
    // 숙련 봇 캠페인(`-- mods campaign 0.25 14000 … smart`과 같은 시드)에서 n번째 정착지만 정기 경매를 k번 보류한 뒤 판다(k = 0…holds).
    // 보류하는 동안에도 봇은 평소처럼 개량·주택을 짓는다. '어느 시점에 파는 게 가장 이득인가'의 곡선을 본다.
    // hold=x : 보류할 때마다 줄어드는 시세(auction.holdMarketPenalty) 후보.
    public static partial class Program
    {
        private static int SellTiming(string[] args)
        {
            var inv = CultureInfo.InvariantCulture;
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            proc.Difficulty.DPerSettlement = 0.25;
            proc.Difficulty.CashPerD = 14000;
            string Arg(string key) => args.FirstOrDefault(a => a.StartsWith(key + "="))?.Substring(key.Length + 1);
            int[] targets = (Arg("n") ?? "2,4").Split(',').Select(x => int.Parse(x, inv)).ToArray();
            int maxHolds = int.Parse(Arg("holds") ?? "4", inv);
            SkilledBot.Diet = args.Contains("diet");
            if (Arg("lab") != null) ApplyLaborCandidate(config, Arg("lab"));
            if (Arg("hold") != null) config.Auction.HoldMarketPenalty = double.Parse(Arg("hold"), inv);

            Header($"매각 시점 — n번째 정착지에서 정기 경매를 k번 보류한 뒤 매각(숙련 봇, 캠페인 시드 1~5{(SkilledBot.Diet ? ", 식품 시설" : "")})");
            var maps = LoadMaps();
            foreach (int n in targets)
            {
                Line($"  {n}번째 정착지");
                Line("    보류 | 매각 시각 | 순이익 평균(최소~최대) | 매각 직전 시간당 운영비 | 주민 | 파산");
                for (int k = 0; k <= maxHolds; k++)
                {
                    var profit = new List<double>();
                    var hours = new List<double>();
                    var upkeep = new List<double>();
                    var residents = new List<double>();
                    int bankrupt = 0;
                    foreach (int seed in new[] { 1, 2, 3, 4, 5 })
                    {
                        var c = new Campaign(config, maps, seed, proc);
                        c.StartNextSettlement();
                        for (int i = 1; i <= n; i++)
                        {
                            SkilledBot.ForceHolds = i == n ? k : -1;
                            double before = c.Cash;
                            Settlement s = c.Current;
                            bool ok = SkilledBot.PlayCampaignSettlement(c);
                            if (i == n)
                            {
                                if (!ok) { bankrupt++; break; }
                                profit.Add(c.Cash - before);
                                hours.Add(s.CurrentHour);
                                upkeep.Add(s.History.Count > 0 ? s.History[s.History.Count - 1].Upkeep : 0);
                                residents.Add(s.Residents);
                                break;
                            }
                            if (!ok) break; // 앞 정착지에서 실패한 캠페인은 이 n에서 뺀다
                            NextSettlementLikeSmartBot(c, config);
                        }
                    }
                    SkilledBot.ForceHolds = -1;
                    string P(List<double> x, string f) => x.Count == 0 ? "-" : x.Average().ToString(f, inv);
                    Line($"    {k,4} | {P(hours, "0"),7}h | {P(profit, "+0;-0"),6} ({(profit.Count == 0 ? "-" : profit.Min().ToString("+0;-0", inv))}~{(profit.Count == 0 ? "-" : profit.Max().ToString("+0;-0", inv))}) | {P(upkeep, "0.0"),5} | {P(residents, "0.0"),4} | {bankrupt}/{profit.Count + bankrupt}");
                }
            }
            return 0;
        }

        /// <summary>`-- mods campaign … smart`의 정착지 사이 행동(장비 구매·강화, 약점이 적은 행선지, 장착)과 같다.</summary>
        private static void NextSettlementLikeSmartBot(Campaign c, EconomyConfig config)
        {
            string[] buyOrder = { "wellPump", "miningGear", "windbreak", "reinforcedMaterials", "oxygenSupply", "irrigation", "extraCrew", "bigStorage", "vaccination", "insectNet" };
            string[] generalOrder = { "wellPump", "miningGear", "windbreak", "vaccination", "insectNet", "reinforcedMaterials", "extraCrew", "bigStorage", "oxygenSupply", "irrigation" };
            foreach (string id in buyOrder)
            {
                var e = config.Equipment.Single(x => x.Id == id);
                if (!c.Owns(id) && c.Cash - e.Price >= 2500) c.BuyEquipment(id);
            }
            foreach (string id in generalOrder)
                while (c.UpgradeCost(id) is double cost && c.Cash - cost >= 2500) c.UpgradeEquipment(id);
            var offers = c.NextOffers();
            RegionOffer pick = offers.OrderBy(o => o.Weaknesses.Count(w => !CounterOf.ContainsKey(w.Id) || !c.Owns(CounterOf[w.Id])))
                .ThenByDescending(o => o.Strengths.Count()).First();
            foreach (string id in c.Loadout.ToList()) c.SetEquipped(id, false);
            foreach (string id in pick.Weaknesses.Where(w => CounterOf.ContainsKey(w.Id)).Select(w => CounterOf[w.Id]).Concat(generalOrder).Distinct())
                if (c.Owns(id)) c.SetEquipped(id, true);
            c.StartNextSettlement(pick.Index);
        }
    }
}
