using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Data.Configurations;

/// <summary>EF Core mapping for auditable payment attempts.</summary>
public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> b)
    {
        b.ToTable("PaymentAttempts", PaymentEngineConfigurationConstants.Schema, table =>
        {
            table.HasCheckConstraint("CK_PaymentEngine_PaymentAttempts_AttemptNumber", "\"AttemptNumber\" > 0");
            table.HasCheckConstraint("CK_PaymentEngine_PaymentAttempts_Amount_Positive", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_PaymentEngine_PaymentAttempts_Status", "\"Status\" BETWEEN 0 AND 7");
            table.HasCheckConstraint("CK_PaymentEngine_PaymentAttempts_FailureReason", "\"FailureReason\" BETWEEN 0 AND 9");
        });

        b.HasKey(attempt => attempt.Id);
        b.Property(attempt => attempt.Id).ValueGeneratedNever();

        b.Property(attempt => attempt.Operation).HasConversion<int>().IsRequired();
        b.Property(attempt => attempt.Status).HasConversion<int>().IsRequired();
        b.Property(attempt => attempt.FailureReason).HasConversion<int>().IsRequired();

        ConfigureMoney(b.OwnsOne(attempt => attempt.Amount));
        ConfigureGatewayReference(b.OwnsOne(attempt => attempt.GatewayReference));

        b.Navigation(attempt => attempt.Amount).IsRequired();

        b.Property(attempt => attempt.ProviderCorrelationId)
            .HasMaxLength(PaymentEngineConfigurationConstants.ExternalReferenceMaxLength);

        b.Property(attempt => attempt.FailureCode)
            .HasMaxLength(PaymentEngineConfigurationConstants.FailureCodeMaxLength);

        b.Property(attempt => attempt.FailureMessage)
            .HasMaxLength(PaymentEngineConfigurationConstants.FailureMessageMaxLength);

        b.Property(attempt => attempt.RequestHash)
            .HasMaxLength(PaymentEngineConfigurationConstants.HashMaxLength);

        b.Property(attempt => attempt.ResponseHash)
            .HasMaxLength(PaymentEngineConfigurationConstants.HashMaxLength);

        b.HasIndex(attempt => new { attempt.PaymentId, attempt.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("UX_PaymentEngine_PaymentAttempts_Payment_AttemptNumber");

        b.HasIndex(attempt => new { attempt.TenantId, attempt.Status, attempt.StartedAtUtc })
            .HasDatabaseName("IX_PaymentEngine_PaymentAttempts_Tenant_Status_StartedAt");

        b.HasIndex(attempt => new { attempt.TenantId, attempt.PaymentId, attempt.Operation })
            .HasDatabaseName("IX_PaymentEngine_PaymentAttempts_Tenant_Payment_Operation");
    }

    private static void ConfigureMoney(OwnedNavigationBuilder<PaymentAttempt, Money> money)
    {
        money.Property(value => value.Amount)
            .HasColumnName("Amount")
            .HasColumnType(Money.DatabaseType)
            .IsRequired();

        money.Property(value => value.Currency)
            .HasColumnName("Currency")
            .HasMaxLength(Money.CurrencyCodeLength)
            .IsRequired();
    }

    private static void ConfigureGatewayReference(OwnedNavigationBuilder<PaymentAttempt, GatewayReference> reference)
    {
        reference.Property(value => value.ProviderCode)
            .HasColumnName("GatewayProviderCode")
            .HasMaxLength(PaymentEngineConfigurationConstants.ProviderCodeMaxLength);

        reference.Property(value => value.Reference)
            .HasColumnName("GatewayReference")
            .HasMaxLength(PaymentEngineConfigurationConstants.ExternalReferenceMaxLength);

        reference.HasIndex(value => new { value.ProviderCode, value.Reference })
            .IsUnique()
            .HasFilter("\"GatewayReference\" IS NOT NULL")
            .HasDatabaseName("UX_PaymentEngine_PaymentAttempts_GatewayReference");
    }
}
