using System;

namespace DesertSettlement.Core.Progression
{
    /// <summary>
    /// 플랫폼과 런타임(Unity Mono / IL2CPP / .NET)에 상관없이 같은 시드면 같은 수열을 내는 난수(SplitMix64).
    /// System.Random은 런타임마다 구현이 다를 수 있어, 절차 맵처럼 '같은 시드 = 같은 맵'이 필요한 곳은 이것을 쓴다.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(int seed)
        {
            // 시드를 한 번 섞는다. 그냥 seed × 증가량으로 시작하면 시드 n+1의 수열이 시드 n의 수열을 한 칸 민 것과 같아진다.
            _state = Mix(unchecked((ulong)(uint)seed + 0x632BE59BD9B4E019UL));
        }

        private ulong NextUInt64() => Mix(_state = unchecked(_state + 0x9E3779B97F4A7C15UL));

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>[0, 1) 실수.</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        /// <summary>[0, maxExclusive) 정수.</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt64() % (ulong)maxExclusive);
        }

        /// <summary>[minInclusive, maxExclusive) 정수.</summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>0 이상 int 최댓값 미만(순서 섞기용).</summary>
        public int Next() => (int)(NextUInt64() >> 33);
    }
}
