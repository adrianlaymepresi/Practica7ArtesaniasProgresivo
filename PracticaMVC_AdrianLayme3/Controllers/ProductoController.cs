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
    public class ProductoController : Controller
    {
        private readonly ArtesaniasDBContext _context;

        public ProductoController(ArtesaniasDBContext context)
        {
            _context = context;
        }

        // GET: Producto
        public async Task<IActionResult> Index(int pagina = 1, int cantidadRegistrosPorPagina = 5, string q = "")
        {
            if (cantidadRegistrosPorPagina < 1) cantidadRegistrosPorPagina = 5;
            if (cantidadRegistrosPorPagina > 99) cantidadRegistrosPorPagina = 99;
            if (pagina < 1) pagina = 1;

            var todos = await _context.Productos
                .AsNoTracking()
                .ToListAsync();

            var termino = (q ?? "").Trim();
            var terminoNorm = NormalizarTexto(termino);

            IEnumerable<ProductoModel> fuente;
            if (terminoNorm.Length == 0)
            {
                fuente = todos
                    .OrderBy(p => p.Nombre)
                    .ThenBy(p => p.Id);
            }
            else
            {
                fuente = todos
                    .Select(p => new
                    {
                        P = p,
                        NombreNorm = NormalizarTexto(p.Nombre ?? "")
                    })
                    .Where(x => x.NombreNorm.Contains(terminoNorm))
                    .Select(x => new
                    {
                        x.P,
                        Relev = CalcularRelevancia(x.NombreNorm, terminoNorm)
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

        // GET: Producto/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var productoModel = await _context.Productos
                .FirstOrDefaultAsync(m => m.Id == id);
            if (productoModel == null) return NotFound();

            return View(productoModel);
        }

        // GET: Producto/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Producto/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Descripcion,Precio,Stock")] ProductoModel productoModel)
        {
            ValidarNombre(productoModel);
            ValidarDescripcion(productoModel);
            ValidarPrecio(productoModel);
            ValidarStock(productoModel);
            await ValidarNombreUnicoAsync(productoModel);

            if (!ModelState.IsValid) return View(productoModel);

            try
            {
                _context.Add(productoModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo guardar el producto. Intenta nuevamente.");
                return View(productoModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                return View(productoModel);
            }
        }

        // GET: Producto/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var productoModel = await _context.Productos.FindAsync(id);
            if (productoModel == null) return NotFound();

            return View(productoModel);
        }

        // POST: Producto/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Descripcion,Precio,Stock")] ProductoModel productoModel)
        {
            if (id != productoModel.Id) return NotFound();

            ValidarNombre(productoModel);
            ValidarDescripcion(productoModel);
            ValidarPrecio(productoModel);
            ValidarStock(productoModel);
            await ValidarNombreUnicoAsync(productoModel, excluirId: id);

            if (!ModelState.IsValid) return View(productoModel);

            try
            {
                var original = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                if (original == null) return NotFound();

                _context.Update(productoModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductoModelExists(productoModel.Id)) return NotFound();
                ModelState.AddModelError(string.Empty, "Otro usuario modificó este registro. Recarga la página.");
                return View(productoModel);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo guardar los cambios. Intenta nuevamente.");
                return View(productoModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                return View(productoModel);
            }
        }

        // GET: Producto/Delete/5 (se deja privado/no enrutable para conservar el código)
        private async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var productoModel = await _context.Productos
                .FirstOrDefaultAsync(m => m.Id == id);
            if (productoModel == null) return NotFound();

            return View(productoModel);
        }

        // POST: Producto/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        private async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var productoModel = await _context.Productos.FindAsync(id);
                if (productoModel != null)
                {
                    _context.Productos.Remove(productoModel);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "Otro usuario modificó o eliminó este registro. Recarga la página.");
                var productoModel = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                return productoModel is null ? RedirectToAction(nameof(Index)) : View("Delete", productoModel);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo eliminar el producto. Intenta nuevamente.");
                var productoModel = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                return productoModel is null ? RedirectToAction(nameof(Index)) : View("Delete", productoModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                var productoModel = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                return productoModel is null ? RedirectToAction(nameof(Index)) : View("Delete", productoModel);
            }
        }

        private bool ProductoModelExists(int id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }

        // =========================
        //  Validaciones en controlador
        // =========================

        private void ValidarNombre(ProductoModel p)
        {
            var nombre = (p.Nombre ?? "").Trim();
            if (string.IsNullOrEmpty(nombre))
                ModelState.AddModelError(nameof(ProductoModel.Nombre), "El nombre es obligatorio.");
            else if (nombre.Length < 4)
                ModelState.AddModelError(nameof(ProductoModel.Nombre), "Debe tener al menos 4 caracteres.");
            else if (nombre.Length > 120)
                ModelState.AddModelError(nameof(ProductoModel.Nombre), "Máximo 120 caracteres.");

            p.Nombre = nombre;
        }

        private void ValidarDescripcion(ProductoModel p)
        {
            var desc = (p.Descripcion ?? "").Trim();
            if (desc.Length > 0 && desc.Length < 4)
                ModelState.AddModelError(nameof(ProductoModel.Descripcion), "Si escribes descripción, debe tener al menos 4 caracteres.");
            else if (desc.Length > 1000)
                ModelState.AddModelError(nameof(ProductoModel.Descripcion), "Máximo 1000 caracteres.");

            p.Descripcion = desc;
        }

        private void ValidarPrecio(ProductoModel p)
        {
            var precio = p.Precio;

            if (precio < 0.01m || precio > 999_999.99m)
            {
                ModelState.AddModelError(nameof(ProductoModel.Precio), "El precio debe estar entre 0.01 y 999,999.99.");
                return;
            }

            var decimales = ((decimal.GetBits(precio)[3] >> 16) & 0x7F);
            if (decimales > 2)
            {
                ModelState.AddModelError(nameof(ProductoModel.Precio), "Máximo 2 decimales.");
            }
        }

        private void ValidarStock(ProductoModel p)
        {
            if (p.Stock < 0 || p.Stock > 100_000)
                ModelState.AddModelError(nameof(ProductoModel.Stock), "El stock debe estar entre 0 y 100,000.");
        }

        private async Task ValidarNombreUnicoAsync(ProductoModel p, int? excluirId = null)
        {
            var nombreNorm = (p.Nombre ?? "").Trim().ToLowerInvariant();

            var existe = await _context.Productos
                .AsNoTracking()
                .AnyAsync(x =>
                    (excluirId == null || x.Id != excluirId.Value) &&
                    ((x.Nombre ?? "").Trim().ToLower()) == nombreNorm
                );

            if (existe)
                ModelState.AddModelError(nameof(ProductoModel.Nombre), "Ya existe un producto con el mismo nombre.");
        }

        // =========================
        //  Utilitarios de búsqueda
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
