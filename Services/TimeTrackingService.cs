using MediatR;
using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Events;
using RestaurantPos.Api.Helpers;
using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public class TimeTrackingService : ITimeTrackingService
    {
        private readonly PosDbContext _context;
        private readonly IMediator _mediator;
        private readonly ILogger<TimeTrackingService> _logger;

        public TimeTrackingService(PosDbContext context, IMediator mediator, ILogger<TimeTrackingService> logger)
        {
            _context = context;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<bool> ClockInAsync(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.StaffProfile)
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null || user.StaffProfile == null)
            {
                _logger.LogWarning($"ClockIn failed: User or StaffProfile not found for {userId}");
                return false;
            }

            // Check if already clocked in today
            var today = DateTime.UtcNow.Date;
            var existingEntry = await _context.TimeEntries
                .Where(te => te.StaffId == user.StaffProfile.Id && te.Date == today && te.ClockOut == null)
                .FirstOrDefaultAsync();

            if (existingEntry != null)
            {
                _logger.LogInformation($"User {user.Username} specified is already clocked in.");
                return true; // Already clocked in, treat as success
            }

            var entry = new TimeEntry
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                StaffId = user.StaffProfile.Id,
                Date = today,
                ClockIn = DateTime.UtcNow
            };

            _context.TimeEntries.Add(entry);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"ClockIn success for {user.Username}");
            return true;
        }

        public async Task<bool> ClockOutAsync(Guid userId)
        {
            var user = await _context.Users
                .Include(u => u.StaffProfile)
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null || user.StaffProfile == null) return false;

            var today = DateTime.UtcNow.Date;
            var entry = await _context.TimeEntries
                .Where(te => te.StaffId == user.StaffProfile.Id && te.Date == today && te.ClockOut == null)
                .OrderByDescending(te => te.ClockIn) // Get latest active session
                .FirstOrDefaultAsync();

            if (entry == null)
            {
                _logger.LogWarning($"ClockOut failed: No active session found for {user.Username}");
                return false;
            }

            // Update ClockOut
            entry.ClockOut = DateTime.UtcNow;

            // Calculate Cost immediately
            // TotalHours is a computed property, we can use (ClockOut - ClockIn).TotalHours
            double hours = (entry.ClockOut.Value - entry.ClockIn.Value).TotalHours;
            
            // Cost = Hours * HourlyWage
            decimal hourlyRate = user.StaffProfile.HourlyWage;
            
            // If HourlyWage is 0, maybe fallback to MonthlySalary / 225? (Standard 225 hours/month)
            if (hourlyRate == 0 && user.StaffProfile.NetSalary > 0)
            {
                hourlyRate = user.StaffProfile.NetSalary / 225m; 
            }

            entry.TotalCost = (decimal)hours * hourlyRate;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"ClockOut success for {user.Username}. Cost: {entry.TotalCost:C2}");

            // Trigger Alert Check
            await CalculateDailyLaborCostAsync(today);

            return true;
        }

        public async Task CalculateDailyLaborCostAsync(DateTime date)
        {
            var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            
            // 1. Get Today's Total Labor Cost
            var dailyLaborCost = await _context.TimeEntries
                .Where(te => te.Date == utcDate)
                .SumAsync(te => te.TotalCost);

            // 2. Get Today's Revenue
            var nextDay = utcDate.AddDays(1);
            var dailyRevenue = await _context.Orders
                .Where(o => o.CreatedAt >= utcDate && o.CreatedAt < nextDay)
                .WherePaid()
                .SumAsync(o => o.TotalAmount);

            if (dailyRevenue > 0)
            {
                decimal ratio = (dailyLaborCost / dailyRevenue) * 100;
                
                if (ratio > 30)
                {
                    _logger.LogWarning($"ALERT: High Labor Cost detected! Ratio: {ratio:F2}% (Limit: 30%)");
                    await _mediator.Publish(new HighLaborCostAlertEvent(utcDate, dailyRevenue, dailyLaborCost, ratio));
                }
            }
        }
    }
}
