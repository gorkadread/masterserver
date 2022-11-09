namespace MSCommon {
    public static class CommonConstants {
        public const int MasterServerPort = 14343;
        public const int GameServerPort = 14242;
        public const int minClientCount = 2;
        public const int MaxClientCount = 2;
        public const long serverReportTimeout = 65000; // milliseconds
        public const long timeForFullGame = 360000; // The amount of milliseconds a full game would take. Used to see if a gameserver is busy with a game or if it's something wrong with it after a game
        public const int sessionLength = 60 * 60 * 24; // Time a users session to the masterserver should be valid. Currently 86400 seconds, 24 hours.
    }
}
