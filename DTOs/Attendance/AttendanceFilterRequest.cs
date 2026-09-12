namespace RestaurantPos.Api.DTOs.Attendance
{
    public class AttendanceFilterRequest
    {
        public Guid? UserId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "CheckInAt";
        public string SortOrder { get; set; } = "desc";
    }
}
