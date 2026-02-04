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
        private readonly string mName;


        public VarBytesModule(Func<byte[]> getBytes, string name = "")
        {
            mGetBytes = getBytes;
            mName = name;
        }

        public int Length => mGetBytes().Length;
        public string Name => mName;

        public void Decode(FrameDecodeContext ctx)
        {
            throw new NotImplementedException();
        }

        public void Encode_Emit(FrameWriteContext ctx)
        {
            ctx.Write(mGetBytes());
        }

        public void Encode_Fixup(FrameWriteContext ctx) { }

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
