using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Data.Configurations;

/// <summary>EF Core mapping for hosted checkout sessions.</summary>
public sealed class PaymentSessionConfiguration : IEntityTypeConfiguration<PaymentSession>
{
    public void Configure(EntityTypeBuilder<PaymentSession> b)
    {
        b.ToTable("PaymentSessions", PaymentEngineConfigurationConstants.Schema, table =>
        {
            table.HasCheckConstraint("CK_PaymentEngine_PaymentSessions_Status", "\"Status\" BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_PaymentEngine_PaymentSessions_Provider_Normalized", "\"ProviderCode\" = lower(btrim(\"ProviderCode\")) AND length(\"ProviderCode\") > 0");
            table.HasCheckConstraint("CK_PaymentEngine_PaymentSessions_CheckoutUrl_Https", "\"CheckoutUrl\" IS NULL OR \"CheckoutUrl\" LIKE 'https://%'");
        });

        b.HasKey(session => session.Id);
        b.Property(session => session.Id).ValueGeneratedNever();

        b.Property(session => session.ProviderCode)
            .HasMaxLength(PaymentEngineConfigurationConstants.ProviderCodeMaxLength)
            .IsRequired();

        b.Property(session => session.CheckoutUrl)
            .HasMaxLength(PaymentSession.CheckoutUrlMaxLength);

        b.Property(session => session.Status)
            .HasConversion<int>()
            .IsRequired();

        ConfigureGatewayReference(b.OwnsOne(session => session.SessionReference));

        b.HasIndex(session => session.PaymentId)
            .IsUnique()
            .HasFilter("\"Status\" IN (0, 1, 2) AND \"DeletedAt\" IS NULL")
            .HasDatabaseName("UX_PaymentEngine_PaymentSessions_Active_PerPayment");

        b.HasIndex(session => new { session.TenantId, session.Status, session.ExpiresAtUtc })
            .HasDatabaseName("IX_PaymentEngine_PaymentSessions_Tenant_Status_ExpiresAt");

        b.HasIndex(session => new { session.TenantId, session.ProviderCode, session.Status })
            .HasDatabaseName("IX_PaymentEngine_PaymentSessions_Tenant_Provider_Status");
    }

    private static void ConfigureGatewayReference(OwnedNavigationBuilder<PaymentSession, GatewayReference> reference)
    {
        reference.Property(value => value.ProviderCode)
            .HasColumnName("SessionProviderCode")
            .HasMaxLength(PaymentEngineConfigurationConstants.ProviderCodeMaxLength);

        reference.Property(value => value.Reference)
            .HasColumnName("SessionReference")
            .HasMaxLength(PaymentEngineConfigurationConstants.ExternalReferenceMaxLength);

        reference.HasIndex(value => new { value.ProviderCode, value.Reference })
            .IsUnique()
            .HasFilter("\"SessionReference\" IS NOT NULL")
            .HasDatabaseName("UX_PaymentEngine_PaymentSessions_SessionReference");
    }
}
