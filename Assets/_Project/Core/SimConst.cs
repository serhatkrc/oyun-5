namespace PG.Core
{
    public static class SimConst
    {
        public const int TicksPerSecond = 20;   // 20 ticks per second at x1
        public const int TicksPerMonth = 60;    // 1 month = 3 s at x1
        public const int MonthsPerYear = 12;    // 1 year = 36 s at x1
        public const int TicksPerYear = TicksPerMonth * MonthsPerYear;
        public const float TickDt = 1f / TicksPerSecond;
        public const int MaxTicksPerFrame = 40; // spiral-of-death guard
        public const float SuperSpeedBudgetMs = 16f;
        public const int SuperSpeedRenderEvery = 4;
    }
}
