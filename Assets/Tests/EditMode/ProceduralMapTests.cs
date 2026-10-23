using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>절차 맵: 설정 JSON, 난이도 수치, 생성 규칙 준수, 결정성(시뮬레이터와 같은 맵), 캠페인 연결.</summary>
    public class ProceduralMapTests
    {
        private static string _configJson;
        private static ProceduralMapConfig _proc;
        private static EconomyConfig _economy;
        private static List<MapDefinition> _maps;

        [OneTimeSetUp]
        public void Load()
        {
            string data = Path.Combine(Application.dataPath, "Data");
            _configJson = File.ReadAllText(Path.Combine(data, "Config", "procedural_map_config.json"));
            _proc = ProceduralMapConfigLoader.Parse(_configJson);
            _economy = EconomyConfigLoader.Parse(File.ReadAllText(Path.Combine(data, "Config", "economy_balance_v0.8_config.json")));
            _maps = Directory.GetFiles(Path.Combine(data, "Maps"), "*.json").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => MapLoader.Parse(File.ReadAllText(p))).ToList();
        }

        // ───────────── 설정 ─────────────

        [Test]
        public void Config_JsonMatchesCodeDefaults()
        {
            // JSON과 코드 기본값이 어긋나면 어느 쪽을 고쳤는지 헷갈리므로 항상 같게 유지한다.
            AssertSameProperties(new DifficultyCurve(), _proc.Difficulty);
            AssertSameProperties(new GeneratorRules(), _proc.Generator);
            AssertSameProperties(new RegionModifierSettings(), _proc.Modifiers);
            Assert.AreEqual(new ProceduralMapConfig().RegionChoices, _proc.RegionChoices);
            Assert.AreEqual(1, _proc.FixedMapsBeforeGenerated);
        }

        private static void AssertSameProperties<T>(T expected, T actual)
        {
            foreach (var prop in typeof(T).GetProperties().Where(x => x.PropertyType.IsValueType || x.PropertyType == typeof(string)))
            {
                Assert.AreEqual(prop.GetValue(expected), prop.GetValue(actual), $"{typeof(T).Name}.{prop.Name}");
            }
        }

        [Test]
        public void Config_RejectsWrongSchemaAndBadValues()
        {
            Assert.Throws<ArgumentException>(() => ProceduralMapConfigLoader.Parse(_configJson.Replace("procedural-map-v1", "procedural-map-v0")));
            Assert.Throws<ArgumentException>(() => ProceduralMapConfigLoader.Parse(_configJson.Replace("\"mountainChance\": 0.5", "\"mountainChance\": 1.5")));
            Assert.Throws<ArgumentException>(() => ProceduralMapConfigLoader.Parse(_configJson.Replace("\"cashPerD\": 14000", "\"cashPerD\": 0")));
        }

        // ───────────── 난이도 수치 ─────────────

        [Test]
        public void Difficulty_FromCash_UsesConfiguredScale()
        {
            DifficultyCurve c = _proc.Difficulty;
            Assert.AreEqual(0.0, DifficultyParams.DifficultyFromCash(500, c), "자금이 기준보다 적으면 0");
            Assert.AreEqual(0.0, DifficultyParams.DifficultyFromCash(c.CashAtD0, c));
            Assert.AreEqual(1.0, DifficultyParams.DifficultyFromCash(c.CashAtD0 + c.CashPerD, c), 1e-12);
            Assert.AreEqual(3.0, DifficultyParams.DifficultyFromCash(c.CashAtD0 + 3 * c.CashPerD, c), 1e-12, "상한 없음");
        }

        [Test]
        public void Difficulty_Values_MatchTunedCurve()
        {
            // 2026-10-23 게임성 보강 R3(Docs/decision_log_auto.md F3): D 0·0.25·0.5·0.75·1 → 광맥 2·1·1·1·1, D<1은 반경 3.
            int[] ores = { 2, 1, 1, 1, 1 };
            for (int i = 0; i < ores.Length; i++)
            {
                DifficultyParams p = DifficultyParams.For(i * 0.25, _proc.Difficulty);
                Assert.AreEqual(ores[i], p.OreCount, $"D {i * 0.25} 광맥");
                Assert.AreEqual(i < 4 ? 3 : 4, p.Radius, $"D {i * 0.25} 반경");
            }
            Assert.AreEqual(2, DifficultyParams.For(0, _proc.Difficulty).WellCount, "R3: D0 우물 3 → 2");
            Assert.AreEqual(2, DifficultyParams.For(1, _proc.Difficulty).WellCount, "반경 4(61칸)에서 37칸당 1 → 2개");
        }

        [Test]
        public void Difficulty_BeyondOne_KeepsGettingHarderWithinLimits()
        {
            DifficultyParams prev = DifficultyParams.For(0, _proc.Difficulty);
            for (double d = 0.25; d <= 10; d += 0.25)
            {
                DifficultyParams p = DifficultyParams.For(d, _proc.Difficulty);
                Assert.GreaterOrEqual(p.ObstacleDensity, prev.ObstacleDensity, $"D {d} 장애물은 줄지 않음");
                Assert.GreaterOrEqual(p.Radius, prev.Radius, $"D {d} 반경은 줄지 않음");
                Assert.LessOrEqual(p.Radius, HexMap.MaxRadius);
                Assert.LessOrEqual(p.ObstacleDensity, _proc.Difficulty.MaxObstacleDensity + 1e-12);
                Assert.GreaterOrEqual(p.WellCount, 1);
                Assert.GreaterOrEqual(p.OreCount, 1);
                Assert.LessOrEqual(p.TargetWellOreDistance, p.Radius);
                prev = p;
            }
        }

        // ───────────── 생성 ─────────────

        [Test]
        public void Generate_SameSeedGivesSameMap()
        {
            DifficultyParams p = DifficultyParams.For(0.6, _proc.Difficulty);
            GeneratedMap a = ProceduralMapGenerator.Generate(p, _proc.Generator, 123);
            GeneratedMap b = ProceduralMapGenerator.Generate(p, _proc.Generator, 123);
            GeneratedMap c = ProceduralMapGenerator.Generate(p, _proc.Generator, 124);
            Assert.AreEqual(MapLoader.ToJson(a.Map), MapLoader.ToJson(b.Map));
            Assert.AreNotEqual(MapLoader.ToJson(a.Map), MapLoader.ToJson(c.Map), "시드가 다르면 다른 맵");
        }

        [Test]
        public void Generate_MatchesSimulatorGoldenMap()
        {
            // Tools/SettlementSim 리포트의 '기준 맵 서명(D 0.50, 시드 42)'과 같아야 한다(Unity와 .NET에서 같은 맵).
            // 생성 규칙·난수·설정을 바꾸면 시뮬레이터를 다시 돌려 이 값을 갱신한다.
            const string golden = "r3 1,-3Wa 3,-1Be 0,0Or 2,0Ob 3,0Ob -2,1Be -1,1Ob 2,1Wa -1,2Ob -1,3Ob"; // 2026-10-23 R3로 갱신
            GeneratedMap g = ProceduralMapGenerator.Generate(DifficultyParams.For(0.5, _proc.Difficulty), _proc.Generator, 42);
            string signature = $"r{g.Map.Radius} " + string.Join(" ", g.Map.Terrain.Select(t => $"{t.Q},{t.R}{t.Type.Substring(0, 2)}"));
            Assert.AreEqual(golden, signature);
        }

        [Test]
        public void Generate_FollowsTerrainRules([Values(0.0, 0.5, 1.0, 2.0, 4.0)] double d)
        {
            DifficultyParams p = DifficultyParams.For(d, _proc.Difficulty);
            for (int seed = 1; seed <= 8; seed++)
            {
                GeneratedMap g = ProceduralMapGenerator.Generate(p, _proc.Generator, seed);
                Assert.IsNotNull(g, $"D {d} 시드 {seed} 생성 실패");
                HexMap map = MapLoader.Parse(MapLoader.ToJson(g.Map)).CreateMap();
                List<HexCoord> Of(TerrainType t) => map.Tiles.Where(x => x.Terrain == t).Select(x => x.Coord).ToList();
                List<HexCoord> wells = Of(TerrainType.WaterSource), ores = Of(TerrainType.OreVein);

                Assert.AreEqual(p.Radius, map.Radius);
                Assert.AreEqual(p.WellCount, wells.Count, "우물 수");
                Assert.AreEqual(p.OreCount, ores.Count, "광맥 수");
                Assert.IsNotEmpty(Of(TerrainType.Bedrock), "암반 최소 1");
                foreach (HexCoord w in wells)
                    Assert.IsTrue(wells.All(o => o == w || o.DistanceTo(w) > _proc.Generator.WellExclusionRadius), "우물 반경 2 안에 다른 우물 없음");
                foreach (HexCoord o in ores)
                    Assert.IsTrue(ores.All(x => x == o || x.DistanceTo(o) >= _proc.Generator.MinOreSpacing), "광맥 간격");

                // 장애물 지형끼리 붙지 않음, 트인 땅(장애물이 아닌 칸)은 하나로 이어짐.
                var owner = new Dictionary<HexCoord, int>();
                for (int i = 0; i < g.Formations.Count; i++) foreach (HexCoord c in g.Formations[i].tiles) owner[c] = i;
                foreach (KeyValuePair<HexCoord, int> kv in owner)
                    Assert.IsFalse(ProceduralMapGenerator.Neighbors(kv.Key).Any(n => owner.TryGetValue(n, out int o) && o != kv.Value), "지형끼리 붙음");
                List<HexCoord> open = map.Tiles.Where(t => t.Terrain != TerrainType.Obstacle).Select(t => t.Coord).ToList();
                var seen = new HashSet<HexCoord> { open[0] };
                var queue = new Queue<HexCoord>(seen);
                while (queue.Count > 0)
                    foreach (HexCoord n in ProceduralMapGenerator.Neighbors(queue.Dequeue()))
                        if (map.Contains(n) && map.GetTile(n).Terrain != TerrainType.Obstacle && seen.Add(n)) queue.Enqueue(n);
                Assert.AreEqual(open.Count, seen.Count, "트인 땅이 끊김");

                Assert.GreaterOrEqual(g.ShortestWellOreCanalPath, 0, "우물에서 광맥까지 수로 경로가 있어야 함");
            }
        }

        // ───────────── 캠페인 ─────────────

        [Test]
        public void Campaign_FirstSettlementFixed_ThenGeneratedFromCash()
        {
            var campaign = new Campaign(_economy, _maps, seed: 7, procedural: _proc);
            Settlement first = campaign.StartNextSettlement();
            Assert.AreEqual(_maps[0].Id, campaign.CurrentMap.Id, "첫 정착지는 고정 맵 1");
            Assert.IsNull(campaign.CurrentGenerated);
            Assert.IsNull(campaign.PreviewNextDifficulty(), "정착지 1 운영 중: 지금 정착지는 고정 맵");

            SellAtFirstAuction(campaign, first);
            DifficultyParams preview = campaign.PreviewNextDifficulty();
            Assert.IsNotNull(preview, "상점: 다음 정착지는 생성 맵");
            Assert.AreEqual(DifficultyParams.DifficultyFromCash(campaign.Cash, _proc.Difficulty), preview.D, 1e-12);

            Settlement second = campaign.StartNextSettlement();
            Assert.IsNotNull(campaign.CurrentGenerated);
            StringAssert.StartsWith("gen_", campaign.CurrentMap.Id);
            Assert.AreEqual(preview.D, campaign.CurrentGenerated.Params.D, 1e-12, "들어가는 순간의 보유 자금으로 난이도");
            Assert.AreEqual(preview.Radius, second.Map.Radius);

            // 같은 캠페인 시드면 같은 생성 맵.
            var again = new Campaign(_economy, _maps, seed: 7, procedural: _proc);
            SellAtFirstAuction(again, again.StartNextSettlement());
            again.StartNextSettlement();
            Assert.AreEqual(campaign.CurrentMap.Id, again.CurrentMap.Id);
            Assert.AreEqual(MapLoader.ToJson(campaign.CurrentMap), MapLoader.ToJson(again.CurrentMap));
        }

        [Test]
        public void Campaign_WithoutProceduralConfig_CyclesFixedMaps()
        {
            var campaign = new Campaign(_economy, _maps, seed: 7);
            SellAtFirstAuction(campaign, campaign.StartNextSettlement());
            Assert.IsNull(campaign.PreviewNextDifficulty());
            campaign.StartNextSettlement();
            Assert.AreEqual(_maps[1].Id, campaign.CurrentMap.Id);
            Assert.IsNull(campaign.CurrentGenerated);
        }

        /// <summary>맵 샘플 배치로 첫 경매까지 돌리고 첫 입찰자에게 판다.</summary>
        private static void SellAtFirstAuction(Campaign campaign, Settlement s)
        {
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(s));
            for (int h = 0; h < 600 && !campaign.Auctions.IsOpen; h++) s.AdvanceHour();
            Assert.IsTrue(campaign.Auctions.IsOpen, "첫 경매가 열려야 함");
            campaign.Auctions.Sell(0);
            Assert.AreEqual(CampaignState.Shopping, campaign.State);
        }
    }
}
