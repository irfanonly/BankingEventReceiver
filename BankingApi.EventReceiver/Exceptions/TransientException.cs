namespace BankingApi.EventReceiver.Exceptions
{
    // Exception classes to differentiate between transient and non-transient errors
    public class TransientException : Exception
    {
        public TransientException(string message) : base(message) { }
    }
}
