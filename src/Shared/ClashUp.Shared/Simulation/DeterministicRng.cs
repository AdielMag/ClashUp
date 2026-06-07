namespace ClashUp.Shared.Simulation
{
    public sealed class DeterministicRng
    {
        private uint _state;

        public DeterministicRng(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public uint Next()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        public float NextFloat()
        {
            return (Next() & 0x7FFFFFFFu) / (float)0x7FFFFFFFu;
        }

        public float NextRange(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        public static DeterministicRng ForTick(uint baseSeed, int tick)
        {
            return new DeterministicRng(baseSeed ^ (uint)tick);
        }
    }
}
