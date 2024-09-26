using BankingApi.EventReceiver.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingApi.EventReceiver
{
    public class BankAccountService 
    {
        private readonly BankingApiDbContext _dbContext;
        private readonly ILogger<BankAccountService> _logger;
        public BankAccountService(BankingApiDbContext dbContext, ILogger<BankAccountService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public virtual async Task AddTransaction(Transaction transaction)
        {
            var bankAccount = await _dbContext.BankAccounts.SingleOrDefaultAsync(x => x.Id == transaction.BankAccountId);
            if (bankAccount == null)
            {
                _logger.LogError($"Bank account with ID {transaction.BankAccountId} not found.");
                throw new NonTransientException($"Bank account with ID {transaction.BankAccountId} not found.");
            }

            using var dbTransaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                
                AdjustBalance(bankAccount, transaction);

                await _dbContext.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError($"Concurrency error while updating the bank account balance. {ex.Message}");
                throw new TransientException("A concurrency error occurred. Retrying operation.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError($"Database error occurred while processing the transaction. {ex.Message}");
                throw new TransientException("A database error occurred. Retrying operation.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An unexpected error occurred while processing the transaction. {ex.Message}");
                throw new NonTransientException("An unexpected error occurred.");
            }
        }

        // Adjust the balance based on the transaction type
        private void AdjustBalance(BankAccount bankAccount, Transaction transaction)
        {
            if (transaction.MessageType == "Credit")
            {
                bankAccount.Balance += transaction.AmountAsDecimal;
            }
            else if (transaction.MessageType == "Debit")
            {
                if (bankAccount.Balance < transaction.AmountAsDecimal)
                {
                    Console.WriteLine($"Insufficient funds for Debit transaction. Account ID: {bankAccount.Id}, Attempted Amount: {transaction.Amount}");
                    throw new NonTransientException("Insufficient funds.");
                }

                bankAccount.Balance -= transaction.AmountAsDecimal;
            }
        }

    }
}
