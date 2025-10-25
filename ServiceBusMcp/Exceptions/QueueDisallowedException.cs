namespace ServiceBusMcp.Exceptions;

public class QueueDisallowedException(string queue) : Exception($"This action is not allowed on `{queue}`, verify your appsettings.json marks this queue as allowed") { }