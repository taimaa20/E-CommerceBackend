namespace RestaurantPos.Api.DTOs.Attendance
{
    public class AttendanceRecordDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string? UserFullName { get; set; }
        public DateTime CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        public int? DurationMinutes { get; set; }
        public string? CheckInNote { get; set; }
        public string? CheckOutNote { get; set; }
        public decimal? CheckInLatitude { get; set; }
        public decimal? CheckInLongitude { get; set; }
        public decimal? CheckInDistanceMeters { get; set; }
        public decimal? CheckOutLatitude { get; set; }
        public decimal? CheckOutLongitude { get; set; }
        public decimal? CheckOutDistanceMeters { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
