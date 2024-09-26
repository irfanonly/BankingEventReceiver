namespace BankingApi.EventReceiver.Exceptions
{
    public class NonTransientException : Exception
    {
        public NonTransientException(string message) : base(message) { }
    }
}
