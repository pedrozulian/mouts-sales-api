using SalesApi.Domain.Common;

namespace SalesApi.Domain.Tests.Common;

public class EntityTests
{
    private sealed class SampleDomainEvent : DomainEvent
    {
    }

    private sealed class SampleEntity : Entity
    {
        public SampleEntity(Guid id) : base(id)
        {
        }

        public void RaiseEvent(DomainEvent domainEvent) => AddDomainEvent(domainEvent);

        public void ClearEvents() => ClearDomainEvents();
    }

    [Fact]
    public void DuasInstanciasComMesmoId_DevemSerIguais()
    {
        var id = Guid.NewGuid();
        var entity1 = new SampleEntity(id);
        var entity2 = new SampleEntity(id);

        Assert.Equal(entity1, entity2);
        Assert.True(entity1.Equals(entity2));
        Assert.Equal(entity1.GetHashCode(), entity2.GetHashCode());
    }

    [Fact]
    public void DuasInstanciasComIdDiferente_NaoDevemSerIguais()
    {
        var entity1 = new SampleEntity(Guid.NewGuid());
        var entity2 = new SampleEntity(Guid.NewGuid());

        Assert.NotEqual(entity1, entity2);
        Assert.False(entity1.Equals(entity2));
    }

    [Fact]
    public void AddDomainEvent_DevePopularDomainEvents()
    {
        var entity = new SampleEntity(Guid.NewGuid());
        var domainEvent = new SampleDomainEvent();

        entity.RaiseEvent(domainEvent);

        Assert.Single(entity.DomainEvents);
        Assert.Contains(domainEvent, entity.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_DeveEsvaziarDomainEvents()
    {
        var entity = new SampleEntity(Guid.NewGuid());
        entity.RaiseEvent(new SampleDomainEvent());

        entity.ClearEvents();

        Assert.Empty(entity.DomainEvents);
    }
}
