using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.FrameSplitter
{
    /// <summary>
    /// MC 3E Binary 拆包器：
    /// - 自动对齐 0xD0 0x00
    /// - FrameLength = 9 + Length(ushort LE at offset 7)
    /// </summary>
    public class Mc3EBinaryFrameSplitter : IFrameSplitter
    {
        private readonly List<byte> mBuffer = new List<byte>();

        /// <summary>最大允许帧长，防止异常数据打爆内存</summary>
        public int MaxFrameLength { get; }

        public event Action<ReadOnlyMemory<byte>> FrameCompleted;

        public Mc3EBinaryFrameSplitter(int maxFrameLength = 9 + 8192)
        {
            if (maxFrameLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxFrameLength));
            MaxFrameLength = maxFrameLength;
        }

        public void Feed(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            // 1) 追加到缓冲
            for (int i = 0; i < data.Length; i++)
                mBuffer.Add(data[i]);

            // 2) 循环拆帧（可能一次收到多帧）
            while (true)
            {
                // 2.1 至少要有 2 字节才能检查 subheader
                if (mBuffer.Count < 2) break;

                // 2.2 帧头对齐：若当前不是 0xD0 0x00，则丢弃直到找到
                AlignToSubheader();
                if (mBuffer.Count < 9) break; // 3E binary 头最少 9 字节才能读 length

                // 2.3 读取 length（offset 7, len 2, little-endian）
                int dataLen = mBuffer[7] | (mBuffer[8] << 8);

                if (dataLen < 0) // 理论不会发生
                {
                    Reset();
                    throw new InvalidOperationException($"Invalid MC length: {dataLen}");
                }

                int frameLen = 9 + dataLen;

                if (frameLen <= 0 || frameLen > MaxFrameLength)
                {
                    // 长度异常：说明可能对齐错了
                    mBuffer.RemoveAt(0);
                    continue;
                }

                // 2.4 未收完整帧
                if (mBuffer.Count < frameLen) break;

                // 2.5 拆出完整帧
                byte[] frame = mBuffer.GetRange(0, frameLen).ToArray();
                mBuffer.RemoveRange(0, frameLen);
                FrameCompleted?.Invoke(frame);
            }
        }

        private void AlignToSubheader()
        {
            // 当前已经保证 mBuffer.Count >= 2
            if (mBuffer[0] == 0xD0 && mBuffer[1] == 0x00) return;

            // 从缓冲中查找下一个 0xD0 0x00
            int idx = -1;
            for (int i = 0; i < mBuffer.Count - 1; i++)
            {
                if (mBuffer[i] == 0xD0 && mBuffer[i + 1] == 0x00)
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                // 找不到任何 subheader，保留最后 1 字节（可能是 0xD0 的一半），其余丢弃
                byte last = mBuffer[mBuffer.Count - 1];
                mBuffer.Clear();
                mBuffer.Add(last);
                return;
            }

            if (idx > 0)
                mBuffer.RemoveRange(0, idx);
        }

        public void Reset()
        {
            mBuffer.Clear();
        }
    }
}
