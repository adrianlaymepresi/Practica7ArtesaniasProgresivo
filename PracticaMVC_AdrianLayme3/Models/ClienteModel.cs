using System.ComponentModel.DataAnnotations;
namespace PracticaMVC_AdrianLayme3.Models
{
    public class ClienteModel
    {
        // Para hacer la migracion en base de datos
        // Tenemos que colocar Id

        public int Id { get; set; }

        [Display(Name = "Nombre del Cliente")]
        [Required, StringLength(120, MinimumLength = 8)] // sol mora (lo minimo)
        public string Nombre { get; set; }

        [Display(Name = "Carnet de Identidad")]
        [Required, Range(1, 999_999_999)]
        public int CarnetIdentidad { get; set; }

        [Display(Name = "Correo Electrónico del Cliente")]
        [StringLength(320, MinimumLength = 7)] // a@z.com (lo minimo)
        public string Email { get; set; }

        [Display(Name = "Dirección del Cliente")]
        [StringLength(350, MinimumLength = 7)] // Oruro 1 (lo minimo)
        public string? Direccion { get; set; }

        public ICollection<PedidoModel>? Pedidos { get; set; }
    }
}
