namespace MSCommon {
    public enum MasterServerResponseTypes {
        NoServers,
        GenericError,
        Queued,
        VerifyQueue,
        LoginResponse,
        NoActiveSession,
        ClientDetails,
        RegisterHostResponse
    }
}