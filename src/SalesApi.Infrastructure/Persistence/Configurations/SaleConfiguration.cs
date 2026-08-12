using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesApi.Domain.Sales;

namespace SalesApi.Infrastructure.Persistence.Configurations;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("sales");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        // xmin do PostgreSQL como token de concorrência otimista do EF Core — coluna de sistema,
        // sem exigir nenhuma migration. Garante que, entre duas escritas concorrentes na mesma
        // venda, a segunda a chegar recebe DbUpdateConcurrencyException em vez de sobrescrever
        // silenciosamente a primeira. `UseXminAsConcurrencyToken()` está obsoleto no provider
        // Npgsql atual em favor da forma padrão do EF Core abaixo.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Ignore(s => s.DomainEvents);

        builder.Property(s => s.SaleNumber)
            .HasColumnName("sale_number")
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(s => s.SaleNumber).IsUnique().HasDatabaseName("ix_sales_sale_number");

        builder.Property(s => s.SaleDate).HasColumnName("sale_date");

        builder.Property(s => s.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)");

        builder.Property(s => s.IsCancelled).HasColumnName("is_cancelled");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.SaleDate).HasDatabaseName("ix_sales_sale_date");

        builder.OwnsOne(s => s.Customer, customer =>
        {
            customer.Property(c => c.Id).HasColumnName("customer_id");
            customer.Property(c => c.Name).HasColumnName("customer_name").HasMaxLength(200);
            customer.HasIndex(c => c.Id).HasDatabaseName("ix_sales_customer_id");
        });

        builder.OwnsOne(s => s.Branch, branch =>
        {
            branch.Property(b => b.Id).HasColumnName("branch_id");
            branch.Property(b => b.Name).HasColumnName("branch_name").HasMaxLength(200);
            branch.HasIndex(b => b.Id).HasDatabaseName("ix_sales_branch_id");
        });

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey("SaleId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
