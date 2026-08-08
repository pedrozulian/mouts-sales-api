using SalesApi.Domain.Common;

namespace SalesApi.Domain.Tests.Common;

public class EntityTests
{
    private sealed class SampleEntity : Entity
    {
        public SampleEntity(Guid id) : base(id)
        {
        }
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
}
