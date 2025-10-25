using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PracticaMVC_AdrianLayme3.Data;
using PracticaMVC_AdrianLayme3.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PracticaMVC_AdrianLayme3.Controllers
{
    public class DetallePedidoController : Controller
    {
        private readonly ArtesaniasDBContext _context;

        public DetallePedidoController(ArtesaniasDBContext context)
        {
            _context = context;
        }

        // Index filtrado por pedido + búsqueda/paginación por nombre de producto
        public async Task<IActionResult> Index(int pedidoId, int pagina = 1, int cantidadRegistrosPorPagina = 5, string q = "")
        {
            if (pedidoId < 1) return NotFound();
            if (cantidadRegistrosPorPagina < 1) cantidadRegistrosPorPagina = 5;
            if (cantidadRegistrosPorPagina > 99) cantidadRegistrosPorPagina = 99;
            if (pagina < 1) pagina = 1;

            var pedido = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);

            if (pedido == null) return NotFound();

            var termino = (q ?? "").Trim();
            var terminoNorm = NormalizarTexto(termino);

            var lista = await _context.DetallePedidos
                .AsNoTracking()
                .Where(d => d.IdPedido == pedidoId)
                .Include(d => d.Producto)
                .ToListAsync();

            IEnumerable<DetallePedidoModel> fuente;
            if (terminoNorm.Length == 0)
            {
                fuente = lista.OrderBy(d => d.Producto!.Nombre).ThenBy(d => d.Id);
            }
            else
            {
                fuente = lista
                    .Select(d => new { D = d, NomNorm = NormalizarTexto(d.Producto?.Nombre ?? "") })
                    .Where(x => x.NomNorm.Contains(terminoNorm))
                    .Select(x => new { x.D, Relev = CalcularRelevancia(x.NomNorm, terminoNorm) })
                    .OrderBy(x => x.Relev.empieza)
                    .ThenBy(x => x.Relev.indice)
                    .ThenBy(x => x.Relev.diferenciaLongitud)
                    .ThenBy(x => x.D.Id)
                    .Select(x => x.D);
            }

            var totalRegistros = fuente.Count();
            var cantidadTotalPaginas = Math.Max(1, (int)Math.Ceiling(totalRegistros / (double)cantidadRegistrosPorPagina));
            if (pagina > cantidadTotalPaginas) pagina = cantidadTotalPaginas;

            const int WindowSize = 10;
            int pageWindowStart = ((pagina - 1) / WindowSize) * WindowSize + 1;
            if (pageWindowStart < 1) pageWindowStart = 1;
            int pageWindowEnd = Math.Min(pageWindowStart + WindowSize - 1, cantidadTotalPaginas);

            int omitir = (pagina - 1) * cantidadRegistrosPorPagina;
            var items = fuente.Skip(omitir).Take(cantidadRegistrosPorPagina).ToList();

            ViewBag.Pedido = pedido;
            ViewBag.PedidoId = pedidoId;

            ViewBag.PaginaActual = pagina;
            ViewBag.CantidadRegistrosPorPagina = cantidadRegistrosPorPagina;
            ViewBag.TextoBusqueda = termino;
            ViewBag.CantidadTotalPaginas = cantidadTotalPaginas;
            ViewBag.PageWindowStart = pageWindowStart;
            ViewBag.PageWindowEnd = pageWindowEnd;
            ViewBag.HasPrevPage = pagina > 1;
            ViewBag.HasNextPage = pagina < cantidadTotalPaginas;

            return View(items);
        }

        // Create
        public async Task<IActionResult> Create(int idPedido)
        {
            if (idPedido < 1) return NotFound();

            var pedido = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == idPedido);
            if (pedido == null) return NotFound();

            var model = new DetallePedidoModel
            {
                IdPedido = idPedido,
                Cantidad = 1
            };

            ViewBag.Pedido = pedido;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,IdPedido,IdProducto,Cantidad,PrecioUnitario")] DetallePedidoModel model)
        {
            // El precio es del producto, ignoramos lo que venga del form
            ModelState.Remove(nameof(DetallePedidoModel.PrecioUnitario));

            await ValidarDetalleAsync(model, isEdit: false);

            // Asignar precio del producto ANTES de validar rango
            if (model.IdProducto > 0)
            {
                var prod = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == model.IdProducto);
                if (prod != null) model.PrecioUnitario = prod.Precio;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Pedido = await _context.Pedidos.AsNoTracking().Include(p => p.Cliente).FirstOrDefaultAsync(p => p.Id == model.IdPedido);
                return View(model);
            }

            _context.Add(model);
            await _context.SaveChangesAsync();

            await RecalcularMontoPedidoAsync(model.IdPedido);

            return RedirectToAction(nameof(Index), new { pedidoId = model.IdPedido });
        }

        // Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var det = await _context.DetallePedidos
                .Include(d => d.Pedido).ThenInclude(p => p.Cliente)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (det == null) return NotFound();

            ViewBag.Pedido = det.Pedido;
            return View(det);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,IdPedido,IdProducto,Cantidad,PrecioUnitario")] DetallePedidoModel model)
        {
            if (id != model.Id) return NotFound();

            // El precio es del producto
            ModelState.Remove(nameof(DetallePedidoModel.PrecioUnitario));

            await ValidarDetalleAsync(model, isEdit: true);

            if (model.IdProducto > 0)
            {
                var prod = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == model.IdProducto);
                if (prod != null) model.PrecioUnitario = prod.Precio;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Pedido = await _context.Pedidos.AsNoTracking().Include(p => p.Cliente).FirstOrDefaultAsync(p => p.Id == model.IdPedido);
                return View(model);
            }

            try
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                await RecalcularMontoPedidoAsync(model.IdPedido);
                return RedirectToAction(nameof(Index), new { pedidoId = model.IdPedido });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DetallePedidoModelExists(model.Id)) return NotFound();
                ModelState.AddModelError(string.Empty, "Otro usuario modificó este registro. Recarga la página.");
                ViewBag.Pedido = await _context.Pedidos.AsNoTracking().Include(p => p.Cliente).FirstOrDefaultAsync(p => p.Id == model.IdPedido);
                return View(model);
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "No se pudo guardar los cambios. Intenta nuevamente.");
                ViewBag.Pedido = await _context.Pedidos.AsNoTracking().Include(p => p.Cliente).FirstOrDefaultAsync(p => p.Id == model.IdPedido);
                return View(model);
            }
        }

        // Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var detalle = await _context.DetallePedidos
                .AsNoTracking()
                .Include(d => d.Pedido).ThenInclude(p => p.Cliente)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (detalle == null) return NotFound();

            return View(detalle);
        }

        // Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var det = await _context.DetallePedidos
                .Include(d => d.Pedido).ThenInclude(p => p.Cliente)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (det == null) return NotFound();

            return View(det);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var det = await _context.DetallePedidos.FindAsync(id);
            if (det == null) return RedirectToAction("Index", "Pedido");

            int pedidoId = det.IdPedido;

            _context.DetallePedidos.Remove(det);
            await _context.SaveChangesAsync();

            await RecalcularMontoPedidoAsync(pedidoId);

            return RedirectToAction(nameof(Index), new { pedidoId });
        }

        private bool DetallePedidoModelExists(int id) => _context.DetallePedidos.Any(e => e.Id == id);

        // AJAX: Buscar productos para el modal
        [HttpGet]
        public async Task<IActionResult> BuscarProductos(string q = "", int pagina = 1, int cantidadRegistrosPorPagina = 5)
        {
            if (cantidadRegistrosPorPagina < 1) cantidadRegistrosPorPagina = 5;
            if (cantidadRegistrosPorPagina > 99) cantidadRegistrosPorPagina = 99;
            if (pagina < 1) pagina = 1;

            var termino = (q ?? "").Trim();
            var terminoNorm = NormalizarTexto(termino);

            var todos = await _context.Productos.AsNoTracking().ToListAsync();

            IEnumerable<ProductoModel> fuente;
            if (terminoNorm.Length == 0)
            {
                fuente = todos.OrderBy(p => p.Nombre).ThenBy(p => p.Id);
            }
            else
            {
                fuente = todos
                    .Select(p => new { P = p, NomNorm = NormalizarTexto(p.Nombre ?? "") })
                    .Where(x => x.NomNorm.Contains(terminoNorm))
                    .Select(x => new { x.P, Relev = CalcularRelevancia(x.NomNorm, terminoNorm) })
                    .OrderBy(x => x.Relev.empieza)
                    .ThenBy(x => x.Relev.indice)
                    .ThenBy(x => x.Relev.diferenciaLongitud)
                    .ThenBy(x => x.P.Id)
                    .Select(x => x.P);
            }

            var totalRegistros = fuente.Count();
            var totalPaginas = Math.Max(1, (int)Math.Ceiling(totalRegistros / (double)cantidadRegistrosPorPagina));
            if (pagina > totalPaginas) pagina = totalPaginas;

            const int WindowSize = 10;
            int pageWindowStart = ((pagina - 1) / WindowSize) * WindowSize + 1;
            if (pageWindowStart < 1) pageWindowStart = 1;
            int pageWindowEnd = Math.Min(pageWindowStart + WindowSize - 1, totalPaginas);

            int omitir = (pagina - 1) * cantidadRegistrosPorPagina;

            var items = fuente.Skip(omitir).Take(cantidadRegistrosPorPagina)
                .Select(p => new
                {
                    id = p.Id,
                    nombre = p.Nombre,
                    precio = p.Precio
                })
                .ToList();

            return Json(new
            {
                pagina,
                cantidadRegistrosPorPagina,
                totalPaginas,
                pageWindowStart,
                pageWindowEnd,
                hasPrev = pagina > 1,
                hasNext = pagina < totalPaginas,
                items
            });
        }

        // Validaciones / Helpers
        private async Task ValidarDetalleAsync(DetallePedidoModel m, bool isEdit)
        {
            if (m.IdPedido < 1 || !await _context.Pedidos.AsNoTracking().AnyAsync(p => p.Id == m.IdPedido))
                ModelState.AddModelError(nameof(DetallePedidoModel.IdPedido), "El pedido no existe.");

            if (m.IdProducto < 1 || !await _context.Productos.AsNoTracking().AnyAsync(p => p.Id == m.IdProducto))
                ModelState.AddModelError(nameof(DetallePedidoModel.IdProducto), "Debe seleccionar un producto válido.");

            if (m.Cantidad < 1 || m.Cantidad > 99_999)
                ModelState.AddModelError(nameof(DetallePedidoModel.Cantidad), "Cantidad permitida: 1 a 99.999.");

            if (m.IdPedido > 0 && m.IdProducto > 0)
            {
                bool existe = await _context.DetallePedidos.AsNoTracking()
                    .AnyAsync(d => d.IdPedido == m.IdPedido && d.IdProducto == m.IdProducto && (isEdit ? d.Id != m.Id : true));
                if (existe)
                    ModelState.AddModelError(string.Empty, "Este producto ya está agregado en el pedido.");
            }
        }

        private async Task RecalcularMontoPedidoAsync(int pedidoId)
        {
            // Ahora sumamos el Subtotal (computado en BD)
            var total = await _context.DetallePedidos
                .AsNoTracking()
                .Where(d => d.IdPedido == pedidoId)
                .SumAsync(d => (decimal?)d.Subtotal) ?? 0m;

            var pedido = await _context.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId);
            if (pedido != null)
            {
                pedido.MontoTotal = Math.Round(total, 2, MidpointRounding.AwayFromZero);
                await _context.SaveChangesAsync();
            }
        }

        private static string NormalizarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            var descompuesto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (var c in descompuesto)
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private static (int empieza, int indice, int diferenciaLongitud) CalcularRelevancia(string nombreNormalizado, string terminoNormalizado)
        {
            var empieza = nombreNormalizado.StartsWith(terminoNormalizado, StringComparison.Ordinal) ? 0 : 1;
            var indice = nombreNormalizado.IndexOf(terminoNormalizado, StringComparison.Ordinal);
            if (indice < 0) indice = int.MaxValue;
            var diferenciaLongitud = Math.Abs(nombreNormalizado.Length - terminoNormalizado.Length);
            return (empieza, indice, diferenciaLongitud);
        }
    }
}
