namespace SephiriaTogether
{
    internal enum SessionTransportChange
    {
        None,
        WaitForSessionEnd,
        EnableIp,
        UpdateIpPort,
        RestoreNative,
        NativeTransportUnavailable
    }

    internal static class SessionTransportPolicy
    {
        internal static bool CanApply(bool serverActive, bool clientActive, bool externalTransition,
            bool runStarted, bool startingSession = false) =>
            !serverActive && !clientActive && (startingSession || !externalTransition && !runStarted);

        internal static ushort ComparisonPort(bool serverActive, bool clientActive, ushort configuredPort, ushort activePort) =>
            clientActive && !serverActive ? activePort : configuredPort;

        internal static ushort ClientPort(ushort configuredPort, ushort? selectedTargetPort) =>
            selectedTargetPort ?? configuredPort;

        internal static bool ClientModeMatchesTarget(bool requestedIp, bool explicitTcpTarget) =>
            requestedIp == explicitTcpTarget;

        internal static SessionTransportChange Decide(bool requestedIp, ushort requestedPort,
            bool currentIp, ushort currentPort, bool canApply, bool nativeTransportAvailable)
        {
            if (requestedIp == currentIp && (!requestedIp || requestedPort == currentPort))
                return SessionTransportChange.None;
            if (!canApply) return SessionTransportChange.WaitForSessionEnd;
            if (requestedIp)
                return currentIp ? SessionTransportChange.UpdateIpPort : SessionTransportChange.EnableIp;
            return nativeTransportAvailable
                ? SessionTransportChange.RestoreNative
                : SessionTransportChange.NativeTransportUnavailable;
        }
    }
}
