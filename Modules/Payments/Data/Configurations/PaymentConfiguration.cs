using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RestaurantPos.Api.Models;
using RestaurantPos.Api.Modules.Payments.Domain.ValueObjects;
using PaymentEnginePayment = RestaurantPos.Api.Modules.Payments.Domain.Entities.Payment;

namespace RestaurantPos.Api.Modules.Payments.Data.Configurations;

/// <summary>EF Core mapping for the Payment aggregate root.</summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentEnginePayment>
{
    private static readonly ValueConverter<MerchantReference, string> MerchantReferenceConverter = new(
        reference => reference.Value,
        value => new MerchantReference(value));

    public void Configure(EntityTypeBuilder<PaymentEnginePayment> b)
    {
        b.ToTable("Payments", PaymentEngineConfigurationConstants.Schema, table =>
        {
            table.HasCheckConstraint("CK_PaymentEngine_Payments_Amount_Positive", "\"RequestedAmount\" > 0");
            table.HasCheckConstraint("CK_PaymentEngine_Payments_Amounts_NonNegative", "\"AuthorizedAmount\" >= 0 AND \"CapturedAmount\" >= 0 AND \"RefundedAmount\" >= 0");
            table.HasCheckConstraint("CK_PaymentEngine_Payments_Amount_Caps", "\"CapturedAmount\" <= \"RequestedAmount\" AND \"RefundedAmount\" <= \"CapturedAmount\"");
            table.HasCheckConstraint("CK_PaymentEngine_Payments_Currency_Consistent", "\"RequestedCurrency\" = \"AuthorizedCurrency\" AND \"RequestedCurrency\" = \"CapturedCurrency\" AND \"RequestedCurrency\" = \"RefundedCurrency\"");
            table.HasCheckConstraint("CK_PaymentEngine_Payments_Provider_Normalized", "\"ProviderCode\" = lower(btrim(\"ProviderCode\")) AND length(\"ProviderCode\") > 0");
            table.HasCheckConstraint("CK_PaymentEngine_Payments_Status", "\"Status\" BETWEEN 0 AND 9");
        });

        b.HasKey(payment => payment.Id);
        b.Property(payment => payment.Id).ValueGeneratedNever();

        b.Property(payment => payment.BranchId).IsRequired();
        b.Property(payment => payment.OrderId).IsRequired();

        b.Property(payment => payment.MerchantReference)
            .HasConversion(MerchantReferenceConverter)
            .HasMaxLength(MerchantReference.MaxLength)
            .IsRequired();

        b.Property(payment => payment.ProviderCode)
            .HasMaxLength(PaymentEngineConfigurationConstants.ProviderCodeMaxLength)
            .IsRequired();

        b.Property(payment => payment.Method)
            .HasConversion<int>()
            .IsRequired();

        b.Property(payment => payment.Status)
            .HasConversion<int>()
            .IsRequired();

        b.Property(payment => payment.IdempotencyKeyHash)
            .HasMaxLength(PaymentEngineConfigurationConstants.HashMaxLength);

        b.Property(payment => payment.RequestFingerprintHash)
            .HasMaxLength(PaymentEngineConfigurationConstants.HashMaxLength);

        ConfigureMoney(b.OwnsOne(payment => payment.RequestedAmount), "RequestedAmount", "RequestedCurrency");
        ConfigureMoney(b.OwnsOne(payment => payment.AuthorizedAmount), "AuthorizedAmount", "AuthorizedCurrency");
        ConfigureMoney(b.OwnsOne(payment => payment.CapturedAmount), "CapturedAmount", "CapturedCurrency");
        ConfigureMoney(b.OwnsOne(payment => payment.RefundedAmount), "RefundedAmount", "RefundedCurrency");

        b.Navigation(payment => payment.RequestedAmount).IsRequired();
        b.Navigation(payment => payment.AuthorizedAmount).IsRequired();
        b.Navigation(payment => payment.CapturedAmount).IsRequired();
        b.Navigation(payment => payment.RefundedAmount).IsRequired();

        b.HasOne<Order>()
            .WithMany()
            .HasForeignKey(payment => payment.OrderId)
            .HasConstraintName("FK_PaymentEngine_Payments_Orders_OrderId")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(payment => payment.BranchId)
            .HasConstraintName("FK_PaymentEngine_Payments_Branches_BranchId")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(payment => payment.Attempts)
            .WithOne()
            .HasForeignKey(attempt => attempt.PaymentId)
            .HasConstraintName("FK_PaymentEngine_PaymentAttempts_Payments_PaymentId")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(payment => payment.Sessions)
            .WithOne()
            .HasForeignKey(session => session.PaymentId)
            .HasConstraintName("FK_PaymentEngine_PaymentSessions_Payments_PaymentId")
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(payment => payment.Events)
            .WithOne()
            .HasForeignKey(paymentEvent => paymentEvent.PaymentId)
            .HasConstraintName("FK_PaymentEngine_PaymentEvents_Payments_PaymentId")
            .OnDelete(DeleteBehavior.Restrict);

        b.Navigation(payment => payment.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(payment => payment.Sessions).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(payment => payment.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(payment => new { payment.TenantId, payment.MerchantReference })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL")
            .HasDatabaseName("UX_PaymentEngine_Payments_Tenant_MerchantReference");

        b.HasIndex(payment => new { payment.TenantId, payment.IdempotencyKeyHash })
            .IsUnique()
            .HasFilter("\"IdempotencyKeyHash\" IS NOT NULL AND \"DeletedAt\" IS NULL")
            .HasDatabaseName("UX_PaymentEngine_Payments_Tenant_IdempotencyKey");

        b.HasIndex(payment => new { payment.TenantId, payment.OrderId })
            .HasDatabaseName("IX_PaymentEngine_Payments_Tenant_Order");

        b.HasIndex(payment => payment.BranchId)
            .HasDatabaseName("IX_PaymentEngine_Payments_BranchId");

        b.HasIndex(payment => payment.OrderId)
            .HasDatabaseName("IX_PaymentEngine_Payments_OrderId");

        b.HasIndex(payment => new { payment.TenantId, payment.Status, payment.UpdatedAt })
            .HasDatabaseName("IX_PaymentEngine_Payments_Tenant_Status_UpdatedAt");

        b.HasIndex(payment => new { payment.TenantId, payment.ProviderCode, payment.Status })
            .HasDatabaseName("IX_PaymentEngine_Payments_Tenant_Provider_Status");
    }

    private static void ConfigureMoney(OwnedNavigationBuilder<PaymentEnginePayment, Money> money, string amountColumn, string currencyColumn)
    {
        money.Property(value => value.Amount)
            .HasColumnName(amountColumn)
            .HasColumnType(Money.DatabaseType)
            .IsRequired();

        money.Property(value => value.Currency)
            .HasColumnName(currencyColumn)
            .HasMaxLength(Money.CurrencyCodeLength)
            .IsRequired();
    }
}
