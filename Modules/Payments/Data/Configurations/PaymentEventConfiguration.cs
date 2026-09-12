using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RestaurantPos.Api.Modules.Payments.Domain.Entities;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;

namespace RestaurantPos.Api.Modules.Payments.Data.Configurations;

/// <summary>EF Core mapping for append-only payment audit events.</summary>
public sealed class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> b)
    {
        b.ToTable("PaymentEvents", PaymentEngineConfigurationConstants.Schema, table =>
        {
            table.HasCheckConstraint("CK_PaymentEngine_PaymentEvents_EventName", "length(btrim(\"EventName\")) > 0");
            table.HasCheckConstraint("CK_PaymentEngine_PaymentEvents_Provider_Normalized", "\"ProviderCode\" = lower(btrim(\"ProviderCode\")) AND length(\"ProviderCode\") > 0");
        });

        b.HasKey(paymentEvent => paymentEvent.Id);
        b.Property(paymentEvent => paymentEvent.Id).ValueGeneratedNever();

        b.Property(paymentEvent => paymentEvent.ProviderCode)
            .HasMaxLength(PaymentEngineConfigurationConstants.ProviderCodeMaxLength)
            .IsRequired();

        b.Property(paymentEvent => paymentEvent.EventName)
            .HasMaxLength(PaymentEvent.EventNameMaxLength)
            .IsRequired();

        b.Property(paymentEvent => paymentEvent.ExternalEventId)
            .HasMaxLength(PaymentEngineConfigurationConstants.ExternalReferenceMaxLength);

        b.Property(paymentEvent => paymentEvent.PayloadHash)
            .HasMaxLength(PaymentEngineConfigurationConstants.HashMaxLength);

        ConfigureGatewayReference(b.OwnsOne(paymentEvent => paymentEvent.GatewayReference));

        b.HasOne<PaymentAttempt>()
            .WithMany()
            .HasForeignKey(paymentEvent => paymentEvent.PaymentAttemptId)
            .HasConstraintName("FK_PaymentEngine_PaymentEvents_PaymentAttempts_PaymentAttemptId")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(paymentEvent => paymentEvent.PaymentAttemptId)
            .HasDatabaseName("IX_PaymentEngine_PaymentEvents_PaymentAttemptId");

        b.HasIndex(paymentEvent => paymentEvent.PaymentId)
            .HasDatabaseName("IX_PaymentEngine_PaymentEvents_PaymentId");

        b.HasIndex(paymentEvent => new { paymentEvent.TenantId, paymentEvent.PaymentId, paymentEvent.OccurredAtUtc })
            .HasDatabaseName("IX_PaymentEngine_PaymentEvents_Tenant_Payment_OccurredAt");

        b.HasIndex(paymentEvent => new { paymentEvent.TenantId, paymentEvent.ProviderCode, paymentEvent.ExternalEventId })
            .IsUnique()
            .HasFilter("\"ExternalEventId\" IS NOT NULL AND \"DeletedAt\" IS NULL")
            .HasDatabaseName("UX_PaymentEngine_PaymentEvents_Tenant_Provider_ExternalEvent");

        b.HasIndex(paymentEvent => new { paymentEvent.TenantId, paymentEvent.EventName, paymentEvent.OccurredAtUtc })
            .HasDatabaseName("IX_PaymentEngine_PaymentEvents_Tenant_EventName_OccurredAt");

        b.HasIndex(paymentEvent => new { paymentEvent.TenantId, paymentEvent.PayloadHash })
            .HasFilter("\"PayloadHash\" IS NOT NULL AND \"DeletedAt\" IS NULL")
            .HasDatabaseName("IX_PaymentEngine_PaymentEvents_Tenant_PayloadHash");
    }

    private static void ConfigureGatewayReference(OwnedNavigationBuilder<PaymentEvent, GatewayReference> reference)
    {
        reference.Property(value => value.ProviderCode)
            .HasColumnName("GatewayProviderCode")
            .HasMaxLength(PaymentEngineConfigurationConstants.ProviderCodeMaxLength);

        reference.Property(value => value.Reference)
            .HasColumnName("GatewayReference")
            .HasMaxLength(PaymentEngineConfigurationConstants.ExternalReferenceMaxLength);
    }
}
