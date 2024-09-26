using BankingApi.EventReceiver.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BankingApi.EventReceiver
{
    public class MessageWorker
    {
        private readonly IServiceBusReceiver _serviceBusReceiver;
        private readonly BankAccountService _bankAccountService;
        private readonly ILogger<MessageWorker> _logger;

        public int[] retryDelays = { 5, 25, 125 }; // Delays In seconds
        public MessageWorker(
            IServiceBusReceiver serviceBusReceiver,
            BankAccountService bankAccountService, 
            ILogger<MessageWorker> logger)
        {
            _serviceBusReceiver = serviceBusReceiver;
            _logger = logger;
            _bankAccountService = bankAccountService;
        }


        public async Task Start()
        {
            while (true)
            {
                await ProcessOneByOne();
            }
        }

        /// <summary>
        /// This is separated for unit test
        /// </summary>
        /// <returns></returns>
        public async Task ProcessOneByOne()
        {
            var message = await _serviceBusReceiver.Peek();

            if (message == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                return;

            }

            try
            {
                _logger.LogInformation($"Processing the message: {message.Id}");
                await ProcessMessageAsync(message);
                _logger.LogInformation($"Completing the message: {message.Id}");
                await _serviceBusReceiver.Complete(message);
                _logger.LogInformation($"The message: {message.Id} is processed successfully");
            }
            catch (TransientException ex)
            {
                _logger.LogError($"TransientException: {message.Id} {ex.Message}");
                // IF transient, Handle transient failures with exponential retry timeout
                await HandleTransientFailureAsync(message);
            }
            catch (NonTransientException ex)
            {
                _logger.LogError($"NonTransientException: {message.Id} {ex.Message}");
                // If non transient, Move to dead-letter queue
                await _serviceBusReceiver.MoveToDeadLetter(message);
            }
        }



        private async Task ProcessMessageAsync(EventMessage message)
        {
            Transaction? transaction;

            try
            {
                transaction = JsonSerializer.Deserialize<Transaction>(message.MessageBody!.ToString(), new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });

            }
            catch (Exception ex)
            {
                _logger.LogError($"Invalid message: {message.Id} {ex.Message}");
                throw new NonTransientException("Invalid message");
            }

            ValidateTransaction(transaction);

            await _bankAccountService.AddTransaction(transaction!);


        }

        private void ValidateTransaction(Transaction? transaction) {
            if (transaction == null || !decimal.TryParse(transaction.Amount, out _) || (transaction.MessageType != "Credit" && transaction.MessageType != "Debit"))
            {
                _logger.LogError($"Invalid transaction: {transaction?.Id}");
                throw new NonTransientException("Invalid transaction");
            }
        }

        private async Task HandleTransientFailureAsync(EventMessage message)
        {
            while (message.ProcessingCount < retryDelays.Length)
            {
                try
                {
                    // delay first time after failure
                    await Task.Delay(TimeSpan.FromSeconds(retryDelays[message.ProcessingCount]));
                    // retry the process
                    message.ProcessingCount++;
                    await ProcessMessageAsync(message);
                    await _serviceBusReceiver.Complete(message);
                    
                }
                catch (TransientException)
                {
                    
                    // if reaches the max retryies
                    if (message.ProcessingCount == retryDelays.Length)
                    {
                        await _serviceBusReceiver.Abandon(message);
                        _logger.LogError($"The message is abandoned : {message.Id}");
                    }
                    

                }
            }
        }
    }
}
