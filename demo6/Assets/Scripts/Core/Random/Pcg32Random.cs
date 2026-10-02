namespace Demo6.Core.Random
{
    public interface IRandom
    {
        /// <summary>[0, 1)</summary>
        double NextDouble();

        /// <summary>[min, max)</summary>
        int NextInt(int min, int max);
    }

    /// <summary>PCG32 (O'Neill). 용도별로 흐름을 나눠 써서 한쪽 소모가 다른 쪽 순서를 밀지 않게 한다.</summary>
    public sealed class Pcg32Random : IRandom
    {
        ulong _state;
        readonly ulong _inc;

        public Pcg32Random(ulong seed, ulong stream = 54)
        {
            _state = 0;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * 6364136223846793005UL + _inc;
                uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
                int rot = (int)(old >> 59);
                return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
            }
        }

        public double NextDouble() => NextUInt() / 4294967296.0;

        public int NextInt(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextDouble() * (max - min));
        }
    }
}
