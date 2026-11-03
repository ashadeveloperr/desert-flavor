using System;

namespace DesertSettlement.Core.Hex
{
    /// <summary>
    /// 육각형 꼭짓점의 정규화된 키. 하나의 꼭짓점은 최대 3개 타일이 공유하지만 키는 항상 하나다.
    /// 규칙: 모든 꼭짓점은 어떤 타일의 North 또는 South 꼭짓점으로 표현된다.
    /// 직접 만들지 말고 <see cref="Of"/>로 얻는다.
    /// </summary>
    public readonly struct VertexId : IEquatable<VertexId>
    {
        public readonly HexCoord Owner;
        /// <summary>true면 Owner의 North 꼭짓점, false면 South 꼭짓점.</summary>
        public readonly bool IsNorth;

        private VertexId(HexCoord owner, bool isNorth)
        {
            Owner = owner;
            IsNorth = isNorth;
        }

        /// <summary>타일 tile의 corner 꼭짓점에 해당하는 정규화 키.</summary>
        public static VertexId Of(HexCoord tile, HexCorner corner)
        {
            switch (corner)
            {
                case HexCorner.North: return new VertexId(tile, true);
                case HexCorner.NorthEast: return new VertexId(tile.Neighbor(HexDirection.NorthEast), false);
                case HexCorner.SouthEast: return new VertexId(tile.Neighbor(HexDirection.SouthEast), true);
                case HexCorner.South: return new VertexId(tile, false);
                case HexCorner.SouthWest: return new VertexId(tile.Neighbor(HexDirection.SouthWest), true);
                case HexCorner.NorthWest: return new VertexId(tile.Neighbor(HexDirection.NorthWest), false);
                default: throw new ArgumentOutOfRangeException(nameof(corner));
            }
        }

        /// <summary>이 꼭짓점의 위치를 대표 타일 기준으로 계산할 때 쓰는 (타일, 꼭짓점) 쌍.</summary>
        public HexCorner OwnerCorner => IsNorth ? HexCorner.North : HexCorner.South;

        public bool Equals(VertexId other) => Owner == other.Owner && IsNorth == other.IsNorth;
        public override bool Equals(object obj) => obj is VertexId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Owner, IsNorth);
        public static bool operator ==(VertexId a, VertexId b) => a.Equals(b);
        public static bool operator !=(VertexId a, VertexId b) => !a.Equals(b);
        public override string ToString() => $"V{Owner}{(IsNorth ? "N" : "S")}";
    }

    /// <summary>
    /// 육각형 변(타일 경계)의 정규화된 키. 수로는 이 변 위에 설치된다.
    /// 규칙: 모든 변은 어떤 타일의 East / SouthEast / SouthWest 변으로 표현된다.
    /// 직접 만들지 말고 <see cref="Of"/>로 얻는다.
    /// </summary>
    public readonly struct EdgeId : IEquatable<EdgeId>
    {
        public readonly HexCoord Owner;
        /// <summary>항상 East, SouthEast, SouthWest 중 하나.</summary>
        public readonly HexDirection Side;

        private EdgeId(HexCoord owner, HexDirection side)
        {
            Owner = owner;
            Side = side;
        }

        /// <summary>타일 tile의 side 방향 변에 해당하는 정규화 키.</summary>
        public static EdgeId Of(HexCoord tile, HexDirection side)
        {
            if ((int)side < 3)
            {
                return new EdgeId(tile, side);
            }
            return new EdgeId(tile.Neighbor(side), HexCoord.Opposite(side));
        }

        /// <summary>변 양쪽의 두 타일 좌표(맵 밖일 수도 있다).</summary>
        public HexCoord TileA => Owner;
        public HexCoord TileB => Owner.Neighbor(Side);

        /// <summary>변의 양 끝 꼭짓점.</summary>
        public VertexId VertexA => VertexId.Of(Owner, (HexCorner)(((int)Side + 1) % 6));
        public VertexId VertexB => VertexId.Of(Owner, (HexCorner)(((int)Side + 2) % 6));

        public bool Equals(EdgeId other) => Owner == other.Owner && Side == other.Side;
        public override bool Equals(object obj) => obj is EdgeId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Owner, (int)Side);
        public static bool operator ==(EdgeId a, EdgeId b) => a.Equals(b);
        public static bool operator !=(EdgeId a, EdgeId b) => !a.Equals(b);
        public override string ToString() => $"E{Owner}{Side}";
    }
}
