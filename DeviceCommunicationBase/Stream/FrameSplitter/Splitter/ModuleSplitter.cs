using DeviceCommunicationBase.Stream.FrameSplitter.SplitModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace DeviceCommunicationBase.Stream.FrameSplitter
{
    public class ModuleSplitter : IFrameSplitter
    {
        private readonly SlidingBuffer mBuffer = new SlidingBuffer();
        private readonly List<ISplitModule> mModules = new List<ISplitModule>();

        private bool mIsValidated = false;

        public ModuleSplitter Add(ISplitModule module)
        {
            mModules.Add(module);
            return this;
        }

        public event Action<ReadOnlyMemory<byte>> FrameCompleted;

        public void Feed(ReadOnlySpan<byte> data)
        {
            // 首次校验模组规则
            if (!mIsValidated)
            {
                Validate(mModules);
                mIsValidated = true;
            }

            // 1) 追加数据到缓冲
            mBuffer.Append(data);

            // 循环拆帧
            while (true)
            {
                if (mBuffer.Count == 0) return;

                // 创建 Context
                var ctx = new FrameSplitContext(mBuffer.Span);

                E_SplitResult result = E_SplitResult.Ok;
                foreach (var m in mModules)
                {
                    result = m.Apply(ref ctx);
                    if (result == E_SplitResult.BadAlign)
                    {
                        break; 
                    }
                    if (result == E_SplitResult.NeedMore)
                        break;
                }

                if (result == E_SplitResult.BadAlign)
                {
                    mBuffer.Consume(ctx.StartIndex + 1);
                    continue;
                }
                if (result == E_SplitResult.NeedMore)
                    break;

                // 所有模块 Apply 完成后
                if (!ctx.HasVariableBefore && ctx.FrameLength <= 0)
                {
                    ctx.TrySetFrameLength(ctx.FixedLengthSoFar);
                }

                if (!ctx.HasFullFrame())
                    break;

                //切出完整帧
                int frameLen = ctx.FrameLength;
                var frame = mBuffer.CopyFrame(ctx.StartIndex, frameLen);

                mBuffer.Consume(ctx.StartIndex + frameLen);

                //触发回调
                FrameCompleted?.Invoke(frame);
            }
        }

        public void Reset()
        {
            mBuffer.Clear();
        }

        private void Validate(IReadOnlyList<ISplitModule> modules)
        {
            if (modules == null || modules.Count == 0)
                throw new InvalidOperationException("Split modules cannot be empty.");

            int headerIndex = FindIndex<HeaderModule>(modules);
            int tailIndex = FindIndex<DelimiterModule>(modules);
            int variableIndex = FindVariableSizeIndex(modules); // size == -1

            if (headerIndex >= 0 && headerIndex != 0)
                throw new InvalidOperationException("HeaderModule must be the first module.");

            if (tailIndex >= 0 && tailIndex != modules.Count - 1)
                throw new InvalidOperationException("DelimiterModule must be the last module.");

            if (variableIndex >= 0 && tailIndex < 0)
                throw new InvalidOperationException("Size=-1 requires DelimiterModule at the end.");
        }

        private int FindIndex<T>(IReadOnlyList<ISplitModule> modules)
        {
            for (int i = 0; i < modules.Count; i++)
                if (modules[i] is T) return i;
            return -1;
        }

        private int FindVariableSizeIndex(IReadOnlyList<ISplitModule> modules)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is SizeModule sm && sm.IsVariable) 
                    return i;
            }
            return -1;
        }
    }
}
