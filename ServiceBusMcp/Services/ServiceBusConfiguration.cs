namespace ServiceBusMcp.Services;

public class ServiceBusConfiguration
{
    public required string Namespace { get; init; }
    public required HashSet<string> AllowedQueues { get; init; }
    public required HashSet<string> AllowedReplayQueues { get; init; }
    public bool TryConnectOnStartup { get; init; }
}
