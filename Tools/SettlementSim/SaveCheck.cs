using System;
using System.Linq;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 저장·불러오기(로드맵 2-①) 확인: dotnet run --project Tools/SettlementSim -- savecheck [정착지 수]
    // 다중 봇이 캠페인을 진행(장비 구매·강화·장착, 지역 선택, 중간 정착지에서 30시간 더 진행)한 뒤 저장 → 같은 시드로 명령 재실행 →
    // 자금·정착지 번호·시간·시설·장비가 같은지 비교한다. 다르면 종료 코드 1.
    public static partial class Program
    {
        private static int SoldInCheck;
        private static int SaveCheck(string[] args)
        {
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            int settlements = args.Length > 0 ? int.Parse(args[0]) : 8;
            Header($"저장·불러오기 확인 — 캠페인 정착지 {settlements}개 진행 뒤 저장 → 재실행 비교");
            int failures = 0;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var c = new Campaign(config, LoadMaps(), seed, proc);
                c.StartNextSettlement();
                for (int n = 1; n < settlements; n++)
                {
                    if (!ReferenceBot.PlayCampaignSettlement(c)) break;
                    foreach (string id in new[] { "wellPump", "miningGear", "windbreak" })
                        if (!c.Owns(id) && c.Cash - config.Equipment.Single(e => e.Id == id).Price >= 2500) c.BuyEquipment(id);
                    if (c.UpgradeCost("wellPump") is double cost && c.Cash - cost >= 2500) c.UpgradeEquipment("wellPump");
                    c.StartNextSettlement(n % 2);
                }
                if (c.State == CampaignState.InSettlement)
                {
                    var plan = ReferenceBot.Plan(c.CurrentMap, BotStrategy.Multi);
                    if (plan != null) new MapDefinition { Id = c.CurrentMap.Id, Radius = c.CurrentMap.Radius, Terrain = c.CurrentMap.Terrain, SampleLayout = plan }.RequestSampleLayout(c.Current);
                    c.Current.BuyStone(3);
                    for (int h = 0; h < 30 && !c.Auctions.IsOpen && !c.Auctions.IsFinished; h++) c.Current.AdvanceHour();
                    // 시장 판매도 다시 실행되는지(2026-10-27 이슈 #18).
                    if (c.Current.SellFood(5) + c.Current.SellStone(2) > 0) SoldInCheck++;
                    for (int h = 0; h < 5 && !c.Auctions.IsOpen && !c.Auctions.IsFinished; h++) c.Current.AdvanceHour();
                }

                SaveData save = SaveGame.Capture(c, "check");
                SaveGame.ReplayResult r = SaveGame.Replay(config, LoadMaps(), proc, save);
                Campaign b = r.Campaign;
                string Sig(Campaign x) =>
                    $"정착지 {x.SettlementNumber} {x.State} 시간 {x.Current?.CurrentHour} 자금 {(x.State == CampaignState.InSettlement ? x.Current.Cash : x.Cash):0.000000} " +
                    $"시설 {x.Current?.Facilities.Count} 석재 {x.Current?.Stone:0.0000} 장비 {string.Join(",", x.OwnedEquipment.OrderBy(e => e).Select(e => e + x.EquipmentLevel(e)))} 명령 {x.CommandLog.Count}";
                string a = Sig(c), bb = Sig(b);
                bool ok = a == bb && r.Problems.Count == 0;
                if (!ok) failures++;
                Line($"  시드 {seed}: {(ok ? "같음" : "다름")} — {a}");
                if (!ok) Line($"          복원 — {bb}  문제 {string.Join("; ", r.Problems.Take(3))}");
            }
            Line($"  시장 판매가 들어간 확인: {SoldInCheck}개 시드");
            Line(failures == 0 ? "  저장·불러오기 확인 통과" : $"  실패 {failures}");
            return failures == 0 ? 0 : 1;
        }
    }
}
