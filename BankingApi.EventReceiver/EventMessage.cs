using Microsoft.Extensions.Options;

namespace BankingApi.EventReceiver;

public class EventMessage
{
    public Guid Id { get; set; }
    public string? MessageBody { get; set; }
    public int ProcessingCount { get; set; }
}
