using System;
using System.Collections.Generic;
using Lidgren.Network;
using System.Net;
using MSCommon;
using Helpers;
using System.Linq;
using System.Timers;

namespace MasterServer {
    class Program {
        static NetPeer peer;

        // List of currently active servers
        static List<GameHost> registeredHosts = new List<GameHost>();
        
        // Debugging explicit or not
        static bool verbose = false;

        // TODO In the future a DB should hold all heroes, but for now, we'll set em up here
        static Heroes heroes = new Heroes(100);

        static void Main(string[] args) {
            

            // List of registered clients that can login
            List<ClientUser> registeredClients = new List<ClientUser>();

            // Dummyregistering a client
            registeredClients.Add(new ClientUser("dread", ""));

            // List of clients that are currently logged in
            List<ClientUser> loggedInClients = new List<ClientUser>();

            // List of clients queueing for a game
            List<Client> clientsQueing = new List<Client>();

            // List of clients that have queued and have received a spot, but need to answer before they're let into the gameserver in case they've already quit.
            List<Client> clientsAwaitingQueueVerification = new List<Client>();

            // List of clients that have logged in and have joined a gameserver but have disconnected or quit before the game finished. 
            List<DisconnectedClient> disconnectedClients = new List<DisconnectedClient>();

            NetPeerConfiguration config = new NetPeerConfiguration("masterserver");
            config.SetMessageTypeEnabled(NetIncomingMessageType.UnconnectedData, true);
            config.Port = CommonConstants.MasterServerPort;

            peer = new NetPeer(config);
            peer.Start();
            PrintMessage("Master server up and running...");

            // Set timer to clean the registered hosts list
            Timer aTimer = new Timer(30000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += CheckForInactiveGameServers;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;

            // Key entered by the user
            ConsoleKeyInfo cki;

            // Shutdown variable. Set when user has pressed Q or Escape once. If they then enter Y, the shutdown variable will be set and the server will shutdown.
            bool shutdownInitiated = false;
            bool shutdown = false;

            // keep going until ESCAPE is pressed
            PrintMessage("Press M for menu and ESC to quit");
            while (!shutdown) {
                NetIncomingMessage msg;
                TimeSpan timespan = DateTime.UtcNow - new DateTime(1970, 1, 1);
                while ((msg = peer.ReadMessage()) != null) {
                    switch (msg.MessageType) {
                        case NetIncomingMessageType.UnconnectedData:
                            //
                            // We've received a message from a client or a host
                            //
                            // by design, the first byte always indicates action
                            switch ((MasterServerMessageType)msg.ReadByte()) {
                                case MasterServerMessageType.RegisterHost:

                                    // It's a host wanting to register its presence
                                    var id = msg.ReadInt64(); // server unique identifier
                                    var thisServer = registeredHosts.Find(item => item.HostId == id);
                                    var gameServerIPEndPoint = msg.ReadIPEndPoint();
                                    var gameServerSenderEndPoint = msg.SenderEndPoint;
                                    var connectedClients = msg.ReadInt32();

                                     if (thisServer == null) {
                                        PrintMessage("Found no server with id: " + id + ", adding server.");
                                        long validUntil = (long)timespan.TotalMilliseconds + CommonConstants.serverReportTimeout;
                                        thisServer = new GameHost(id, gameServerIPEndPoint, gameServerSenderEndPoint, validUntil, CommonConstants.MaxClientCount - connectedClients);
                                        registeredHosts.Add(thisServer);
                                        PrintMessage("Servers active:" + registeredHosts.Count);
                                        PrintMessage("------------------------------------");
                                        for(int i = 0; i < registeredHosts.Count; i++) {
                                            PrintMessage("Server " + i + ", id: " + registeredHosts[i].HostId);
                                            PrintMessage("InternalHostAddress: " + registeredHosts[i].InternalHostAddress);
                                            PrintMessage("ExternalHostAddress: " + registeredHosts[i].ExternalHostAddress);

                                            var count = registeredHosts[i].DisconnectedClients.Count;
                                            var dissedClients = "";
                                            var disconnectedClientsMessage = "";
                                            if(count > 0)
                                            {
                                                foreach (DisconnectedClient disconnectedClient in registeredHosts[i].DisconnectedClients)
                                                {
                                                    if(dissedClients.Length > 0)
                                                    {
                                                        dissedClients += ", ";
                                                    }
                                                    dissedClients += disconnectedClient.Username;
                                                }

                                                disconnectedClientsMessage = "DisconnectedClients: " + registeredHosts[i].DisconnectedClients.Count + "(" + dissedClients + ")";
                                            } else
                                            {
                                                disconnectedClientsMessage = "DisconnectedClients: " + registeredHosts[i].DisconnectedClients.Count;
                                            }
                                            PrintMessage(disconnectedClientsMessage);
                                            PrintMessage("Number of slots available: " + registeredHosts[i].NumberOfSlotsAvailable);
                                            PrintMessage("Valid until: " + registeredHosts[i].ValidUntil + ", which is " + (registeredHosts[i].ValidUntil - (long)timespan.TotalMilliseconds)/1000 + " seconds from now");
                                        }
                                        PrintMessage("Gameserver " + gameServerIPEndPoint.Address.ToString() + ":" + gameServerIPEndPoint.Port.ToString() + "(" + id + ") registered as available.");
                                    }
                                    else {
                                        var time = (long)timespan.TotalMilliseconds + CommonConstants.serverReportTimeout;
                                        thisServer.ValidUntil = time;
                                        thisServer.NumberOfSlotsAvailable = CommonConstants.MaxClientCount - connectedClients;
                                        if(verbose)
                                        {
                                            PrintMessage("Gameserver " + thisServer.HostId + " was already registered, but reported in.");
                                        }
                                    }

                                    registeredHosts = registeredHosts.OrderByDescending(o => o.NumberOfSlotsAvailable).ToList();

                                    if (clientsQueing.Count > 0) {

                                        int slotsReady = thisServer.NumberOfSlotsAvailable;

                                        for (int i = 0; i < slotsReady; i++) {
                                            if (clientsQueing.Count == 0) {
                                                break;
                                            }

                                            clientsAwaitingQueueVerification.Add(clientsQueing[0]);
                                            RequestQueueVerification(clientsQueing[0]);
                                            clientsQueing.RemoveAt(0);
                                        }

                                        PrintMessage("Queuepositions have changed, notify queueing clients.");

                                        // Return the new position in the queue to queueing clients
                                        for (int j = 0; j < clientsQueing.Count; j++) {
                                            ReturnPositionInQueue(clientsQueing[j].ExternalAddress, clientsQueing.Count >= 1 ? clientsQueing.Count : 1);
                                        }
                                    }
                                    break;
                                case MasterServerMessageType.NotifyMasterOfClientCount:
                                    var serverId = msg.ReadInt64(); // server unique identifier
                                    var server = registeredHosts.Find(item => item.HostId == serverId);
                                    int clientCount = msg.ReadInt32();
                                    server.NumberOfSlotsAvailable = CommonConstants.MaxClientCount - clientCount;
                                    server.ValidUntil = (long)timespan.TotalMilliseconds + CommonConstants.serverReportTimeout;
                                    PrintMessage("Gameserver " + serverId + " updating it's clientCount. There are now " + server.NumberOfSlotsAvailable + " slots available at that server.");
                                    break;
                                case MasterServerMessageType.GameIsStarting:
                                    // Gameserver has enough players to start. Set it to busy
                                    serverId = msg.ReadInt64();
                                    server = registeredHosts.Find(item => item.HostId == serverId);
                                    server.Busy = true;
                                    var currentTime = (long)timespan.TotalMilliseconds;
                                    server.BusyWasSetAt = currentTime;
                                    server.ValidUntil = (long)timespan.TotalMilliseconds + CommonConstants.serverReportTimeout;
                                    PrintMessage("Gameserver " + serverId + " starting a new game and is from now on set to busy.");
                                    break;
                                case MasterServerMessageType.NotifyMasterOfGameFinished:
                                    // Game has finished on gameserver. Remove disconnected clients from the disconnected clients list,
                                    // set gameserver to ready and later on, report statistics
                                    serverId = msg.ReadInt64();
                                    server = registeredHosts.Find(item => item.HostId == serverId);
                                    if(server != null)
                                    {
                                        var gameNumber = msg.ReadInt16();
                                        server.GameId = gameNumber++;

                                        // Clear disconnected clients from the list associated with this server since the game has finished
                                        server.DisconnectedClients.Clear();
                                        server.NumberOfSlotsAvailable = CommonConstants.MaxClientCount;
                                        server.Busy = false;
                                        TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                                        server.ValidUntil = (long)t.TotalMilliseconds + CommonConstants.serverReportTimeout;
                                        PrintMessage("Server " + serverId + " has finished a game. Clearing all data related to that game.");
                                    } else
                                    {
                                        PrintMessage("Server " + serverId + " has finished a game, but was not found among the registered servers. It's probably been purged since the game-session took to long for some reason. Do nothing and let it re-register itself the normal way");
                                    }
                                    
                                    break;
                                case MasterServerMessageType.NotifyMasterOfDisconnectedClient:
                                    serverId = msg.ReadInt64();
                                    server = registeredHosts.Find(item => item.HostId == serverId);
                                    var clientName = msg.ReadString();
                                    var gameId = msg.ReadInt16();

                                    // Add disconnected client to the list of disconnected clients. Also update the reporting gameservers list of disconnected clients
                                    var c = registeredClients.Find(item => (item.Username == clientName));
                                    
                                    // This may occur if the gameserver has been removed from the serverlist since it didn't respond in time. Let it know it should register again and reset it's state
                                    if(server != null && c != null)
                                    {
                                        var dc = new DisconnectedClient(clientName, server, c.SessionKey.ToString(), gameId);
                                        disconnectedClients.Add(dc);
                                        server.DisconnectedClients.Add(dc);
                                        PrintMessage("Client " + clientName + " with sessionId: " + c.SessionKey + " disconnected from gameserver");
                                    } else
                                    {
                                        if(server == null)
                                        {
                                            PrintMessage("Server reported a disconnected client, but server isn't registered.");
                                            // TODO Handle this. Server should be notified it's old and reset everything. Masterserver should be master here.
                                        } else
                                        {
                                            PrintMessage("Server reported a disconnected client, but the client isn't registered.");
                                            // Ignore the disconnected client.             
                                        }
                                    }
                                    
                                    break;
                                case MasterServerMessageType.ClientReconnected:
                                    serverId = msg.ReadInt64();
                                    server = registeredHosts.Find(item => item.HostId == serverId);
                                    var clientSessionId = msg.ReadString();

                                    // Remove the reconnected client from the list of disconnected clients
                                    var disc = disconnectedClients.Find(item => (item.Token == clientSessionId));
                                    if(disc != null) {
                                        disconnectedClients.Remove(disc);
                                        PrintMessage("Client " + disc.Username + " with sessionId: " + disc.Token + " has reconnected to gameserver " + serverId);
                                    } else {
                                        PrintMessage("Gameserver " + serverId + " claims that user has reconnected but that user does not exist in the disconnected list, clientSessionId: " + clientSessionId);
                                    }

                                    // Update the requesting servers list of disconnected clients and remove this client
                                    disc = server.DisconnectedClients.Find(item => (item.Token == clientSessionId));
                                    PrintMessage("There are " + server.DisconnectedClients.Count + " disconnected clients reported on server " + server.HostId + " before the removal of client " + disc.Username);
                                    server.DisconnectedClients.Remove(disc);
                                    PrintMessage("Now there are " + server.DisconnectedClients.Count + " disconnected clients reported on server " + server.HostId);

                                    break;
                                case MasterServerMessageType.LoginRequest:
                                    // TODO This response should return all the possible heroes the player has to choose from
                                    IPEndPoint clientInternal = msg.ReadIPEndPoint();
                                    IPEndPoint senderEndPoint = msg.SenderEndPoint;
                                    var username = msg.ReadString().ToLower();

                                    if(username.Length == 0) {
                                        username = clientInternal.Address.ToString();
                                    }

                                    var password = msg.ReadString();
                                    PrintMessage("Loginrequest from username: " + username + ", password: " + password + ". Check if already registered");

                                    // Check if client is registered
                                    var registeredClient = registeredClients.Find(item => (item.Username == username ) && (item.Password == password));
                                    if (registeredClient != null) {

                                        // Check if client has a valid session, otherwise update the current one
                                        if (!IsClientSessionValid(registeredClient.SessionValidUntil))
                                        {
                                            // Clients session isn't valid anymore
                                            registeredClient.SessionKey = Guid.NewGuid();
                                            registeredClient.SessionValidUntil = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + CommonConstants.sessionLength;
                                        }

                                        var loggedInClient = loggedInClients.Find(item => (item.SessionKey == registeredClient.SessionKey));
                                        if(loggedInClient == null)
                                        {
                                            loggedInClients.Add(registeredClient);
                                        } 

                                        PrintMessage("Client was registered and is now logged in. Returning sessionKey: " + registeredClient.SessionKey);
                                        LoginResponse(registeredClient, msg);

                                        // If a client has disconnected, move them immediately to the game they disconnected from.
                                        CheckIfClientDisconnected(disconnectedClients, clientInternal, senderEndPoint, registeredClient.SessionKey.ToString());
                                        break;
                                    }
                                    else {
                                        // For now, register as a new client
                                        // TODO Setup a proper new user-flow. Create user, set password and points of contact, email for instance.
                                        // PrintMessage("Client does not exist. Returning errormessage");
                                        // LoginResponse(null, msg);
                                        var newClient = new ClientUser(username, password);
                                        registeredClients.Add(newClient);
                                        newClient.SessionKey = Guid.NewGuid();
                                        newClient.SessionValidUntil = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + CommonConstants.sessionLength;
                                        loggedInClients.Add(newClient);
                                        PrintMessage("Client was NOT registered but have been now and is logged in. Returning sessionKey: " + newClient.SessionKey);
                                        PrintMessage("Client has " + newClient.UnlockedHeroes.Count + " heroes unlocked.");
                                        LoginResponse(newClient, msg);
                                    }
                                    
                                    break;
                                case MasterServerMessageType.RequestToJoinGameQueue:
                                    // It's a client requesting a spot for a game
                                    clientInternal = msg.ReadIPEndPoint();
                                    senderEndPoint = msg.SenderEndPoint;
                                    string token = msg.ReadString();

                                    // Make sure the client is logged in
                                    var clientLoggedIn = loggedInClients.Find(item => item.SessionKey.ToString() == token);
                                    if(clientLoggedIn == null) {
                                        NetOutgoingMessage om = peer.CreateMessage();
                                        om.Write((byte)MasterServerResponseTypes.NoActiveSession);
                                        SendMessage(msg.SenderEndPoint, om, NetDeliveryMethod.ReliableUnordered);
                                        break;
                                    }

                                    // Hero the player has chosen to play this game with
                                    // TODO THis should map with unlocked heroes at the user, and result in some horrific punishment if they're sending in an id to a hero they
                                    // have not unlocked. Ofc.
                                    int playerChoice = msg.ReadInt32();
                                    clientLoggedIn.ChosenHero = playerChoice;

                                    // Map the player has chosen to play
                                    Maps map = (Maps)msg.ReadByte();
                                    clientLoggedIn.SelectedMap = map;

                                    // Check if there are any servers online
                                    if (registeredHosts.Count > 0) {
                                        bool needQueue = true;
                                        var serverOnlineWithThisMap = registeredHosts.Find(item => item.Map == map && item.NumberOfSlotsAvailable > 0);
                                        if (serverOnlineWithThisMap != null)
                                        {
                                            PrintMessage("Found a gameServer already online and awaiting players on the correct map; " + serverOnlineWithThisMap.ExternalHostAddress.Address.ToString() + ":" + serverOnlineWithThisMap.ExternalHostAddress.Port);
                                            peer.Introduce(
                                                serverOnlineWithThisMap.InternalHostAddress, // host internal
                                                serverOnlineWithThisMap.ExternalHostAddress, // host external
                                                clientInternal,                             // client internal
                                                msg.SenderEndPoint,                         // client external
                                                token                                       // request token
                                            );

                                            serverOnlineWithThisMap.NumberOfSlotsAvailable--;

                                            needQueue = false;
                                            break;
                                        } else
                                        {
                                            for (int i = 0; i < registeredHosts.Count; i++) {
                                                if (registeredHosts[i].NumberOfSlotsAvailable != 0) {
                                                    PrintMessage("Introducing " + clientLoggedIn.Username + ", sessionKey: " + clientLoggedIn.SessionKey  + " and adress: " + msg.SenderEndPoint.Address.ToString() + ":" + msg.SenderEndPoint.Port + " to gameServer " + registeredHosts[i].ExternalHostAddress.Address.ToString() + ":" + registeredHosts[i].ExternalHostAddress.Port);
                                                    peer.Introduce(
                                                        registeredHosts[i].InternalHostAddress, // host internal
                                                        registeredHosts[i].ExternalHostAddress, // host external
                                                        clientInternal,                         // client internal
                                                        msg.SenderEndPoint,                     // client external
                                                        token                                   // request token
                                                    );

                                                    registeredHosts[i].NumberOfSlotsAvailable--;

                                                    needQueue = false;
                                                    break;
                                                }
                                            }
                                        }

                                        if (needQueue) {
                                            // There are no spots available in the server/s. Check if client is already queued, otherwise add them
                                            var clientQueueing = clientsQueing.Find(item => item.Token.ToString() == token);
                                            if(clientQueueing == null) {
                                                clientsQueing.Add(new Client(clientInternal, msg.SenderEndPoint, token));
                                                PrintMessage("Client requested to play but no server has room. Added to queue. Currently there are " + clientsQueing.Count + " clients in queue");

                                                // Return that the player is queued and their position in queue
                                                ReturnPositionInQueue(clientInternal, clientsQueing.Count >= 1 ? clientsQueing.Count : 1);
                                            }
                                            else {
                                                int index = clientsQueing.FindIndex(item => item.Token.ToString() == token);
                                                PrintMessage("Client requested to play but no server has room. Client was already in queue and has resumed it's position at " + index);

                                                // Return that the player is queued and position in queue
                                                ReturnPositionInQueue(clientInternal, index >= 1 ? index : 1);
                                            }
                                        }
                                    }
                                    else {
                                        // No servers online. Queue client if a server appears, and let the client know there are no servers online. 
                                        var clientQueueing = clientsQueing.Find(item => item.Token.ToString() == token);
                                        if (clientQueueing == null) {
                                            clientsQueing.Add(new Client(clientInternal, msg.SenderEndPoint, token));
                                            PrintMessage("Client requested to play but there are no servers online. Added to queue. Currently there are " + clientsQueing.Count + " clients in queue");
                                        }
                                        else {
                                            int index = clientsQueing.FindIndex(item => item.Token.ToString() == token);
                                            PrintMessage("Client requested to play but there are no servers online. Client was already in queue and has resumed it's position at " + index);
                                        }

                                        ReturnNoServersOnline(clientInternal);
                                    }
                                    break;
                                case MasterServerMessageType.VerifyQueue:
                                    Client client = clientsAwaitingQueueVerification.Find(i => i.ExternalAddress.Equals(msg.SenderEndPoint));
                                    if(client != null) {
                                        for(int i = 0; i < registeredHosts.Count; i++) {
                                            if (registeredHosts[i].NumberOfSlotsAvailable > 0) {
                                                registeredHosts[i].NumberOfSlotsAvailable--;
                                                peer.Introduce(
                                                    registeredHosts[i].InternalHostAddress, // host internal
                                                    registeredHosts[i].ExternalHostAddress, // host external
                                                    client.InternalAddress, // client internal
                                                    client.ExternalAddress, // client external
                                                    client.Token // request token
                                                );

                                                clientsAwaitingQueueVerification.Remove(client);
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                case MasterServerMessageType.RequestClientDetails:
                                    // Client has logged in and connected to the gameserver who wants the details for that client
                                    serverId = msg.ReadInt64();

                                    token = msg.ReadString();
                                    PrintMessage("Gameserver with id: " + serverId + " requesting details of client that just connected to it. Verify it's a logged in client and then submit clientdetails connected to the submitted guid: " + token);
                                    
                                    // Make sure the client is logged in
                                    clientLoggedIn = loggedInClients.Find(item => item.SessionKey.ToString() == token);
                                    if(clientLoggedIn != null) {
                                        PrintMessage("Client with sessionId: " + token + " was logged in. Submit clientDetails to gameserver of id: " + serverId + " if gameserver is registered");

                                        bool foundHost = false;
                                        for (int i = 0; i < registeredHosts.Count; i++) {
                                            if (serverId == registeredHosts[i].HostId) {
                                                PrintMessage("ClientDetails requested by host " + registeredHosts[i].HostId + " which is a registered host. Send response");
                                                ReturnClientDetails(clientLoggedIn, registeredHosts[i].ExternalHostAddress);
                                                foundHost = true;
                                                break;
                                            }
                                        }

                                        if (!foundHost) {
                                            PrintMessage("Could not find requesting host among the registered hosts. Ignoring request");
                                        }
                                    }
                                    else {
                                        PrintMessage("Client with sessionId: " + token + " was not logged in. Send something to the gameserver to know it can kick the client out since it lacks a valid sessionkey");
                                    }

                                    break;
                                case MasterServerMessageType.Developer:
                                    // This is a login for a developer.
                                    // Setup all credentials and submit to a gameserver.
                                    clientInternal = msg.ReadIPEndPoint();
                                    senderEndPoint = msg.SenderEndPoint;

                                    PrintMessage("Devlogin from " + clientInternal);

                                    // Check if client is registered
                                    // Don't bother
                                    /*registeredClient = registeredClients.Find(item => (item.Username == username));
                                    if (registeredClient != null)
                                    {
                                        if (!VerifyClientSession(registeredClient.SessionValidUntil))
                                        {
                                            registeredClient.SessionKey = Guid.NewGuid();
                                            registeredClient.SessionValidUntil = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + sessionValid;
                                        }

                                        var loggedInClient = loggedInClients.Find(item => (item.SessionKey == registeredClient.SessionKey));
                                        if (loggedInClient == null)
                                        {
                                            loggedInClients.Add(registeredClient);
                                        }

                                        LoginResponse(registeredClient, msg);
                                        CheckIfClientDisconnected(disconnectedClients, clientInternal, senderEndPoint, registeredClient.SessionKey.ToString());
                                        break;
                                    }
                                    else
                                    {*/
                                        var devClient = new ClientUser("dev01", "");
                                        registeredClients.Add(devClient);
                                        devClient.SessionKey = Guid.NewGuid();
                                        devClient.SessionValidUntil = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds + CommonConstants.sessionLength;
                                        loggedInClients.Add(devClient);
                                        LoginResponse(devClient, msg);
                                    // }
                                    break;
                                case MasterServerMessageType.ClientDisconnected:
                                    // Client wants to disconnect from masterserver. No need to remove the sessionkey, we'll just set it to expire and then it will renew on next login,
                                    // and generate a new sessionkey. Then we just need to remove the client from the loggedInClients. 
                                    token = msg.ReadString();
                                    clientLoggedIn = loggedInClients.Find(item => item.SessionKey.ToString() == token);
                                    if (clientLoggedIn != null)
                                    {
                                        PrintMessage("Client " + token + " drifted off into nothingness. Welcome back later!");
                                        clientLoggedIn.SessionValidUntil = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                                        loggedInClients.Remove(clientLoggedIn);
                                        break;
                                    }
                                    break;
                            }
                            break;

                        case NetIncomingMessageType.DebugMessage:
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.WarningMessage:
                        case NetIncomingMessageType.ErrorMessage:
                            // print diagnostics message
                            PrintMessage(msg.ReadString());
                            break;
                    }
                }

                if (Console.KeyAvailable)
                {
                    cki = Console.ReadKey();
                    switch (cki.Key)
                    {
                        case ConsoleKey.M:
                            Console.WriteLine();
                            Console.WriteLine("Menu");
                            Console.WriteLine("-------------");
                            Console.WriteLine("s - List servers");
                            Console.WriteLine("p - List currently logged in players");
                            string v = verbose ? "On" : "Off";
                            Console.WriteLine("v - Toggle verbose mode (Currently: " + v + ")");
                            Console.WriteLine("esc/q - Quit");
                            Console.WriteLine("-------------");
                            break;
                        case ConsoleKey.S:
                            Console.WriteLine();
                            Console.WriteLine("List servers");
                            Console.WriteLine("-------------");
                            Console.WriteLine("There are currently " + registeredHosts.Count + " active gameservers");
                            for (int i = registeredHosts.Count; i > 0; i--)
                            {
                                Console.WriteLine("Id: " + registeredHosts[i - 1].HostId);
                                Console.WriteLine("Busy: " + registeredHosts[i - 1].Busy);
                                if (registeredHosts[i - 1].Busy)
                                {
                                    Console.WriteLine("Busy since: " + registeredHosts[i - 1].BusyWasSetAt);
                                }
                                Console.WriteLine("Available slots: " + registeredHosts[i - 1].NumberOfSlotsAvailable);
                                Console.WriteLine("Disconnected clients: " + registeredHosts[i - 1].DisconnectedClients.Count);
                                Console.WriteLine("ExternalHostAddress: " + registeredHosts[i - 1].ExternalHostAddress);
                                Console.WriteLine("InternalHostAddress: " + registeredHosts[i - 1].InternalHostAddress);
                                Console.WriteLine("Valid until: " + registeredHosts[i - 1].ValidUntil);
                            }

                            Console.WriteLine("-------------");
                            break;
                        case ConsoleKey.P:
                            Console.WriteLine();
                            Console.WriteLine("Currently logged in players");
                            Console.WriteLine("-------------");
                            Console.WriteLine("There are currently " + loggedInClients.Count + " players logged in");
                            for (int i = loggedInClients.Count; i > 0; i--)
                            {
                                Console.WriteLine("Username: " + loggedInClients[i - 1].Username);
                                Console.WriteLine("SessionKey: " + loggedInClients[i - 1].SessionKey);
                                Console.WriteLine("Session valid until: " + loggedInClients[i - 1].SessionValidUntil);
                                Console.WriteLine("Level: " + loggedInClients[i - 1].Level);
                                // Console.WriteLine("ExternalAddress: " + loggedInClients[i - 1].Client.ExternalAddress);
                                // Console.WriteLine("InternalAddress: " + loggedInClients[i - 1].Client.InternalAddress);
                                // Console.WriteLine("Token: " + loggedInClients[i - 1].Client.Token);
                            }
                            Console.WriteLine("-------------");
                            break;
                        case ConsoleKey.V:
                            Console.WriteLine();
                            var verboseMode = verbose ? "Off " : "On";
                            Console.WriteLine("-------------");
                            Console.WriteLine("Verbose-mode: " + verboseMode);
                            verbose = !verbose;
                            Console.WriteLine("-------------");
                            break;
                        case ConsoleKey.Escape:
                        case ConsoleKey.Q:
                            Console.WriteLine();
                            Console.WriteLine("-------------");
                            Console.WriteLine("Shutdown initiated. Are you certain you want to shutdown this masterserver? (y/n) ");
                            shutdownInitiated = true;
                            Console.WriteLine("-------------");
                            break;
                        case ConsoleKey.Y:
                            if (shutdownInitiated)
                            {
                                shutdown = true;
                            }

                            break;
                        case ConsoleKey.N:
                            if (shutdownInitiated)
                            {
                                shutdownInitiated = false;
                                Console.WriteLine();
                                Console.WriteLine("-------------");
                                Console.WriteLine("Shutdown canceled.");
                                shutdownInitiated = false;
                                Console.WriteLine("-------------");
                            }
                            break;
                        default:
                            Console.WriteLine();
                            Console.WriteLine("-------------");
                            Console.WriteLine("That input has no mapping. To view the menu, press m");
                            Console.WriteLine("-------------");
                            break;
                    }
                }

                peer.Recycle(msg);
            }

            peer.Shutdown("shutting down");
        }

        private static bool CheckIfClientDisconnected(List<DisconnectedClient> disconnectedClients, IPEndPoint clientInternal, IPEndPoint senderEndPoint, string token) {
            for (int i = 0; i < disconnectedClients.Count; i++) {
                if (token == disconnectedClients[i].Token) {

                    PrintMessage("Found client " + disconnectedClients[i].Username + " flagged as disconnected. Checking if the game " + disconnectedClients[i].GameId + "  is still active on server: " + disconnectedClients[i].DisconnectedFromServer.HostId);
                    
                    if(disconnectedClients[i].GameId == disconnectedClients[i].DisconnectedFromServer.GameId)
                    {
                        peer.Introduce(
                            disconnectedClients[i].DisconnectedFromServer.InternalHostAddress, // host internal
                            disconnectedClients[i].DisconnectedFromServer.ExternalHostAddress, // host external
                            clientInternal, // client internal
                            senderEndPoint, // client external
                            token // request token
                        );
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Make sure the client that queued up is still in queue and not disconnected or quit
        /// </summary>
        static void RequestQueueVerification(Client c) {
            NetOutgoingMessage om = peer.CreateMessage();
            om.Write((byte)MasterServerResponseTypes.VerifyQueue);

            SendMessage(c.ExternalAddress, om, NetDeliveryMethod.ReliableUnordered);
        }

        static void ReturnPositionInQueue(IPEndPoint clientEndPoint, int position) {
            NetOutgoingMessage om = peer.CreateMessage();
            om.Write((byte)MasterServerResponseTypes.Queued);
            om.Write(position);

            SendMessage(clientEndPoint, om, NetDeliveryMethod.ReliableUnordered);
        }

        static void ReturnNoServersOnline(IPEndPoint clientEndPoint) {
            NetOutgoingMessage om = peer.CreateMessage();
            om.Write((byte)MasterServerResponseTypes.NoServers);

            SendMessage(clientEndPoint, om, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        /// Returns clientinformation to gameserver
        /// </summary>
        /// <param name="clientLoggedIn">Client in question</param>
        private static void ReturnClientDetails(ClientUser clientLoggedIn, IPEndPoint gameserverEndPoint) {
            NetOutgoingMessage om = peer.CreateMessage();
            om.Write((byte)MasterServerResponseTypes.ClientDetails);
            PrintMessage("Client username: " + clientLoggedIn.Username + ", client level: " + clientLoggedIn.Level + ", chosen race: " + clientLoggedIn.ChosenHero);
            om.Write(clientLoggedIn.SessionKey.ToString());
            om.Write(clientLoggedIn.Username);
            om.Write(clientLoggedIn.Level);
            om.Write(clientLoggedIn.ChosenHero);
            om.Write((byte)clientLoggedIn.SelectedMap);

            PrintMessage("Sending message to gameServer " + gameserverEndPoint.ToString());
            SendMessage(gameserverEndPoint, om, NetDeliveryMethod.ReliableUnordered);
        }

        static void LoginResponse(ClientUser registeredClient, NetIncomingMessage msg) {
            NetOutgoingMessage om = peer.CreateMessage();
            om.Write((byte)MasterServerResponseTypes.LoginResponse);

            if (registeredClient == null) {
                om.Write((byte)LoginResultType.FailedNamePassword);
            }
            else if (registeredClient.Username == "dev01") {
                om.Write((byte)LoginResultType.DevSuccess);
                om.Write(registeredClient.SessionKey.ToString());
                om.Write(registeredClient.Level);
                om.Write(registeredClient.UnlockedHeroes.Count);
                for (int i = 0; i < registeredClient.UnlockedHeroes.Count; i++)
                {
                    // TODO these should be real hero-id's.
                    om.Write(registeredClient.UnlockedHeroes[i]);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].Name);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].Level);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].FullHealth);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].CurrentHealth);
                    om.Write((byte)heroes.allHeroes[registeredClient.UnlockedHeroes[i]].HeroClass1);
                }
            } else {
                om.Write((byte)LoginResultType.Success);
                om.Write(registeredClient.SessionKey.ToString());
                om.Write(registeredClient.Level);
                om.Write(registeredClient.UnlockedHeroes.Count);
                for (int i = 0; i < registeredClient.UnlockedHeroes.Count; i++)
                {
                    om.Write(registeredClient.UnlockedHeroes[i]);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].Name);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].Level);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].FullHealth);
                    om.Write(heroes.allHeroes[registeredClient.UnlockedHeroes[i]].CurrentHealth);
                    om.Write((byte)heroes.allHeroes[registeredClient.UnlockedHeroes[i]].HeroClass1);
                }
            }

            SendMessage(msg.SenderEndPoint, om, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        /// Test if the session that exists for a client is still valid.
        /// </summary>
        /// <param name="sessionValidUntil"></param>
        /// <returns></returns>
        internal static bool IsClientSessionValid(int sessionValidUntil)
        {
            /*Debug.Instance.Write("Testing session: ");
            Debug.Instance.Write("UTCNow: " + DateTime.UtcNow);
            Debug.Instance.Write("sessionValidUntil: " + sessionValidUntil);
            Debug.Instance.Write("The testable: " + (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
            int diff = sessionValidUntil - (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            Debug.Instance.Write("Time until session turns invalid: " + diff + " seconds");*/
            return sessionValidUntil > (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        /// <summary>
        /// Happens every 30 seconds; checks if the registered gameservers have checked in or not. If they haven't checked in for 65 seconds, they're removed from the list of active servers
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        internal static void CheckForInactiveGameServers(Object source, ElapsedEventArgs e)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var currentTime = (long)t.TotalMilliseconds;
            if (verbose)
            {
                PrintMessage("Checking registered serverlist for dead or busy gameservers...");
            }

            int serversRemoved = 0;
            int serversAvailable = 0;
            int serversBusyWithGames = 0;
            if (verbose)
            {
                PrintMessage("Current time is: " + currentTime);
            }
            
            for (int i = registeredHosts.Count; i > 0; i--)
            {
                if (!registeredHosts[i - 1].Busy && registeredHosts[i - 1].ValidUntil < currentTime)
                {
                    // Server hasn't reported in and has not reported as being busy
                    PrintMessage("Server " + registeredHosts[i - 1].HostId + " was not marked as busy and hasn't reported back in time. Removing from registeredHosts-list");
                    registeredHosts.Remove(registeredHosts[i - 1]);
                    // registeredHosts[i].Busy = true;
                    serversRemoved++;
                } else {
                    if(registeredHosts[i - 1].Busy)
                    {
                        // Check how long ago it was reported as being busy
                        if (registeredHosts[i - 1].BusyWasSetAt + CommonConstants.timeForFullGame < currentTime)
                        {
                            // It's been busy too long to only be a game, probably it's offline or has some other problem. Remove it for now.
                            PrintMessage("Server " + registeredHosts[i - 1].HostId + " was marked as busy, but last reported at " + registeredHosts[i - 1].BusyWasSetAt + " and now it is " + currentTime + ", difference being " + (currentTime - registeredHosts[i - 1].BusyWasSetAt) + ".");
                            registeredHosts.Remove(registeredHosts[i - 1]);
                            serversRemoved++;
                        } else
                        {
                            serversBusyWithGames++;
                        }
                    } else
                    {
                        serversAvailable++;
                        if (verbose)
                        {
                            PrintMessage("Server " + registeredHosts[i - 1].HostId + " is valid for another " + (registeredHosts[i - 1].ValidUntil - currentTime) / 1000 + " seconds.");
                        }
                    }
                }
            }

            var additionalServerStatuses = "";
            if(serversRemoved > 0)
            {
                additionalServerStatuses = serversRemoved + " servers was removed since they hadn't reported in time";
            }

            additionalServerStatuses = serversBusyWithGames > 0 ? additionalServerStatuses += " and " + serversBusyWithGames + " servers are busy serving games." : ".";
            var isOrAre = serversAvailable == 1 ? "is 1 gameserver " : "are " + serversAvailable + " gameservers ";
            if (verbose)
            {
                PrintMessage("There " + isOrAre + "ready for connections after gameserver-cleanup. " + additionalServerStatuses);
            }
            
        }

        // Send an unconnected message
        internal static void SendMessage(IPEndPoint clientEndpoint, NetOutgoingMessage msg, NetDeliveryMethod deliveryMethod) {
            peer.SendUnconnectedMessage(msg, clientEndpoint);
        }

        static void PrintMessage(string message) {
            Debug.Instance.Write(message);
        }
    }
}