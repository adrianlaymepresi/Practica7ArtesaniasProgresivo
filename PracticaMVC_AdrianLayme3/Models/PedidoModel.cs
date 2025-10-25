using System.ComponentModel.DataAnnotations;

namespace PracticaMVC_AdrianLayme3.Models
{
    public class PedidoModel
    {
        public int Id { get; set; }
        [Display(Name = "Fecha del Pedido")]
        [Required] // Validacion externa
        public DateTime FechaPedido { get; set; }

        [Display(Name = "Informacion Cliente")]
        [Required, Range(1, int.MaxValue)] // 1 (lo minimo)
        public int IdCliente { get; set; }

        [Display(Name = "Dirección de Entrega")]
        [Required, StringLength(350, MinimumLength = 7)] // Oruro 1 (lo minimo)
        public string Direccion { get; set; }

        [Display(Name = "Monto Total (Bs)")]
        [Required, Range(0.00, 9999999.99)] // 0.01 a 9 999 999.99 Bs (lo minimo)
        public decimal MontoTotal { get; set; }

        // Un pedido pertenece a un solo cliente.
        public ClienteModel? Cliente { get; set; }
        // Un pedido puede tener muchos detalles de pedido.
        public ICollection<DetallePedidoModel>? DetallePedidos { get; set; }
    }
}
