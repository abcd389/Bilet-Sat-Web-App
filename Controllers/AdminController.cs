using BiletSatisWebApp.Data;
using BiletSatisWebApp.Models;
using BiletSatisWebApp.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BiletSatisWebApp.Controllers
{
    [Authorize(Roles = "Admin")] // Sadece Admin rolü olanlar erişebilir
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // İstatistik kartları için admin anasayfa
        public async Task<IActionResult> Index()
        {
            var totalTrips = await _context.Trips.CountAsync();
            var totalUsers = await _context.Users.CountAsync();
            var totalTicketsSold = await _context.Tickets.Where(t => !t.IsCancelled).CountAsync();
            var totalRevenue = await _context.Tickets.Where(t => !t.IsCancelled).SumAsync(t => t.TotalPrice);
            
            var todaySales = await _context.Tickets
                .Where(t => t.PurchaseDate.Date == DateTime.Today && !t.IsCancelled)
                .CountAsync();

            var todayRevenue = await _context.Tickets
                .Where(t => t.PurchaseDate.Date == DateTime.Today && !t.IsCancelled)
                .SumAsync(t => t.TotalPrice);

            ViewBag.TotalTrips = totalTrips;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalTicketsSold = totalTicketsSold;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TodaySales = todaySales;
            ViewBag.TodayRevenue = todayRevenue;

            return View();
        }

        // Bilet satışa çıkarma sayfası (GET)
        [HttpGet]
        public IActionResult SellTicket()
        {
            return View();
        }

        // Bilet satışa çıkarma işlemi (POST)
        [HttpPost]
        public IActionResult SellTicket(Trip model)
        {
            if (model.DepartureDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("DepartureDate", "Bilet kalkış tarihi bugünden önce olamaz.");
                return View(model);
            }

            if (ModelState.IsValid)
            {
                model.SaleDate = DateTime.Now;
                model.TicketsSold = 0; // Başlangıçta hiç satılmadı
                _context.Trips.Add(model);
                _context.SaveChanges();
                TempData["Success"] = "Bilet başarıyla satışa çıkarıldı.";
                return RedirectToAction("SellTicket");
            }
            return View(model);
        }

        // Mevcut seyahat işlemleri
        public IActionResult Trips()
        {
            var now = DateTime.Now;

            var trips = _context.Trips.ToList(); // Verileri belleğe alıyoruz

            var model = new AdminTripListVM
            {
                UpcomingTrips = trips
                    .Where(t => t.DepartureDate.Add(t.DepartureTime) >= now)
                    .OrderBy(t => t.DepartureDate)
                    .ThenBy(t => t.DepartureTime)
                    .ToList(),

                PastTrips = trips
                    .Where(t => t.DepartureDate.Add(t.DepartureTime) < now)
                    .OrderByDescending(t => t.DepartureDate)
                    .ThenByDescending(t => t.DepartureTime)
                    .ToList()
            };

            return View(model);
        }

        // Satılan biletler sayfası
        public async Task<IActionResult> SoldTickets()
        {
            var soldTickets = await _context.Tickets
                .Include(t => t.Trip)
                .Include(t => t.User)
                .OrderByDescending(t => t.PurchaseDate)
                .ToListAsync();

            return View(soldTickets);
        }

        // Belirli bir sefer için satılan biletler
        public async Task<IActionResult> TripTickets(int tripId)
        {
            var trip = await _context.Trips.FindAsync(tripId);
            if (trip == null)
            {
                return NotFound();
            }

            var tickets = await _context.Tickets
                .Include(t => t.User)
                .Where(t => t.TripId == tripId)
                .OrderByDescending(t => t.PurchaseDate)
                .ToListAsync();

            ViewBag.Trip = trip;
            return View(tickets);
        }

        // Sefer koltuk durumunu görüntüleme
        public async Task<IActionResult> TripSeats(int tripId)
        {
            var trip = await _context.Trips.FindAsync(tripId);
            if (trip == null)
            {
                return NotFound();
            }

            var seats = await _context.Seats
                .Include(s => s.User)
                .Where(s => s.TripId == tripId)
                .OrderBy(s => s.SeatNumber)
                .ToListAsync();

            // Determine max seats based on transport type
            int maxSeats = trip.TransportType switch
            {
                "Otobüs" => 30,
                "Uçak" => 180,
                "Tren" => 120,
                _ => 30
            };

            var seatStatus = new Dictionary<int, Seat>();
            foreach (var seat in seats)
            {
                seatStatus[seat.SeatNumber] = seat;
            }

            ViewBag.Trip = trip;
            ViewBag.MaxSeats = maxSeats;
            ViewBag.SeatStatus = seatStatus;

            return View();
        }

        public IActionResult DeleteTrip(int id)
        {
            var trip = _context.Trips.Find(id);
            if (trip != null)
            {
                // Check if trip has sold tickets
                var hasTickets = _context.Tickets.Any(t => t.TripId == id && !t.IsCancelled);
                if (hasTickets)
                {
                    TempData["Error"] = "Bu seferde satılmış biletler olduğu için silinemez.";
                    return RedirectToAction("Trips");
                }

                // Delete related seats first
                var seats = _context.Seats.Where(s => s.TripId == id);
                _context.Seats.RemoveRange(seats);

                _context.Trips.Remove(trip);
                _context.SaveChanges();
                TempData["Success"] = "Sefer başarıyla silindi.";
            }
            return RedirectToAction("Trips");
        }
    }
}
