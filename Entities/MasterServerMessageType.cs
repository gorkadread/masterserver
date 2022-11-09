
namespace MSCommon {
    public enum MasterServerMessageType {
        RegisterHost,
        DeregisterHost,
        RequestHostList,
        RequestToJoinGameQueue,
        RequestIntroduction,
        VerifyQueue,
        NotifyMasterOfClientCount,
        NotifyMasterOfGameFinished,
        NotifyMasterOfDisconnectedClient,
        RequestClientDetails,
        LoginRequest,
        ClientReconnected,
        ClientDisconnected,
        GameIsStarting,
        Developer,
        ClientRequestedToDisconnect
    }
}
