using RestaurantPos.Api.Models;

namespace RestaurantPos.Api.Services
{
    public static class MobileRequestLabels
    {
        public static string LeaveType(string code)
            => code switch
            {
                MobileLeaveRequestTypes.Annual => "Annual",
                MobileLeaveRequestTypes.Sick => "Sick",
                MobileLeaveRequestTypes.Personal => "Personal",
                MobileLeaveRequestTypes.Emergency => "Emergency",
                _ => code
            };

        public static string LoanType(string code)
            => code switch
            {
                MobileLoanRequestTypes.MarriageLoan => "Marriage loan",
                MobileLoanRequestTypes.LifeExpensesLoan => "Life expenses loan",
                _ => code
            };

        public static string LoanOwner(string code)
            => code switch
            {
                MobileLoanOwners.Self => "For myself",
                MobileLoanOwners.Dependent => "For daughter / son",
                _ => code
            };

        public static string PermissionType(string code)
            => code switch
            {
                MobilePermissionRequestTypes.Personal => "Personal",
                MobilePermissionRequestTypes.Official => "Official",
                MobilePermissionRequestTypes.Medical => "Medical",
                _ => code
            };

        public static string Status(string status)
            => status switch
            {
                MobileRequestStatuses.Approved => "Approved",
                MobileRequestStatuses.Pending => "Pending",
                MobileRequestStatuses.Rejected => "Rejected",
                MobileRequestStatuses.Cancelled => "Cancelled",
                _ => status
            };

        public static string BonusType(string code)
            => code switch
            {
                HrBonusTypes.Performance => "Performance",
                HrBonusTypes.Holiday => "Holiday",
                HrBonusTypes.Annual => "Annual",
                HrBonusTypes.Referral => "Referral",
                HrBonusTypes.Spot => "Spot",
                HrBonusTypes.Other => "Other",
                _ => code
            };

        public static string BonusStatus(string code)
            => code switch
            {
                HrBonusStatuses.Pending => "Pending",
                HrBonusStatuses.Approved => "Approved",
                HrBonusStatuses.Paid => "Paid",
                HrBonusStatuses.Cancelled => "Cancelled",
                _ => code
            };
    }
}
