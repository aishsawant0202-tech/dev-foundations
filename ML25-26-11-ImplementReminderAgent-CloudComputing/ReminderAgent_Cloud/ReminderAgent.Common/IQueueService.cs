using System;
using System.Collections.Generic;
using System.Text;

namespace ReminderAgent.Common
{
    /// <summary>Azure Storage Queue operations used for bulk-upload job processing.</summary>
    public interface IQueueService
    {
        #region Queue Operations

        /// <summary>Sends a message to the named queue.</summary>
        Task SendMessageAsync(string queueName, string messageBody);

        /// <summary>Receives up to <paramref name="maxMessages"/> messages from the named queue.</summary>
        Task<IEnumerable<(string MessageId, string PopReceipt, string Body, long DequeueCount)>> ReceiveMessagesAsync(
            string queueName, int maxMessages = 10);

        /// <summary>Deletes a message after it's been successfully processed.</summary>
        Task DeleteMessageAsync(string queueName, string messageId, string popReceipt);

        #endregion
    }
}
