using FluentAssertions;
using RapidRepo.Concurrency;
using RapidRepo.Entities;

namespace RapidRepo.Tests.Concurrency;

public class ConcurrencyExceptionTests
{
    private sealed class Order : BaseVersionedEntity<int>;

    private sealed class Invoice : BaseVersionedEntity<int>;

    [Fact]
    public void KeepsTheEntitiesAndTheInnerException()
    {
        var entity = new Order();
        var inner = new InvalidOperationException("provider");

        var exception = new ConcurrencyException([entity], inner);

        exception.Entities.Should().ContainSingle().Which.Should().BeSameAs(entity);
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Message_NamesASingleEntity()
    {
        new ConcurrencyException([new Order()]).Message
            .Should().StartWith("The commit was rejected because the Order was changed or deleted");
    }

    [Fact]
    public void Message_CountsSeveralEntities_AndNamesEachTypeOnce()
    {
        new ConcurrencyException([new Order(), new Order(), new Invoice()]).Message
            .Should().StartWith("The commit was rejected because 3 entities (Order, Invoice) were changed or deleted");
    }

    [Fact]
    public void WithoutEntities_UsesTheDefaultMessage()
    {
        var exception = new ConcurrencyException();

        exception.Entities.Should().BeEmpty();
        new ConcurrencyException(Array.Empty<object>()).Message.Should().Be(exception.Message);
    }
}
