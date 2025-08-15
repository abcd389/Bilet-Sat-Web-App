using BiletSatisWebApp.Data;
using BiletSatisWebApp.Models;
using BiletSatisWebApp.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BiletSatisWebApp.Controllers
{
    public class BiletController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BiletController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var tickets = await _context.Trips
                .Where(t => t.SaleDate != null)
                .ToListAsync();

            return View(tickets);
        }

        [HttpGet]
        public IActionResult Otobus()
        {
            var fromCities = _context.Trips
                .Where(t => t.TransportType == "Otobüs") // sadece otobüs seferleri
                .Select(t => t.FromCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList(); // DB'den çekme

            var toCities = _context.Trips
                .Where(t => t.TransportType == "Otobüs") // sadece otobüs seferleri
                .Select(t => t.ToCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList(); // DB'den çekme

            // Birleştir ve tekrar distinct yapma
            var cities = fromCities
                .Concat(toCities)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Hiç sefer yoksa fallback şehir listesi
            if (!cities.Any())
            {
                cities = new List<string> { "İstanbul", "Ankara", "İzmir", "Bursa", "Antalya" };
            }

            ViewBag.Cities = cities;
            return View();
        }

        [HttpPost]
        public IActionResult Otobus(string Nereden, string Nereye, DateTime GidisTarihi, string YolculukTuru)
        {
            var fromCities = _context.Trips
                .Where(t => t.TransportType == "Otobüs")
                .Select(t => t.FromCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            var toCities = _context.Trips
                .Where(t => t.TransportType == "Otobüs")
                .Select(t => t.ToCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            ViewBag.Cities = fromCities
                .Concat(toCities)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Arama sonucu
            var trips = _context.Trips
                .Where(t =>
                    t.FromCity == Nereden &&
                    t.ToCity == Nereye &&
                    t.DepartureDate.Date == GidisTarihi.Date)
                .ToList();

            return RedirectToAction("OtobusListesi", new { from = Nereden, to = Nereye, tarih = GidisTarihi.ToString("dd.MM.yyyy") });
        }

        public IActionResult OtobusListesi(string from, string to, string tarih)
        {
            OtobusListesiVM _vm = new OtobusListesiVM();

            List<Trip> trips = new List<Trip>();
            if (from != null && to != null && tarih != null)
            {
                DateTime dt = Convert.ToDateTime(tarih);

                // Arama sonucu
                var tripsResults = _context.Trips
                    .Where(t =>
                        t.FromCity == from &&
                        t.ToCity == to &&
                        t.DepartureDate.Date == dt &&
                        t.TransportType == "Otobüs")
                    .ToList();
                _vm.Trips = tripsResults;

                _vm.Nereden = from;
                _vm.Nereye = to;
                _vm.GidisTarihi = dt;

                return View(_vm);
            }
            else return View();
        }

        [HttpGet]
        public async Task<ActionResult> KoltukSecimi(int tripId)
        {
            var trip = await GetTripById(tripId);
            if (trip == null)
            {
                TempData["ErrorMessage"] = "Sefer bulunamadı!";
                return RedirectToAction("Index", "Home");
            }

            // Get sold seats for this trip
            var soldSeats = await _context.Seats
                .Where(s => s.TripId == tripId && s.IsOccupied)
                .Select(s => s.SeatNumber)
                .ToListAsync();

            var selectedSeats = new List<int>(); // Initialize with any pre-selected seats if needed

            // Determine max seats based on transport type
            int maxSeats = trip.TransportType switch
            {
                "Otobüs" => 30,
                "Uçak" => 180,
                "Tren" => 120,
                _ => 30
            };

            var koltukSecimiVM = new KoltukSecimiVM
            {
                Trip = trip,
                SelectedSeats = selectedSeats,
                SoldSeats = soldSeats,
                TransportType = trip.TransportType,
                MaxSeats = maxSeats
            };

            return View(koltukSecimiVM);
        }

        [HttpPost]
        public async Task<ActionResult> KoltukSecimi([FromBody] SeatSelectionRequest request)
        {
            var trip = await GetTripById(request.TripId);
            if (trip == null)
            {
                return Json(new { success = false, message = "Sefer bulunamadı!" });
            }

            // Check if any of the selected seats are already occupied
            var occupiedSeats = await _context.Seats
                .Where(s => s.TripId == request.TripId && 
                           request.SelectedSeats.Contains(s.SeatNumber) && 
                           s.IsOccupied)
                .Select(s => s.SeatNumber)
                .ToListAsync();

            if (occupiedSeats.Any())
            {
                return Json(new { success = false, message = $"Koltuklar {string.Join(", ", occupiedSeats)} zaten dolu!" });
            }

            // Store selected seats in TempData for payment process
            TempData["TripId"] = request.TripId;
            TempData["SelectedSeats"] = string.Join(",", request.SelectedSeats);

            return Json(new { success = true, message = "Koltuk seçimi tamamlandı!" });
        }

        [HttpGet]
        public async Task<IActionResult> Odeme()
        {
            var tripId = TempData["TripId"];
            var selectedSeatsStr = TempData["SelectedSeats"];

            if (tripId == null || selectedSeatsStr == null)
            {
                TempData["ErrorMessage"] = "Koltuk seçimi bulunamadı. Lütfen tekrar deneyin.";
                return RedirectToAction("Index", "Home");
            }

            // Keep data in TempData for POST
            TempData.Keep("TripId");
            TempData.Keep("SelectedSeats");

            var trip = await GetTripById((int)tripId);
            if (trip == null)
            {
                TempData["ErrorMessage"] = "Sefer bulunamadı!";
                return RedirectToAction("Index", "Home");
            }

            var selectedSeats = selectedSeatsStr.ToString().Split(',').Select(int.Parse).ToList();
            var totalPrice = trip.Price * selectedSeats.Count;

            var paymentVM = new PaymentVM
            {
                Trip = trip,
                SelectedSeats = selectedSeats,
                TotalPrice = totalPrice
            };

            return View(paymentVM);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Odeme(PaymentVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var tripId = TempData["TripId"];
            var selectedSeatsStr = TempData["SelectedSeats"];

            if (tripId == null || selectedSeatsStr == null)
            {
                TempData["ErrorMessage"] = "Koltuk seçimi bulunamadı. Lütfen tekrar deneyin.";
                return RedirectToAction("Index", "Home");
            }

            var trip = await GetTripById((int)tripId);
            if (trip == null)
            {
                TempData["ErrorMessage"] = "Sefer bulunamadı!";
                return RedirectToAction("Index", "Home");
            }

            var selectedSeats = selectedSeatsStr.ToString().Split(',').Select(int.Parse).ToList();
            var user = await _userManager.GetUserAsync(User);

            // Create ticket
            var ticket = new Ticket
            {
                UserId = user.Id,
                TripId = trip.Id,
                SeatNumbers = selectedSeats,
                PassengerFirstName = model.FirstName,
                PassengerLastName = model.LastName,
                PassengerEmail = model.Email,
                PassengerPhone = model.Phone,
                TotalPrice = model.TotalPrice,
                PurchaseDate = DateTime.Now
            };

            _context.Tickets.Add(ticket);

            // Mark seats as occupied
            foreach (var seatNumber in selectedSeats)
            {
                var existingSeat = await _context.Seats
                    .FirstOrDefaultAsync(s => s.TripId == trip.Id && s.SeatNumber == seatNumber);

                if (existingSeat == null)
                {
                    var seat = new Seat
                    {
                        TripId = trip.Id,
                        SeatNumber = seatNumber,
                        IsOccupied = true,
                        UserId = user.Id,
                        ReservationDate = DateTime.Now
                    };
                    _context.Seats.Add(seat);
                }
                else
                {
                    existingSeat.IsOccupied = true;
                    existingSeat.UserId = user.Id;
                    existingSeat.ReservationDate = DateTime.Now;
                }
            }

            // Update trip sold tickets count
            trip.TicketsSold += selectedSeats.Count;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Biletiniz başarıyla alınmıştır. Bilet bilgileriniz mailinize ve telefonunuza iletilmiştir.";
            TempData["TicketId"] = ticket.Id;

            return RedirectToAction("OdemeTamamlandi");
        }

        [HttpGet]
        public IActionResult OdemeTamamlandi()
        {
            var successMessage = TempData["SuccessMessage"];
            var ticketId = TempData["TicketId"];

            if (successMessage == null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.SuccessMessage = successMessage;
            ViewBag.TicketId = ticketId;

            return View();
        }

        private async Task<Trip> GetTripById(int tripId)
        {
            return await _context.Trips.FirstOrDefaultAsync(p => p.Id == tripId);
        }


        [HttpGet]
        public IActionResult Ucak()
        {
            var fromCities = _context.Trips
               .Where(t => t.TransportType == "Uçak") // sadece otobüs seferleri
               .Select(t => t.FromCity)
               .Where(c => !string.IsNullOrWhiteSpace(c))
               .Distinct()
               .ToList(); // DB'den çekme

            var toCities = _context.Trips
                .Where(t => t.TransportType == "Uçak") // sadece otobüs seferleri
                .Select(t => t.ToCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList(); // DB'den çekme

            // Birleştir ve tekrar distinct yapma
            var cities = fromCities
                .Concat(toCities)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Hiç sefer yoksa fallback şehir listesi
            if (!cities.Any())
            {
                cities = new List<string> { "İstanbul", "Ankara", "İzmir", "Bursa", "Antalya" };
            }

            ViewBag.Cities = cities;
            return View();
        }

        [HttpPost]
        public IActionResult Ucak(string Nereden, string Nereye, DateTime GidisTarihi, string YolculukTuru)
        {
            var fromCities = _context.Trips
                .Where(t => t.TransportType == "Uçak")
                .Select(t => t.FromCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            var toCities = _context.Trips
                .Where(t => t.TransportType == "Uçak")
                .Select(t => t.ToCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            ViewBag.Cities = fromCities
                .Concat(toCities)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Arama sonucu
            var trips = _context.Trips
                .Where(t =>
                    t.FromCity == Nereden &&
                    t.ToCity == Nereye &&
                    t.DepartureDate.Date == GidisTarihi.Date)
                .ToList();


            return RedirectToAction("UcakListesi", new { from = Nereden, to = Nereye, tarih = GidisTarihi.ToString("dd.MM.yyyy") });
        }

        public IActionResult UcakListesi(string from, string to, string tarih)
        {
            UcakListesiVM _vm = new UcakListesiVM();

            List<Trip> trips = new List<Trip>();
            if (from != null && to != null && tarih != null)
            {
                DateTime dt = Convert.ToDateTime(tarih);

                // Arama sonucu
                var tripsResults = _context.Trips
                    .Where(t =>
                        t.FromCity == from &&
                        t.ToCity == to &&
                        t.DepartureDate.Date == dt &&
                        t.TransportType == "Uçak")
                    .ToList();
                _vm.Trips = tripsResults;

                _vm.Nereden = from;
                _vm.Nereye = to;
                _vm.GidisTarihi = dt;

                return View(_vm);
            }
            else return View();
        }

        [HttpGet]
        public IActionResult Tren()
        {
            var fromCities = _context.Trips
                .Where(t => t.TransportType == "Tren") // sadece otobüs seferleri
                .Select(t => t.FromCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList(); // DB'den çekme

            var toCities = _context.Trips
                .Where(t => t.TransportType == "Tren") // sadece otobüs seferleri
                .Select(t => t.ToCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList(); // DB'den çekme

            // Birleştir ve tekrar distinct yapma
            var cities = fromCities
                .Concat(toCities)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Hiç sefer yoksa fallback şehir listesi
            if (!cities.Any())
            {
                cities = new List<string> { "İstanbul", "Ankara", "İzmir", "Bursa", "Antalya" };
            }

            ViewBag.Cities = cities;
            return View();
        }

        [HttpPost]
        public IActionResult Tren(string Nereden, string Nereye, DateTime GidisTarihi, string YolculukTuru)
        {
            var fromCities = _context.Trips
                .Where(t => t.TransportType == "Tren")
                .Select(t => t.FromCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            var toCities = _context.Trips
                .Where(t => t.TransportType == "Tren")
                .Select(t => t.ToCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            ViewBag.Cities = fromCities
                .Concat(toCities)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Arama sonucu
            var trips = _context.Trips
                .Where(t =>
                    t.FromCity == Nereden &&
                    t.ToCity == Nereye &&
                    t.DepartureDate.Date == GidisTarihi.Date)
                .ToList();


            return RedirectToAction("TrenListesi", new { from = Nereden, to = Nereye, tarih = GidisTarihi.ToString("dd.MM.yyyy") });
        }
        public IActionResult TrenListesi(string from, string to, string tarih)
        {
            TrenListesiVM _vm = new TrenListesiVM();

            List<Trip> trips = new List<Trip>();
            if (from != null && to != null && tarih != null)
            {
                DateTime dt = Convert.ToDateTime(tarih);

                // Arama sonucu
                var tripsResults = _context.Trips
                    .Where(t =>
                        t.FromCity == from &&
                        t.ToCity == to &&
                        t.DepartureDate.Date == dt &&
                        t.TransportType == "Tren")
                    .ToList();
                _vm.Trips = tripsResults;

                _vm.Nereden = from;
                _vm.Nereye = to;
                _vm.GidisTarihi = dt;

                return View(_vm);
            }
            else return View();
        }

    }
}
