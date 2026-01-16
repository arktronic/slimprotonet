namespace SlimProtoNet.Wrappers
{
    public class TcpClientFactory
    {
        public virtual TcpClientWrapper CreateTcpClient()
        {
            return new TcpClientWrapper();
        }
    }
}
