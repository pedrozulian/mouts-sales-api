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

        builder.Ignore(s => s.DomainEvents);

        builder.Property(s => s.SaleNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(s => s.SaleNumber).IsUnique();

        builder.Property(s => s.TotalAmount).HasColumnType("numeric(18,2)");

        builder.OwnsOne(s => s.Customer, customer =>
        {
            customer.Property(c => c.Id).HasColumnName("customer_id");
            customer.Property(c => c.Name).HasColumnName("customer_name").HasMaxLength(200);
        });

        builder.OwnsOne(s => s.Branch, branch =>
        {
            branch.Property(b => b.Id).HasColumnName("branch_id");
            branch.Property(b => b.Name).HasColumnName("branch_name").HasMaxLength(200);
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
