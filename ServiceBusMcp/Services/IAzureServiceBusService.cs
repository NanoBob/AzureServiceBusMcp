using Azure.Messaging.ServiceBus;

namespace ServiceBusMcp.Services;

public interface IAzureServiceBusService
{
    Task<IEnumerable<string>> GetQueuesAsync();
    Task<long> GetDeadletterMessageCountAsync(string queue);
    Task<IEnumerable<ServiceBusReceivedMessage>> GetDeadletterMessagesAsync(string queue);
    Task<long> GetMessageCountAsync(string queue);
    Task<IEnumerable<ServiceBusReceivedMessage>> GetMessagesAsync(string queue);
    Task<bool> ResubmitDeadletterMessageAsync(string queue, string messageId);
    Task ResubmitDeadletterMessagesAsync(string queue);
    Task<bool> TryConnect();
}
