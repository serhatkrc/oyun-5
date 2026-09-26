namespace PG.Core
{
    public sealed class GameClock
    {
        public static readonly int[] SpeedLevels = { 0, 1, 2, 3, 5, 10, 20 };

        int _lastRunningSpeed = 1;
        bool _stepRequested;

        public long Tick { get; private set; }
        public int Month => (int)(Tick / SimConst.TicksPerMonth % SimConst.MonthsPerYear);
        public int Year => (int)(Tick / SimConst.TicksPerYear);
        public int SpeedIndex { get; private set; } = 1;
        public int SpeedMultiplier => SpeedLevels[SpeedIndex];
        public bool Paused => SpeedLevels[SpeedIndex] == 0;
        public bool SuperSpeed { get; set; }
        public bool IsMonthStart => Tick % SimConst.TicksPerMonth == 0;
        public bool IsYearStart => Tick % SimConst.TicksPerYear == 0;

        // Called by UI buttons and the 1-7 keys.
        public void SetSpeed(int index)
        {
            if (index < 0) index = 0;
            if (index >= SpeedLevels.Length) index = SpeedLevels.Length - 1;
            SpeedIndex = index;
            if (index > 0) _lastRunningSpeed = index;
        }

        public void TogglePause() => SetSpeed(Paused ? _lastRunningSpeed : 0);

        // While paused: run exactly one tick on the next frame.
        public void StepOnce()
        {
            if (Paused) _stepRequested = true;
        }

        public bool ConsumeStepRequest()
        {
            bool requested = _stepRequested;
            _stepRequested = false;
            return requested;
        }

        internal void AdvanceTick() => Tick++;

        internal void SetTick(long tick) => Tick = tick;
    }
}
