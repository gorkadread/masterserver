using Gameplay;
using System;
using System.Collections.Generic;

namespace MasterServer {
    internal class ClientUser {
        Client _client;
        string _username;
        string _password;
        int _level = 1;
        Guid _sessionKey;
        Int32 _sessionValidUntil = 0;
        int chosenHero;
        Maps selectedMap;

        /// <summary>
        /// TODO This should be fetched from the future DB
        /// </summary>
        List<int> unlockedHeroes = new List<int> { };

        internal Client Client
        {
            get
            {
                return _client;
            }

            set
            {
                _client = value;
            }
        }

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

        public string Password
        {
            get
            {
                return _password;
            }

            set
            {
                _password = value;
            }
        }

        public int Level
        {
            get
            {
                return _level;
            }

            set
            {
                _level = value;
            }
        }

        public Guid SessionKey
        {
            get
            {
                return _sessionKey;
            }

            set
            {
                _sessionKey = value;
            }
        }

        public Int32 SessionValidUntil {
            get {
                return _sessionValidUntil;
            }

            set {
                _sessionValidUntil = value;
            }
        }

        /// <summary>
        /// This is updated on every queued game
        /// </summary>
        public int ChosenHero { get => chosenHero;
            set {
                if (!UnlockedHeroes.Contains(value))
                {
                    // The hero has not unlocked the requested hero
                    // INITIATE HORRIBLE PUNISHMENT!
                    // TODO Handle this
                    chosenHero = UnlockedHeroes[0];
                } else
                {
                    chosenHero = value;
                }
            }
        }
        public Maps SelectedMap { get => selectedMap; set => selectedMap = value; }
        public List<int> UnlockedHeroes { get => unlockedHeroes; set => unlockedHeroes = value; }

        public ClientUser(string username, string password) {
            Username = username;
            Password = password;
            Level = 1;
            Random r = new Random();
            int numberOfRandomlyUnlockedHeroes = r.Next(1, 5);
            for(int i = 0; i < numberOfRandomlyUnlockedHeroes; i++)
            {
                UnlockedHeroes.Add(r.Next(1, 100));
            }
        }
    }
}
