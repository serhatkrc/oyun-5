using System;
using System.Globalization;

namespace PG.Boot
{
    // --determinism-check <ticks>, --headless --years <n>, --seed <n>, --size <preset>, --template <id>
    public sealed class CommandLine
    {
        public int DeterminismTicks;
        public bool Headless;
        public int Years = 100;
        public ulong Seed;
        public string Size;
        public string Template;

        public static CommandLine Parse(string[] args)
        {
            var cl = new CommandLine();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (args[i])
                {
                    case "--determinism-check":
                        cl.DeterminismTicks = ParseInt(next, 1000);
                        i++;
                        break;
                    case "--headless":
                        cl.Headless = true;
                        break;
                    case "--years":
                        cl.Years = ParseInt(next, 100);
                        i++;
                        break;
                    case "--seed":
                        if (ulong.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed)) cl.Seed = seed;
                        i++;
                        break;
                    case "--size":
                        cl.Size = next;
                        i++;
                        break;
                    case "--template":
                        cl.Template = next;
                        i++;
                        break;
                }
            }
            return cl;
        }

        static int ParseInt(string s, int fallback) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : fallback;
    }
}
