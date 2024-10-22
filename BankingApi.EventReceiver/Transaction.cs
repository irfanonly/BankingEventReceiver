using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace BankingApi.EventReceiver;

public class Transaction
{
    public string Id { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MessageType MessageType { get; set; } // "Credit" or "Debit"
    public Guid BankAccountId { get; set; }

    // Change Amount to string
    public string Amount { get; set; }

    // Method to get Amount as decimal
    public decimal AmountAsDecimal => decimal.Parse(Amount);

}
public enum MessageType
{
    Credit,
    Debit
}