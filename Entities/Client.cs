using System.Net;

namespace MasterServer {
    class Client {
        IPEndPoint _internalAddress;
        IPEndPoint _externalAddress;
        string _token;

        public IPEndPoint InternalAddress
        {
            get
            {
                return _internalAddress;
            }

            set
            {
                _internalAddress = value;
            }
        }

        public IPEndPoint ExternalAddress
        {
            get
            {
                return _externalAddress;
            }

            set
            {
                _externalAddress = value;
            }
        }

        public string Token
        {
            get
            {
                return _token;
            }

            set
            {
                _token = value;
            }
        }

        public Client(IPEndPoint internalAddress, IPEndPoint externalAddress, string token) {
            InternalAddress = internalAddress;
            ExternalAddress = externalAddress;
            Token = token;
        }
    }
}
