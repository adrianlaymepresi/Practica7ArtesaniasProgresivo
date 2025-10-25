using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PracticaMVC_AdrianLayme3.Migrations
{
    /// <inheritdoc />
    public partial class AddCarnetToCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CarnetIdentidad",
                table: "Clientes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarnetIdentidad",
                table: "Clientes");
        }
    }
}
