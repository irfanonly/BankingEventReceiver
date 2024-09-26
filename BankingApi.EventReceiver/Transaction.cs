namespace BankingApi.EventReceiver;

public class Transaction
{
    public string Id { get; set; }
    public string MessageType { get; set; } // "Credit" or "Debit"
    public Guid BankAccountId { get; set; }

    // Change Amount to string
    public string Amount { get; set; }

    // Method to get Amount as decimal
    public decimal AmountAsDecimal => decimal.Parse(Amount);

}