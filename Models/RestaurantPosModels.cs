using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantPos.Api.Models
{
    // Pagination Response Wrapper
    public class PaginatedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    // Enums
    public enum StationType
    {
        Kitchen = 0,
        Bar = 1
    }
    
    public enum StationRouting
    {
        KitchenOnly = 0,
        BarOnly = 1,
        Both = 2
    }
    
    [Flags]
    public enum AllergenType
    {
        None = 0,
        Gluten = 1,
        Dairy = 2,
        Nuts = 4,
        Eggs = 8,
        Fish = 16,
        Shellfish = 32,
        Soy = 64,
        Sesame = 128
    }

    public enum SelectionType
    {
        Single = 0,
        Multiple = 1
    }

    /// <summary>How the modifier group is rendered in the order UI</summary>
    public enum DisplayType
    {
        Radio = 0,           // Single choice (radio buttons)
        Checkbox = 1,        // Multiple choice (checkboxes)
        QuantitySelector = 2 // Stepper per option (e.g. extra cheese ×2)
    }

    /// <summary>How an individual modifier option is priced</summary>
    public enum PricingType
    {
        Fixed = 0,       // Flat amount added (e.g. +2.00)
        Percentage = 1,  // Percentage of product base price (e.g. +10%)
        Included = 2     // Free within limit
    }

    /// <summary>Flags – which order channels an option is available for</summary>
    [Flags]
    public enum OrderTypeFlag
    {
        None = 0,
        DineIn = 1,
        Takeaway = 2,
        Delivery = 4,
        All = DineIn | Takeaway | Delivery
    }

    public enum OrderStatus
    {
        New = 0,
        Preparing = 1,
        Ready = 2,
        Served = 3,
        Paid = 4,
        Cancelled = 5,
        /// <summary>
        /// Successful terminal state after payment, kitchen readiness, and
        /// handoff have all been confirmed.
        /// </summary>
        Completed = 6,
        PendingPayment = 7,
        PaymentCancelled = 8
    }

    public enum OrderType
    {
        DineIn = 0,
        Takeaway = 1,
        Delivery = 2
    }

    public enum OrderSource
    {
        Pos = 0,
        Talabat = 1,
        DeliveryPartner = 2,
        Mobile = 3,
        Online = 4
    }

    public enum OfferDiscountType
    {
        Fixed = 0,
        Percentage = 1
    }

    public enum UserRole
    {
        Admin = 0,
        Waiter = 1,
        Kitchen = 2,
        Cashier = 3,
        TrackerPickup = 4,
        Manager = 5,
        Owner = 6,
        Marketing = 7,
        // Superset of Admin. Carries every Admin permission today; reserved for
        // future sensitive grants (raw-material stock/unit-cost editing). Append
        // only — never reorder existing values.
        SuperAdmin = 8
    }
    
    public enum ContractStatus
    {
        Pending = 0,      // Bekliyor
        Signed = 1,       // İmzalandı
        Terminated = 2    // Sonlandırıldı
    }
    
    public enum BloodType
    {
        Unknown = 0,
        APositive = 1,    // A+
        ANegative = 2,    // A-
        BPositive = 3,    // B+
        BNegative = 4,    // B-
        ABPositive = 5,   // AB+
        ABNegative = 6,   // AB-
        OPositive = 7,    // O+
        ONegative = 8     // O-
    }

    public enum CustomerTier
    {
        Standard = 0,
        Bronze = 1,     // %3 Puan
        Silver = 2,     // %5 Puan
        Gold = 3,       // %10 Puan
        VIP = 4         // %15 Puan
    }
    
    public class Customer : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string? NameAr { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } // Unique Identifier for Search

        public string? Email { get; set; }

        public DateTime? BirthDate { get; set; }

        public CustomerTier Tier { get; set; } = CustomerTier.Standard;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LoyaltyPoints { get; set; } = 0; // Birikmiş Puanlar

        // Marketing Engine: additive, nullable public loyalty reference. No backfill; generated
        // lazily on first wallet creation. Read by the Marketing module only.
        [MaxLength(20)]
        public string? LoyaltyCustomerCode { get; set; }

        public DateTime LastVisit { get; set; }

        // Navigation
        public ICollection<Order> Orders { get; set; }
    }

    // Whether a discount value is a percentage of the base or a flat currency amount.
    // Mirrors the frontend manual-discount OrderDiscountType ("Percentage"/"FixedAmount").
    public enum DiscountValueType
    {
        Percentage = 0,
        FixedAmount = 1
    }

    // Affiliation-based discount group (e.g. "University Students" 10%, "Company Employees" 5.00 off).
    // A cashier applies a group to an order after verifying the customer belongs to it; the
    // customer does NOT need to be a saved Customer. Groups are reusable across orders and
    // tenants (tenant-scoped). Type + value are snapshotted onto the order at creation, so later
    // edits here never change historical orders. Deactivated (not hard-deleted) when retired.
    public class DiscountGroup : BaseEntity
    {
        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? NameAr { get; set; }

        // Percentage (0–100) or flat currency amount, interpreted per DiscountType.
        public DiscountValueType DiscountType { get; set; } = DiscountValueType.Percentage;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } = 0; // e.g. 10.00 (=10%) or 5.00 (=5 off)

        public bool IsActive { get; set; } = true;

        [MaxLength(300)]
        public string? Description { get; set; }

        // Optional cashier hint for what proof to check (e.g. "University Card"). Free text only;
        // no sensitive document/card details are ever stored.
        [MaxLength(120)]
        public string? VerificationNote { get; set; }
    }

    public class User : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string Username { get; set; }
        
        [Required]
        public string PasswordHash { get; set; }
        
        public UserRole Role { get; set; }
        
        // HR/Payroll Fields
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlySalary { get; set; } = 0;
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal CommissionRate { get; set; } = 0; // e.g., 2.00 for 2%
        
        [MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(100)]
        public string? FullNameAr { get; set; }

        /// <summary>
        /// Optional per-cashier receipt printer override. When set, orders
        /// created or paid by this user emit their customer receipt to this
        /// printer instead of the tenant default. Nullable — falls back to
        /// the tenant's default receipt printer.
        /// SetNull on Printer delete keeps the user record valid.
        /// </summary>
        public Guid? ReceiptPrinterId { get; set; }
        public Printer? ReceiptPrinter { get; set; }

        // Navigation Property
        public StaffProfile? StaffProfile { get; set; }
    }
    
    // HR - Staff Profile (1-to-1 with User)
    public class StaffProfile : BaseEntity
    {
        // Foreign Key to User
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        // Identity
        [MaxLength(20)]
        public string? StaffNo { get; set; } // Personel No

        [MaxLength(6)]
        public string? PinCode { get; set; } // Kiosk Giriş PIN
        
        public BloodType BloodType { get; set; } = BloodType.Unknown;
        
        // Contact
        [MaxLength(20)]
        public string? Phone { get; set; }
        
        [MaxLength(500)]
        public string? Address { get; set; }
        
        [MaxLength(500)]
        public string? PhotoUrl { get; set; }
        
        // Employment
        public DateTime? StartDate { get; set; }
        
        public ContractStatus ContractStatus { get; set; } = ContractStatus.Pending;
        
        // Finance
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetSalary { get; set; } = 0; // Net Maaş
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal SgkPremium { get; set; } = 0; // SGK Primi / Brüt Maliyet

        [Column(TypeName = "decimal(18,2)")]
        public decimal HourlyWage { get; set; } = 0; // Saatlik Ücret (Part-time veya Fazla Mesai için)
        
        // Shift Pattern
        [MaxLength(200)]
        public string? WeeklyShiftPattern { get; set; } // e.g., "Pzt-Cum: 09:00-18:00"
        
        // Navigation
        public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    }
    
    // HR - Time Entry (Puantaj)
    public class TimeEntry : BaseEntity
    {
        public Guid StaffId { get; set; }
        public StaffProfile Staff { get; set; }
        
        public DateTime Date { get; set; }
        
        public DateTime? ClockIn { get; set; }
        
        public DateTime? ClockOut { get; set; }

        [MaxLength(500)]
        public string? CheckInNote { get; set; }

        [MaxLength(500)]
        public string? CheckOutNote { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? CheckInLatitude { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? CheckInLongitude { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CheckInDistanceMeters { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? CheckOutLatitude { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? CheckOutLongitude { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CheckOutDistanceMeters { get; set; }

        public int? DurationMinutes { get; set; }
        
        // Computed field
        [NotMapped]
        public double TotalHours
        {
            get
            {
                if (ClockIn.HasValue && ClockOut.HasValue)
                {
                    return (ClockOut.Value - ClockIn.Value).TotalHours;
                }
                return 0;
            }
        }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; } = 0; // Hesaplanan Maliyet (Saat * Ücret)
    }

    // SaaS Tenant Model
    public class Tenant
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string? Domain { get; set; } // e.g. "burgerx" for burgerx.pos.com
        
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Branding
        [MaxLength(500)]
        public string? BusinessName { get; set; }   // Display name shown in UI header

        public string? LogoUrl { get; set; }         // base64 data URL or external URL

        [MaxLength(20)]
        public string? PrimaryColor { get; set; }    // hex e.g. "#f97316"

        [MaxLength(20)]
        public string? PrimaryForeground { get; set; } // hex for text on primary bg

        [MaxLength(20)]
        public string? AccentColor { get; set; }     // secondary accent hex
    }

    // Abstract Base for SaaS Multi-tenancy
    public abstract class BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TenantId { get; set; } // SaaS Tenant Indicator

        // Audit columns - auto-stamped by PosDbContext.SaveChangesAsync
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete - null when active. Excluded by global query filter.
        public DateTime? DeletedAt { get; set; }
    }

    // 1. Product Model - Enterprise PIM
    public class Product : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? DescriptionAr { get; set; }

        public int? Calories { get; set; }

        // Pricing - Enterprise Level
        [Column(TypeName = "decimal(18,2)")]
        public decimal BasePrice { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? CostPrice { get; set; } // Maliyet fiyatı

        [MaxLength(20)]
        public string PricingMode { get; set; } = ProductPricingModes.Manual;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Markup { get; set; } = 1;

        [MaxLength(20)]
        public string MarkupType { get; set; } = ProductMarkupTypes.Multiplier; // Legacy API field

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountedPrice { get; set; } // İndirimli/Kampanyalı fiyat

        // Public-menu-only promotional pricing (السعر الترويجي).
        // Affects ONLY the public menu / informative website. POS, orders and
        // backend pricing logic continue to use BasePrice / DiscountedPrice.
        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomPrice { get; set; }

        public bool UseCustomPrice { get; set; } = false;

        public Guid? CategoryId { get; set; } // Foreign Key to a Category (nullable for migration)
        public Category? Category { get; set; } // Navigation Property
        public Guid? SubcategoryId { get; set; }
        public Subcategory? Subcategory { get; set; }

        // Operational Fields
        public bool IsActive { get; set; } = true;

        // Public-menu-only "Coming Soon" flag. Purely presentational.
        // POS, orders, pricing and kitchen flows ignore this value — it only
        // affects rendering on the public /menu page and the informative site.
        public bool IsSoon { get; set; } = false;

        // Allergen Information
        public AllergenType Allergens { get; set; } = AllergenType.None;
        
        // Station Routing (Kitchen/Bar/Both)
        public StationRouting StationRouting { get; set; } = StationRouting.KitchenOnly;

        // Legacy field - kept for backward compatibility
        public StationType PreparationStation { get; set; } = StationType.Kitchen;

        // Printer Configuration (JSON array of printer GUIDs)
        [Column(TypeName = "text")]
        public string? PrinterIds { get; set; } // Stored as JSON: ["guid1","guid2"]

        // Multi-kitchen routing — nullable so legacy products keep working until
        // a kitchen is assigned. Items without a KitchenId fall back to the
        // tenant's default kitchen (first active) or the receipt-only path.
        public Guid? KitchenId { get; set; }
        public Kitchen? Kitchen { get; set; }

        // When true, the product prints on every active kitchen printer in the
        // tenant and KitchenPrinters mappings are ignored. Defaults to false so
        // legacy products keep their existing single-printer behavior.
        public bool AllKitchenPrinters { get; set; } = false;

        // Multi-printer assignment (see ProductKitchenPrinter). Empty list
        // means: fall back to the legacy Kitchen → first-active-printer rule.
        public ICollection<ProductKitchenPrinter> KitchenPrinters { get; set; } = new List<ProductKitchenPrinter>();
        
        //[Column(TypeName = "text")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// GUID stem (no extension, no path) of the optimized WebP variants
        /// produced by <see cref="Services.ImageProcessingService"/>. When set,
        /// the frontend derives responsive URLs via the
        /// <c>/uploads/images/{key}-{thumb|medium|full}.webp</c> convention and
        /// falls back to <see cref="ImageUrl"/> when this is null (legacy rows).
        /// Nullable + additive: absence must never be treated as an error.
        /// </summary>
        [MaxLength(64)]
        public string? ImageKey { get; set; }

        public DateOnly? AvailableStartDate { get; set; }
        public DateOnly? AvailableEndDate { get; set; }
        public TimeOnly? AvailableFrom { get; set; }
        public TimeOnly? AvailableTo { get; set; }

        public ICollection<OfferProduct> OfferProducts { get; set; } = new List<OfferProduct>();

        // M-N Relationship Navigation
        public ICollection<ProductModifierGroup> ProductModifierGroups { get; set; }
        
        // Recipe
        public ICollection<RecipeItem> RecipeItems { get; set; }

        // Options (Variants) — each option defines its own price and recipe
        public ICollection<ProductOption> Options { get; set; } = new List<ProductOption>();

        /// <summary>
        /// Additional storefront gallery images. Empty for every product that predates the
        /// gallery — the primary image is still <see cref="ImageUrl"/> / <see cref="ImageKey"/>.
        /// </summary>
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }

    /// <summary>A named variant of a product (e.g. "Pita", "Saj"). Selecting an option
    /// overrides both the price and the ingredient list of the parent product.</summary>
    public class ProductOption : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? NameAr { get; set; }

        /// <summary>Fixed selling price for this option (replaces product BasePrice/DiscountedPrice).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        /// <summary>Pre-selected in POS when the product is opened.</summary>
        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public ICollection<ProductOptionRecipeItem> RecipeItems { get; set; } = new List<ProductOptionRecipeItem>();
    }

    /// <summary>One ingredient row belonging to a ProductOption recipe.</summary>
    public class ProductOptionRecipeItem : BaseEntity
    {
        public Guid ProductOptionId { get; set; }
        public ProductOption ProductOption { get; set; } = null!;

        public Guid RawMaterialId { get; set; }
        public RawMaterial RawMaterial { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
    }

    public class Offer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? DescriptionAr { get; set; }

        public string? ImageUrl { get; set; }

        public OfferDiscountType DiscountType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalPrice { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<OfferProduct> OfferProducts { get; set; } = new List<OfferProduct>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    public class OfferProduct
    {
        [Key]
        public int Id { get; set; }

        public int OfferId { get; set; }
        public Offer Offer { get; set; } = null!;

        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public int Quantity { get; set; }
    }

    // 2. Modifier Group Model (e.g. "Pizza Crusts", "Extra Toppings")
    public class ModifierGroup : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string? NameAr { get; set; }

        public SelectionType SelectionType { get; set; } // Single or Multi

        /// <summary>How the group renders in the ordering UI</summary>
        public DisplayType DisplayType { get; set; } = DisplayType.Radio;

        public int MinSelection { get; set; } // e.g. 0 (optional) or 1 (mandatory)
        public int MaxSelection { get; set; } // e.g. 1 or 5

        /// <summary>Shorthand: true ⇒ minSelection >= 1, false ⇒ minSelection = 0</summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>Which order channels this group appears on (flags)</summary>
        public OrderTypeFlag AvailableForOrderTypes { get; set; } = OrderTypeFlag.All;

        /// <summary>Print option names on customer receipt</summary>
        public bool PrintOnReceipt { get; set; } = true;

        /// <summary>Print option names on kitchen ticket</summary>
        public bool PrintInKitchen { get; set; } = true;

        // 1-N Relationship: A group has many options
        public ICollection<Modifier> Modifiers { get; set; }

        // M-N Relationship Navigation
        public ICollection<ProductModifierGroup> ProductGroups { get; set; }
    }

    // 3. Modifier / Option Model (e.g. "Thin Crust", "Extra Cheese")
    public class Modifier : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string? NameAr { get; set; }

        // ── Pricing ──

        /// <summary>How this option is priced (Fixed / Percentage / Included)</summary>
        public PricingType PricingType { get; set; } = PricingType.Fixed;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAdjustment { get; set; } // e.g. +5.00 or 0

        /// <summary>If true the option is always free regardless of PriceAdjustment</summary>
        public bool IsFree { get; set; } = false;

        /// <summary>Max quantity considered "included" (free) before PriceAdjustment kicks in</summary>
        public int FreeQuantityLimit { get; set; } = 0;

        // ── Behaviour ──

        /// <summary>Pre-selected when product is added to cart</summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>Inactive modifiers are hidden from order UI without deletion</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Max quantity a customer can add (0 = unlimited)</summary>
        public int MaxQuantity { get; set; } = 0;

        // ── Inventory link ──

        /// <summary>Optional raw-material / ingredient linked for auto stock deduction</summary>
        public Guid? LinkedRawMaterialId { get; set; }

        /// <summary>Optional product linked when this modifier represents an add-on product.</summary>
        public Guid? LinkedProductId { get; set; }
        public Product? LinkedProduct { get; set; }

        /// <summary>Amount of the raw material consumed per unit of this modifier</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal LinkedMaterialAmount { get; set; } = 0;

        // Foreign Key
        public Guid ModifierGroupId { get; set; }
        public ModifierGroup ModifierGroup { get; set; }

        public ICollection<ModifierRecipeItem> RecipeItems { get; set; } = new List<ModifierRecipeItem>();
    }

    public class ModifierRecipeItem : BaseEntity
    {
        public Guid ModifierId { get; set; }
        public Modifier Modifier { get; set; }

        public Guid RawMaterialId { get; set; }
        public RawMaterial RawMaterial { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
    }

    // 4. Bridge Entity for M-N Relationship
    public class ProductModifierGroup
    {
        // Composite Key components
        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public Guid ModifierGroupId { get; set; }
        public ModifierGroup ModifierGroup { get; set; }
        
        // Optional: Sort order of this group for this specific product
        public int SortOrder { get; set; }

        // Good practice to include TenantId here too for global query filters
        public Guid TenantId { get; set; } 
    }

    // --- Table Category ---

    public class TableCategory : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch? Branch { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? NameAr { get; set; }

        // CSS-friendly color token e.g. "sky", "emerald", "amber", "purple"
        [MaxLength(30)]
        public string? Color { get; set; }

        public ICollection<Table> Tables { get; set; } = new List<Table>();
    }

    // --- Order Models ---

    public enum TableStatus
    {
        Free = 0,
        Occupied = 1,
        Reserved = 2
    }

    // New Table Model
    public class Table : BaseEntity
    {
        public Guid BranchId { get; set; }
        // Nullable: this entity is model-bound directly by TablesController.CreateTable,
        // so a required nav would fail request validation. The FK itself stays required
        // (non-nullable BranchId).
        public Branch? Branch { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? NameAr { get; set; }

        public int Capacity { get; set; }

        public TableStatus Status { get; set; }

        // Optional category
        public Guid? TableCategoryId { get; set; }
        public TableCategory? Category { get; set; }

        // Global incremental ticket ID counter (never reset)
        public int NextTicketId { get; set; } = 1;

        // Tracks the active session ticket for this table (null when not in session)
        public int? CurrentTicketId { get; set; } = null;
    }

    public class Order : BaseEntity
    {
        public string OrderNumber { get; set; }

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        [MaxLength(64)]
        public string? ClientOrderUuid { get; set; }

        [MaxLength(64)]
        public string? PublicOrderNumber { get; set; }

        // Human-friendly number generated from OrderNumberingConfig. It may repeat
        // after the configured reset boundary; OrderNumber is the unique system ID.
        [MaxLength(40)]
        public string? DisplayOrderNumber { get; set; }

        public OrderType OrderType { get; set; } = OrderType.DineIn;
        public OrderSource OrderSource { get; set; } = OrderSource.Pos;
        public Guid? TableId { get; set; } // Can be nullable if generic order
        public Table Table { get; set; } // Foreign Key Navigation

        public Guid? CustomerId { get; set; } // CRM Bağlantısı
        public Customer? Customer { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        /// <summary>
        /// Optional customer-name snapshot for walk-in sales where no <see cref="Customer"/>
        /// record exists. Additive and nullable — nothing in the existing order, payment,
        /// printing or reporting paths reads it. Populated by the retail workbook importer,
        /// whose sales rows carry a name but no phone number (the Customer natural key).
        /// </summary>
        [MaxLength(120)]
        public string? CustomerName { get; set; }

        [MaxLength(200)]
        public string? CustomerEmail { get; set; }

        [MaxLength(64)]
        public string? TalabatOrderNumber { get; set; }

        [MaxLength(120)]
        public string? TalabatCustomerName { get; set; }

        [MaxLength(20)]
        public string? TalabatCustomerPhone { get; set; }

        [MaxLength(50)]
        public string? TalabatPaymentMethod { get; set; }

        public DateTime? TalabatPickupTime { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TalabatDeliveryFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TalabatServiceFee { get; set; }

        public Guid? DeliveryPartnerId { get; set; }
        public DeliveryPartner? DeliveryPartner { get; set; }

        [MaxLength(140)]
        public string? DeliveryPartnerName { get; set; }

        [MaxLength(140)]
        public string? DeliveryPartnerNameAr { get; set; }

        [MaxLength(60)]
        public string? DeliveryPartnerCode { get; set; }

        public bool HasPriceDifference { get; set; } = false;

        [MaxLength(20)]
        public string? PriceDifferenceCorrectionMode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OriginalPartnerTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CorrectPartnerTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalDifferenceAmount { get; set; }

        [MaxLength(64)]
        public string? PartnerOrderNumber { get; set; }

        [MaxLength(120)]
        public string? PartnerCustomerName { get; set; }

        [MaxLength(20)]
        public string? PartnerCustomerPhone { get; set; }

        [MaxLength(50)]
        public string? PartnerPaymentMethod { get; set; }

        public DateTime? PartnerPickupTime { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PartnerDeliveryFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PartnerServiceFee { get; set; }

        [MaxLength(100)]
        public string TableName { get; set; } = string.Empty;
        
        // Persistent incremental ticket ID for this table (1,2,3..., never reset)
        public int TicketId { get; set; } = 0;
        
        // Waiter tracking for commission calculation
        public Guid? WaiterId { get; set; }
        public User? Waiter { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FoodSubtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CustomerDeliveryFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualDeliveryCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryMargin { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MarketplaceDeliveryFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MarketplaceServiceFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetRestaurantRevenue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostSharingTotalCommission { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostSharingRestaurantShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostSharingCounterpartyShare { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostSharingNetSettlement { get; set; }

        [Column(TypeName = "text")]
        public string? CostSharingDetailsJson { get; set; }

        public DateTime? CostSharingCalculatedAt { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        /// <summary>
        /// Transfer/transaction reference the customer declared when placing the order, captured
        /// only for methods the merchant configured with
        /// <see cref="PaymentMethod.RequiresReferenceNumber"/>. Additive and nullable: it is a
        /// snapshot of a claim, never proof of payment — <see cref="Payment.ReferenceNumber"/>
        /// remains the reference of a settled payment.
        /// </summary>
        [MaxLength(120)]
        public string? SubmittedPaymentReference { get; set; }

        public Guid? PaidByUserId { get; set; }
        public User? PaidByUser { get; set; }

        public DateTime? PaidAt { get; set; }
        
        public OrderStatus Status { get; set; }

        [MaxLength(20)]
        public string? CancelMode { get; set; }

        public Guid? CanceledById { get; set; }
        public User? CanceledBy { get; set; }

        public DateTime? CanceledAt { get; set; }

        [MaxLength(500)]
        public string? CancelReason { get; set; }

        /// <summary>
        /// Updated whenever items are added, removed, or modified on an existing order.
        /// Used by the kitchen screen to sort orders with the most recently changed on top.
        /// </summary>
        public DateTime? LastUpdatedAt { get; set; }

        /// <summary>
        /// Canonical kitchen-prep-complete timestamp: the first moment every item on
        /// the order became ready. Stamped once by <see cref="Helpers.OrderCompletion"/>
        /// and never overwritten. Null for legacy orders created before this field
        /// existed and for orders that never reached full readiness — those are
        /// excluded from preparation-time analytics. Reusable by future branch /
        /// warehouse reporting without a further schema change.
        /// </summary>
        public DateTime? ReadyAt { get; set; }

        /// <summary>
        /// Delivery handoff-to-courier timestamp. This is an independent signal,
        /// not a parallel order status; Served remains the customer handoff and
        /// Completed remains the successful terminal state.
        /// </summary>
        public DateTime? DispatchedAt { get; set; }

        /// <summary>
        /// Server-authoritative timestamp stamped every time the server confirms a
        /// write against this order (create, pay, status change). Surfaced to the
        /// offline client so it can distinguish "queued locally" from "seen by server".
        /// Never set by input DTOs — server-owned only.
        /// </summary>
        public DateTime? SyncedAt { get; set; }

        public DateTime? ScheduledFor { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercentage { get; set; } = 0; // e.g. 10.00 for 10%

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; } = 0; // Sum of item line totals (excl. complimentary)

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0; // Subtotal × DiscountPercentage / 100

        // Affiliation discount group applied to this order, snapshotted at creation time.
        // Applied after the manual order discount and before service charge / tax. Kept
        // separate from DiscountPercentage/DiscountAmount so it stays visible on its own line
        // and is preserved historically even if the group's configuration changes later.
        // DiscountGroupId is a soft reference (no hard FK) — the Name/Type/Value/Amount
        // snapshot is what guarantees historical immutability and drives reporting.
        public Guid? DiscountGroupId { get; set; }

        [MaxLength(120)]
        public string? DiscountGroupName { get; set; } // Snapshot of the group name at apply time

        // Snapshotted type + configured value (percentage OR flat amount). Recalc paths
        // (add/remove item, payment) recompute the amount from these so a fixed discount
        // survives item edits; DiscountGroupAmount is the resulting applied amount.
        public DiscountValueType DiscountGroupType { get; set; } = DiscountValueType.Percentage;

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountGroupValue { get; set; } = 0; // 10.00 (=10%) or 5.00 (=5 off)

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountGroupAmount { get; set; } = 0; // Computed applied amount (on Subtotal − DiscountAmount)

        [Column(TypeName = "decimal(5,2)")]
        public decimal ServiceChargeRate { get; set; } = 0; // e.g. 10.00 for 10%

        [Column(TypeName = "decimal(18,2)")]
        public decimal ServiceChargeAmount { get; set; } = 0; // Computed service charge amount

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 0; // e.g. 18.00 for 18%

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0; // Computed tax amount

        public bool IsVoucherApplied { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal VoucherDiscountAmount { get; set; } = 0;

        public DateTime? VoucherAppliedAt { get; set; }

        // Financials (COGS & Profit) - Calculated upon payment
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; } = 0; // Cost of Goods Sold

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetProfit { get; set; } = 0; // Revenue - Cost

        // Cash payment details
        [Column(TypeName = "decimal(18,2)")]
        public decimal? AmountTendered { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ChangeAmount { get; set; }

        public int? OfferId { get; set; }
        public Offer? Offer { get; set; }

        [MaxLength(500)]
        public string? OfferNote { get; set; }

        // --- Delivery snapshot (populated only when OrderType == Delivery) ---
        // Values are copied from the DeliveryZone at order time so historical orders are
        // immune to later zone edits. All nullable => fully backward compatible with
        // existing orders and existing create/calculation flows.
        public Guid? DeliveryZoneId { get; set; }

        [MaxLength(120)]
        public string? DeliveryZoneName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DeliveryFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DeliveryCost { get; set; }

        public DeliveryPaymentMode? DeliveryPaymentMode { get; set; }

        [MaxLength(500)]
        public string? DeliveryAddress { get; set; }

        [MaxLength(500)]
        public string? DeliveryNotes { get; set; }

        // Navigation
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<VoucherUsageAudit> VoucherUsageAudits { get; set; } = new List<VoucherUsageAudit>();
        public OrderProfitabilitySnapshot? ProfitabilitySnapshot { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; }
    }

    /// <summary>
    /// Per-tenant monthly counter that backs the human-friendly DisplayOrderNumber.
    /// One row per (TenantId, OrderTypeCode, Year, Month). Concurrency safety is
    /// provided by a database row-level lock (Postgres SELECT ... FOR UPDATE inside
    /// the order-creation transaction); there is no in-memory counter.
    /// </summary>
    public class OrderDisplaySequence : BaseEntity
    {
        // "TA" | "DI" | "DP" | "QR" | "GLOBAL"
        // "GLOBAL" is used when OrderNumberingConfig.Scope = Global so a single
        // counter is shared across all order types.
        [Required]
        [MaxLength(8)]
        public string OrderTypeCode { get; set; } = string.Empty;

        // Year/Month kept for historical inspection and back-compat — older code paths
        // may still join on them. Always populated from utcNow at insert time.
        public int Year { get; set; }
        public int Month { get; set; }

        // Identifies the reset bucket. Drives uniqueness with (TenantId, OrderTypeCode).
        //   Never  -> "ALL"
        //   Daily  -> "DAY:YYYYMMDD"
        //   Month  -> "MONTH:YYYYMM"     (existing rows backfilled to this shape)
        //   Yearly -> "YEAR:YYYY"
        //   Shift  -> "SHIFT:<guid>"     ("SHIFT:NONE" when no open shift)
        [Required]
        [MaxLength(48)]
        public string BucketKey { get; set; } = string.Empty;

        // The value that will be assigned to the NEXT order under this bucket.
        public long NextValue { get; set; } = 1;
    }

    public enum OrderNumberResetStrategy
    {
        Never      = 0,
        Daily      = 1,
        Monthly    = 2,
        Yearly     = 3,
        ShiftOpen  = 4,
        ShiftClose = 5
    }

    public enum OrderNumberSequenceScope
    {
        Global        = 0,
        PerOrderType  = 1
    }

    public enum ActiveShiftRule
    {
        Unlimited           = 0,
        SinglePerBranch     = 1,
        SinglePerPosDevice  = 2,
        SinglePerCashier    = 3
    }

    public enum ConfigAuditEventType
    {
        NumberingConfigChanged = 0,
        ShiftRulesChanged      = 1,
        ShiftOpened            = 2,
        ShiftClosed            = 3,
        ShiftForceClosed       = 4,
        CostSharingConfigChanged = 5,
        BranchConfigChanged    = 6,
        UserBranchAssignmentChanged = 7,
        BranchContextChanged = 8,
        BranchConfigurationChanged = 9
    }

    /// <summary>
    /// Append-only audit trail for configuration changes and shift lifecycle events.
    /// PreviousValue / NewValue are JSON snapshots of the relevant entity. UserId may
    /// be null for system-triggered events (none currently — kept for future).
    /// </summary>
    public class ConfigAuditLog : BaseEntity
    {
        public ConfigAuditEventType EventType { get; set; }

        public Guid? ChangedByUserId { get; set; }

        [MaxLength(150)]
        public string? ChangedByUserName { get; set; }

        /// <summary>Subject Id: shift id for shift events, null otherwise.</summary>
        public Guid? TargetId { get; set; }

        [MaxLength(16)]
        public string? BranchCode { get; set; }

        public string? PreviousValue { get; set; }
        public string? NewValue      { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Per-tenant configuration for shift open/close rules. All flags default to
    /// the permissive value so the legacy behavior is preserved when no row exists.
    /// Enforced in <see cref="Services.ICashierShiftService"/> (open + close paths).
    /// </summary>
    public class ShiftRulesConfig : BaseEntity
    {
        // ── Opening Rules ──────────────────────────────────────────────────
        public ActiveShiftRule ActiveShiftRule { get; set; } = ActiveShiftRule.Unlimited;

        // ── Closing Rules (hard requirements — checked before close) ───────
        public bool RequireAllOrdersPaid      { get; set; } = false;
        public bool RequireAllOrdersReady     { get; set; } = false;
        public bool RequireAllOrdersServed    { get; set; } = false;
        public bool RequireAllOrdersCompleted { get; set; } = false;

        // ── Closing Rules (permissive overrides — when true, that status is OK) ─
        public bool AllowPendingOrders               { get; set; } = true;
        public bool AllowPreparingOrders             { get; set; } = true;
        public bool AllowReadyOrders                 { get; set; } = true;

        /// <summary>
        /// DEPRECATED — kept for backward compat with stored rows and existing
        /// API clients. Has NO effect on the close validator: completion is now
        /// determined uniformly across all OrderTypes via
        /// <c>Helpers.OrderCompletion.IsCompleted</c>, so Delivery / Partner
        /// orders share the same gates as DineIn / Takeaway.
        /// </summary>
        public bool AllowPendingDeliveryOrders       { get; set; } = true;

        public bool AllowPendingCancellationRequests { get; set; } = true;

        // ── Force close ────────────────────────────────────────────────────
        // Per user spec: simple on/off — no manager approval flow. When true,
        // a cashier may close past blocking validation by passing force=true.
        public bool AllowForcedShiftClose { get; set; } = false;

        // Convenience: blanket override that skips the validation engine entirely.
        // Useful for environments that intentionally close with open orders.
        public bool AllowShiftCloseWithOpenOrders { get; set; } = false;
    }

    /// <summary>
    /// Per-tenant configuration for human-friendly order number generation.
    /// Absence of a row keeps the legacy TA-YYYYMM-N format (Monthly + PerOrderType).
    /// </summary>
    public class OrderNumberingConfig : BaseEntity
    {
        public OrderNumberResetStrategy ResetStrategy { get; set; } = OrderNumberResetStrategy.Monthly;
        public OrderNumberSequenceScope Scope         { get; set; } = OrderNumberSequenceScope.PerOrderType;

        // Optional override of the leading code. When null, channel code (TA/DI/DP/QR/GLOBAL) is used.
        [MaxLength(16)]
        public string? Prefix { get; set; }

        public bool IncludeDate        { get; set; } = false;
        public bool IncludeMonth       { get; set; } = true;
        public bool IncludeYear        { get; set; } = true;
        public bool IncludeShiftNumber { get; set; } = false;
        public bool IncludeBranchCode  { get; set; } = false;

        [MaxLength(16)]
        public string? BranchCode { get; set; }
    }

    public class PublicReceiptToken
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid OrderId { get; set; }

        [MaxLength(96)]
        public string Token { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ExpireAtUtc { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Payment : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; }

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        [MaxLength(120)]
        public string? ClientActionId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(50)]
        public string Method { get; set; } = string.Empty;

        public Guid? PaymentMethodId { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }

        [MaxLength(120)]
        public string? PaymentMethodName { get; set; }

        [MaxLength(120)]
        public string? PaymentMethodNameAr { get; set; }

        [MaxLength(60)]
        public string? PaymentMethodCode { get; set; }

        [MaxLength(120)]
        public string? ReferenceNumber { get; set; }

        public CostSharingMode CostSharingMode { get; set; } = CostSharingMode.RestaurantBearsAll;

        public CostSharingScope CostSharingScope { get; set; } = CostSharingScope.PerPartnerOrCard;

        [Column(TypeName = "decimal(5,2)")]
        public decimal CostSharingCommissionPercentage { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal CostSharingRestaurantPercentage { get; set; } = 100m;

        [Column(TypeName = "decimal(5,2)")]
        public decimal CostSharingCounterpartyPercentage { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostSharingCommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostSharingRestaurantShareAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostSharingCounterpartyShareAmount { get; set; }

        public Guid? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }
    }

    public class VoucherUsageAudit : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; }

        public DateOnly BusinessDate { get; set; }
        public DateTime UsedAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [MaxLength(50)]
        public string VoucherCode { get; set; } = string.Empty;

        public Guid? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }
    }

    public class PartnerPriceOverrideAudit : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OldPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NewPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OldDiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NewDiscountAmount { get; set; }

        [MaxLength(60)]
        public string? ReasonCode { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public Guid? DeliveryPartnerId { get; set; }

        [MaxLength(140)]
        public string? DeliveryPartnerName { get; set; }

        [MaxLength(140)]
        public string? DeliveryPartnerNameAr { get; set; }

        [MaxLength(60)]
        public string? DeliveryPartnerCode { get; set; }

        [MaxLength(20)]
        public string? CorrectionMode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OriginalTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CorrectTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DifferenceAmount { get; set; }

        public Guid? UpdatedBy { get; set; }
    }

    public class OrderItem : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; }

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid ProductId { get; set; } // Reference to original product
        public Product Product { get; set; } // Navigation Property
        
        [MaxLength(200)]
        public string ProductName { get; set; } // Snapshot

        public int Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // Unit Price at the moment of order

        [Column(TypeName = "decimal(18,2)")]
        public decimal? UnitPriceSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LineTotalSnapshot { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PartnerPriceSnapshot { get; set; }

        public bool HasPartnerDiscountOverride { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PartnerOriginalUnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PartnerDiscountedUnitPrice { get; set; }

        [MaxLength(500)]
        public string? PartnerDiscountReason { get; set; }

        public DateTime? PartnerDiscountUpdatedAt { get; set; }
        public Guid? PartnerDiscountUpdatedBy { get; set; }
        public User? PartnerDiscountUpdatedByUser { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsComplimentary { get; set; } = false;
        public bool IsReady { get; set; } = false;

        /// <summary>
        /// True when this item was added to an existing order (not part of the original order).
        /// Cleared when the item is marked ready. Used for kitchen "NEW" badge.
        /// </summary>
        public bool IsNewlyAdded { get; set; } = false;

        public DateTime? StockDeductedAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal StockDeductedCost { get; set; } = 0;

        /// <summary>
        /// The Offer.Id this item belongs to (null = regular item).
        /// Used to group items into multiple offer bundles for display.
        /// </summary>
        public int? OfferLineId { get; set; }
        public string? OfferLineName { get; set; }
        public string? OfferLineNameAr { get; set; }

        // Selected product option snapshot (null = no option selected → default recipe used)
        public Guid? SelectedOptionId { get; set; }

        [MaxLength(200)]
        public string? SelectedOptionName { get; set; }

        [MaxLength(200)]
        public string? SelectedOptionNameAr { get; set; }

        // Navigation
        public ICollection<OrderItemModifier> Modifiers { get; set; }
        public ICollection<OrderItemRecipeSnapshot> RecipeSnapshotItems { get; set; }
    }

    public class OrderItemModifier : BaseEntity
    {
        public Guid OrderItemId { get; set; }
        public OrderItem OrderItem { get; set; }

        public Guid? ModifierId { get; set; }

        [MaxLength(200)]
        public string ModifierName { get; set; } // Snapshot Name (e.g. "Acılı")

        [MaxLength(200)]
        public string? ModifierNameAr { get; set; } // Snapshot Arabic Name

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // Snapshot Price (e.g. 10.00)

        public int Quantity { get; set; } = 1; // Modifier quantity (e.g. 2x Extra Cheese)
    }

    public class OrderItemRecipeSnapshot : BaseEntity
    {
        public Guid OrderItemId { get; set; }
        public OrderItem OrderItem { get; set; }

        public Guid RawMaterialId { get; set; }
        public RawMaterial RawMaterial { get; set; }

        public Guid? SourceModifierId { get; set; }

        /// <summary>Set when the snapshot row was generated from a ProductOption recipe.</summary>
        public Guid? SourceOptionId { get; set; }

        /// <summary>Set when an ingredient-level alternative overrode the original RecipeItem.</summary>
        public Guid? SourceRecipeItemId { get; set; }
        public Guid? SourceAlternativeId { get; set; }

        /// <summary>Snapshot of the alternative's display name at order time.
        /// Survives later deletion of the RecipeItemAlternative row.</summary>
        [MaxLength(200)]
        public string? SourceAlternativeName { get; set; }

        [MaxLength(200)]
        public string? SourceAlternativeNameAr { get; set; }

        [MaxLength(200)]
        public string RawMaterialName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? RawMaterialNameAr { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }
    }

    // Shift/Time Tracking Model
    public class Shift : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; }
        
        public DateTime ClockIn { get; set; }
        public DateTime? ClockOut { get; set; }
        
        // Calculated field
        public double TotalHours => ClockOut.HasValue 
            ? (ClockOut.Value - ClockIn).TotalHours 
            : 0;
    }
    // --- Inventory Models ---
    
    public enum Unit
    {
        Gram = 0,
        Kilogram = 1,
        Adet = 2,
        Litre = 3,
        Mililitre = 4
    }

    public class RawMaterial : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } // Örn: Kıyma, Kola

        [MaxLength(200)]
        public string? NameAr { get; set; }

        public Unit Unit { get; set; } // Örn: Gram, Adet

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentStock { get; set; } // Mevcut Stok

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumAlertLevel { get; set; } // Uyarı Sınırı

        [Column(TypeName = "decimal(18,3)")]
        public decimal CostPerUnit { get; set; } // Birim Maliyeti

        public bool ShowInMenu { get; set; }

        public bool IsPostPrice { get; set; }

        // Multi-warehouse: when set, consumption sources from this warehouse;
        // otherwise the tenant's Main warehouse is used. Nullable for backward
        // compatibility — existing materials migrate cleanly.
        public Guid? DefaultWarehouseId { get; set; }
        public Warehouse? DefaultWarehouse { get; set; }
    }

    public class RecipeItem : BaseEntity
    {
        public Guid ProductId { get; set; }
        public Product Product { get; set; }

        public Guid RawMaterialId { get; set; }
        public RawMaterial RawMaterial { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // Reçetedeki miktar

        /// <summary>Ingredient-level alternatives the cashier can pick instead of RawMaterial.
        /// Each alternative replaces only THIS recipe row (not the full product recipe).</summary>
        public ICollection<RecipeItemAlternative> Alternatives { get; set; } = new List<RecipeItemAlternative>();
    }

    public enum RecipeItemAlternativePricingType
    {
        Included = 0,
        PriceDifference = 1,
        FixedOverride = 2
    }

    /// <summary>An interchangeable raw material for a specific RecipeItem
    /// (e.g. swap "Pita" for "Saj" on a shawarma). Only the parent RecipeItem is
    /// replaced in the snapshot — the rest of the recipe is unchanged.</summary>
    public class RecipeItemAlternative : BaseEntity
    {
        public Guid RecipeItemId { get; set; }
        public RecipeItem RecipeItem { get; set; } = null!;

        public Guid RawMaterialId { get; set; }
        public RawMaterial RawMaterial { get; set; } = null!;

        /// <summary>Optional display override. Falls back to RawMaterial.Name when null.</summary>
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// <summary>Replacement price for this recipe row when the alternative is selected.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAdjustment { get; set; } = 0m;

        public RecipeItemAlternativePricingType PricingType { get; set; } = RecipeItemAlternativePricingType.PriceDifference;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomerAdditionalPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? FixedOverridePrice { get; set; }

        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
    }

    // --- Enterprise Inventory (FIFO) ---

    // --- Procurement / Satın Alma ---

    public enum PurchaseOrderStatus
    {
        Draft = 0,      // Taslak
        Received = 1,   // Teslim Alındı (Batches created, not yet in stock)
        Approved = 2,   // Onaylandı (Stock updated with weighted average cost)
        Cancelled = 3   // İptal
    }

    public enum BatchStatus
    {
        Good = 0,
        Finished = 1,
        Expired = 2
    }

    public class Supplier : BaseEntity
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string? NameAr { get; set; }

        [MaxLength(50)]
        public string? TaxNumber { get; set; }

        [MaxLength(200)]
        public string? VendorCode { get; set; } // Tedarikçi Kodu

        [MaxLength(200)]
        [EmailAddress(ErrorMessage = "Email must be a valid email address")]
        public string? Email { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^\+?[0-9]{7,15}$", ErrorMessage = "Mobile number must contain 7-15 digits and optional '+' prefix")]
        public string? MobileNumber { get; set; }

        // Kept for backward compatibility - will be migrated to MobileNumber
        [Obsolete("Use Email and MobileNumber instead")]
        public string? ContactInfo { get; set; }

        public int LeadTimeDays { get; set; } = 3; // Ortalama Teslim Süresi

        // Additive, nullable, retail-sourced. Populated by the retail workbook importer;
        // null for every supplier created through the existing procurement flow, which
        // neither reads nor writes them.
        [MaxLength(80)]
        public string? Country { get; set; }

        [MaxLength(300)]
        public string? Website { get; set; }

        /// <summary>Brands this supplier carries, as free text. A supplier↔brand relation
        /// replaces this once brands become a first-class entity.</summary>
        [MaxLength(300)]
        public string? Brands { get; set; }

        /// <summary>Best-selling products this supplier is known for, as free text. Sourced
        /// from the retail workbook; unstructured by nature, so stored verbatim.</summary>
        [MaxLength(1000)]
        public string? BestSellers { get; set; }
    }

    public class PurchaseOrder : BaseEntity
    {
        public Guid BranchId { get; set; }
        // Nullable: this entity is model-bound directly by ProcurementController
        // Create/Update actions, so a required nav would reject every request that
        // (correctly) omits it. The FK itself stays required (non-nullable BranchId).
        public Branch? Branch { get; set; }

        public Guid SupplierId { get; set; }
        public Supplier Supplier { get; set; }

        [MaxLength(50)]
        public string OrderNumber { get; set; } // Sipariş No

        [MaxLength(50)]
        public string? InvoiceNumber { get; set; } // Fatura No (Teslim alırken girilir)
        
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

        public DateTime ExpectedDate { get; set; }
        
        public DateTime? ReceivedDate { get; set; }
        
        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalAmount { get; set; }

        public int ItemCount { get; set; } = 0;

        [Column(TypeName = "decimal(18,3)")]
        public decimal UnitCost { get; set; } = 0; // Unit cost snapshot at order creation time

        public ICollection<PurchaseOrderItem> Items { get; set; }

        public ExpenseInvoice? SupplierInvoice { get; set; }
    }

    public class PurchaseOrderItem : BaseEntity
    {
        public Guid PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        public Guid BranchId { get; set; }
        // Nullable for the same reason as PurchaseOrder.Branch — items arrive
        // model-bound inside the PurchaseOrder payload.
        public Branch? Branch { get; set; }

        public Guid RawMaterialId { get; set; }
        public RawMaterial? RawMaterial { get; set; }

        [MaxLength(200)]
        public string? RawMaterialName { get; set; } // Store the name at time of order

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal UnitPrice { get; set; } // Alış Birim Fiyatı
    }

    public class StockBatch : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid MaterialId { get; set; }
        public RawMaterial RawMaterial { get; set; }

        public Guid? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        [MaxLength(100)]
        public string BatchNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PurchaseOrderNumber { get; set; } // Reference to PO number for easy lookup

        [Column(TypeName = "decimal(18,3)")]
        public decimal UnitCost { get; set; } // Unit purchase cost

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; } // Initial received quantity

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingQuantity { get; set; } // Remaining quantity after consumption

        [Column(TypeName = "decimal(18,3)")]
        public decimal TotalCost { get; set; } // Total cost = Quantity * UnitCost (stored for query performance)

        public DateTime ExpiryDate { get; set; }

        public bool IsApproved { get; set; } = false; // Track if this batch has been approved and inventory updated

        public BatchStatus Status { get; set; } = BatchStatus.Good;

        // Expiry tracking (added for Waste/Expiry feature)
        public bool NearExpiryNotified { get; set; } = false;
        public bool ExpiredWasteLogged { get; set; } = false;
    }

    public enum StockAdjustmentType
    {
        // Manual quantity correction performed by a SuperAdmin.
        ManualSuperAdmin = 0
    }

    /// <summary>Distinguishes what a manual adjustment changed. Quantity is the
    /// default so existing rows and the stock-quantity path stay unambiguous.</summary>
    public enum StockAdjustmentKind
    {
        StockQuantity = 0,
        UnitCost = 1
    }

    /// <summary>
    /// Immutable audit trail for a manual raw-material adjustment performed by a
    /// SuperAdmin. Covers both quantity changes (applied through the batch/FIFO
    /// engine) and unit-cost changes (revaluation of remaining Good batches);
    /// <see cref="Kind"/> discriminates. One row per adjustment — never updated.
    /// Quantity fields are populated for quantity rows; the nullable cost fields
    /// are populated for cost rows.
    /// </summary>
    public class StockAdjustmentLog : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid MaterialId { get; set; }
        public RawMaterial RawMaterial { get; set; } = null!;

        public StockAdjustmentKind Kind { get; set; } = StockAdjustmentKind.StockQuantity;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousStock { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NewStock { get; set; }

        // Signed delta: NewStock - PreviousStock (+ increase / - decrease).
        [Column(TypeName = "decimal(18,2)")]
        public decimal AdjustmentQuantity { get; set; }

        // Populated only for UnitCost adjustments (null for quantity rows).
        [Column(TypeName = "decimal(18,3)")]
        public decimal? PreviousUnitCost { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? NewUnitCost { get; set; }

        public Unit Unit { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public Guid PerformedByUserId { get; set; }
        public User PerformedByUser { get; set; } = null!;

        // Snapshot — survives user renames/deletes so the audit stays readable.
        [MaxLength(150)]
        public string PerformedByUserName { get; set; } = string.Empty;

        public StockAdjustmentType AdjustmentType { get; set; } = StockAdjustmentType.ManualSuperAdmin;
    }

    // Cashier Balance Shift
    public enum VarianceClassification
    {
        Match = 0,
        Surplus = 1,
        Deficit = 2
    }

    public class CashierBalanceShift : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        // Cashier whose cash drawer is being tracked
        public Guid CashierId { get; set; }
        public User Cashier { get; set; }

        [MaxLength(120)]
        public string? OpenTransactionId { get; set; }

        [MaxLength(120)]
        public string? CloseTransactionId { get; set; }

        [Required]
        [MaxLength(150)]
        public string CashierName { get; set; } // snapshot — survives user renames

        // Manager who opened the shift
        public Guid OpenedByManagerId { get; set; }
        public User OpenedByManager { get; set; }

        public int? ShiftNumber { get; set; }

        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,3)")]
        public decimal OpeningBalance { get; set; }

        // Filled on shift close
        public DateTime? ClosedAt { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? ClosingBalance { get; set; }

        /// <summary>ClosingBalance − OpeningBalance (auto-computed on close)</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal? Variance { get; set; }

        public VarianceClassification? Classification { get; set; }

        public int? PaidOrderCount { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? OrdersTotal { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? CashOrdersTotal { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? CardOrdersTotal { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? ExpectedBalance { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? RefundsTotal { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal? NetTotal { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? ClosingComment { get; set; }

        // Optional POS device identifier captured at open time. Only meaningful
        // when ShiftRulesConfig.ActiveShiftRule = SinglePerPosDevice — left null
        // otherwise, so existing callers/clients are unaffected.
        [MaxLength(120)]
        public string? PosDeviceId { get; set; }

        // Force-close audit fields. Populated only when the close went through
        // the AllowForcedShiftClose path. Null on normal closes (back-compat).
        public bool ForceClosed { get; set; } = false;

        [MaxLength(500)]
        public string? ForceCloseReason { get; set; }

        public bool IsClosed => ClosedAt.HasValue;
        public bool IsOpen => !ClosedAt.HasValue;
    }

    // --- Waste & Cancel Enums ---

    public enum WasteCategory
    {
        CancelProduct = 0,
        ExpiryProduct = 1,
        ManualProduct = 2,
        ManualMaterial = 3
    }

    public enum WasteLogType
    {
        Cancel = 0,
        Waste = 1
    }

    public enum ManualWasteType
    {
        EXPIRED = 0,
        DAMAGED = 1,
        GIFT = 2,
        STOLEN = 3,
        STAFF_MEAL = 4,
        TESTING = 5,
        WRONG_PREPARATION = 6,
        CUSTOMER_COMPENSATION = 7,
        OTHER = 8
    }

    public enum WasteLogStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public enum WasteLogAuditAction
    {
        Created = 0,
        Approved = 1,
        Rejected = 2,
        Updated = 3
    }

    public enum InventoryTransactionType
    {
        Waste = 0,
        WasteReversal = 1
    }

    public enum CancelledByRole
    {
        Cashier = 0,
        Waiter = 1,  // Garçon in the POS spec
        Manager = 2
    }

    public enum CancellationApprovalStatus
    {
        PendingKitchenApproval = 0,
        ApprovedCancelled = 1,
        Rejected = 2,
        Waste = 3
    }

    public enum KitchenCancellationDecision
    {
        ApprovedCancel = 0,
        Rejected = 1,
        Waste = 2,
        // Paid-order settlements (manager approval queue): refund the customer, optionally
        // wasting the prepared inventory. Stored as int — appended, never reordered.
        Refund = 3,
        RefundWaste = 4
    }

    public static class OrderCancelModes
    {
        public const string Direct = "DIRECT";
        public const string Kitchen = "KITCHEN";

        public static bool IsDirect(string? mode)
            => string.Equals(mode, Direct, StringComparison.OrdinalIgnoreCase);

        public static bool IsKnown(string? mode)
            => string.IsNullOrWhiteSpace(mode) ||
               IsDirect(mode) ||
               string.Equals(mode, Kitchen, StringComparison.OrdinalIgnoreCase);
    }

    // Notification System
    public enum NotificationType
    {
        NewOrder = 0,
        OrderReady = 1,
        OrderPaid = 2,
        LowStock = 3,
        ExpiryWarning = 4,
        NewItemAdded = 5,
        ItemReady = 6,
        BalanceVariance = 7,
        // Waste / Cancel / Expiry
        OrderCancelled = 8,
        ItemExpired = 9,
        ItemNearExpiry = 10,
        // Shift & maintenance alerts
        ShiftLeftOpen = 11,
        Loyalty = 12,
        Promotion = 13,
        General = 14,
        PaymentAdjustmentRequested = 15
    }

    public class Notification : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public NotificationType Type { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(200)]
        public string? TitleAr { get; set; }

        [Required]
        [MaxLength(500)]
        public string Message { get; set; }

        [MaxLength(500)]
        public string? MessageAr { get; set; }

        /// <summary>Reference to the related entity (OrderId, MaterialId, etc.)</summary>
        [MaxLength(100)]
        public string? ReferenceId { get; set; }

        /// <summary>Target role filter: "Kitchen", "Cashier", "Admin", "Waiter", or null for all.</summary>
        [MaxLength(50)]
        public string? TargetRole { get; set; }

        public Guid? CustomerId { get; set; }

        public bool IsRead { get; set; } = false;
    }

    // ─── Waste Log ────────────────────────────────────────────────────
    /// <summary>
    /// Tracks every wasted item — either from order cancellation or batch expiry.
    /// category = CancelProduct → ItemId is ProductId; ExpiryProduct → ItemId is RawMaterialId.
    /// </summary>
    public class WasteLog : BaseEntity
    {
        public WasteLogType Type { get; set; } = WasteLogType.Waste;

        public WasteCategory Category { get; set; }

        [MaxLength(40)]
        public string? WasteNumber { get; set; }

        public ManualWasteType? WasteType { get; set; }

        public DateTime? WasteDate { get; set; }

        /// <summary>ProductId (cancel) or RawMaterialId (expiry). Nullable for safety.</summary>
        public Guid? ItemId { get; set; }

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public Guid? MaterialId { get; set; }
        public RawMaterial? Material { get; set; }

        [Required, MaxLength(200)]
        public string ItemName { get; set; } = string.Empty; // snapshot

        [MaxLength(200)]
        public string? ItemNameAr { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(30)]
        public string Unit { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePriceLoss { get; set; }

        public WasteLogStatus Status { get; set; } = WasteLogStatus.Approved;

        public Guid? LoggedById { get; set; }
        public User? LoggedBy { get; set; }

        [MaxLength(150)]
        public string? LoggedByName { get; set; } // snapshot; null = system/auto

        [MaxLength(150)]
        public string? CreatedBy { get; set; }

        public Guid? ApprovedById { get; set; }
        public User? ApprovedByUser { get; set; }

        [MaxLength(150)]
        public string? ApprovedBy { get; set; }

        public DateTime? DecisionAt { get; set; }

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid? OrderId { get; set; }

        [MaxLength(500)]
        public string? AttachmentUrl { get; set; }

        public bool IsAffectingInventory { get; set; }

        public Guid? CashierActionById { get; set; }

        [MaxLength(150)]
        public string? CashierActionByName { get; set; }

        public KitchenCancellationDecision? KitchenDecision { get; set; }

        public Guid? KitchenDecisionById { get; set; }

        [MaxLength(150)]
        public string? KitchenDecisionByName { get; set; }

        public Guid? SourceOrderId { get; set; }
        public Order? SourceOrder { get; set; }

        public Guid? SourceOrderItemId { get; set; }
        public OrderItem? SourceOrderItem { get; set; }

        public Guid? ShiftId { get; set; }
        public CashierBalanceShift? Shift { get; set; }

        // Batch linkage — populated only for ExpiryProduct entries written by the nightly job.
        // BatchNumber is snapshotted as a string so the audit record survives batch deletion.
        public Guid? SourceBatchId { get; set; }
        public StockBatch? SourceBatch { get; set; }

        [MaxLength(100)]
        public string? SourceBatchNumber { get; set; }

        public ICollection<WasteLogAudit> AuditTrail { get; set; } = new List<WasteLogAudit>();
        public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

        // Staff-meal participants — only populated when WasteType == STAFF_MEAL.
        // Multiple employees can share one meal; the join row preserves a name
        // snapshot so historical records survive a rename or user deletion.
        public ICollection<WasteLogEmployee> Employees { get; set; } = new List<WasteLogEmployee>();
    }

    public class WasteLogEmployee : BaseEntity
    {
        public Guid WasteLogId { get; set; }
        public WasteLog WasteLog { get; set; } = null!;

        public Guid? EmployeeId { get; set; }
        public User? Employee { get; set; }

        [Required, MaxLength(150)]
        public string EmployeeNameSnapshot { get; set; } = string.Empty;
    }

    public class WasteLogAudit : BaseEntity
    {
        public Guid WasteLogId { get; set; }
        public WasteLog WasteLog { get; set; } = null!;

        public WasteLogAuditAction Action { get; set; }

        public Guid? PerformedById { get; set; }
        public User? PerformedBy { get; set; }

        [MaxLength(150)]
        public string? PerformedByName { get; set; }

        [MaxLength(30)]
        public string? FromStatus { get; set; }

        [MaxLength(30)]
        public string? ToStatus { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? ChangedFields { get; set; }

        [Column(TypeName = "text")]
        public string? PreviousValues { get; set; }

        [Column(TypeName = "text")]
        public string? NewValues { get; set; }
    }

    public class InventoryTransaction : BaseEntity
    {
        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public InventoryTransactionType TransactionType { get; set; } = InventoryTransactionType.Waste;

        public Guid WasteLogId { get; set; }
        public WasteLog WasteLog { get; set; } = null!;

        public Guid? ProductId { get; set; }
        public Product? Product { get; set; }

        public Guid? MaterialId { get; set; }
        public RawMaterial? Material { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [MaxLength(30)]
        public string Unit { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostAmount { get; set; }

        [MaxLength(40)]
        public string? ReferenceNumber { get; set; }

        public Guid? CreatedById { get; set; }

        [MaxLength(150)]
        public string? CreatedBy { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    // ─── Cancel Log ───────────────────────────────────────────────────
    /// <summary>
    /// Records every cancellation event (whole order or single item).
    /// OrderItemId == null → full-order cancel; otherwise → item-level cancel.
    /// </summary>
    public class CancelLog : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public Guid? OrderItemId { get; set; }
        public OrderItem? OrderItem { get; set; }

        public Guid? ItemId { get; set; }

        [MaxLength(200)]
        public string? ItemName { get; set; }

        [MaxLength(200)]
        public string? ItemNameAr { get; set; }

        [Column(TypeName = "decimal(10,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public Guid CancelledById { get; set; }
        public User CancelledBy { get; set; } = null!;

        [Required, MaxLength(150)]
        public string CancelledByName { get; set; } = string.Empty; // snapshot

        public CancelledByRole CancelledByRole { get; set; }

        public DateTime OrderTime { get; set; } // CreatedAt of the original order

        public DateTime CancelledAt { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(50)]
        public string CancelReasonCode { get; set; } = string.Empty; // snapshot e.g. "CLIENT_CHANGED_MIND"

        [MaxLength(500)]
        public string? CancelReasonNote { get; set; }

        [MaxLength(20)]
        public string? CancelMode { get; set; }

        public bool RequiresKitchenApproval { get; set; } = false;

        public CancellationApprovalStatus ApprovalStatus { get; set; } = CancellationApprovalStatus.ApprovedCancelled;

        public KitchenCancellationDecision? KitchenDecision { get; set; }

        public DateTime? KitchenDecidedAt { get; set; }

        public Guid? KitchenDecisionById { get; set; }

        [MaxLength(150)]
        public string? KitchenDecisionByName { get; set; }

        /// <summary>
        /// Points to the single WasteLog for item-level cancels.
        /// Null for full-order cancels (multiple WasteLogs linked via SourceOrderId).
        /// </summary>
        public Guid? WasteLogId { get; set; }
        public WasteLog? WasteLog { get; set; }

        public Guid? ShiftId { get; set; }
        public CashierBalanceShift? Shift { get; set; }
    }

    // ─── Refund Log ───────────────────────────────────────────────────────────
    /// <summary>
    /// Created when a Paid order is refunded (post-payment cancel).
    /// Financial analytics retain the original sale and deduct this immutable
    /// refund record once on its processing date.
    /// </summary>
    public class RefundLog : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public Guid BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        /// <summary>Full amount refunded (equals Order.TotalAmount at refund time).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }

        /// <summary>Short reason code chosen by staff (e.g. "QUALITY_ISSUE", "WRONG_ORDER").</summary>
        [Required, MaxLength(100)]
        public string RefundReason { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? RefundNote { get; set; }

        /// <summary>Payment method of the original order (snapshot for reporting).</summary>
        [MaxLength(50)]
        public string? OriginalPaymentMethod { get; set; }

        public Guid ProcessedById { get; set; }

        [Required, MaxLength(150)]
        public string ProcessedByName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string ProcessedByRole { get; set; } = string.Empty;

        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    // ─── Cancel Reasons (lookup, not BaseEntity — tenant-optional) ────
    public class CancelReason
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;   // e.g. "CLIENT_CHANGED_MIND"

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;   // English label

        [Required, MaxLength(200)]
        public string NameAr { get; set; } = string.Empty; // Arabic label

        /// <summary>When true, CancelReasonNote is mandatory (min 5 chars).</summary>
        public bool RequiresNote { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        /// <summary>null = global/default; non-null = tenant-specific override/addition.</summary>
        public Guid? TenantId { get; set; }
    }

    public static class RestaurantTimeDefaults
    {
        public const string TimeZoneId = "Asia/Qatar";
    }

    public class SystemSettings
    {
        [Key]
        public Guid Id { get; set; }

        public Guid? TenantId { get; set; } // nullable for future multi-tenant

        // Restaurant Details
        [MaxLength(200)]
        public string RestaurantName { get; set; } = "RestoPOS";

        [MaxLength(300)]
        public string? RestaurantTagline { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(500)]
        public string? CoverImageUrl { get; set; }

        [MaxLength(500)]
        public string? RestaurantAddress { get; set; }

        [MaxLength(500)]
        public string? RestaurantAddressAr { get; set; }

        [MaxLength(50)]
        public string? RestaurantPhone { get; set; }

        [MaxLength(50)]
        public string? WhatsAppNumber { get; set; }

        [MaxLength(200)]
        [EmailAddress]
        public string? RestaurantEmail { get; set; }

        [MaxLength(500)]
        public string? WebsiteUrl { get; set; }

        [MaxLength(50)]
        public string? TaxNumber { get; set; }

        [MaxLength(500)]
        public string? FacebookUrl { get; set; }

        [MaxLength(500)]
        public string? InstagramUrl { get; set; }

        [MaxLength(500)]
        public string? TikTokUrl { get; set; }

        [MaxLength(500)]
        public string? SnapchatUrl { get; set; }

        [MaxLength(500)]
        public string? XUrl { get; set; }

        [MaxLength(500)]
        public string? YouTubeUrl { get; set; }

        [MaxLength(500)]
        public string? GoogleMapsLocationUrl { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal? Longitude { get; set; }

        public int? AllowedRadiusMeters { get; set; }

        [MaxLength(500)]
        public string? GoogleReviewUrl { get; set; }

        // QR menu URL printed on receipts (and shown in QR menu modal). Stored in
        // settings so it does not depend on which device's window.location renders.
        [MaxLength(500)]
        public string? QrMenuUrl { get; set; }

        // Finance
        [MaxLength(10)]
        public string Currency { get; set; } = "JOD";

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; } = 16;

        [Column(TypeName = "decimal(5,2)")]
        public decimal ServiceChargeRate { get; set; } = 0;

        public bool EnableDiscounts { get; set; } = true;
        public bool EnableLoyalty { get; set; } = true;
        public DuplicateInvoiceBehavior DuplicateInvoiceBehavior { get; set; }
            = DuplicateInvoiceBehavior.WarningOnly;

        [Column(TypeName = "decimal(18,4)")]
        public decimal LoyaltyPointValue { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LoyaltyMinimumRedeemPoints { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LoyaltyMaximumRedeemPoints { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LoyaltyBronzeThreshold { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LoyaltySilverThreshold { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LoyaltyGoldThreshold { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LoyaltyVipThreshold { get; set; }

        // Voucher
        public bool VoucherEnabled { get; set; } = VoucherDefaults.Enabled;

        [Column(TypeName = "decimal(18,2)")]
        public decimal VoucherAmount { get; set; } = VoucherDefaults.Amount;

        public int VoucherDailyLimit { get; set; } = VoucherDefaults.DailyLimit;

        // Receipt
        [MaxLength(500)]
        public string ReceiptFooterNote { get; set; } = "Thank you for your visit!";

        [MaxLength(200)]
        public string? WorkingHours { get; set; }

        [MaxLength(200)]
        public string TermsTitle { get; set; } = "Terms & Conditions";

        [MaxLength(200)]
        public string TermsTitleAr { get; set; } = "الشروط والأحكام";

        [Column(TypeName = "text")]
        public string? TermsContent { get; set; }

        [Column(TypeName = "text")]
        public string? TermsContentAr { get; set; }

        [MaxLength(200)]
        public string PrivacyTitle { get; set; } = "Privacy Policy";

        [MaxLength(200)]
        public string PrivacyTitleAr { get; set; } = "سياسة الخصوصية";

        [Column(TypeName = "text")]
        public string? PrivacyContent { get; set; }

        [Column(TypeName = "text")]
        public string? PrivacyContentAr { get; set; }

        public int ReceiptCopies { get; set; } = 1;
        public bool AutoPrintReceipt { get; set; } = false;
        public bool ShowTaxOnReceipt { get; set; } = true;
        public bool ShowLogoOnReceipt { get; set; } = true;

        // Notifications
        public bool NotifyNewOrder { get; set; } = true;
        public bool NotifyOrderReady { get; set; } = true;
        public bool NotifyLowStock { get; set; } = true;
        public bool SoundEnabled { get; set; } = true;

        // Expiry / Waste
        public int NearExpiryDays { get; set; } = 3;

        [MaxLength(10)]
        public string ExpiryJobRunTime { get; set; } = "01:00";

        [MaxLength(100)]
        public string ExpiryNotifyRoles { get; set; } = "Admin,Manager";

        // Cashier Balance
        /// <summary>Alert manager when |variance| exceeds this threshold. 0 = disabled.</summary>
        [Column(TypeName = "decimal(18,3)")]
        public decimal VarianceThreshold { get; set; } = 0;

        // System
        [MaxLength(10)]
        public string Language { get; set; } = "en";

        [MaxLength(100)]
        public string TimeZoneId { get; set; } = RestaurantTimeDefaults.TimeZoneId;

        public int OrderPrepTimeout { get; set; } = 30;
        public bool KitchenDisplayEnabled { get; set; } = true;
        public bool AllowDirectCancel { get; set; } = false;
        public bool AllowMobileCancelPreparing { get; set; } = false;
        public bool TableAutoRelease { get; set; } = false;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FollowUsClick : BaseEntity
    {
        [Required, MaxLength(50)]
        public string Platform { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        [MaxLength(500)]
        public string? Referrer { get; set; }
    }
}
