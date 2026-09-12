using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Modules.Payments.Data.Configurations;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;

namespace RestaurantPos.Api.Modules.Payments.Data;

/// <summary>
/// Single EF Core integration point for the reusable Payment Engine module.
/// </summary>
public static class PaymentEngineModelBuilderExtensions
{
    /// <summary>Applies all Payment Engine mappings without scattering module configuration through PosDbContext.</summary>
    public static void ApplyPaymentEngineConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentSessionConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentEventConfiguration());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (IsPaymentEngineEntity(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(PaymentEngineConfigurationConstants.RowVersionProperty)
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }
    }

    private static bool IsPaymentEngineEntity(Type clrType)
        => clrType == typeof(Payment)
            || clrType == typeof(PaymentAttempt)
            || clrType == typeof(PaymentSession)
            || clrType == typeof(PaymentEvent);
}
