using System.Globalization;
using UnityEngine;

namespace PG.Content
{
    public static class HexColor
    {
        // "#RRGGBB" or "#RRGGBBAA".
        public static bool TryParse(string hex, out Color32 color)
        {
            color = default;
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || (hex.Length != 7 && hex.Length != 9)) return false;
            if (!uint.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
            if (hex.Length == 7) v = (v << 8) | 0xFF;
            color = new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
            return true;
        }

        public static Color32[] ParseVariants(string[] hex, string owner, System.Collections.Generic.List<string> errors)
        {
            var result = new Color32[4];
            if (hex == null || hex.Length != 4)
            {
                errors.Add($"{owner}: expected 4 color variants");
                return result;
            }
            for (int i = 0; i < 4; i++)
                if (!TryParse(hex[i], out result[i])) errors.Add($"{owner}: invalid color '{hex[i]}'");
            return result;
        }
    }
}
