using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>
    /// 기대값은 Tools/SettlementSim 시뮬레이션과 원본 v0.8 Python 시뮬레이터 회귀값에서 왔다.
    /// 우물은 모두 (0,0)이 기본.
    /// </summary>
    public class WaterNetworkSolverTests
    {
        private const double Tol = 1e-6;
        private HexTopology _topology;

        [SetUp]
        public void SetUp()
        {
            _topology = new HexTopology(new HexMap(3));
        }

        private static EdgeId E(int q, int r, HexDirection d) => EdgeId.Of(new HexCoord(q, r), d);
        private static WaterConsumer Use(string id, int q, int r, double demand) => new WaterConsumer(id, new HexCoord(q, r), demand);
        private static WaterSource Well(double supply, int q = 0, int r = 0, string id = "well") => new WaterSource(id, new HexCoord(q, r), supply);

        private CanalGraph Canals(params EdgeId[] edges)
        {
            var g = new CanalGraph(_topology);
            foreach (EdgeId e in edges) Assert.IsTrue(g.TryInstall(e), "수로 설치 실패: " + e);
            return g;
        }

        private static WaterAllocation Solve(CanalGraph g, IEnumerable<WaterSource> wells, IEnumerable<WaterConsumer> users,
            WaterNetworkSettings settings = null) =>
            WaterNetworkSolver.Solve(g, wells.ToList(), users.ToList(), settings ?? new WaterNetworkSettings());

        // 원본 v0.8 직렬 배치: 우물 북동 꼭짓점에서 바깥으로 뻗는 수로 3개.
        private static readonly EdgeId[] SerialA =
        {
            EdgeId.Of(new HexCoord(1, -1), HexDirection.SouthEast),
            EdgeId.Of(new HexCoord(1, 0), HexDirection.NorthEast),
            EdgeId.Of(new HexCoord(2, -1), HexDirection.SouthEast),
        };

        [Test]
        public void SerialLayout_ReproducesV08Regression()
        {
            WaterAllocation a = Solve(Canals(SerialA), new[] { Well(100) },
                new[] { Use("farm", 1, -1, 30), Use("quarry", 2, -1, 25), Use("mine", 2, 0, 50) });

            Assert.AreEqual(1.0, a.RateOf("farm"), Tol);
            Assert.AreEqual(1.0, a.RateOf("quarry"), Tol);
            Assert.AreEqual(0.7427964, a.RateOf("mine"), 1e-6, "v0.8 광산 가동률 74.28%");
            Assert.AreEqual(1, a.Consumers["farm"].PriorityDistance);
            Assert.AreEqual(3, a.Consumers["quarry"].PriorityDistance);
            Assert.AreEqual(5, a.Consumers["mine"].PriorityDistance);
            Assert.AreEqual(WaterShortageReason.SourceSupply, a.Consumers["mine"].Shortage);
            Assert.AreEqual(100.0, a.TotalReleased, Tol);
        }

        [Test]
        public void WellReleasesFromAllTileCorners_BorderCanalsStartAtDistanceZero()
        {
            // 우물 동쪽 테두리 변 → 남동 꼭짓점에서 바깥으로 → 다음 변.
            CanalGraph g = Canals(E(0, 0, HexDirection.East), E(1, 0, HexDirection.SouthWest), E(0, 1, HexDirection.East));
            WaterAllocation a = Solve(g, new[] { Well(100) },
                new[] { Use("farm", 1, 0, 30), Use("quarry", 0, 1, 25), Use("mine", 1, 1, 50) });

            Assert.AreEqual(1, a.Consumers["farm"].PriorityDistance);
            Assert.AreEqual(1, a.Consumers["quarry"].PriorityDistance, "두 번째 변도 우물 꼭짓점에서 시작");
            Assert.AreEqual(3, a.Consumers["mine"].PriorityDistance);
            Assert.AreEqual(0.8069, a.RateOf("mine"), 1e-4);
        }

        [Test]
        public void SameDistance_SplitsInProportionToDemand()
        {
            CanalGraph g = Canals(E(1, -1, HexDirection.SouthEast), E(-1, 1, HexDirection.NorthWest));
            WaterAllocation a = Solve(g, new[] { Well(100) }, new[] { Use("mineA", 1, -1, 50), Use("mineB", -1, 1, 50) });
            Assert.AreEqual(a.RateOf("mineA"), a.RateOf("mineB"), Tol);
            Assert.AreEqual(Math.Sqrt(0.95), a.RateOf("mineA"), 1e-9, "각 50 방출 → 반쪽 변 손실 후 도착");
        }

        [Test]
        public void SameDistance_DifferentDemands_GetEqualFulfillmentRatio()
        {
            CanalGraph g = Canals(E(1, -1, HexDirection.SouthEast), E(-1, 1, HexDirection.NorthWest));
            WaterAllocation a = Solve(g, new[] { Well(60) }, new[] { Use("big", 1, -1, 60), Use("small", -1, 1, 30) });
            Assert.AreEqual(a.RateOf("big"), a.RateOf("small"), Tol);
            Assert.AreEqual(2.0, a.Consumers["big"].Delivered / a.Consumers["small"].Delivered, Tol);
        }

        [Test]
        public void CloserConsumerIsServedFirst()
        {
            CanalGraph g = Canals(E(1, -1, HexDirection.SouthEast), E(-1, 1, HexDirection.NorthWest), E(-1, 0, HexDirection.SouthWest));
            WaterAllocation a = Solve(g, new[] { Well(100) }, new[] { Use("near", 1, -1, 50), Use("far", -2, 1, 50) });
            Assert.AreEqual(1.0, a.RateOf("near"), Tol);
            Assert.AreEqual(0.9019, a.RateOf("far"), 1e-4);
        }

        // 가지 A(광산 2개를 거쳐 농장)와 가지 B(같은 길이로 농장)에 모두 접한 농장.
        private static readonly EdgeId[] BranchA = SerialA;
        private static readonly EdgeId[] BranchB =
        {
            EdgeId.Of(new HexCoord(1, 0), HexDirection.SouthWest),
            EdgeId.Of(new HexCoord(1, 0), HexDirection.SouthEast),
            EdgeId.Of(new HexCoord(1, 1), HexDirection.NorthEast),
        };
        private static readonly WaterConsumer[] TwoBranchUsers =
        {
            new WaterConsumer("mine1", new HexCoord(1, -1), 50),
            new WaterConsumer("mine2", new HexCoord(2, -1), 50),
            new WaterConsumer("farm", new HexCoord(2, 0), 30),
        };

        [Test]
        public void ConsumerBlockedOnOneCanal_TakesRestFromAnotherCanal()
        {
            WaterAllocation a = Solve(Canals(BranchA.Concat(BranchB).ToArray()), new[] { Well(140) }, TwoBranchUsers);

            Assert.AreEqual(1.0, a.RateOf("farm"), Tol, "가지 A가 막혀도 가지 B로 받는다");
            Assert.AreEqual(0.9019, a.RateOf("mine2"), 1e-4);
            Assert.AreEqual(WaterShortageReason.CanalCapacity, a.Consumers["mine2"].Shortage);
            Assert.AreEqual(BranchA[0], a.Consumers["mine2"].BlockingEdge);
            Assert.AreEqual(100.0, a.EdgeLoads[BranchA[0]], Tol, "첫 변은 용량까지 찬다");
            Assert.IsTrue(a.Consumers["farm"].Intakes.All(i => i.IntakeEdge == BranchB[2]));
        }

        [Test]
        public void ClosedGate_ReroutesToOtherCanal()
        {
            CanalGraph g = Canals(BranchA.Concat(BranchB).ToArray());
            WaterAllocation open = Solve(g, new[] { Well(140) }, new[] { Use("farm", 2, 0, 30) });
            EdgeId first = open.Consumers["farm"].Intakes[0].IntakeEdge;

            Assert.IsTrue(g.TrySetGate(first, true));
            Assert.IsTrue(g.TrySetGateOpen(first, false));
            WaterAllocation closed = Solve(g, new[] { Well(140) }, new[] { Use("farm", 2, 0, 30) });

            Assert.AreEqual(1.0, closed.RateOf("farm"), Tol);
            Assert.AreNotEqual(first, closed.Consumers["farm"].Intakes[0].IntakeEdge);
            Assert.IsFalse(closed.EdgeLoads.ContainsKey(first), "닫힌 수문 변에는 물이 흐르지 않는다");
        }

        [Test]
        public void CyclesAreAllowed_WaterFollowsShortestPaths()
        {
            var ring = HexTopology.EdgesOf(new HexCoord(2, -1)).ToList();
            ring.Add(E(1, -1, HexDirection.SouthEast));
            CanalGraph g = Canals(ring.ToArray());
            Assert.IsTrue(g.ContainsCycle());

            WaterAllocation a = Solve(g, new[] { Well(100) }, new[] { Use("farm", 2, -1, 30), Use("mine", 3, -1, 50) });
            Assert.AreEqual(1.0, a.RateOf("farm"), Tol);
            Assert.AreEqual(1.0, a.RateOf("mine"), Tol);
            Assert.Less(a.EdgeLoads.Count, g.SegmentCount, "고리의 일부 변에는 물이 흐르지 않는다");
        }

        [Test]
        public void ConnectedWells_BothSupplyTheNetwork()
        {
            CanalGraph g = Canals(
                E(0, 0, HexDirection.NorthWest), E(0, 0, HexDirection.NorthEast), E(1, -1, HexDirection.SouthEast),
                E(-1, 0, HexDirection.NorthEast), E(-1, 0, HexDirection.NorthWest));
            var wells = new[] { Well(100, 0, 0, "well1"), Well(100, -2, 0, "well2") };
            WaterAllocation a = Solve(g, wells, new[] { Use("mine1", 0, -1, 50), Use("mine2", 1, -1, 50), Use("farm", 1, 0, 30) });

            Assert.AreEqual(1.0, a.RateOf("mine1"), Tol);
            Assert.AreEqual(1.0, a.RateOf("mine2"), Tol);
            Assert.AreEqual(1.0, a.RateOf("farm"), Tol);
            Assert.AreEqual(100.0, a.SourceReleased["well1"], Tol, "가까운 우물을 먼저 다 쓴다");
            Assert.Greater(a.SourceReleased["well2"], 30.0);
        }

        [Test]
        public void CapacityComesFromCanalLevel()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json"));
            WaterNetworkSettings settings = WaterNetworkSettings.FromConfig(EconomyConfigLoader.Parse(json));
            CanalGraph g = Canals(SerialA);
            var users = new[] { Use("m1", 1, -1, 50), Use("m2", 2, -1, 50), Use("m3", 2, 0, 50) };

            WaterAllocation l1 = Solve(g, new[] { Well(140) }, users, settings);
            Assert.AreEqual(0.0, l1.RateOf("m3"), Tol, "L1 수로(100) 하나로는 우물 L2(140)를 다 못 쓴다");
            Assert.AreEqual(40.0, l1.TotalSupply - l1.TotalReleased, Tol);

            g.TrySetLevel(SerialA[0], 2);
            WaterAllocation l2 = Solve(g, new[] { Well(140) }, users, settings);
            Assert.AreEqual(0.6105, l2.RateOf("m3"), 1e-4, "첫 변을 L2(150)로 올리면 우물을 다 쓴다");
            Assert.AreEqual(140.0, l2.TotalReleased, Tol);
        }

        [Test]
        public void NoAdjacentCanal_IsUnreachable_ZeroDemandIsFull()
        {
            WaterAllocation a = Solve(Canals(SerialA), new[] { Well(100) },
                new[] { Use("far", -3, 0, 30), Use("idle", 1, -1, 0) });
            Assert.AreEqual(0.0, a.RateOf("far"));
            Assert.AreEqual(WaterShortageReason.Unreachable, a.Consumers["far"].Shortage);
            Assert.AreEqual(-1, a.Consumers["far"].PriorityDistance);
            Assert.AreEqual(1.0, a.RateOf("idle"));
        }

        [Test]
        public void DuplicateIds_AreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                Solve(Canals(SerialA), new[] { Well(100) }, new[] { Use("x", 1, -1, 10), Use("x", 2, -1, 10) }));
        }

        [Test]
        public void RandomTrees_ConserveWater_RespectCapacity_AndAreDeterministic()
        {
            var rng = new System.Random(20261022);
            List<HexCoord> tiles = _topology.Map.Tiles.Select(t => t.Coord).ToList();
            for (int k = 0; k < 300; k++)
            {
                HexCoord wellTile = tiles[rng.Next(tiles.Count)];
                List<EdgeId> edges = RandomTree(rng, wellTile, rng.Next(4, 18));
                List<HexCoord> touching = edges.SelectMany(e => _topology.TilesOf(e)).Distinct().Where(t => t != wellTile).ToList();
                if (touching.Count == 0) continue;
                List<WaterConsumer> users = touching.OrderBy(_ => rng.Next()).Take(rng.Next(1, Math.Min(7, touching.Count + 1)))
                    .Select((t, i) => new WaterConsumer("c" + i, t, new[] { 25, 30, 50 }[rng.Next(3)])).ToList();
                var wells = new[] { new WaterSource("w", wellTile, new[] { 100, 140 }[rng.Next(2)]) };

                WaterAllocation a = Solve(Canals(edges.ToArray()), wells, users);
                Assert.LessOrEqual(a.TotalReleased, a.TotalSupply + Tol, $"#{k} 방출 ≤ 생산");
                Assert.LessOrEqual(a.TotalDelivered, a.TotalReleased + Tol, $"#{k} 전달 ≤ 방출");
                Assert.IsTrue(a.EdgeLoads.Values.All(v => v <= 100 + Tol), $"#{k} 용량");
                foreach (WaterConsumer u in users)
                {
                    WaterConsumerResult r = a.Consumers[u.Id];
                    Assert.LessOrEqual(r.Delivered, u.DemandPerHour + Tol);
                    Assert.AreEqual(r.Delivered, r.Intakes.Sum(i => i.Delivered), Tol, $"#{k} 경로별 합");
                    if (r.Rate < 1 - 1e-9) Assert.AreNotEqual(WaterShortageReason.None, r.Shortage, $"#{k} 부족 이유");
                }

                WaterAllocation shuffled = Solve(Canals(edges.OrderBy(_ => rng.Next()).ToArray()), wells, users.OrderBy(_ => rng.Next()));
                foreach (WaterConsumer u in users)
                {
                    Assert.AreEqual(a.Consumers[u.Id].Delivered, shuffled.Consumers[u.Id].Delivered, 1e-9, $"#{k} 결정성");
                }
            }
        }

        // 우물 꼭짓점에서 시작해 순환 없이 자라는 무작위 수로 트리.
        private List<EdgeId> RandomTree(System.Random rng, HexCoord well, int size)
        {
            var visited = new HashSet<VertexId>(HexTopology.VerticesOf(well).Where(_topology.Contains));
            var frontier = visited.ToList();
            var edges = new List<EdgeId>();
            for (int i = 0; i < size * 4 && edges.Count < size; i++)
            {
                VertexId from = frontier[rng.Next(frontier.Count)];
                List<EdgeId> options = _topology.EdgesOf(from).Where(e => !edges.Contains(e)).ToList();
                if (options.Count == 0) continue;
                EdgeId pick = options[rng.Next(options.Count)];
                VertexId other = pick.VertexA == from ? pick.VertexB : pick.VertexA;
                if (visited.Contains(other)) continue;
                edges.Add(pick);
                visited.Add(other);
                frontier.Add(other);
            }
            return edges;
        }
    }
}
