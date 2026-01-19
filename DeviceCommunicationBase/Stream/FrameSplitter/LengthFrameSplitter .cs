using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 长度字段分包器：
    /// 从字节流中按 [头部 + 长度字段 + 数据] 的规则拆分完整帧
    /// </summary>
    public class LengthFrameSplitter:IFrameSplitter
    {
        /// <summary>最大允许帧长，防止异常数据打爆内存</summary>
        public int MaxFrameLength { get; }

        /// <summary>长度字段在帧中的偏移（从帧起始位置算起）</summary>
        public int LengthFieldOffset { get; }

        /// <summary>长度字段长度（单位：字节），支持 1/2/4</summary>
        public int LengthFieldLength { get; }

        /// <summary>长度字段是否小端</summary>
        public bool LengthFieldLittleEndian { get; }

        /// <summary>
        /// 头部总长度 = 从帧开始到数据区开始的总字节数
        /// （通常 = LengthFieldOffset + LengthFieldLength）
        /// </summary>
        public int HeaderSize { get; }

        /// <summary>
        /// 长度字段的值是否包含头部长度
        /// </summary>
        public bool LengthIncludesHeader { get; }

        private readonly List<byte> mBuffer = new List<byte>();

        public event Action<ReadOnlyMemory<byte>> FrameCompleted;

        public LengthFrameSplitter(int maxFrameLength,int lengthFieldOffset,int lengthFieldLength,bool lengthFieldLittleEndian,int headerSize,bool lengthIncludesHeader = false)
        {
            if (maxFrameLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxFrameLength));
            if (lengthFieldOffset < 0) throw new ArgumentOutOfRangeException(nameof(lengthFieldOffset));
            if (lengthFieldLength != 1 && lengthFieldLength != 2 && lengthFieldLength != 4)
                throw new ArgumentOutOfRangeException(nameof(lengthFieldLength), "LengthFieldLength must be 1, 2 or 4.");
            if (headerSize <= 0) throw new ArgumentOutOfRangeException(nameof(headerSize));
            if (headerSize < lengthFieldOffset + lengthFieldLength)
                throw new ArgumentException("headerSize must be >= lengthFieldOffset + lengthFieldLength");

            MaxFrameLength = maxFrameLength;
            LengthFieldOffset = lengthFieldOffset;
            LengthFieldLength = lengthFieldLength;
            LengthFieldLittleEndian = lengthFieldLittleEndian;
            HeaderSize = headerSize;
            LengthIncludesHeader = lengthIncludesHeader;
        }

        public void Feed(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            // 1. 把新数据追加到内部缓冲
            for (int i = 0; i < data.Length; i++)
            {
                mBuffer.Add(data[i]);
            }

            // 2. 尝试循环拆出完整帧
            while (true)
            {
                // 至少要有头部长度的数据
                if (mBuffer.Count < HeaderSize)
                    break;

                // 2.1 读取长度字段
                int lenValue = ReadLengthField(mBuffer);

                if (lenValue < 0 || lenValue > MaxFrameLength)
                {
                    // 协议错误：长度不合理，直接清空缓冲并终止
                    Reset();
                    throw new InvalidOperationException($"Frame length out of range: {lenValue}");
                }

                // 2.2 算出完整帧长度
                int frameLength = LengthIncludesHeader
                    ? lenValue
                    : HeaderSize + lenValue;

                if (frameLength > MaxFrameLength)
                {
                    Reset();
                    throw new InvalidOperationException($"Frame length out of range: {frameLength}");
                }

                // 2.3 判断缓冲区是否已经收到完整帧
                if (mBuffer.Count < frameLength)
                {
                    // 还没收完，退出循环，等待更多数据
                    break;
                }

                // 2.4 复制出完整帧
                byte[] frame = mBuffer.GetRange(0, frameLength).ToArray();

                // 2.5 从缓冲中移除这段帧数据
                mBuffer.RemoveRange(0, frameLength);

                // 2.6 通知上层有一帧完整包
                FrameCompleted?.Invoke(frame);
            }
        }

        private int ReadLengthField(List<byte> buf)
        {
            // 假设 buf.Count >= HeaderSize 已经检查
            int start = LengthFieldOffset;

            switch (LengthFieldLength)
            {
                case 1:
                    return buf[start];

                case 2:
                    if (LengthFieldLittleEndian)
                        return buf[start] | (buf[start + 1] << 8);
                    else
                        return (buf[start] << 8) | buf[start + 1];

                case 4:
                    if (LengthFieldLittleEndian)
                        return buf[start]
                             | (buf[start + 1] << 8)
                             | (buf[start + 2] << 16)
                             | (buf[start + 3] << 24);
                    else
                        return (buf[start] << 24)
                             | (buf[start + 1] << 16)
                             | (buf[start + 2] << 8)
                             | buf[start + 3];

                default:
                    throw new InvalidOperationException("Unsupported length field size.");
            }
        }

        public void Reset()
        {
            mBuffer.Clear();
        }
    }
}
