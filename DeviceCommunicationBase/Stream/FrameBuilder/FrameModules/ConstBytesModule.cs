using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 固定的一些功能码
    /// </summary>
    public sealed class ConstBytesModule : IFrameModule
    {
        private readonly byte[] mBytes;
        private readonly string mName;

        public ConstBytesModule(byte[] bytes, string name = "") 
        {
            mBytes = bytes;
            mName = name;
        }

        public int Length => mBytes.Length;

        public string Name => mName;

        public void Encode_Emit(FrameWriteContext ctx) => ctx.Write(mBytes);
        public void Encode_Fixup(FrameWriteContext ctx) { }

        public void Scan(FrameDecodeContext ctx)
        {
            //假设当前起始位置还在
            if (!ctx.IsFirstConst && ctx.StartIndex == 0)
            {
                var span = new ReadOnlySpan<byte>(mBytes);
                ctx.StartIndex =  ctx.GetIndex(span);
            }
        }


        public void Decode(FrameDecodeContext ctx)
        {
            // 1. 尝试读 N 个字节（如果不够，ReadBytes 会抛 DataNotEnough，Splitter 会停下来等）
            byte[] actual = ctx.ReadBytes(mBytes.Length);

            // 2. 检查内容
            if (!actual.SequenceEqual(mBytes))
            {
                // 内容不对，抛出不匹配异常，Splitter 会丢弃开头字节并重试
                throw new ProtocolMismatchException();
            }
        }

        public byte[] GetConstData()
        {
            return mBytes;
        }
    }
}
