using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Hex;
using NUnit.Framework;

namespace DesertSettlement.Core.Tests
{
    public class CanalGraphTests
    {
        private CanalGraph _graph;
        private readonly HexCoord _center = HexCoord.Zero;

        [SetUp]
        public void SetUp()
        {
            _graph = new CanalGraph(new HexTopology(new HexMap(3)));
        }

        private EdgeId Edge(HexDirection side) => EdgeId.Of(_center, side);
        private VertexId Corner(HexCorner corner) => VertexId.Of(_center, corner);

        /// <summary>중앙 타일의 North 꼭짓점에서 시계 방향으로 NE → E → SE 변을 잇는 수로.</summary>
        private void InstallClockwiseHalf()
        {
            Assert.IsTrue(_graph.TryInstall(Edge(HexDirection.NorthEast)));
            Assert.IsTrue(_graph.TryInstall(Edge(HexDirection.East)));
            Assert.IsTrue(_graph.TryInstall(Edge(HexDirection.SouthEast)));
        }

        [Test]
        public void Install_SharedEdgeFromEitherTile_IsTheSameCanal()
        {
            Assert.IsTrue(_graph.TryInstall(Edge(HexDirection.East)));
            EdgeId sameFromNeighbor = EdgeId.Of(_center.Neighbor(HexDirection.East), HexDirection.West);
            Assert.IsFalse(_graph.TryInstall(sameFromNeighbor), "같은 변에 수로를 두 번 만들 수 없다");
            Assert.AreEqual(1, _graph.SegmentCount);
        }

        [Test]
        public void Install_BoundaryEdgeIsAllowed_OutsideMapIsRejected()
        {
            Assert.IsTrue(_graph.TryInstall(EdgeId.Of(new HexCoord(3, 0), HexDirection.East)), "경계 변 설치 가능");
            Assert.IsFalse(_graph.TryInstall(EdgeId.Of(new HexCoord(5, 0), HexDirection.East)), "맵 밖 변은 불가");
        }

        [Test]
        public void PathLengths_FollowCanalsAlongEdges()
        {
            InstallClockwiseHalf();
            Dictionary<VertexId, int> d = _graph.ComputePathLengths(new[] { Corner(HexCorner.North) });

            Assert.AreEqual(0, d[Corner(HexCorner.North)]);
            Assert.AreEqual(1, d[Corner(HexCorner.NorthEast)]);
            Assert.AreEqual(2, d[Corner(HexCorner.SouthEast)]);
            Assert.AreEqual(3, d[Corner(HexCorner.South)]);
            Assert.IsFalse(d.ContainsKey(Corner(HexCorner.SouthWest)), "수로가 없는 꼭짓점은 도달 불가");
        }

        [Test]
        public void PathLengths_ClosedGateBlocksFlow_OpenGatePasses()
        {
            InstallClockwiseHalf();
            Assert.IsTrue(_graph.TrySetGate(Edge(HexDirection.East), true));
            Assert.IsTrue(_graph.TrySetGateOpen(Edge(HexDirection.East), false));

            Dictionary<VertexId, int> closed = _graph.ComputePathLengths(new[] { Corner(HexCorner.North) });
            Assert.IsTrue(closed.ContainsKey(Corner(HexCorner.NorthEast)));
            Assert.IsFalse(closed.ContainsKey(Corner(HexCorner.SouthEast)), "닫힌 수문 너머는 도달 불가");

            _graph.TrySetGateOpen(Edge(HexDirection.East), true);
            Dictionary<VertexId, int> open = _graph.ComputePathLengths(new[] { Corner(HexCorner.North) });
            Assert.AreEqual(2, open[Corner(HexCorner.SouthEast)]);
        }

        [Test]
        public void Gate_CannotOpenOrCloseWithoutGate()
        {
            InstallClockwiseHalf();
            Assert.IsFalse(_graph.TrySetGateOpen(Edge(HexDirection.East), false));
            Assert.IsFalse(_graph.TrySetGate(Edge(HexDirection.West), true), "수로 없는 변에는 수문을 달 수 없다");
        }

        [Test]
        public void PathLengths_TakeShortestRouteAroundRing()
        {
            foreach (HexDirection side in System.Enum.GetValues(typeof(HexDirection)))
            {
                _graph.TryInstall(Edge(side));
            }
            Dictionary<VertexId, int> d = _graph.ComputePathLengths(new[] { Corner(HexCorner.North) });
            Assert.AreEqual(3, d[Corner(HexCorner.South)]);
            Assert.AreEqual(1, d[Corner(HexCorner.NorthWest)], "반대 방향이 더 짧으면 그쪽으로");
        }

        [Test]
        public void Cycle_DetectedOnlyWhenRingCloses()
        {
            var sides = new[]
            {
                HexDirection.NorthEast, HexDirection.East, HexDirection.SouthEast,
                HexDirection.SouthWest, HexDirection.West, HexDirection.NorthWest,
            };
            for (int i = 0; i < sides.Length - 1; i++)
            {
                _graph.TryInstall(Edge(sides[i]));
                Assert.IsFalse(_graph.ContainsCycle(), $"{i + 1}개 설치 시 고리 없음");
            }
            _graph.TryInstall(Edge(sides[sides.Length - 1]));
            Assert.IsTrue(_graph.ContainsCycle());
        }

        [Test]
        public void ConnectedComponents_SplitWhenMiddleRemoved()
        {
            InstallClockwiseHalf();
            Assert.AreEqual(1, _graph.ConnectedComponents().Count);

            _graph.Remove(Edge(HexDirection.East));
            List<List<EdgeId>> parts = _graph.ConnectedComponents();
            Assert.AreEqual(2, parts.Count);
            Assert.IsTrue(parts.All(p => p.Count == 1));
        }

        [Test]
        public void SegmentsAdjacentTo_FindsCanalsOnBothSidesOfSharedEdge()
        {
            _graph.TryInstall(Edge(HexDirection.East));
            HexCoord east = _center.Neighbor(HexDirection.East);
            Assert.AreEqual(1, _graph.SegmentsAdjacentTo(_center).Count());
            Assert.AreEqual(1, _graph.SegmentsAdjacentTo(east).Count(), "변 반대편 타일도 같은 수로에 접한다");
        }

        [Test]
        public void Changed_FiresOnStructuralChanges()
        {
            int count = 0;
            _graph.Changed += () => count++;
            _graph.TryInstall(Edge(HexDirection.East));
            _graph.TrySetGate(Edge(HexDirection.East), true);
            _graph.TrySetGateOpen(Edge(HexDirection.East), false);
            _graph.TrySetLevel(Edge(HexDirection.East), 2);
            _graph.Remove(Edge(HexDirection.East));
            _graph.TryInstall(EdgeId.Of(new HexCoord(9, 9), HexDirection.East)); // 실패 → 이벤트 없음
            Assert.AreEqual(5, count);
        }
    }
}
