using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream.FrameSplitter
{
    /// <summary>
    /// 长度模组，用于记录长度块，初始化默认-1就是不定长的数据
    /// </summary>
    public sealed class SizeModule : ISplitModule
    {
        private readonly int mSize;
        public SizeModule(int size = -1) => mSize = size;
        public bool IsVariable => mSize <= 0;
        public E_SplitResult Apply(ref FrameSplitContext ctx)
        {
            if (mSize > 0)
            {
                if (!ctx.AddFixedLength(mSize))
                    return E_SplitResult.NeedMore;
                else
                    return E_SplitResult.Ok;
            }
            else
            {
                ctx.MarkVariable(); // size<=0 视为不定长
                return E_SplitResult.Ok;
            }
        }
    }
}
