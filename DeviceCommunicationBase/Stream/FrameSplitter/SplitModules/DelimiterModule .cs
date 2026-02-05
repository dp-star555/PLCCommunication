using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace DeviceCommunicationBase.Stream.FrameSplitter.SplitModules
{
    /// <summary>
    /// 结束符模块
    /// </summary>
    public sealed class DelimiterModule : ISplitModule
    {
        private readonly byte[] mDelimiter;
        public DelimiterModule(byte[] delimiter) => mDelimiter = delimiter;

        public E_SplitResult Apply(ref FrameSplitContext ctx)
        {
            // 结束符必须存在
            if (mDelimiter == null || mDelimiter.Length == 0) 
                throw new Exception("结束符异常，请检索结束符");

            if (!ctx.HasVariableBefore)
            {
                // 前文全定长：结束符必须紧跟在固定长度之后
                var span = ctx.SliceFromStart(ctx.FixedLengthSoFar, mDelimiter.Length);
                if (span.Length == 0) return E_SplitResult.NeedMore;

                // 严格匹配
                if (span.SequenceEqual(mDelimiter))
                {
                    int frameLen = ctx.FixedLengthSoFar + mDelimiter.Length;
                    ctx.TrySetFrameLength(frameLen);
                    return E_SplitResult.Ok;
                }
                // 否则失败，不允许“中间空隙”
                return E_SplitResult.BadAlign;
            }
            else
            {
                // 前文存在不定长：结束符需要搜索
                int searchStart = ctx.StartIndex + ctx.FixedLengthSoFar;
                int idx = ctx.IndexOf(mDelimiter, searchStart);
                if (idx >= 0)
                {
                    int frameLen = (idx - ctx.StartIndex) + mDelimiter.Length;
                    ctx.TrySetFrameLength(frameLen);
                    return E_SplitResult.Ok;
                }
                return E_SplitResult.NeedMore;
            }
        }
    }
}
