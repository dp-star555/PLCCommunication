using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 长度模组：用于回填某段数据的长度
    /// </summary>
    public sealed class LengthModule : IFrameModule
    {
        private readonly string mName;

        private readonly string mFromMark;
        private readonly string mToMark;
        private readonly bool mLittleEndian;
        private int mPatchPos;
        private int mLength;

        public LengthModule(string fromMark, string toMark, int byteNum = 2, string name = "", bool littleEndian = true)
        {
            mFromMark = fromMark;
            mToMark = toMark;
            mLength = byteNum;
            mLittleEndian = littleEndian;
            mName = name;
        }

        public int Length => mLength;
        public string Name => mName;
        public void Encode_Emit(FrameWriteContext ctx)
        {
            mPatchPos = ctx.Reserve(Length);
        }

        public void Encode_Fixup(FrameWriteContext ctx)
        {
            var length = ctx.Slice(mFromMark, mToMark).Length;
            byte[] bytes = new byte[Length];
            switch (Length)
            {
                case 1:
                    bytes[0] = (byte)(length & 0xFF);
                    break;
                case 2:
                    if (mLittleEndian)
                    {
                        bytes[0] = (byte)(length & 0xFF);
                        bytes[1] = (byte)(length >> 8);
                    }
                    else
                    {
                        bytes[0] = (byte)(length >> 8);
                        bytes[1] = (byte)(length & 0xFF);
                    }
                    break;
                case 4:
                    if (mLittleEndian)
                    {
                        bytes[0] = (byte)(length & 0xFF);
                        bytes[1] = (byte)(length >> 8);
                        bytes[2] = (byte)(length >> 16);
                        bytes[3] = (byte)(length >> 24);
                    }
                    else
                    {
                        bytes[0] = (byte)(length >> 24);
                        bytes[1] = (byte)(length >> 16);
                        bytes[2] = (byte)(length >> 8);
                        bytes[3] = (byte)(length & 0xFF);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Length overflow: {length}, range={mFromMark}->{mToMark}");
            }
            ctx.Patch(mPatchPos, bytes);
        }

        public void Decode(FrameDecodeContext ctx)
        {
            //// 1. 从上下文获取之前 LengthModule 解析出的长度
            //int len = ctx.GetVar<int>(mLengthRefKey);
            //int realLen = len + mFixLen;

            //if (realLen < 0) throw new ProtocolMismatchException(); // 长度不可能为负

            //// 2. 尝试读 Body（如果不够，抛 DataNotEnough，Splitter 等待）
            //byte[] data = ctx.ReadBytes(realLen);

            //// 3. 存入结果
            //ctx.SetVar(mStoreKey, data);
        }

        public void Scan(FrameDecodeContext ctx)
        {
            throw new NotImplementedException();
        }

        public byte[] GetConstData()
        {
            return null;
        }
    }
}
