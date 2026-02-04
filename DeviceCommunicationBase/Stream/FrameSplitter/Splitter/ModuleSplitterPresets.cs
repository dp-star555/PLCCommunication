using DeviceCommunicationBase.Stream.FrameSplitter.SplitModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream.FrameSplitter.Splitter
{
    /// <summary>
    /// 模组分帧器的预设工厂
    /// </summary>
    public static class ModuleSplitterPresets
    {
        /// <summary>
        ///  | 固定Size |
        /// </summary>
        /// <param name="size">固定长度</param>
        /// <returns></returns>
        public static ModuleSplitter FixedSize(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException($"{size} 不是固定长度分帧器的允许的大小。");

            return new ModuleSplitter()
                .Add(new SizeModule(size));
        }

        /// <summary> 
        /// | 从0开始不定长数据 | | 包尾 |
        /// </summary>
        /// <param name="delimiter">包尾的byte指示</param>
        /// <returns></returns>
        public static ModuleSplitter Delimited(byte[] delimiter)
        {
            return new ModuleSplitter()
                .Add(new SizeModule())
                .Add(new DelimiterModule(delimiter));
        }

        /// <summary>
        /// | 包头 | | 定长数据 |
        /// </summary>
        /// <param name="header">包头的byte指示</param>
        /// <param name="size">定长数据的大小</param>
        /// <returns></returns>
        public static ModuleSplitter HeaderFixed(byte[] header, int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException($"{size} 不是定长数据的允许的大小。");

            return new ModuleSplitter()
                .Add(new HeaderModule(header))
                .Add(new SizeModule(size));
        }

        /// <summary>
        /// | 包头 | | 定长数据 | | 包尾 |
        /// </summary>
        /// <param name="header">包头的byte指示</param>
        /// <param name="size">定长数据的大小</param>
        /// <param name="tail">包尾的byte指示</param>
        /// <returns></returns>
        public static ModuleSplitter HeaderFixedTail(byte[] header, int size, byte[] tail)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException($"{size} 不是定长数据的允许的大小。");

            return new ModuleSplitter()
                .Add(new HeaderModule(header))
                .Add(new SizeModule(size))
                .Add(new DelimiterModule(tail));
        }

        /// <summary>
        /// | 包头 | | 不定长数据 | | 包尾 |
        /// </summary>
        /// <param name="header">包头的byte指示</param>
        /// <param name="delimiter">包尾的byte指示</param>
        /// <returns></returns>
        public static ModuleSplitter HeaderDelimited(byte[] header, byte[] delimiter)
        {
            return new ModuleSplitter()
                .Add(new HeaderModule(header))
                .Add(new SizeModule(-1))          // 不定长
                .Add(new DelimiterModule(delimiter));
        }
    }
}
