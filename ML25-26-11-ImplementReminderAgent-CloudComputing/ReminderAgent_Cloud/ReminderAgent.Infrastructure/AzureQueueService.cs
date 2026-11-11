using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using ReminderAgent.Common;

namespace ReminderAgent.Infrastructure;

/// <summary>Azure Storage Queue-backed implementation of <see cref="IQueueService"/>.</summary>
public class AzureQueueService : IQueueService
{
    #region Fields

    private readonly string _connectionString;

    #endregion

    #region Constructor

    public AzureQueueService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("QueueStorage")
            ?? throw new InvalidOperationException(
                "Connection string 'QueueStorage' not found in appsettings.json");
    }

    #endregion

    #region Queue Operations

    /// <summary>Sends a message to the named queue.</summary>
    public async Task SendMessageAsync(string queueName, string messageBody)
    {
        var client = GetClient(queueName);
        await client.SendMessageAsync(messageBody);
    }

    /// <summary>Receives up to <paramref name="maxMessages"/> messages from the named queue.</summary>
    public async Task<IEnumerable<(string, string, string, long)>> ReceiveMessagesAsync(
    string queueName, int maxMessages = 10)
    {
        var client = GetClient(queueName);
        var response = await client.ReceiveMessagesAsync(maxMessages);
        return response.Value.Select(m =>
            (m.MessageId, m.PopReceipt, m.MessageText, m.DequeueCount)).ToList();
    }

    /// <summary>Deletes a message after it's been successfully processed.</summary>
    public async Task DeleteMessageAsync(string queueName, string messageId, string popReceipt)
    {
        var client = GetClient(queueName);
        await client.DeleteMessageAsync(messageId, popReceipt);
    }

    #endregion

    #region Private Helpers

    /// <summary>Gets the client for the named queue, creating the queue if needed.</summary>
    private QueueClient GetClient(string queueName)
    {
        var client = new QueueClient(_connectionString, queueName);
        client.CreateIfNotExists();
        return client;
    }

    #endregion
}