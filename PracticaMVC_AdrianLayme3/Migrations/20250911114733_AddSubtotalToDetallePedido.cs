using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PracticaMVC_AdrianLayme3.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtotalToDetallePedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "DetallePedidos",
                type: "decimal(12,2)",
                nullable: false,
                computedColumnSql: "[Cantidad] * [PrecioUnitario]",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "DetallePedidos");
        }
    }
}
