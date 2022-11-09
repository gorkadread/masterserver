using System;

namespace MasterServer {
    internal class DisconnectedClient {
        string _username;
        GameHost _disconnectedFromServer;
        string _token;
        int _gameId;

        public string Username
        {
            get
            {
                return _username;
            }

            set
            {
                _username = value;
            }
        }

        internal GameHost DisconnectedFromServer
        {
            get
            {
                return _disconnectedFromServer;
            }

            set
            {
                _disconnectedFromServer = value;
            }
        }

        public string Token {
            get => _token;
            set => _token = value;
        }

        public int GameId {
            get => _gameId;
            set => _gameId = value;
        }

        public DisconnectedClient(string clientName, GameHost disconnectedFromServer, string token, int gameId) {
            Username = clientName;
            DisconnectedFromServer = disconnectedFromServer;
            Token = token;
            GameId = gameId;
        }
    }
}
