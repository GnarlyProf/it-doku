using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITDoku.Migrations
{
    /// <inheritdoc />
    public partial class NetworkAndDeviceIp_RelationsAndValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DokuObjectId1",
                table: "DeviceIPs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceIPs_DokuObjectId1",
                table: "DeviceIPs",
                column: "DokuObjectId1");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceIPs_Objects_DokuObjectId1",
                table: "DeviceIPs",
                column: "DokuObjectId1",
                principalTable: "Objects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeviceIPs_Objects_DokuObjectId1",
                table: "DeviceIPs");

            migrationBuilder.DropIndex(
                name: "IX_DeviceIPs_DokuObjectId1",
                table: "DeviceIPs");

            migrationBuilder.DropColumn(
                name: "DokuObjectId1",
                table: "DeviceIPs");
        }
    }
}
