using Microsoft.EntityFrameworkCore;
using PracticaMVC_AdrianLayme3.Models;

namespace PracticaMVC_AdrianLayme3.Data
{
    public class ArtesaniasDBContext : DbContext
    {
        public ArtesaniasDBContext(DbContextOptions<ArtesaniasDBContext> options) : base(options) { }

        public DbSet<ProductoModel> Productos { get; set; }
        public DbSet<ClienteModel> Clientes { get; set; }
        public DbSet<PedidoModel> Pedidos { get; set; }
        public DbSet<DetallePedidoModel> DetallePedidos { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // PARA LOS DECIMALES
            modelBuilder.Entity<ProductoModel>()
                .Property(p => p.Precio)
                .HasColumnType("decimal(8,2)");

            modelBuilder.Entity<PedidoModel>()
                .Property(p => p.MontoTotal)
                .HasColumnType("decimal(9,2)");

            modelBuilder.Entity<DetallePedidoModel>()
                .Property(d => d.PrecioUnitario)
                .HasColumnType("decimal(8,2)");

            // Subtotal como columna computada y almacenada (SQL Server)
            modelBuilder.Entity<DetallePedidoModel>()
                .Property(d => d.Subtotal)
                .HasColumnType("decimal(12,2)")
                .HasComputedColumnSql("[Cantidad] * [PrecioUnitario]", stored: true);

            // Configuración de la relación entre Pedido y Cliente (Many-to-One)
            // Un Pedido tiene Un Cliente, y Un Cliente tiene Muchos Pedidos
            modelBuilder.Entity<PedidoModel>()
                .HasOne(p => p.Cliente) // Un Pedido tiene un Cliente
                .WithMany(c => c.Pedidos) // Un Cliente tiene muchos Pedidos
                .HasForeignKey(p => p.IdCliente); // La clave foránea es IdCliente

            // Configuración de la relación entre Pedido y DetallePedido (One-to-Many)
            // Un Pedido tiene Muchos Detalles de Pedido
            modelBuilder.Entity<PedidoModel>()
                .HasMany(p => p.DetallePedidos) // Un Pedido tiene muchos DetallePedidos
                .WithOne(d => d.Pedido) // Un DetallePedido pertenece a un Pedido
                .HasForeignKey(d => d.IdPedido); // La clave foránea es IdPedido

            // Configuración de la relación entre DetallePedido y Producto (Many-to-One)
            // Un DetallePedido tiene Un Producto, y Un Producto tiene Muchos Detalles de Pedido
            modelBuilder.Entity<DetallePedidoModel>()
                .HasOne(d => d.Producto) // Un DetallePedido tiene un Producto
                .WithMany(p => p.DetallePedidos) // Un Producto tiene muchos DetallePedidos
                .HasForeignKey(d => d.IdProducto); // La clave foránea es IdProducto
        }
    }
}
