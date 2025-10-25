using System.ComponentModel.DataAnnotations;

namespace PracticaMVC_AdrianLayme3.Models
{
    public class ProductoModel
    {
        // Las llaves primarias suele ser unicamente Id y listo
        public int Id { get; set; }

        [Display(Name = "Nombre del Producto")]
        [Required, StringLength(120, MinimumLength = 4)] // Olla (lo minimo)
        public string Nombre { get; set; }

        [Display(Name = "Descripción del Producto")]
        [StringLength(1000, MinimumLength = 4)] // Puede ser opcional
        public string Descripcion { get; set; }

        [Display(Name = "Precio del Producto (Bs)")]
        [Required, Range(0.01, 999999.99)] // desde 0.01 Bs hasta 999 999.99 Bs
        public decimal Precio { get; set; }

        [Display(Name = "Stock Disponible")]
        [Required, Range(0, 100000)] // puede ser 0 hasta 100000 unidades
        public int Stock { get; set; }

        // Propiedad de navegación para la relación con DetallePedido.
        // Un producto puede estar en muchos detalles de pedido.
        public ICollection<DetallePedidoModel>? DetallePedidos { get; set; }
    }
}
