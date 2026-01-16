using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Port
{
    internal class CommPortManager
    {
        private readonly ConcurrentDictionary<string, ICommPort> mPorts = new ConcurrentDictionary<string, ICommPort>();

        public bool AddPort(string portName, ICommPort port)
        {
            return mPorts.TryAdd(portName, port);
        }

        public ICommPort GetPort(string portName)
        {
            if (mPorts.TryGetValue(portName, out var port))
            {
                return port;
            }
            throw new Exception($"找不到名为 [{portName}] 的通信口，请检查是否已添加。");
        }

        public bool RemovePort(string portName)
        {
            return mPorts.TryRemove(portName, out _);
        }
    }
}
