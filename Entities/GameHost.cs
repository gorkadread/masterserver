using System.Collections.Generic;
using System.Net;

namespace MasterServer {
    internal class GameHost {
        long _hostId;
        IPEndPoint _internalHostAddress;
        IPEndPoint _externalHostAddress;
        int _numberOfSlotsAvailable = 10;
        long _validUntil = 0;
        List<DisconnectedClient> _disconnectedClients = new List<DisconnectedClient>();
        bool _busy;
        long _busyWasSetAt = 0;
        int gameId = 0;
        Maps map;

        public long HostId
        {
            get
            {
                return _hostId;
            }

            set
            {
                _hostId = value;
            }
        }

        public IPEndPoint InternalHostAddress
        {
            get
            {
                return _internalHostAddress;
            }

            set
            {
                _internalHostAddress = value;
            }
        }

        public IPEndPoint ExternalHostAddress
        {
            get
            {
                return _externalHostAddress;
            }

            set
            {
                _externalHostAddress = value;
            }
        }

        public int NumberOfSlotsAvailable
        {
            get
            {
                return _numberOfSlotsAvailable;
            }

            set
            {
                _numberOfSlotsAvailable = value;
            }
        }

        public List<DisconnectedClient> DisconnectedClients
        {
            get
            {
                return _disconnectedClients;
            }

            set
            {
                _disconnectedClients = value;
            }
        }

        public long ValidUntil {
            get {
                return _validUntil;
            }

            set {
                _validUntil = value;
            }
        }

        public bool Busy { get => _busy; set => _busy = value; }
        public long BusyWasSetAt { get => _busyWasSetAt; set => _busyWasSetAt = value; }
        public int GameId { get => gameId; set => gameId = value; }
        public Maps Map { get => map; set => map = value; }

        public GameHost(long hostId, IPEndPoint internalHostAddress, IPEndPoint externalHostAddress, long validUntil, int numberOfSlotsAvailable = 0) {
            HostId = hostId;
            InternalHostAddress = internalHostAddress;
            ExternalHostAddress = externalHostAddress;
            NumberOfSlotsAvailable = numberOfSlotsAvailable;
            ValidUntil = validUntil;
        }
    }
}
