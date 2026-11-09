using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceDay.Migrations
{
    public partial class AddEnrolmentStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The default also populates existing enrolment rows.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Enrolment",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Confirmed");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Enrolment");
        }
    }
}
