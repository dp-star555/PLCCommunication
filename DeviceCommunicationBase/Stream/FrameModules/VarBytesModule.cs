using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 数据模组：用于写入可变长度的数据
    /// </summary>
    public sealed class VarBytesModule : IFrameModule
    {
        private readonly Func<byte[]> mGetBytes;

        public VarBytesModule(Func<byte[]> getBytes)
        {
            mGetBytes = getBytes;
        }

        public int Length => mGetBytes().Length;

        public void Decode(FrameDecodeContext ctx)
        {
            throw new NotImplementedException();
        }

        public void Emit(FrameBuildContext ctx)
        {
            ctx.WriteBytes(mGetBytes());
        }

        public void Fixup(FrameBuildContext ctx) { }

        public byte[] GetConstData()
        {
            return null;
        }

        public void Scan(FrameDecodeContext ctx)
        {
            throw new NotImplementedException();
        }
    }
}
