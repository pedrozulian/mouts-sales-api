using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesApi.Domain.Sales;

namespace SalesApi.Infrastructure.Persistence.Configurations;

public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("sale_items");

        builder.HasKey(i => i.Id);

        // Id é sempre gerado pela aplicação (Guid.NewGuid() no construtor), nunca pelo EF Core ou
        // pelo banco. Sem isso, itens novos adicionados à coleção de uma Sale já rastreada (caso de
        // Sale.Update) são erroneamente detectados como "Unchanged"/"Modified" em vez de "Added",
        // pois o Id já vem preenchido — gerando DbUpdateConcurrencyException (0 linhas afetadas) ao
        // tentar UPDATE em uma linha que ainda não existe.
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Ignore(i => i.DomainEvents);

        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.DiscountPercentage).HasColumnType("numeric(5,4)").IsRequired();
        builder.Property(i => i.DiscountAmount).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.TotalAmount).HasColumnType("numeric(18,2)").IsRequired();

        builder.OwnsOne(i => i.Product, product =>
        {
            product.Property(p => p.Id).HasColumnName("product_id");
            product.Property(p => p.Name).HasColumnName("product_name").HasMaxLength(200);
        });

        // Índice único composto (sale_id + product_id, INV-03) é criado diretamente na migration
        // (CreateSales), pois HasIndex não suporta caminhos de navegação de owned types (EF Core 8).
    }
}
