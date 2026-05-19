using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RideMate.Infrastructure.Data;

#nullable disable

namespace RideMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260517163500_CircleMembershipAndLiveLocation")]
    public partial class CircleMembershipAndLiveLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CircleMembers");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropForeignKey(
                name: "FK_Circles_AspNetUsers_ApplicationUserId",
                table: "Circles");

            migrationBuilder.DropIndex(
                name: "IX_Circles_ApplicationUserId",
                table: "Circles");

            migrationBuilder.DropIndex(
                name: "IX_Circles_InviteCode",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Circles");

            migrationBuilder.AddColumn<string>(
                name: "CreatorId",
                table: "Circles",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Circles",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "InviteCode",
                table: "Circles",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<Guid>(
                name: "CircleId",
                table: "LocationLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "LocationLogs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "CircleMembers",
                columns: table => new
                {
                    CircleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircleMembers", x => new { x.CircleId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CircleMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CircleMembers_Circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationLogs_CircleId_Timestamp",
                table: "LocationLogs",
                columns: new[] { "CircleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationLogs_UserId",
                table: "LocationLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Circles_InviteCode",
                table: "Circles",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CircleMembers_UserId",
                table: "CircleMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CircleMembers");

            migrationBuilder.DropIndex(
                name: "IX_LocationLogs_CircleId_Timestamp",
                table: "LocationLogs");

            migrationBuilder.DropIndex(
                name: "IX_LocationLogs_UserId",
                table: "LocationLogs");

            migrationBuilder.DropColumn(
                name: "CircleId",
                table: "LocationLogs");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "LocationLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "Circles");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "Circles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Circles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "InviteCode",
                table: "Circles",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Circles",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CircleMembers",
                columns: table => new
                {
                    CirclesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircleMembers", x => new { x.CirclesId, x.MembersId });
                    table.ForeignKey(
                        name: "FK_CircleMembers_Circles_CirclesId",
                        column: x => x.CirclesId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CircleMembers_User_MembersId",
                        column: x => x.MembersId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Circles_ApplicationUserId",
                table: "Circles",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Circles_InviteCode",
                table: "Circles",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CircleMembers_MembersId",
                table: "CircleMembers",
                column: "MembersId");

            migrationBuilder.AddForeignKey(
                name: "FK_Circles_AspNetUsers_ApplicationUserId",
                table: "Circles",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
