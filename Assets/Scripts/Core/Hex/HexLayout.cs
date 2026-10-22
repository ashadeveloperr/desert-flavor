using System;

namespace DesertSettlement.Core.Hex
{
    /// <summary>바닥 평면(X, Z) 위의 점. Core는 UnityEngine을 참조하지 않으므로 Vector3 대신 사용한다.</summary>
    public readonly struct HexPoint
    {
        public readonly double X;
        public readonly double Z;

        public HexPoint(double x, double z)
        {
            X = x;
            Z = z;
        }

        public override string ToString() => $"({X:0.###}, {Z:0.###})";
    }

    /// <summary>
    /// 헥스 좌표 → 월드 평면 좌표 변환 (pointy-top).
    /// 타일 크기를 바꾸려면 <see cref="Size"/>(중심~꼭짓점 거리)만 바꾸면 된다.
    /// </summary>
    public sealed class HexLayout
    {
        private static readonly double Sqrt3 = Math.Sqrt(3.0);

        /// <summary>타일 중심에서 꼭짓점까지의 거리(월드 단위).</summary>
        public double Size { get; }

        public HexLayout(double size = 1.0)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
        }

        public HexPoint TileCenter(HexCoord coord)
        {
            double x = Size * Sqrt3 * (coord.Q + coord.R / 2.0);
            double z = -Size * 1.5 * coord.R;
            return new HexPoint(x, z);
        }

        public HexPoint CornerPosition(HexCoord coord, HexCorner corner)
        {
            HexPoint center = TileCenter(coord);
            double angle = Math.PI / 3.0 * (int)corner; // 북쪽에서 시계 방향 60°씩
            return new HexPoint(center.X + Size * Math.Sin(angle), center.Z + Size * Math.Cos(angle));
        }
    }
}
