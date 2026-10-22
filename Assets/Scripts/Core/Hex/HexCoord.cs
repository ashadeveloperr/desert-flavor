using System;
using System.Collections.Generic;

namespace DesertSettlement.Core.Hex
{
    /// <summary>
    /// 헥스 타일의 변(이웃) 방향. Pointy-top(꼭짓점이 위·아래) 기준, 동쪽부터 시계 방향.
    /// 숫자 값은 순서 계산에 쓰이므로 바꾸지 않는다. 반대 방향 = (값 + 3) % 6.
    /// </summary>
    public enum HexDirection
    {
        East = 0,
        SouthEast = 1,
        SouthWest = 2,
        West = 3,
        NorthWest = 4,
        NorthEast = 5,
    }

    /// <summary>
    /// 헥스 타일의 꼭짓점 위치. 북쪽부터 시계 방향.
    /// 변 d는 꼭짓점 (d+1)%6 과 (d+2)%6 사이에 있다. 예: East 변 = NorthEast~SouthEast 꼭짓점.
    /// </summary>
    public enum HexCorner
    {
        North = 0,
        NorthEast = 1,
        SouthEast = 2,
        South = 3,
        SouthWest = 4,
        NorthWest = 5,
    }

    /// <summary>
    /// Axial 헥스 좌표 (q, r). r이 커질수록 남쪽(월드 -Z), q가 커질수록 동쪽(월드 +X).
    /// 월드 좌표 변환은 <see cref="HexLayout"/>에서 한다.
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;

        /// <summary>Cube 좌표의 세 번째 축. q + r + s = 0.</summary>
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static readonly HexCoord Zero = new HexCoord(0, 0);

        // HexDirection 순서와 같아야 한다.
        private static readonly HexCoord[] DirectionOffsets =
        {
            new HexCoord(1, 0),   // East
            new HexCoord(0, 1),   // SouthEast
            new HexCoord(-1, 1),  // SouthWest
            new HexCoord(-1, 0),  // West
            new HexCoord(0, -1),  // NorthWest
            new HexCoord(1, -1),  // NorthEast
        };

        public static HexDirection Opposite(HexDirection direction) => (HexDirection)(((int)direction + 3) % 6);

        public HexCoord Neighbor(HexDirection direction)
        {
            HexCoord offset = DirectionOffsets[(int)direction];
            return new HexCoord(Q + offset.Q, R + offset.R);
        }

        /// <summary>두 타일 사이의 타일 거리(이동 칸 수). 수로 거리가 아니다.</summary>
        public int DistanceTo(HexCoord other)
        {
            int dq = Math.Abs(Q - other.Q);
            int dr = Math.Abs(R - other.R);
            int ds = Math.Abs(S - other.S);
            return Math.Max(dq, Math.Max(dr, ds));
        }

        /// <summary>원점에서 반경 radius 이내의 모든 좌표. 개수 = 3R² + 3R + 1.</summary>
        public static IEnumerable<HexCoord> Range(int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Math.Max(-radius, -q - radius);
                int rMax = Math.Min(radius, -q + radius);
                for (int r = rMin; r <= rMax; r++)
                {
                    yield return new HexCoord(q, r);
                }
            }
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Q, R);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
        public override string ToString() => $"({Q},{R})";
    }
}
