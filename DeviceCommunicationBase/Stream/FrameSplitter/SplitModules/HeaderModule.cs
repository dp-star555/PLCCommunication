using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream.FrameSplitter
{
    /// <summary>
    /// 用于指示帧头的模组
    /// </summary>
    public class HeaderModule : ISplitModule
    {
        private readonly byte[] mHeader;

        public HeaderModule(byte[] header)
        {
            mHeader = header;
        }

        public E_SplitResult Apply(ref FrameSplitContext ctx)
        {
            int idx = ctx.IndexOf(mHeader, ctx.StartIndex);
            if (idx < 0) return E_SplitResult.NeedMore;

            bool set = ctx.TrySetStart(idx);
            if (set)
                ctx.AddFixedLength(mHeader.Length);
            else
                throw new Exception("帧头模组添加长度失败，请检查数据。");
            return E_SplitResult.Ok;
        }
    }
}
