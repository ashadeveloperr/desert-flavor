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
        public void SerialLayout_VertexIntake()
        {
            // 원본 v0.8 직렬 배치. 꼭짓점 취수(W2 변경)라 농장은 우물과 꼭짓점을 공유해 거리 0,
            // 채석장·광산은 수로 끝 꼭짓점에서 받는다 → 원본(변 중간점, 광산 74.28%)보다 손실이 적다.
            WaterAllocation a = Solve(Canals(SerialA), new[] { Well(100) },
                new[] { Use("farm", 1, -1, 30), Use("quarry", 2, -1, 25), Use("mine", 2, 0, 50) });

            Assert.AreEqual(1.0, a.RateOf("farm"), Tol);
            Assert.AreEqual(1.0, a.RateOf("quarry"), Tol);
            Assert.AreEqual(0.7885, a.RateOf("mine"), 1e-4);
            Assert.AreEqual(0, a.Consumers["farm"].PriorityDistance);
            Assert.AreEqual(2, a.Consumers["quarry"].PriorityDistance);
            Assert.AreEqual(4, a.Consumers["mine"].PriorityDistance);
            Assert.AreEqual(WaterShortageReason.SourceSupply, a.Consumers["mine"].Shortage);
            Assert.AreEqual(100.0, a.TotalReleased, Tol);

            // WD1(이슈 #2): 농장의 거리 0 취수는 그 꼭짓점을 이어 준 첫 수로에 '직접 취수'로 따로 잡히고, 용량(흐름)에는 안 들어간다.
            Assert.AreEqual(30.0, a.DirectIntakeLoads[SerialA[0]], Tol);
            Assert.AreEqual(1, a.DirectIntakeLoads.Count);
            CollectionAssert.AreEqual(new[] { SerialA[0] }, a.Consumers["farm"].Intakes.Single().Edges, "물길 띠도 그 수로에");
            Assert.AreEqual(70.0, a.EdgeLoads[SerialA[0]], 1e-6, "첫 수로 흐름 = 방출 100 − 농장 직접 30(농장 몫까지 넣으면 용량 100 초과)");
        }

        [Test]
        public void AdjacentWellAndFarm_BorderCanal_ShowsDirectIntake()
        {
            // 이슈 #2 재현: 우물 (0,0)과 농장 (1,-1)이 붙어 있고 둘 사이 경계 변에만 수로. 물은 변을 지나지 않아 흐름 0이었다.
            EdgeId border = HexTopology.EdgesOf(HexCoord.Zero).Single(e => e.TileB == new HexCoord(1, -1) || e.TileA == new HexCoord(1, -1));
            WaterAllocation a = Solve(Canals(border), new[] { Well(100) }, new[] { Use("farm", 1, -1, 30) });

            Assert.AreEqual(1.0, a.RateOf("farm"), Tol);
            Assert.AreEqual(0, a.Consumers["farm"].PriorityDistance);
            Assert.IsFalse(a.EdgeLoads.ContainsKey(border), "변을 지나지 않으므로 용량 기준 흐름은 0 그대로");
            Assert.AreEqual(30.0, a.DirectIntakeLoads[border], Tol, "이어 준 수로에 직접 취수량");
            CollectionAssert.AreEqual(new[] { border }, a.Consumers["farm"].Intakes.Single().Edges);

            var sent = WaterRoutes.DeliveriesFrom(a, "well");
            Assert.AreEqual(1, sent.Count);
            Assert.AreEqual("farm", sent[0].ConsumerId);
            Assert.AreEqual(0, sent[0].Flow.Distance);

            WaterAllocation none = Solve(Canals(), new[] { Well(100) }, new[] { Use("farm", 1, -1, 30) });
            Assert.AreEqual(0.0, none.RateOf("farm"), Tol, "수로가 없으면 끊김(규칙 W2 그대로)");
        }

        [Test]
        public void DeliveriesFrom_ListsEveryRoute_NearestFirst()
        {
            WaterAllocation a = Solve(Canals(SerialA), new[] { Well(100) },
                new[] { Use("mine", 2, 0, 50), Use("quarry", 2, -1, 25), Use("farm", 1, -1, 30) });
            var sent = WaterRoutes.DeliveriesFrom(a, "well");
            CollectionAssert.AreEqual(new[] { "farm", "quarry", "mine" }, sent.Select(x => x.ConsumerId).ToArray());
            Assert.AreEqual(a.TotalDelivered, sent.Sum(x => x.Flow.Delivered), Tol);
        }

        [Test]
        public void CanalTouchingOnlyOneCornerOfTheTile_StillSupplies()
        {
            // 수로 하나(우물 북동 꼭짓점 → 바깥)의 끝점만 닿는 (2,-1) 타일.
            CanalGraph g = Canals(E(1, -1, HexDirection.SouthEast));
            WaterAllocation a = Solve(g, new[] { Well(100) }, new[] { Use("quarry", 2, -1, 25) });
            Assert.AreEqual(1.0, a.RateOf("quarry"), Tol, "변이 아니라 꼭짓점 하나만 닿아도 취수");
            Assert.AreEqual(2, a.Consumers["quarry"].PriorityDistance);
            Assert.AreEqual(VertexId.Of(new HexCoord(1, 0), HexCorner.North), a.Consumers["quarry"].Intakes[0].IntakeVertex);
        }

        [Test]
        public void FacilityTouchingTheWellTile_NeedsNoCanal()
        {
            // 우물과 꼭짓점을 공유하지만 수로가 하나도 없으면 연결 없음(우물 자체는 수로망 노드가 아니다).
            WaterAllocation none = Solve(Canals(), new[] { Well(100) }, new[] { Use("farm", 1, 0, 30) });
            Assert.AreEqual(WaterShortageReason.Unreachable, none.Consumers["farm"].Shortage);

            // 우물 꼭짓점에 수로가 하나라도 닿으면, 그 꼭짓점을 공유하는 이웃 시설은 손실 없이 받는다.
            WaterAllocation some = Solve(Canals(E(1, -1, HexDirection.SouthEast)), new[] { Well(100) }, new[] { Use("farm", 1, 0, 30) });
            Assert.AreEqual(1.0, some.RateOf("farm"), Tol);
            Assert.AreEqual(0, some.Consumers["farm"].PriorityDistance);
            Assert.AreEqual(30.0, some.TotalReleased, Tol, "거리 0 → 손실 없음");
        }

        [Test]
        public void WellReleasesFromAllTileCorners_BorderCanalsStartAtDistanceZero()
        {
            // 우물 동쪽 테두리 변 → 남동 꼭짓점에서 바깥으로 → 다음 변.
            CanalGraph g = Canals(E(0, 0, HexDirection.East), E(1, 0, HexDirection.SouthWest), E(0, 1, HexDirection.East));
            WaterAllocation a = Solve(g, new[] { Well(100) },
                new[] { Use("farm", 1, 0, 30), Use("quarry", 0, 1, 25), Use("mine", 1, 1, 50) });

            Assert.AreEqual(0, a.Consumers["farm"].PriorityDistance);
            Assert.AreEqual(0, a.Consumers["quarry"].PriorityDistance);
            Assert.AreEqual(2, a.Consumers["mine"].PriorityDistance, "두 번째 변도 우물 꼭짓점에서 시작");
            Assert.AreEqual(0.8550, a.RateOf("mine"), 1e-4);
        }

        // 우물 북동·남서 꼭짓점에서 바깥으로 한 구간씩. 끝점에만 닿는 (2,-1)과 (-2,1)은 우물에서 같은 거리(2).
        private CanalGraph TwoArms() => Canals(E(1, -1, HexDirection.SouthEast), E(-1, 1, HexDirection.NorthWest));

        [Test]
        public void SameDistance_SplitsInProportionToDemand()
        {
            WaterAllocation a = Solve(TwoArms(), new[] { Well(100) }, new[] { Use("mineA", 2, -1, 50), Use("mineB", -2, 1, 50) });
            Assert.AreEqual(2, a.Consumers["mineA"].PriorityDistance);
            Assert.AreEqual(2, a.Consumers["mineB"].PriorityDistance);
            Assert.AreEqual(a.RateOf("mineA"), a.RateOf("mineB"), Tol);
            Assert.AreEqual(0.95, a.RateOf("mineA"), 1e-9, "각 50 방출 → 변 하나 손실 5% 후 도착");
        }

        [Test]
        public void SameDistance_DifferentDemands_GetEqualFulfillmentRatio()
        {
            WaterAllocation a = Solve(TwoArms(), new[] { Well(60) }, new[] { Use("big", 2, -1, 60), Use("small", -2, 1, 30) });
            Assert.AreEqual(a.RateOf("big"), a.RateOf("small"), Tol);
            Assert.AreEqual(2.0, a.Consumers["big"].Delivered / a.Consumers["small"].Delivered, Tol);
        }

        [Test]
        public void CloserConsumerIsServedFirst()
        {
            CanalGraph g = Canals(E(1, -1, HexDirection.SouthEast), E(-1, 1, HexDirection.NorthWest), E(-1, 0, HexDirection.SouthWest));
            WaterAllocation a = Solve(g, new[] { Well(100) }, new[] { Use("near", 2, -1, 50), Use("far", -2, 0, 50) });
            Assert.AreEqual(2, a.Consumers["near"].PriorityDistance);
            Assert.AreEqual(4, a.Consumers["far"].PriorityDistance);
            Assert.AreEqual(1.0, a.RateOf("near"), Tol);
            double expectedFar = (100 - 50 / 0.95) * 0.95 * 0.95 / 50;
            Assert.AreEqual(expectedFar, a.RateOf("far"), 1e-9, "가까운 쪽이 다 받은 뒤 남은 물");
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
            // 수로 용량 60, 농장 수요 60: 같은 거리의 두 가지 중 어느 하나만으로는 농장을 다 채울 수 없다.
            var settings = new WaterNetworkSettings { CapacityOf = _ => 60 };
            var users = new[] { TwoBranchUsers[0], TwoBranchUsers[1], Use("farm", 2, 0, 60) };
            WaterAllocation a = Solve(Canals(BranchA.Concat(BranchB).ToArray()), new[] { Well(190) }, users, settings);

            Assert.AreEqual(1.0, a.RateOf("mine2"), Tol);
            Assert.AreEqual(1.0, a.RateOf("farm"), Tol, "한 가지가 용량에 막히면 나머지는 다른 가지로 받는다");
            Assert.AreEqual(2, a.Consumers["farm"].Intakes.Count, "두 경로에서 나눠 받음");
            double firstA = a.EdgeLoads[BranchA[0]], firstB = a.EdgeLoads[BranchB[0]];
            Assert.AreEqual(60.0, Math.Max(firstA, firstB), Tol, "먼저 쓴 가지의 첫 변은 용량까지 찬다");
        }

        [Test]
        public void BlockedCanal_ReroutesToOtherCanal()
        {
            CanalGraph g = Canals(BranchA.Concat(BranchB).ToArray());
            EdgeId gated = BranchA[1];
            Assert.IsTrue(g.TrySetBlocked(gated, true));
            WaterAllocation closed = Solve(g, new[] { Well(140) }, new[] { Use("farm", 2, 0, 30) });

            Assert.AreEqual(1.0, closed.RateOf("farm"), Tol, "가지 A가 막히면 가지 B로");
            Assert.IsFalse(closed.EdgeLoads.ContainsKey(gated), "막힌 수로 변에는 물이 흐르지 않는다");
            Assert.IsTrue(closed.EdgeLoads.ContainsKey(BranchB[1]));
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
            // m1은 우물과 꼭짓점을 공유(수로 불필요), m2·m3은 첫 변을 지나야 한다. 우물 L3(190)으로 우물이 아닌 수로가 병목이 되게.
            var users = new[] { Use("m1", 1, -1, 50), Use("m2", 2, -1, 50), Use("m3", 2, 0, 50) };

            WaterAllocation l1 = Solve(g, new[] { Well(190) }, users, settings);
            Assert.Less(l1.RateOf("m3"), 1.0, "L1 수로(100)가 병목");
            Assert.AreEqual(WaterShortageReason.CanalCapacity, l1.Consumers["m3"].Shortage);
            Assert.AreEqual(SerialA[0], l1.Consumers["m3"].BlockingEdge);
            Assert.AreEqual(100.0, l1.EdgeLoads[SerialA[0]], Tol);

            g.TrySetLevel(SerialA[0], 2);
            WaterAllocation l2 = Solve(g, new[] { Well(190) }, users, settings);
            Assert.AreEqual(1.0, l2.RateOf("m3"), Tol, "첫 변을 L2(150)로 올리면 모두 받는다");
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
