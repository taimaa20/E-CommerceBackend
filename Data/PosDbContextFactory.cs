//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Design;
//using RestaurantPos.Api.Services;

//namespace RestaurantPos.Api.Data
//{
//    /// <summary>
//    /// Creates PosDbContext for EF Core tooling without booting the full API host.
//    /// </summary>
//    public sealed class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
//    {
//        private const string DefaultConnectionString =
//            "Host=localhost;Port=5432;Database=appdb;Username=devuser;Password=devpassword";

//        public PosDbContext CreateDbContext(string[] args)
//        {
//            var connectionString =
//                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
//                ?? Environment.GetEnvironmentVariable("DefaultConnection")
//                ?? DefaultConnectionString;

//            var options = new DbContextOptionsBuilder<PosDbContext>()
//                .UseNpgsql(connectionString)
//                .Options;

//            return new PosDbContext(options, new DesignTimeTenantResolver());
//        }

//        private sealed class DesignTimeTenantResolver : ITenantResolver
//        {
//            public Guid GetTenantId() => SeedData.DefaultTenantId;
//        }
//    }
//}
