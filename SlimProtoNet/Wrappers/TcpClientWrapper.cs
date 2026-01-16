using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SlimProtoNet.Wrappers
{
    public class TcpClientWrapper : IDisposable
    {
        private TcpClient _tcpClient;
        private bool _disposedValue;

        public TcpClientWrapper()
        {
            _tcpClient = new TcpClient();
        }

        public virtual bool Connected => _tcpClient.Connected;

        public virtual Task ConnectAsync(IPAddress ipAddress, int port)
        {
            return _tcpClient.ConnectAsync(ipAddress, port);
        }

        public virtual Stream GetStream()
        {
            return _tcpClient.GetStream();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _tcpClient.Dispose();
                    _tcpClient = null!;
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
