# masterserver
This is the alpha version of my masterserver for one of my games developed in my spare time.

A C# console server built on the Lidgren Networking library (https://github.com/lidgren/lidgren-network-gen3), acting as the main point of entry for gameclients and gameservers alike. Handles logins, queues and matchmaking for gameclients towards gameservers. Also takes care of NAT punchthroughs by passing reference of clients to gameservers. 
