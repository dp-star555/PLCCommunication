using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>构建上下文：输出缓冲 + Mark 定位点</summary>
    public sealed class FrameBuildContext
    {
        private readonly List<byte> mBuffer = new List<byte>();
        private readonly Dictionary<string, int> mMarks = new Dictionary<string, int>();

        public int Position => mBuffer.Count;

        public IReadOnlyList<byte> Buffer => mBuffer;

        public byte[] ToArray() => mBuffer.ToArray();

        public void Mark(string name)
        {
            mMarks[name] = mBuffer.Count;
        }

        public int GetMark(string name)
        {
            if (!mMarks.TryGetValue(name, out var pos))
                throw new InvalidOperationException($"Mark not found: {name}");
            return pos;
        }

        public (byte[] Data, int Length) Slice(string fromMark, string toMark)
        {
            int s = GetMark(fromMark);
            int e = GetMark(toMark);
            if (e < s) throw new InvalidOperationException($"Invalid mark range: {fromMark}->{toMark}");
            int len = e - s;
            byte[] result = new byte[len];
            mBuffer.CopyTo(s, result, 0, len);
            return (result, len);
        }

        public int SliceLength(string fromMark, string toMark)
        {
            int s = GetMark(fromMark);
            int e = GetMark(toMark);
            if (e < s) throw new InvalidOperationException($"Invalid mark range: {fromMark}->{toMark}");
            return e - s;
        }

        public void WriteByte(byte v) => mBuffer.Add(v);

        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                mBuffer.Add(bytes[i]);
        }

        public int Reserve(int count)
        {
            int pos = mBuffer.Count;
            for (int i = 0; i < count; i++)
                mBuffer.Add(0);
            return pos;
        }

        public void Patch(int pos, byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                mBuffer[pos + i] = value[i];
            }
        }
    }

}
