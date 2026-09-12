using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileHrRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileLeaveRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DurationInDays = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileLeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileLeaveRequests_StaffProfiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalTable: "StaffProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobileLoanRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentPeriodMonths = table.Column<int>(type: "integer", nullable: false),
                    EmploymentStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PreviousLoanBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LoanType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InstallmentStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InstallmentEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MarriageDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BasicSalary = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Owner = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nationality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DependentName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MonthlyInstallment = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileLoanRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileLoanRequests_StaffProfiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalTable: "StaffProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MobilePermissionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PermissionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeTo = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttachmentStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttachmentOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AttachmentSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilePermissionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePermissionRequests_StaffProfiles_StaffProfileId",
                        column: x => x.StaffProfileId,
                        principalTable: "StaffProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileLeaveRequests_StaffProfileId",
                table: "MobileLeaveRequests",
                column: "StaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileLeaveRequests_TenantId_StaffProfileId_CreatedAt",
                table: "MobileLeaveRequests",
                columns: new[] { "TenantId", "StaffProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileLeaveRequests_TenantId_StaffProfileId_Status_StartDat~",
                table: "MobileLeaveRequests",
                columns: new[] { "TenantId", "StaffProfileId", "Status", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileLoanRequests_StaffProfileId",
                table: "MobileLoanRequests",
                column: "StaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MobileLoanRequests_TenantId_StaffProfileId_CreatedAt",
                table: "MobileLoanRequests",
                columns: new[] { "TenantId", "StaffProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileLoanRequests_TenantId_StaffProfileId_Status_RequestDa~",
                table: "MobileLoanRequests",
                columns: new[] { "TenantId", "StaffProfileId", "Status", "RequestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePermissionRequests_StaffProfileId",
                table: "MobilePermissionRequests",
                column: "StaffProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePermissionRequests_TenantId_StaffProfileId_CreatedAt",
                table: "MobilePermissionRequests",
                columns: new[] { "TenantId", "StaffProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePermissionRequests_TenantId_StaffProfileId_Status_Date",
                table: "MobilePermissionRequests",
                columns: new[] { "TenantId", "StaffProfileId", "Status", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileLeaveRequests");

            migrationBuilder.DropTable(
                name: "MobileLoanRequests");

            migrationBuilder.DropTable(
                name: "MobilePermissionRequests");
        }
    }
}
