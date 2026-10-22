using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Hex;
using NUnit.Framework;

namespace DesertSettlement.Core.Tests
{
    public class HexGridTests
    {
        private const double Epsilon = 1e-9;

        [TestCase(3, 37)]
        [TestCase(5, 91)]
        [TestCase(7, 169)]
        public void Map_HasExpectedTileCount(int radius, int expected)
        {
            Assert.AreEqual(expected, new HexMap(radius).TileCount);
        }

        [TestCase(2)]
        [TestCase(8)]
        public void Map_RejectsRadiusOutsideDesignRange(int radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HexMap(radius));
        }

        [Test]
        public void Neighbor_ThenOpposite_ReturnsToStart()
        {
            var start = new HexCoord(2, -1);
            for (int i = 0; i < 6; i++)
            {
                var dir = (HexDirection)i;
                Assert.AreEqual(start, start.Neighbor(dir).Neighbor(HexCoord.Opposite(dir)));
                Assert.AreEqual(1, start.DistanceTo(start.Neighbor(dir)));
            }
        }

        [Test]
        public void Distance_MatchesKnownValues()
        {
            Assert.AreEqual(3, HexCoord.Zero.DistanceTo(new HexCoord(3, -3)));
            Assert.AreEqual(3, HexCoord.Zero.DistanceTo(new HexCoord(-1, -2)));
            Assert.AreEqual(4, new HexCoord(-2, 0).DistanceTo(new HexCoord(2, 0)));
        }

        [Test]
        public void Layout_NeighborCentersAreEquidistant()
        {
            var layout = new HexLayout(1.0);
            HexPoint center = layout.TileCenter(HexCoord.Zero);
            for (int i = 0; i < 6; i++)
            {
                HexPoint n = layout.TileCenter(HexCoord.Zero.Neighbor((HexDirection)i));
                double d = Math.Sqrt(Math.Pow(n.X - center.X, 2) + Math.Pow(n.Z - center.Z, 2));
                Assert.AreEqual(Math.Sqrt(3.0), d, Epsilon, $"방향 {(HexDirection)i}");
            }
        }

        [Test]
        public void Layout_PointyTop_NorthCornerIsStraightUp()
        {
            var layout = new HexLayout(2.0);
            HexPoint north = layout.CornerPosition(HexCoord.Zero, HexCorner.North);
            Assert.AreEqual(0.0, north.X, Epsilon);
            Assert.AreEqual(2.0, north.Z, Epsilon);
        }

        [Test]
        public void PointToHex_RoundTripsCentersAndPointsNearCorners()
        {
            var layout = new HexLayout(1.5);
            foreach (HexCoord c in HexCoord.Range(5))
            {
                HexPoint center = layout.TileCenter(c);
                Assert.AreEqual(c, layout.PointToHex(center));
                for (int i = 0; i < 6; i++)
                {
                    // 중심에서 꼭짓점 방향으로 90% 지점은 아직 같은 타일.
                    HexPoint corner = layout.CornerPosition(c, (HexCorner)i);
                    var inside = new HexPoint(center.X + (corner.X - center.X) * 0.9, center.Z + (corner.Z - center.Z) * 0.9);
                    Assert.AreEqual(c, layout.PointToHex(inside));
                }
            }
        }

        [Test]
        public void NearestEdge_PicksTheEdgeTowardThePoint()
        {
            var layout = new HexLayout(1.0);
            HexCoord tile = new HexCoord(1, -2);
            for (int i = 0; i < 6; i++)
            {
                EdgeId edge = EdgeId.Of(tile, (HexDirection)i);
                HexPoint mid = HexTopology.MidpointOf(edge, layout);
                HexPoint center = layout.TileCenter(tile);
                // 중심과 변 중간점 사이 80% 지점 → 그 변.
                var p = new HexPoint(center.X + (mid.X - center.X) * 0.8, center.Z + (mid.Z - center.Z) * 0.8);
                Assert.AreEqual(edge, layout.NearestEdge(p), $"방향 {(HexDirection)i}");
            }
        }

        [TestCase(3, 96, 132)]
        [TestCase(5, 216, 306)]
        [TestCase(7, 384, 552)]
        public void Topology_HasExpectedVertexAndEdgeCounts(int radius, int vertices, int edges)
        {
            var topology = new HexTopology(new HexMap(radius));
            Assert.AreEqual(vertices, topology.VertexCount, "꼭짓점 수 = 6(R+1)²");
            Assert.AreEqual(edges, topology.EdgeCount, "변 수 = 꼭짓점 + 타일 - 1 (오일러 공식)");
        }

        [Test]
        public void VertexId_SameKeyAlwaysMeansSamePosition_AndDifferentKeysDifferentPositions()
        {
            var map = new HexMap(3);
            var layout = new HexLayout(1.0);
            var positions = new Dictionary<VertexId, HexPoint>();

            foreach (HexTile tile in map.Tiles)
            {
                for (int i = 0; i < 6; i++)
                {
                    VertexId id = VertexId.Of(tile.Coord, (HexCorner)i);
                    HexPoint actual = layout.CornerPosition(tile.Coord, (HexCorner)i);
                    if (positions.TryGetValue(id, out HexPoint known))
                    {
                        Assert.AreEqual(known.X, actual.X, 1e-6, $"{id} X");
                        Assert.AreEqual(known.Z, actual.Z, 1e-6, $"{id} Z");
                    }
                    else
                    {
                        positions.Add(id, actual);
                    }
                }
            }

            List<HexPoint> all = positions.Values.ToList();
            for (int a = 0; a < all.Count; a++)
            {
                for (int b = a + 1; b < all.Count; b++)
                {
                    double d = Math.Abs(all[a].X - all[b].X) + Math.Abs(all[a].Z - all[b].Z);
                    Assert.Greater(d, 1e-3, "서로 다른 키가 같은 위치에 있으면 안 된다");
                }
            }
        }

        [Test]
        public void EdgeId_NeighborsShareTheSameEdgeKey()
        {
            var map = new HexMap(3);
            foreach (HexTile tile in map.Tiles)
            {
                for (int i = 0; i < 6; i++)
                {
                    var dir = (HexDirection)i;
                    EdgeId fromHere = EdgeId.Of(tile.Coord, dir);
                    EdgeId fromNeighbor = EdgeId.Of(tile.Coord.Neighbor(dir), HexCoord.Opposite(dir));
                    Assert.AreEqual(fromHere, fromNeighbor);
                    Assert.Less((int)fromHere.Side, 3, "정규화된 변은 East/SouthEast/SouthWest만 사용");
                }
            }
        }

        [Test]
        public void EdgeId_EndpointsAreTheTwoCornersOfThatSide()
        {
            var layout = new HexLayout(1.0);
            var tile = new HexCoord(1, 1);
            for (int i = 0; i < 6; i++)
            {
                EdgeId edge = EdgeId.Of(tile, (HexDirection)i);
                HexPoint a = HexTopology.PositionOf(edge.VertexA, layout);
                HexPoint b = HexTopology.PositionOf(edge.VertexB, layout);
                double length = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
                Assert.AreEqual(1.0, length, 1e-6, "변 길이 = 타일 크기");

                // 변 중점에서 이 타일 중심 방향과 이웃 중심 방향이 대칭이어야 한다.
                HexPoint mid = HexTopology.MidpointOf(edge, layout);
                HexPoint c1 = layout.TileCenter(edge.TileA);
                HexPoint c2 = layout.TileCenter(edge.TileB);
                Assert.AreEqual((c1.X + c2.X) / 2.0, mid.X, 1e-6);
                Assert.AreEqual((c1.Z + c2.Z) / 2.0, mid.Z, 1e-6);
            }
        }

        [Test]
        public void Topology_InteriorVertexHasThreeEdgesAndTiles_BoundaryEdgeHasOneTile()
        {
            var topology = new HexTopology(new HexMap(3));

            VertexId interior = VertexId.Of(HexCoord.Zero, HexCorner.North);
            Assert.AreEqual(3, topology.EdgesOf(interior).Count);
            Assert.AreEqual(3, topology.TilesOf(interior).Count);

            // (3,0)은 맵 동쪽 끝 타일. East 변은 경계다.
            EdgeId boundary = EdgeId.Of(new HexCoord(3, 0), HexDirection.East);
            Assert.IsTrue(topology.Contains(boundary));
            Assert.IsTrue(topology.IsBoundary(boundary));

            EdgeId inner = EdgeId.Of(HexCoord.Zero, HexDirection.East);
            Assert.IsFalse(topology.IsBoundary(inner));

            int boundaryEdges = topology.Edges.Count(topology.IsBoundary);
            Assert.AreEqual(6 * (2 * 3 + 1), boundaryEdges, "반경 R 맵의 경계 변 수 = 6(2R+1)");
        }
    }
}
