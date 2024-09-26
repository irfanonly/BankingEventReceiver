namespace BankingAPI.EventReceiver.UnitTests;

using System;
using System.Threading.Tasks;
using BankingApi.EventReceiver;
using BankingApi.EventReceiver.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class MessageWorkerTests
{
    private readonly Mock<BankingApiDbContext> _BankingApiDbContext;
    private readonly Mock<ILogger<BankAccountService>> _LoggerBankAccountService;
    private readonly Mock<ILogger<MessageWorker>> _LoggerMessageWorker;
    private readonly Mock<IServiceBusReceiver> _serviceBusReceiverMock;
    private readonly Mock<BankAccountService> _bankAccountServiceMock;
    private readonly MessageWorker _messageWorker;

    public MessageWorkerTests()
    {
        _BankingApiDbContext = new Mock<BankingApiDbContext>();
        _LoggerBankAccountService = new Mock<ILogger<BankAccountService>>();
        _LoggerMessageWorker = new Mock<ILogger<MessageWorker>>();
        _serviceBusReceiverMock = new Mock<IServiceBusReceiver>();
        _bankAccountServiceMock = new Mock<BankAccountService>(_BankingApiDbContext.Object, _LoggerBankAccountService.Object);
        _messageWorker = new MessageWorker(_serviceBusReceiverMock.Object, _bankAccountServiceMock.Object, _LoggerMessageWorker.Object);
        _messageWorker.retryDelays = [1, 2, 3];
    }

    [Fact]
    public async Task ProcessOneByOne_ShouldWaitWhenPeekReturnsNull()
    {
        // Arrange
        _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync((EventMessage)null);

        // Act
        var startTask = _messageWorker.ProcessOneByOne();

        
        await Task.Delay(100);

        // Assert
        _serviceBusReceiverMock.Verify(x => x.Peek(), Times.AtLeastOnce);
        _serviceBusReceiverMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessOneByOne_ShouldProcessMessageAndCompleteOnSuccess()
    {
        // Arrange
        var message = new EventMessage { 
            MessageBody = "{ \"id\": \"89479d8a-549b-41ea-9ccc-25a4106070a1\", \"messageType\": \"Credit\", \"bankAccountId\": \"7d445724-24ec-4d52-aa7a-ff2bac9f191d\", \"amount\": \"90.00\" }" 
        };
        _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync(message);
        _bankAccountServiceMock.Setup(x => x.AddTransaction(It.IsAny<Transaction>()))
            .Returns(Task.CompletedTask);

        // Act
        var startTask = _messageWorker.ProcessOneByOne();

        await Task.Delay(100);
        // Assert
        _bankAccountServiceMock.Verify(x => x.AddTransaction(It.IsAny<Transaction>()), Times.Once);
        _serviceBusReceiverMock.Verify(x => x.Complete(message), Times.Once);
    }

    [Fact]
    public async Task ProcessOneByOne_ShouldHandleTransientFailureWithRetries()
    {
        // Arrange
        var message = new EventMessage
        {
            MessageBody = "{ \"id\": \"89479d8a-549b-41ea-9ccc-25a4106070a1\", \"messageType\": \"Credit\", \"bankAccountId\": \"7d445724-24ec-4d52-aa7a-ff2bac9f191d\", \"amount\": \"90.00\" }"
        };
        _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync(message);
        _bankAccountServiceMock.Setup(x => x.AddTransaction(It.IsAny<Transaction>())).ThrowsAsync(new TransientException(""));

        // Act
        var startTask = _messageWorker.ProcessOneByOne();


        await Task.Delay(TimeSpan.FromSeconds(8));

        // Assert
        _serviceBusReceiverMock.Verify(x => x.Complete(message), Times.Never);
        _serviceBusReceiverMock.Verify(x => x.Abandon(message), Times.Once);
    }

    [Fact]
    public async Task ProcessOneByOne_ShouldMoveToDeadLetterOnNonTransientExceptionInvalidAmount()
    {
        // Arrange
        var message = new EventMessage
        {
            MessageBody = "{ \"id\": \"89479d8a-549b-41ea-9ccc-25a4106070a1\", \"messageType\": \"Credit\", \"bankAccountId\": \"7d445724-24ec-4d52-aa7a-ff2bac9f191d\", \"amount\": \"abc.00\" }"
        };
        _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync(message);

        // Act
        var startTask = _messageWorker.ProcessOneByOne();

        // Await briefly to validate behavior
        await Task.Delay(100);

        // Assert
        _serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(message), Times.Once);
    }

    [Fact]
    public async Task ProcessOneByOne_ShouldMoveToDeadLetterOnNonTransientExceptionInvalidType()
    {
        // Arrange
        var message = new EventMessage { MessageBody = "{\"MessageType\": \"InvalidType\", \"BankAccountId\": \"123\", \"Amount\": 100.00}" };
        _serviceBusReceiverMock.Setup(x => x.Peek()).ReturnsAsync(message);

        // Act
        var startTask = _messageWorker.ProcessOneByOne();

        // Await briefly to validate behavior
        await Task.Delay(100);

        // Assert
        _serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(message), Times.Once);
    }
}