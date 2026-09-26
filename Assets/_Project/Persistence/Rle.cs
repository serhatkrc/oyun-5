using System.IO;

namespace PG.Persistence
{
    // Run-length coding of one chunk's values: (runLength:ushort, value) pairs until `count` values are covered.
    public static class Rle
    {
        public static void WriteBytes(BinaryWriter w, byte[] src, int[] indices)
        {
            int n = indices.Length, k = 0;
            while (k < n)
            {
                byte v = src[indices[k]];
                int run = 1;
                while (k + run < n && run < ushort.MaxValue && src[indices[k + run]] == v) run++;
                w.Write((ushort)run);
                w.Write(v);
                k += run;
            }
        }

        public static void WriteUShorts(BinaryWriter w, ushort[] src, int[] indices)
        {
            int n = indices.Length, k = 0;
            while (k < n)
            {
                ushort v = src[indices[k]];
                int run = 1;
                while (k + run < n && run < ushort.MaxValue && src[indices[k + run]] == v) run++;
                w.Write((ushort)run);
                w.Write(v);
                k += run;
            }
        }

        public static void ReadBytes(BinaryReader r, byte[] dst, int[] indices)
        {
            int k = 0;
            while (k < indices.Length)
            {
                int run = r.ReadUInt16();
                byte v = r.ReadByte();
                for (int j = 0; j < run && k < indices.Length; j++) dst[indices[k++]] = v;
            }
        }

        public static void ReadUShorts(BinaryReader r, ushort[] dst, int[] indices)
        {
            int k = 0;
            while (k < indices.Length)
            {
                int run = r.ReadUInt16();
                ushort v = r.ReadUInt16();
                for (int j = 0; j < run && k < indices.Length; j++) dst[indices[k++]] = v;
            }
        }
    }
}
