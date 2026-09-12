using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Repositories
{
    public class UserBranchRepository : IUserBranchRepository
    {
        private readonly PosDbContext _context;

        public UserBranchRepository(PosDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<UserBranchUserSummary?> GetUserSummaryAsync(Guid userId, CancellationToken ct = default)
            => _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserBranchUserSummary(u.Id, u.Username, u.FullName, u.FullNameAr))
                .FirstOrDefaultAsync(ct);

        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken ct = default)
            => _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, ct);

        public Task<List<UserBranch>> GetAssignmentsAsync(Guid userId, CancellationToken ct = default)
            => _context.UserBranches
                .AsNoTracking()
                .Include(ub => ub.Branch)
                .Where(ub => ub.UserId == userId)
                .OrderByDescending(ub => ub.IsDefault)
                .ThenBy(ub => ub.Branch.Name)
                .ToListAsync(ct);

        public Task<UserBranch?> GetAssignmentAsync(Guid userId, Guid branchId, CancellationToken ct = default)
            => _context.UserBranches
                .AsNoTracking()
                .Include(ub => ub.Branch)
                .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BranchId == branchId, ct);

        public Task<UserBranch?> GetTrackedAssignmentAsync(Guid userId, Guid branchId, CancellationToken ct = default)
            => _context.UserBranches
                .Include(ub => ub.Branch)
                .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BranchId == branchId, ct);

        public Task<UserBranch?> GetDefaultAsync(Guid userId, CancellationToken ct = default)
            => _context.UserBranches
                .AsNoTracking()
                .Include(ub => ub.Branch)
                .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.IsDefault, ct);

        public Task<int> CountAssignmentsAsync(Guid userId, CancellationToken ct = default)
            => _context.UserBranches.AsNoTracking().CountAsync(ub => ub.UserId == userId, ct);

        public async Task<UserBranch> AddAsync(
            UserBranch assignment,
            bool clearExistingDefault,
            Guid? updatedById,
            CancellationToken ct = default)
        {
            if (!clearExistingDefault)
            {
                _context.UserBranches.Add(assignment);
                await _context.SaveChangesAsync(ct);
                return assignment;
            }

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await ClearDefaultAsync(assignment.UserId, updatedById, ct);
            _context.UserBranches.Add(assignment);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return assignment;
        }

        public async Task<UserBranch> SetDefaultAsync(
            UserBranch assignment,
            Guid? updatedById,
            CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await ClearDefaultAsync(assignment.UserId, updatedById, ct);
            assignment.IsDefault = true;
            assignment.UpdatedById = updatedById;
            _context.UserBranches.Update(assignment);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return assignment;
        }

        public async Task RemoveAsync(
            UserBranch assignment,
            UserBranch? newDefault,
            Guid? updatedById,
            CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            if (newDefault is not null)
            {
                await ClearDefaultAsync(assignment.UserId, updatedById, ct);
                newDefault.IsDefault = true;
                newDefault.UpdatedById = updatedById;
                _context.UserBranches.Update(newDefault);
            }

            assignment.DeletedAt = DateTime.UtcNow;
            assignment.IsDefault = false;
            assignment.UpdatedById = updatedById;
            _context.UserBranches.Update(assignment);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        private Task ClearDefaultAsync(Guid userId, Guid? updatedById, CancellationToken ct)
            => _context.UserBranches
                .Where(ub => ub.UserId == userId && ub.IsDefault)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ub => ub.IsDefault, false)
                    .SetProperty(ub => ub.UpdatedAt, DateTime.UtcNow)
                    .SetProperty(ub => ub.UpdatedById, updatedById), ct);
    }
}
