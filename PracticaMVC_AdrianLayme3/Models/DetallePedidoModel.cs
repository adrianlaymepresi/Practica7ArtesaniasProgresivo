using PracticaMVC_AdrianLayme3.Controllers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticaMVC_AdrianLayme3.Models
{
    public class DetallePedidoModel
    {
        public int Id { get; set; }
        [Display(Name = "Pedido")]
        [Required, Range(1, int.MaxValue)] // 1 (lo minimo)
        public int IdPedido { get; set; }

        [Display(Name = "Producto")]
        [Required, Range(1, int.MaxValue)] // 1 (lo minimo)
        public int IdProducto { get; set; }

        [Display(Name = "Cantidad")]
        [Required, Range(1, 100000)] // 1 (lo minimo)
        public int Cantidad { get; set; }

        [Display(Name = "Precio Unitario (Bs)")]
        [Required, Range(0.01, 999999.99)] // 0.01 a 999 999.99 Bs (lo minimo)
        public decimal PrecioUnitario { get; set; }

        [Display(Name = "Subtotal (Bs)")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)] // lo calcula la BD
        public decimal Subtotal { get; set; }

        public PedidoModel? Pedido { get; set; }
        public ProductoModel? Producto { get; set; }
    }
}
