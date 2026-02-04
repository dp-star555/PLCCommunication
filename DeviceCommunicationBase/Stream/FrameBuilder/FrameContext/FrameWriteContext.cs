using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    public sealed class FrameWriteContext
    {
        private readonly List<byte> mBuffer = new List<byte>();
        private readonly Dictionary<string, int> mMarks = new Dictionary<string, int>();

        public int Length => mBuffer.Count;

        // 标记当前位置（用于 Length/CRC 计算）
        public void Mark(string name) => mMarks[name] = mBuffer.Count;

        public int GetMark(string name)
        {
            if (!mMarks.TryGetValue(name, out var pos))
                throw new InvalidOperationException($"Mark not found: {name}");
            return pos;
        }

        // 写入字节序列
        public void Write(ReadOnlySpan<byte> data)
        {
            for (int i = 0; i < data.Length; i++) mBuffer.Add(data[i]);
        }

        // 预留 count 字节，返回起始位置
        public int Reserve(int count)
        {
            int pos = mBuffer.Count;
            for (int i = 0; i < count; i++) mBuffer.Add(0);
            return pos;
        }

        // 回填（覆盖写）
        public void Patch(int pos, ReadOnlySpan<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
                mBuffer[pos + i] = data[i];
        }

        // 取片段（用于 CRC/Length）
        public ReadOnlySpan<byte> Slice(string fromMark, string toMark)
        {
            int s = GetMark(fromMark);
            int e = GetMark(toMark);
            if (e < s) throw new InvalidOperationException($"Invalid mark range: {fromMark}->{toMark}");
            int len = e - s;
            return mBuffer.ToArray().AsSpan(s, len);
        }

        public byte[] ToArray() => mBuffer.ToArray();
    }

}
