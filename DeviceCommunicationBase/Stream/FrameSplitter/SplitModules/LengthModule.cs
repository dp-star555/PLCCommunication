using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream.FrameSplitter.SplitModules
{
    public class LengthModule : ISplitModule
    {
        private readonly bool mLittleEndian;
        private readonly int mLength;

        public LengthModule(int byteNum = 2, bool littleEndian = true) 
        {
            mLength = byteNum;
            mLittleEndian = littleEndian;
        }

        public E_SplitResult Apply(ref FrameSplitContext ctx)
        {
            ReadOnlySpan<byte> bytes = ctx.SliceFromStart( ctx.FixedLengthSoFar, mLength);
            int number = Convert.ToInt32( bytes.ToUInt(mLittleEndian));
            if (!ctx.AddFixedLength(mLength) || !ctx.AddFixedLength(number))
            {
                return E_SplitResult.NeedMore;
            }
            else 
            {
                return E_SplitResult.Ok;
            }
        }
    }
}
