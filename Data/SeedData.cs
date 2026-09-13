using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Data
{
    public static class SeedData
    {
        public static readonly Guid DefaultTenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        public static readonly Guid AdminUserId = Guid.Parse("81084756-6e49-4865-be2c-5378d4e79ac8");
        public static readonly Guid WaiterUserId = Guid.Parse("24594ed6-0fb7-4d03-8ae7-5d75414e72ec");
        public static readonly Guid KitchenUserId = Guid.Parse("d035fbeb-52e3-4404-b287-14df85e3b93e");
        public static readonly Guid CashierUserId = Guid.Parse("aa0a5731-df34-4688-89da-9d52e083d15c");
        public static readonly Guid TrackerPickupUserId = Guid.Parse("f92f4ca4-9172-4b2f-afab-62fef87d94e0");
        public static readonly Guid DefaultBranchId = BranchDefaults.MainBranchId;

        public const string DefaultPasswordHash = "e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7";
        private static readonly DateTime SeedTimestamp = new(2026, 5, 5, 13, 7, 57, 883, DateTimeKind.Utc);

        /// <summary>
        /// The currency lookup's starting rows.
        ///
        /// Deliberately the exact set the back office could already pick from before this table
        /// existed, all active — so introducing the lookup changes nothing a business can select
        /// today, and QAR and USD are both present from the first deploy. Fixed ids keep the
        /// seed idempotent: EF inserts each row once and leaves it alone on every later
        /// migration, so repeated deploys cannot duplicate QAR or USD.
        ///
        /// Nothing here declares a base currency. The currency the business operates in stays
        /// <see cref="Models.SystemSettings.Currency"/> and is untouched by this seed.
        /// </summary>
        public static Currency[] DefaultCurrencies =>
        [
            CreateCurrency("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2101", "JOD", "Jordanian Dinar", "دينار أردني", "د.ا", 1),
            CreateCurrency("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2102", "USD", "US Dollar",       "دولار أمريكي", "$",   2),
            CreateCurrency("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2103", "QAR", "Qatari Riyal",    "ريال قطري",    "ر.ق", 3),
            CreateCurrency("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2104", "SAR", "Saudi Riyal",     "ريال سعودي",   "ر.س", 4),
            CreateCurrency("b0e7b8c2-5f1a-4d3e-9c71-0a1c5f8e2105", "EGP", "Egyptian Pound",  "جنيه مصري",    "£",   5)
        ];

        public static PaymentMethod[] DefaultPaymentMethods =>
        [
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d501", "Cash", "نقدي", "CASH", 1, true),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d502", "Visa / Mastercard", "فيزا / ماستركارد", "VISA_MASTERCARD", 2),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d503", "InstaPay", "إنستاباي", "INSTAPAY", 3),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d504", "Vodafone Cash", "فودافون كاش", "VODAFONE_CASH", 4),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d505", "Etisalat Cash", "اتصالات كاش", "ETISALAT_CASH", 5),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d506", "Orange Cash", "أورنج كاش", "ORANGE_CASH", 6),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d507", "Meeza", "ميزة", "MEEZA", 7),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d508", "CliQ", "كليك", "CLIQ", 8),
            CreatePaymentMethod("1ab8e74c-9912-42fa-9c1d-56d6f8f2d509", "Bank Transfer", "تحويل بنكي", "BANK_TRANSFER", 9)
        ];

        public static Branch[] DefaultBranches =>
        [
            new Branch
            {
                Id = DefaultBranchId,
                TenantId = DefaultTenantId,
                Name = BranchDefaults.MainBranchName,
                NameAr = BranchDefaults.MainBranchNameAr,
                Code = BranchDefaults.MainBranchCode,
                IsMainBranch = true,
                IsActive = true,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            }
        ];

        public static User[] DefaultUsers =>
        [
            new User
            {
                Id = AdminUserId,
                TenantId = DefaultTenantId,
                Username = "admin",
                PasswordHash = DefaultPasswordHash,
                Role = UserRole.Admin,
                CreatedAt = SeedTimestamp.AddTicks(4994),
                UpdatedAt = SeedTimestamp.AddTicks(4997)
            },
            new User
            {
                Id = WaiterUserId,
                TenantId = DefaultTenantId,
                Username = "garson",
                PasswordHash = DefaultPasswordHash,
                Role = UserRole.Waiter,
                CreatedAt = SeedTimestamp.AddTicks(5001),
                UpdatedAt = SeedTimestamp.AddTicks(5001)
            },
            new User
            {
                Id = KitchenUserId,
                TenantId = DefaultTenantId,
                Username = "cheif",
                PasswordHash = DefaultPasswordHash,
                Role = UserRole.Kitchen,
                CreatedAt = SeedTimestamp.AddTicks(5002),
                UpdatedAt = SeedTimestamp.AddTicks(5002)
            },
            new User
            {
                Id = CashierUserId,
                TenantId = DefaultTenantId,
                Username = "cashier",
                PasswordHash = DefaultPasswordHash,
                Role = UserRole.Cashier,
                CreatedAt = SeedTimestamp.AddTicks(5004),
                UpdatedAt = SeedTimestamp.AddTicks(5004)
            },
            new User
            {
                Id = TrackerPickupUserId,
                TenantId = DefaultTenantId,
                Username = "trackerpickup",
                PasswordHash = DefaultPasswordHash,
                FullName = "Tracker Pickup",
                Role = UserRole.TrackerPickup,
                CreatedAt = SeedTimestamp.AddTicks(5005),
                UpdatedAt = SeedTimestamp.AddTicks(5005)
            }
        ];

        private static Currency CreateCurrency(
            string id,
            string code,
            string name,
            string nameAr,
            string symbol,
            int sortOrder)
        {
            return new Currency
            {
                Id = Guid.Parse(id),
                TenantId = DefaultTenantId,
                Code = code,
                Name = name,
                NameAr = nameAr,
                Symbol = symbol,
                IsActive = true,
                SortOrder = sortOrder,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            };
        }

        private static PaymentMethod CreatePaymentMethod(
            string id,
            string nameEn,
            string nameAr,
            string code,
            int displayOrder,
            bool isDefault = false)
        {
            return new PaymentMethod
            {
                Id = Guid.Parse(id),
                TenantId = DefaultTenantId,
                NameEn = nameEn,
                NameAr = nameAr,
                Code = code,
                DisplayOrder = displayOrder,
                IsActive = true,
                IsDefault = isDefault,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp
            };
        }
    }
}
