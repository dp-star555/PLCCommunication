using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 用于校验的类型
    /// </summary>
    public enum CheckType
    {
        CRC16,
        SUM8,
        XOR8
    }

    /// <summary>
    /// 用于校验的模组，可以配置校验类型
    /// </summary>
    public sealed class CheckModule : IFrameModule
    {
        private readonly string mFromMark;
        private readonly string mToMark;
        private int mPatchPos;
        private int mLength;
        private readonly string mName;

        private CheckType mCheckType;

        public CheckModule(string fromMark, string toMark, CheckType checkType, int byteNum, string name = "")
        {
            mFromMark = fromMark;
            mToMark = toMark;
            mName = name;

            switch (checkType)
            {
                case CheckType.CRC16:
                    mLength = 2;
                    break;
                case CheckType.SUM8:
                    mLength = 1;
                    break;
                case CheckType.XOR8:
                    mLength = 1;
                    break;
                default:
                    mLength = byteNum;
                    break;
            }
        }
        public int Length => mLength;
        public string Name => mName;

        public void Decode(FrameDecodeContext ctx)
        {
            throw new NotImplementedException();
        }

        public void Encode_Emit(FrameWriteContext ctx)
        {
            mPatchPos = ctx.Reserve(Length);
        }
        public void Encode_Fixup(FrameWriteContext ctx)
        {
            var data = ctx.Slice(mFromMark, mToMark);
            ushort crc = 0;//校验的代码实现 Crc16.ComputeChecksum(span);
            byte[] bytes = new byte[2];
            bytes[0] = (byte)(crc & 0xFF);
            bytes[1] = (byte)(crc >> 8);
            ctx.Patch(mPatchPos, bytes);
        }

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
