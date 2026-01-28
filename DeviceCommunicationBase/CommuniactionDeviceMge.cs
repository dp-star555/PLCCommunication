using CommunicationBase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase
{
    public class CommunicationDeviceMge
    {
        private readonly ConcurrentDictionary<string, ICommunication> mPorts = new ConcurrentDictionary<string, ICommunication>();

        public bool AddPort(string portName, ICommunication device)
        {
            return mPorts.TryAdd(portName, device);
        }

        public ICommunication GetPort(string deviceName)
        {
            if (mPorts.TryGetValue(deviceName, out var port))
            {
                return port;
            }
            throw new Exception($"找不到名为 [{deviceName}] 的通信口，请检查是否已添加。");
        }

        public bool RemovePort(string deviceName)
        {
            return mPorts.TryRemove(deviceName, out _);
        }
    }
}
