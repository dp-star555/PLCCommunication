using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 按指定“结束符”拆分帧，例如 0x0D 0x0A 或任意自定义字节序列
    /// </summary>
    internal class DelimiterFrameSplitter : IFrameSplitter
    {
        private byte[] mDelimiter;
        private readonly List<byte> mBbuffer = new List<byte>();

        /// <summary>
        /// 当前使用的结束符
        /// </summary>
        public ReadOnlyMemory<byte> Delimiter => mDelimiter;

        public event Action<ReadOnlyMemory<byte>> FrameCompleted;

        /// <summary>
        /// 使用自定义字节序列作为结尾符
        /// </summary>
        public DelimiterFrameSplitter(params byte[] delimiter)
        {
            SetDelimiter(delimiter);
        }

        /// <summary>
        /// 使用字符串 + 编码 作为结尾符（例如 "\r\n"）
        /// </summary>
        public static DelimiterFrameSplitter FromString(string delimiter, Encoding encoding = null)
        {
            if (delimiter == null) throw new ArgumentNullException(nameof(delimiter));
            if (encoding == null)
            {
                encoding = Encoding.ASCII;
            }
            var bytes = encoding.GetBytes(delimiter);
            return new DelimiterFrameSplitter(bytes);
        }

        /// <summary>
        /// 运行时更新结束符
        /// </summary>
        public void SetDelimiter(ReadOnlySpan<byte> delimiter)
        {
            if (delimiter.IsEmpty)
                throw new ArgumentException("Delimiter must not be empty.", nameof(delimiter));

            mDelimiter = delimiter.ToArray();
            mBbuffer.Clear();
        }

        public void Feed(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0) return;

            for (int i = 0; i < data.Length; i++)
                mBbuffer.Add(data[i]);

            while (true)
            {
                int idx = IndexOfDelimiter(mBbuffer, mDelimiter);
                if (idx < 0)
                    break;

                int frameLen = idx; // 不包含分隔符本身
                if (frameLen > 0)
                {
                    byte[] frame = mBbuffer.GetRange(0, frameLen).ToArray();
                    FrameCompleted?.Invoke(frame);
                }

                // 移除 [frame + delimiter]
                mBbuffer.RemoveRange(0, frameLen + mDelimiter.Length);
            }
        }

        private static int IndexOfDelimiter(List<byte> buffer, byte[] delimiter)
        {
            if (buffer.Count < delimiter.Length)
                return -1;

            for (int i = 0; i <= buffer.Count - delimiter.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < delimiter.Length; j++)
                {
                    if (buffer[i + j] != delimiter[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i;
            }

            return -1;
        }

        public void Reset()
        {
            mBbuffer.Clear();
        }
    }
}
