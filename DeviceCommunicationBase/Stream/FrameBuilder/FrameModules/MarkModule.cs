using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    public enum MarkType
    {
        Length,
        Check
    }
    /// <summary>
    /// 标签模组：用于标记位置起始
    /// </summary>
    public sealed class MarkModule : IFrameModule
    {
        private readonly string mName;
        public MarkModule(MarkType mtype, string name)
        {
            mName = name;
            MarkType = mtype;
        }

        public MarkType MarkType { get; }
        public int Length => 0;
        public string Name => mName;

        public void Decode(FrameDecodeContext ctx)
        {
            throw new NotImplementedException();
        }

        public void Encode_Emit(FrameWriteContext ctx) => ctx.Mark(mName);
        public void Encode_Fixup(FrameWriteContext ctx) { }

        public byte[] GetConstData()
        {
            throw new NotImplementedException();
        }

        public void Scan(FrameDecodeContext ctx)
        {
            throw new NotImplementedException();
        }
    }
}
