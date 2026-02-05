using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Options;
using ServiceBusMcp.Exceptions;
using System.Text.Json;

namespace ServiceBusMcp.Services;

public class AzureServiceBusService : IAzureServiceBusService
{
    private readonly ServiceBusClient client;
    private readonly ServiceBusAdministrationClient adminClient;
    private readonly IOptions<ServiceBusConfiguration> options;

    public AzureServiceBusService(IOptions<ServiceBusConfiguration> options)
    {
        this.options = options;
        this.client = new ServiceBusClient(options.Value.Namespace, new DefaultAzureCredential());
        this.adminClient = new ServiceBusAdministrationClient(options.Value.Namespace, new DefaultAzureCredential());
    }

    public async Task<long> GetMessageCountAsync(string queue)
    {
        ThrowIfDisallowed(queue);

        var props = await adminClient.GetQueueRuntimePropertiesAsync(queue);
        return props.Value.ActiveMessageCount;
    }

    public async Task<long> GetDeadletterMessageCountAsync(string queue)
    {
        ThrowIfDisallowed(queue);

        var props = await adminClient.GetQueueRuntimePropertiesAsync(queue);
        return props.Value.DeadLetterMessageCount;
    }

    public Task<IEnumerable<ServiceBusReceivedMessage>> GetMessagesAsync(string queue)
    {
        ThrowIfDisallowed(queue);

        return GetMessagesFromQueueAsync(queue);
    }

    public Task<IEnumerable<ServiceBusReceivedMessage>> GetDeadletterMessagesAsync(string queue)
    {
        ThrowIfDisallowed(queue);

        return GetMessagesFromQueueAsync(queue, SubQueue.DeadLetter);
    }

    public async Task<bool> ResubmitDeadletterMessageAsync(string queue, string messageId)
    {
        ThrowIfDisallowedForReplay(queue);

        var receiver = this.client.CreateReceiver(queue, new ServiceBusReceiverOptions()
        {
            SubQueue = SubQueue.DeadLetter
        });

        var sender = this.client.CreateSender(queue);

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(maxMessages: 100, maxWaitTime: TimeSpan.FromSeconds(10));
            if (messages.Count == 0)
                break;

            foreach (var message in messages)
            {
                if (message.MessageId == messageId)
                {
                    // Save message content to disk
                    await SaveMessageContentToDiskAsync(message);

                    var replayMessage = new ServiceBusMessage(message);

                    if (!replayMessage.ApplicationProperties.ContainsKey("ResubmittedVia"))
                        replayMessage.ApplicationProperties["ResubmittedVia"] = "Azure ServiceBus MCP";

                    if (!replayMessage.ApplicationProperties.ContainsKey("ResubmittedTimeUtc"))
                        replayMessage.ApplicationProperties["ResubmittedTimeUtc"] = DateTime.UtcNow.ToString();

                    await sender.SendMessageAsync(replayMessage);
                    await receiver.CompleteMessageAsync(message);

                    foreach (var remainingMessage in messages)
                        await receiver.AbandonMessageAsync(remainingMessage);

                    return true;
                } else
                {
                    await receiver.AbandonMessageAsync(message);
                }
            }
        }

        return false;
    }

    public async Task ResubmitDeadletterMessagesAsync(string queue)
    {
        ThrowIfDisallowedForReplay(queue);

        var receiver = this.client.CreateReceiver(queue, new ServiceBusReceiverOptions()
        {
            SubQueue = SubQueue.DeadLetter
        });

        var sender = this.client.CreateSender(queue);

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(maxMessages: 100, maxWaitTime: TimeSpan.FromSeconds(10));
            if (messages.Count == 0)
                break;

            foreach (var message in messages)
            {
                // Save message content to disk
                await SaveMessageContentToDiskAsync(message);

                var replayMessage = new ServiceBusMessage(message);

                if (!replayMessage.ApplicationProperties.ContainsKey("ResubmittedVia"))
                    replayMessage.ApplicationProperties["ResubmittedVia"] = "Azure ServiceBus MCP";

                if (!replayMessage.ApplicationProperties.ContainsKey("ResubmittedTimeUtc"))
                    replayMessage.ApplicationProperties["ResubmittedTimeUtc"] = DateTime.UtcNow.ToString();

                await sender.SendMessageAsync(replayMessage);
                await receiver.CompleteMessageAsync(message);

                foreach (var remainingMessage in messages)
                    await receiver.AbandonMessageAsync(remainingMessage);
            }
        }
    }

    public async Task<bool> TryConnect()
    {
        try
        {
            var queues = await adminClient.GetQueuesAsync().ToListAsync();

            if (queues.Count != 0)
            {
                var receiver = this.client.CreateReceiver(queues.First().Name);
                await receiver.PeekMessageAsync();
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetQueuesAsync()
    {
        var existingQueues = new List<string>();
        await foreach (var queue in adminClient.GetQueuesAsync())
            if (options.Value.AllowedQueues.Contains(queue.Name))
                existingQueues.Add(queue.Name);

        return existingQueues;
    }

    private async Task<IEnumerable<ServiceBusReceivedMessage>> GetMessagesFromQueueAsync(string queue, SubQueue subQueue = SubQueue.None)
    {
        var receiver = this.client.CreateReceiver(queue, new ServiceBusReceiverOptions()
        {
            SubQueue = subQueue
        });

        var messages = await GetMessagesFromReceiverAsync(receiver);
        return messages;
    }

    private async Task<IEnumerable<ServiceBusReceivedMessage>> GetMessagesFromReceiverAsync(ServiceBusReceiver receiver)
    {
        var messageCollector = new List<ServiceBusReceivedMessage>();
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(maxMessages: 100, maxWaitTime: TimeSpan.FromSeconds(10));
            if (messages.Count == 0)
                break;

            messageCollector.AddRange(messages);
        }

        foreach (var message in messageCollector)
            await receiver.AbandonMessageAsync(message);

        return messageCollector;
    }

    private async Task SaveMessageContentToDiskAsync(ServiceBusReceivedMessage message)
    {
        if (string.IsNullOrWhiteSpace(options.Value.DeadletterMessageStoragePath))
            return;

        var storagePath = options.Value.DeadletterMessageStoragePath;
        
        // Create directory if it doesn't exist
        Directory.CreateDirectory(storagePath);

        // Create a unique filename using timestamp and message ID
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var sanitizedMessageId = SanitizeFileName(message.MessageId);
        var fileName = $"{timestamp}_{sanitizedMessageId}.json";
        var filePath = Path.Combine(storagePath, fileName);

        // Prepare message data for storage
        var messageData = new
        {
            message.MessageId,
            message.CorrelationId,
            message.Subject,
            message.ContentType,
            Body = message.Body.ToString(),
            message.EnqueuedTime,
            message.DeadLetterReason,
            message.DeadLetterErrorDescription,
            message.ApplicationProperties,
            SavedAt = DateTime.UtcNow
        };

        // Serialize and save to file
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(messageData, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "no-id";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // Limit length to avoid path issues
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private void ThrowIfDisallowed(string queueName)
    {
        if (!options.Value.AllowedQueues.Contains(queueName))
            throw new QueueDisallowedException(queueName);
    }

    private void ThrowIfDisallowedForReplay(string queueName)
    {
        if (!options.Value.AllowedReplayQueues.Contains(queueName))
            throw new QueueDisallowedException(queueName);
    }
}
