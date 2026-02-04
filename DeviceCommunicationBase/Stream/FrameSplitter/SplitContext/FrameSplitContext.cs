using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream.FrameSplitter
{
    public class FrameSplitContext
    {
        public FrameSplitContext(ReadOnlyMemory<byte> buffer)
        {
            mBuffer = buffer;
        }

        private ReadOnlyMemory<byte> mBuffer;


        // 帧起点（由 HeaderModule 设置）
        public int StartIndex { get; private set; } = 0;

        // 固定长度累计（由 SizeModule 累加）
        public int FixedLengthSoFar { get; private set; } = 0;

        // 前面是否出现过不定长段
        public bool HasVariableBefore { get; private set; } = false;

        // 如果找到完整帧，填充长度
        public int FrameLength { get; private set; } = -1;


        public bool TrySetStart(int idx)
        {
            if (idx < 0 || idx >= mBuffer.Length) return false;
            StartIndex = idx;
            return true;
        }


        public bool AddFixedLength(int len)
        {
            if (len <= 0) return true;

            int next = StartIndex + FixedLengthSoFar + len;
            if (next > mBuffer.Length) return false;

            FixedLengthSoFar += len;
            return true;
        }

        public void MarkVariable()
        {
            HasVariableBefore = true;
        }

        public bool TrySetFrameLength(int len)
        {
            if (len <= 0) return false;
            FrameLength = len;
            return true;
        }

        public bool HasFullFrame()
            => FrameLength > 0 && (StartIndex + FrameLength) <= mBuffer.Length;

        // 从 StartIndex 偏移读取
        public ReadOnlySpan<byte> SliceFromStart(int offset, int length)
        {
            int s = StartIndex + offset;
            if (s < 0 || s + length > mBuffer.Length) return ReadOnlySpan<byte>.Empty;
            return mBuffer.Span.Slice(s,length);
        }

        // 从指定起点搜索 pattern
        public int IndexOf(ReadOnlySpan<byte> pattern, int start)
        {
            if (pattern.IsEmpty) return -1;
            if (start < 0 || start >= mBuffer.Length) return -1;
            int rel = mBuffer.Span.Slice(start).IndexOf(pattern);
            return rel < 0 ? -1 : start + rel;
        }

    }
}
