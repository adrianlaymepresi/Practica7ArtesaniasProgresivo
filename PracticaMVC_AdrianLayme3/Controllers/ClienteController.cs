using DnsClient;
using DnsClient.Protocol;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PracticaMVC_AdrianLayme3.Data;
using PracticaMVC_AdrianLayme3.Models;
using System.Globalization;
using System.Net.Mail;
using System.Text;


namespace PracticaMVC_AdrianLayme3.Controllers
{
    public class ClienteController : Controller
    {
        private readonly ArtesaniasDBContext _context;

        // Reutilizamos el cliente DNS (cache + timeout razonable)
        private static readonly LookupClient Dns = new LookupClient(new LookupClientOptions
        {
            UseCache = true,
            Retries = 1,
            Timeout = TimeSpan.FromSeconds(3)
        });

        public ClienteController(ArtesaniasDBContext context)
        {
            _context = context;
        }

        // GET: Cliente
        public async Task<IActionResult> Index(int pagina = 1, int cantidadRegistrosPorPagina = 5, string q = "")
        {
            if (cantidadRegistrosPorPagina < 1) cantidadRegistrosPorPagina = 5;
            if (cantidadRegistrosPorPagina > 99) cantidadRegistrosPorPagina = 99;
            if (pagina < 1) pagina = 1;

            var todos = await _context.Clientes
                .AsNoTracking()
                .ToListAsync();

            var termino = (q ?? "").Trim();
            var terminoNorm = NormalizarTexto(termino);

            IEnumerable<ClienteModel> fuente;
            if (terminoNorm.Length == 0)
            {
                fuente = todos
                    .OrderBy(c => c.Nombre)
                    .ThenBy(c => c.Id);
            }
            else
            {
                fuente = todos
                    .Select(c => new
                    {
                        C = c,
                        NombreNorm = NormalizarTexto(c.Nombre ?? "")
                    })
                    .Where(x => x.NombreNorm.Contains(terminoNorm))
                    .Select(x => new
                    {
                        x.C,
                        Relev = CalcularRelevancia(x.NombreNorm, terminoNorm)
                    })
                    .OrderBy(x => x.Relev.empieza)
                    .ThenBy(x => x.Relev.indice)
                    .ThenBy(x => x.Relev.diferenciaLongitud)
                    .ThenBy(x => x.C.Id)
                    .Select(x => x.C);
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

        // GET: Cliente/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var clienteModel = await _context.Clientes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (clienteModel == null) return NotFound();

            return View(clienteModel);
        }

        // GET: Cliente/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Cliente/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,CarnetIdentidad,Email,Direccion")] ClienteModel clienteModel)
        {
            // Validaciones en controlador
            ValidarNombre(clienteModel);
            ValidarCarnet(clienteModel);
            ValidarEmail(clienteModel);                // sintaxis/longitud
            await ValidarEmailDominioAsync(clienteModel); // dominio con MX
            ValidarDireccion(clienteModel);
            await ValidarDuplicadoNombreCiAsync(clienteModel);

            if (!ModelState.IsValid) return View(clienteModel);

            try
            {
                _context.Add(clienteModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo guardar el cliente. Intenta nuevamente.");
                return View(clienteModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                return View(clienteModel);
            }
        }

        // GET: Cliente/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var clienteModel = await _context.Clientes.FindAsync(id);
            if (clienteModel == null) return NotFound();

            return View(clienteModel);
        }

        // POST: Cliente/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,CarnetIdentidad,Email,Direccion")] ClienteModel clienteModel)
        {
            if (id != clienteModel.Id) return NotFound();

            // Validaciones en controlador
            ValidarNombre(clienteModel);
            ValidarCarnet(clienteModel);
            ValidarEmail(clienteModel);                // sintaxis/longitud
            await ValidarEmailDominioAsync(clienteModel); // dominio con MX
            ValidarDireccion(clienteModel);
            await ValidarDuplicadoNombreCiAsync(clienteModel, excluirId: id);

            if (!ModelState.IsValid) return View(clienteModel);

            try
            {
                var original = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                if (original == null) return NotFound();

                _context.Update(clienteModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClienteModelExists(clienteModel.Id)) return NotFound();

                ModelState.AddModelError(string.Empty, "Otro usuario modificó este registro. Recarga la página.");
                return View(clienteModel);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo guardar los cambios. Intenta nuevamente.");
                return View(clienteModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                return View(clienteModel);
            }
        }

        // Mantienes tus acciones Delete como privadas (no enrutables)
        private async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var clienteModel = await _context.Clientes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (clienteModel == null) return NotFound();

            return View(clienteModel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        private async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var clienteModel = await _context.Clientes.FindAsync(id);
                if (clienteModel != null)
                {
                    _context.Clientes.Remove(clienteModel);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "Otro usuario modificó o eliminó este registro. Recarga la página.");
                var clienteModel = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                return clienteModel is null ? RedirectToAction(nameof(Index)) : View("Delete", clienteModel);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo eliminar el cliente. Intenta nuevamente.");
                var clienteModel = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                return clienteModel is null ? RedirectToAction(nameof(Index)) : View("Delete", clienteModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado. Intenta nuevamente.");
                var clienteModel = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                return clienteModel is null ? RedirectToAction(nameof(Index)) : View("Delete", clienteModel);
            }
        }

        private bool ClienteModelExists(int id)
        {
            return _context.Clientes.Any(e => e.Id == id);
        }

        // =========================
        //  Validaciones en controlador
        // =========================

        private void ValidarNombre(ClienteModel c)
        {
            var nombre = (c.Nombre ?? "").Trim();
            if (string.IsNullOrEmpty(nombre))
                ModelState.AddModelError(nameof(ClienteModel.Nombre), "El nombre es obligatorio.");
            else if (nombre.Length < 8)
                ModelState.AddModelError(nameof(ClienteModel.Nombre), "Debe tener al menos 8 caracteres.");
            else if (nombre.Length > 120)
                ModelState.AddModelError(nameof(ClienteModel.Nombre), "Máximo 120 caracteres.");

            c.Nombre = nombre;
        }

        // Link: https://www.facebook.com/100066434450399/posts/1049874768449271/?mibextid=rS40aB7S9Ucbxw6v
        // Ahi dice que nuestros carnets en el SEGIP son NUMERICOS ENTEROS
        private void ValidarCarnet(ClienteModel c)
        {
            if (c.CarnetIdentidad < 1 || c.CarnetIdentidad > 999_999_999)
                ModelState.AddModelError(nameof(ClienteModel.CarnetIdentidad), "El CI debe estar entre 1 y 999.999.999.");
        }

        private void ValidarEmail(ClienteModel c)
        {
            var email = (c.Email ?? "").Trim();

            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(nameof(ClienteModel.Email), "El email es obligatorio.");
                return;
            }
            if (email.Length < 7)
                ModelState.AddModelError(nameof(ClienteModel.Email), "Debe tener al menos 7 caracteres.");
            else if (email.Length > 320)
                ModelState.AddModelError(nameof(ClienteModel.Email), "Máximo 320 caracteres.");
            else
            {
                // Parseo de sintaxis fuerte
                try
                {
                    var _ = new MailAddress(email);
                }
                catch
                {
                    ModelState.AddModelError(nameof(ClienteModel.Email), "El formato de correo no es válido.");
                }
            }

            c.Email = email;
        }

        // Validación adicional: el dominio del email debe tener registros MX
        private async Task ValidarEmailDominioAsync(ClienteModel c)
        {
            // Si ya hay error de formato/longitud, no seguimos
            if (ModelState.TryGetValue(nameof(ClienteModel.Email), out var entry) && entry.Errors.Count > 0)
                return;

            var domain = TryGetDomainFromEmail(c.Email ?? "");
            if (domain == null)
            {
                ModelState.AddModelError(nameof(ClienteModel.Email), "El formato de correo no es válido.");
                return;
            }

            var ok = await DominioTieneMxAsync(domain);
            if (!ok)
                ModelState.AddModelError(nameof(ClienteModel.Email), "El dominio del correo no existe o no tiene registros MX válidos.");
        }

        private void ValidarDireccion(ClienteModel c)
        {
            var dir = (c.Direccion ?? "").Trim();
            if (string.IsNullOrEmpty(dir))
                ModelState.AddModelError(nameof(ClienteModel.Direccion), "La dirección es obligatoria.");
            else if (dir.Length < 7)
                ModelState.AddModelError(nameof(ClienteModel.Direccion), "Debe tener al menos 7 caracteres.");
            else if (dir.Length > 350)
                ModelState.AddModelError(nameof(ClienteModel.Direccion), "Máximo 350 caracteres.");

            c.Direccion = dir;
        }

        private async Task ValidarDuplicadoNombreCiAsync(ClienteModel c, int? excluirId = null)
        {
            var nombreNorm = (c.Nombre ?? "").Trim().ToLowerInvariant();
            var ci = c.CarnetIdentidad;

            var existe = await _context.Clientes.AsNoTracking().AnyAsync(x =>
                (excluirId == null || x.Id != excluirId.Value) &&
                x.CarnetIdentidad == ci &&
                ((x.Nombre ?? "").Trim().ToLower()) == nombreNorm
            );

            if (existe)
                ModelState.AddModelError(string.Empty, "Ya existe un cliente con el mismo Nombre y CI.");
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

        // =========================
        //  Utilitarios de email / DNS MX
        // link informacion: https://www.cloudflare.com/es-es/learning/dns/dns-records/dns-mx-record/
        // =========================

        private static string? TryGetDomainFromEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                var host = addr.Host?.Trim().TrimEnd('.');
                if (string.IsNullOrEmpty(host)) return null;

                // Soporte IDN (dominios con tildes/ñ) -> punycode ASCII
                var idn = new IdnMapping();
                return idn.GetAscii(host).ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        private static async Task<bool> DominioTieneMxAsync(string domain)
        {
            try
            {
                var resp = await Dns.QueryAsync(domain, QueryType.MX);
                var mx = resp.AllRecords.OfType<MxRecord>()
                                        .Where(r => !string.IsNullOrWhiteSpace(r.Exchange?.Value));
                return mx.Any();
            }
            catch
            {
                return false;
            }
        }
    }
}
