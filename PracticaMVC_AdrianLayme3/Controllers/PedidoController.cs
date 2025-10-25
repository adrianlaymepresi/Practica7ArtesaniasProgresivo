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
    public class PedidoController : Controller
    {
        private readonly ArtesaniasDBContext _context;

        public PedidoController(ArtesaniasDBContext context)
        {
            _context = context;
        }

        // GET: Pedido
        public async Task<IActionResult> Index(int pagina = 1, int cantidadRegistrosPorPagina = 5, string q = "")
        {
            if (cantidadRegistrosPorPagina < 1) cantidadRegistrosPorPagina = 5;
            if (cantidadRegistrosPorPagina > 99) cantidadRegistrosPorPagina = 99;
            if (pagina < 1) pagina = 1;

            var todos = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Cliente)
                .ToListAsync();

            var termino = (q ?? "").Trim();
            var terminoNorm = NormalizarTexto(termino);

            IEnumerable<PedidoModel> fuente;
            if (terminoNorm.Length == 0)
            {
                fuente = todos
                    .OrderBy(p => p.FechaPedido)
                    .ThenBy(p => p.Id);
            }
            else
            {
                fuente = todos
                    .Select(p => new
                    {
                        P = p,
                        NombreCliNorm = NormalizarTexto(p.Cliente?.Nombre ?? "")
                    })
                    .Where(x => x.NombreCliNorm.Contains(terminoNorm))
                    .Select(x => new
                    {
                        x.P,
                        Relev = CalcularRelevancia(x.NombreCliNorm, terminoNorm)
                    })
                    .OrderBy(x => x.Relev.empieza)
                    .ThenBy(x => x.Relev.indice)
                    .ThenBy(x => x.Relev.diferenciaLongitud)
                    .ThenBy(x => x.P.Id)
                    .Select(x => x.P);
            }

            var totalRegistros = fuente.Count();
            var cantidadTotalPaginas = Math.Max(1, (int)Math.Ceiling(totalRegistros / (double)cantidadRegistrosPorPagina));
            if (pagina > cantidadTotalPaginas) pagina = cantidadTotalPaginas;

            const int WindowSize = 10;
            int pageWindowStart = ((pagina - 1) / WindowSize) * WindowSize + 1;
            if (pageWindowStart < 1) pageWindowStart = 1;
            int pageWindowEnd = Math.Min(pageWindowStart + WindowSize - 1, cantidadTotalPaginas);

            int omitir = (pagina - 1) * cantidadRegistrosPorPagina;

            var items = fuente
                .Skip(omitir)
                .Take(cantidadRegistrosPorPagina)
                .ToList();

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

        // GET: Pedido/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var pedidoModel = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (pedidoModel == null) return NotFound();

            // Monto actualizado (suma de Subtotal)
            pedidoModel.MontoTotal = await CalcularMontoTotalAsync(pedidoModel.Id);

            return View(pedidoModel);
        }

        // GET: Pedido/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Pedido/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FechaPedido,IdCliente,Direccion,MontoTotal")] PedidoModel pedidoModel)
        {
            ValidarFechaPedido(pedidoModel);
            await ValidarClienteAsync(pedidoModel);
            ValidarDireccion(pedidoModel);

            // Se ignora el MontoTotal posteado; se calcula siempre
            pedidoModel.MontoTotal = 0m;

            if (!ModelState.IsValid) return View(pedidoModel);

            try
            {
                _context.Add(pedidoModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo guardar el pedido. Intenta nuevamente.");
                return View(pedidoModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                return View(pedidoModel);
            }
        }

        // GET: Pedido/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var pedidoModel = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pedidoModel == null) return NotFound();

            pedidoModel.MontoTotal = await CalcularMontoTotalAsync(pedidoModel.Id);
            return View(pedidoModel);
        }

        // POST: Pedido/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FechaPedido,IdCliente,Direccion,MontoTotal")] PedidoModel pedidoModel)
        {
            if (id != pedidoModel.Id) return NotFound();

            ValidarFechaPedido(pedidoModel);
            await ValidarClienteAsync(pedidoModel);
            ValidarDireccion(pedidoModel);

            // Monto siempre desde DetallePedidos (Subtotal)
            pedidoModel.MontoTotal = await CalcularMontoTotalAsync(pedidoModel.Id);

            if (!ModelState.IsValid) return View(pedidoModel);

            try
            {
                var original = await _context.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                if (original == null) return NotFound();

                _context.Update(pedidoModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PedidoModelExists(pedidoModel.Id)) return NotFound();
                ModelState.AddModelError(string.Empty, "Otro usuario modificó este registro. Recarga la página.");
                return View(pedidoModel);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo guardar los cambios. Intenta nuevamente.");
                return View(pedidoModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                return View(pedidoModel);
            }
        }

        // GET: Pedido/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var pedidoModel = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (pedidoModel == null) return NotFound();

            pedidoModel.MontoTotal = await CalcularMontoTotalAsync(pedidoModel.Id);
            return View(pedidoModel);
        }

        // POST: Pedido/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var pedidoModel = await _context.Pedidos.FindAsync(id);
            if (pedidoModel != null)
            {
                _context.Pedidos.Remove(pedidoModel);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PedidoModelExists(int id) => _context.Pedidos.Any(e => e.Id == id);

        // =========================
        //   Buscador clientes (Modal)
        // =========================
        [HttpGet]
        public async Task<IActionResult> BuscarClientes(string q = "", string modo = "nombre", int pagina = 1, int cantidadRegistrosPorPagina = 5)
        {
            if (cantidadRegistrosPorPagina < 1) cantidadRegistrosPorPagina = 5;
            if (cantidadRegistrosPorPagina > 99) cantidadRegistrosPorPagina = 99;
            if (pagina < 1) pagina = 1;

            var todos = await _context.Clientes.AsNoTracking().ToListAsync();

            var termino = (q ?? "").Trim();
            var terminoNorm = NormalizarTexto(termino);

            IEnumerable<ClienteModel> fuente = todos;

            if (!string.IsNullOrEmpty(termino))
            {
                if ((modo ?? "nombre").ToLowerInvariant() == "ci")
                {
                    var filtro = new string(termino.Where(char.IsDigit).ToArray());
                    fuente = todos
                        .Where(c => c.CarnetIdentidad.ToString().Contains(filtro))
                        .OrderBy(c => c.CarnetIdentidad)
                        .ThenBy(c => c.Id);
                }
                else
                {
                    fuente = todos
                        .Select(c => new { C = c, NombreNorm = NormalizarTexto(c.Nombre ?? "") })
                        .Where(x => x.NombreNorm.Contains(terminoNorm))
                        .Select(x => new { x.C, Relev = CalcularRelevancia(x.NombreNorm, terminoNorm) })
                        .OrderBy(x => x.Relev.empieza)
                        .ThenBy(x => x.Relev.indice)
                        .ThenBy(x => x.Relev.diferenciaLongitud)
                        .ThenBy(x => x.C.Id)
                        .Select(x => x.C);
                }
            }
            else
            {
                fuente = todos.OrderBy(c => c.Nombre).ThenBy(c => c.Id);
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
                .Select(c => new
                {
                    id = c.Id,
                    nombre = c.Nombre,
                    ci = c.CarnetIdentidad,
                    direccion = c.Direccion ?? ""
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

        // =========================
        //   Validaciones de campos
        // =========================

        private void ValidarFechaPedido(PedidoModel p)
        {
            var f = new DateTime(p.FechaPedido.Year, p.FechaPedido.Month, p.FechaPedido.Day, p.FechaPedido.Hour, p.FechaPedido.Minute, 0);
            var min = DateTime.Today; // desde hoy 00:00
            var max = new DateTime(2100, 12, 31, 23, 59, 0);

            if (f < min)
                ModelState.AddModelError(nameof(PedidoModel.FechaPedido), "La fecha debe ser hoy o futura.");
            else if (f > max)
                ModelState.AddModelError(nameof(PedidoModel.FechaPedido), "La fecha no puede ser posterior al 31/12/2100.");

            p.FechaPedido = f;
        }

        private async Task ValidarClienteAsync(PedidoModel p)
        {
            if (p.IdCliente < 1)
            {
                ModelState.AddModelError(nameof(PedidoModel.IdCliente), "Debe seleccionar un cliente.");
                return;
            }

            var existe = await _context.Clientes.AsNoTracking().AnyAsync(c => c.Id == p.IdCliente);
            if (!existe)
                ModelState.AddModelError(nameof(PedidoModel.IdCliente), "El cliente seleccionado no existe.");
        }

        private void ValidarDireccion(PedidoModel p)
        {
            var dir = (p.Direccion ?? "").Trim();
            if (string.IsNullOrEmpty(dir))
                ModelState.AddModelError(nameof(PedidoModel.Direccion), "La dirección es obligatoria.");
            else if (dir.Length < 7)
                ModelState.AddModelError(nameof(PedidoModel.Direccion), "Debe tener al menos 7 caracteres.");
            else if (dir.Length > 350)
                ModelState.AddModelError(nameof(PedidoModel.Direccion), "Máximo 350 caracteres.");
            p.Direccion = dir;
        }

        private async Task<decimal> CalcularMontoTotalAsync(int pedidoId)
        {
            if (pedidoId <= 0) return 0m;

            // AHORA: sumamos Subtotal (columna computada)
            var total = await _context.DetallePedidos
                .AsNoTracking()
                .Where(d => d.IdPedido == pedidoId)
                .SumAsync(d => (decimal?)d.Subtotal) ?? 0m;

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }

        // =========================
        //   Utilitarios de búsqueda
        // =========================

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
