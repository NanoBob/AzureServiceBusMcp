# Azure Service Bus MCP Server

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server that provides tools for interacting with Azure Service Bus queues.

This tool is deliberately limited in its capabilities, since sending ServiceBus messages is a potentially sensitive operation.

## Features

### Available Tools

- **GetQueues** - List all available queues in the Service Bus namespace
- **GetMessageCount** - Get the number of active messages in a Service Bus queue
- **GetDeadletterMessageCount** - Get the number of dead letter messages in a queue
- **GetMessages** - Retrieve messages from a Service Bus queue (active messages only)
- **GetDeadletterMessages** - Retrieve dead letter messages from a queue
- **ResubmitDeadletterMessage** - Resubmit a specific dead letter message back to the active queue
- **ResubmitAllDeadletterMessages** - Resubmit all dead letter messages on a queue back to the active queue

## Configuration

### appsettings.json

Create or update your `appsettings.json` file with your Service Bus configuration:

```json
{
  "ServiceBus": {
    "ConnectionString": "your-service-bus-connection-string",
    "Namespace": "your-service-bus-namespace"
  }
}
```

### Authentication

The server uses Azure Identity for authentication. You can authenticate using:

- **Azure CLI**: `az login`
- **Visual Studio**: Sign in to your Azure account
- **Environment Variables**: Set `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`
- **Managed Identity**: When running in Azure

## Running the MCP Server

For now you can run the MCP server by simply modifying your mcp.json, and adding a reference to the executable via stdio.
```json
"servicebus": {
    "type": "stdio",
    "command": "<path\\to\\ServiceBusMcp.exe>"
},
```

## Example Usage

Once connected, you can use the tools through your MCP client:

```
"How many messages are in the 'orders' queue?"
→ Uses GetMessageCount tool

"Show me the dead letter messages in the 'payments' queue"
→ Uses GetDeadletterMessages tool

"What queues are available?"
→ Uses GetQueues tool

"Resubmit the dead letter message with ID 'abc123' from the 'orders' queue"
→ Uses ResubmitDeadletterMessage tool
```
