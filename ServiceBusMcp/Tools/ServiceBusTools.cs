using ModelContextProtocol.Server;
using ServiceBusMcp.Exceptions;
using ServiceBusMcp.Services;
using System.ComponentModel;
using System.Text.Json;

namespace ServiceBusMcp.Tools;

[McpServerToolType]
public static class ServiceBusTools
{
    [McpServerTool(Name = nameof(GetMessageCount))]
    [Description("Gets the number of messages in a Service Bus queue.")]
    public static async Task<string> GetMessageCount(IAzureServiceBusService serviceBus, string queue)
    {
        try
        {
            return (await serviceBus.GetMessageCountAsync(queue)).ToString();
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(GetDeadletterMessageCount))]
    [Description("Gets the number of deadletter messages in a Service Bus queue.")]
    public static async Task<string> GetDeadletterMessageCount(IAzureServiceBusService serviceBus, string queue)
    {
        try
        {
            return (await serviceBus.GetDeadletterMessageCountAsync(queue)).ToString();
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(GetMessages))]
    [Description("Gets messages from a Service Bus queue. This only returns messages from the regular queue, not deadletters")]
    public static async Task<string> GetMessages(IAzureServiceBusService serviceBus, string queue)
    {
        try
        {
            var messages = await serviceBus.GetMessagesAsync(queue);

            var matchingMessages = messages
                .Select(x => new
                {
                    Message = x,
                    Body = x.Body.ToString()
                });

            var result = matchingMessages.Select(x => new
            {
                x.Message.MessageId,
                x.Body,

                x.Message.EnqueuedTime,
                x.Message.CorrelationId,

                x.Message.ApplicationProperties
            });

            return JsonSerializer.Serialize(result);
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(GetMessagesContaining))]
    [Description("Gets messages from a Service Bus queue that contain a specific search string. This only returns messages from the regular queue, not deadletters")]
    public static async Task<string> GetMessagesContaining(IAzureServiceBusService serviceBus, string queue, string search)
    {
        try
        {
            var messages = await serviceBus.GetMessagesAsync(queue);

            var matchingMessages = messages
                .Select(x => new
                {
                    Message = x,
                    Body = x.Body.ToString()
                })
                .Where(x => x.Body.Contains(search));

            var result = matchingMessages.Select(x => new
            {
                x.Message.MessageId,
                x.Body,

                x.Message.EnqueuedTime,
                x.Message.CorrelationId,

                x.Message.ApplicationProperties
            });

            return JsonSerializer.Serialize(result);
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(GetDeadletterMessages))]
    [Description("Gets deadletter messages from a Service Bus queue. This only returns messages from the deadletter queue, not the main queue.")]
    public static async Task<string> GetDeadletterMessages(IAzureServiceBusService serviceBus, string queue)
    {
        try
        {
            var messages = await serviceBus.GetDeadletterMessagesAsync(queue);

            var matchingMessages = messages
                .Select(x => new
                {
                    Message = x,
                    Body = x.Body.ToString()
                });

            var result = matchingMessages.Select(x => new
            {
                x.Message.MessageId,
                x.Body,

                x.Message.EnqueuedTime,
                x.Message.CorrelationId,

                x.Message.ApplicationProperties
            });

            return JsonSerializer.Serialize(result);
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(GetDeadletterMessagesContaining))]
    [Description("Gets deadletter messages from a Service Bus queue that contain a specific search string, this only returns messages from the deadletter queue, not the main queue.")]
    public static async Task<string> GetDeadletterMessagesContaining(IAzureServiceBusService serviceBus, string queue, string search)
    {
        try
        {
            var messages = await serviceBus.GetDeadletterMessagesAsync(queue);

            var matchingMessages = messages
                .Select(x => new
                {
                    Message = x,
                    Body = x.Body.ToString()
                })
                .Where(x => x.Body.Contains(search));

            var result = matchingMessages.Select(x => new
            {
                x.Message.MessageId,
                x.Body,

                x.Message.EnqueuedTime,
                x.Message.CorrelationId,

                x.Message.ApplicationProperties
            });

            return JsonSerializer.Serialize(result);
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(GetQueues))]
    [Description("Gets all (allowed) queues in the servicebus namespace")]
    public static async Task<object> GetQueues(IAzureServiceBusService serviceBus)
    {
        try
        {
            return await serviceBus.GetQueuesAsync();
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(ResubmitDeadletterMessage))]
    [Description("Resubmits a deadletter message from a Service Bus deadletter queue back to the main queue.")]
    public static async Task<string> ResubmitDeadletterMessage(IAzureServiceBusService serviceBus, string queue, string messageId)
    {
        try
        {
            return (await serviceBus.ResubmitDeadletterMessageAsync(queue, messageId)).ToString();
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }

    [McpServerTool(Name = nameof(ResubmitAllDeadletterMessage))]
    [Description("Resubmits all deadletter messages on a Service Bus deadletter queue back to the main queue.")]
    public static async Task<string> ResubmitAllDeadletterMessage(IAzureServiceBusService serviceBus, string queue)
    {
        try
        {
            await serviceBus.ResubmitDeadletterMessagesAsync(queue);
            return "Successfully resbumitted deadletters";
        }
        catch (QueueDisallowedException ex)
        {
            return ex.Message;
        }
    }
}
