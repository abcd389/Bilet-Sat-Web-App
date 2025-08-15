using BiletSatis.Models;
using BiletSatisWebApp.Models;
using BiletSatisWebApp.Models.ViewModel;
using BiletSatisWebApp.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

namespace BiletSatisWebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<ApplicationUser> userManager,
                                  SignInManager<ApplicationUser> signInManager,
                                  ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        // ------------------- KAYIT -------------------
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    Ad = model.FirstName, // Burada FirstName'i AspNetUsers.Ad sütununa yazıyoruz
                    Soyad = model.LastName,
                    UserType = "User"
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // ------------------- GİRİŞ -------------------
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi.");
            }

            return View(model);
        }

        // ------------------- ÇIKIŞ -------------------
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        
        // -------------- PROFİL -----------------------
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Get user's tickets  
            var tickets = await _context.Tickets
                .Include(t => t.Trip)
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.PurchaseDate)
                .ToListAsync();

            var userTickets = new List<UserTravelVM>();

            foreach (var ticket in tickets)
            {
                var canCancel = CanCancelTicket(ticket);
                var status = GetTicketStatus(ticket);

                userTickets.Add(new UserTravelVM
                {
                    TicketId = ticket.Id,
                    Trip = ticket.Trip,
                    SeatNumbers = ticket.SeatNumbers,
                    PassengerName = $"{ticket.PassengerFirstName} {ticket.PassengerLastName}",
                    PassengerEmail = ticket.PassengerEmail,
                    PassengerPhone = ticket.PassengerPhone,
                    TotalPrice = ticket.TotalPrice,
                    PurchaseDate = ticket.PurchaseDate,
                    IsCancelled = ticket.IsCancelled,
                    CancellationDate = ticket.CancellationDate,
                    CanCancel = canCancel,
                    StatusText = status.Item1,
                    StatusColor = status.Item2
                });
            }

            var model = new ProfileTravelVM
            {
                Ad = user.Ad,
                Soyad = user.Soyad,
                Email = user.Email,
                UserType = user.UserType,
                Tickets = userTickets
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CancelTicket(int ticketId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Kullanıcı bulunamadı!" });
            }

            var ticket = await _context.Tickets
                .Include(t => t.Trip)
                .FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == user.Id);

            if (ticket == null)
            {
                return Json(new { success = false, message = "Bilet bulunamadı!" });
            }

            if (!CanCancelTicket(ticket))
            {
                return Json(new { success = false, message = "Bu bilet iptal edilemez. Sefer saatine 1 saatten az kaldı veya sefer tarihi geçti." });
            }

            // Cancel the ticket
            ticket.IsCancelled = true;
            ticket.CancellationDate = DateTime.Now;

            // Free the seats
            var seats = await _context.Seats
                .Where(s => s.TripId == ticket.TripId && 
                           ticket.SeatNumbers.Contains(s.SeatNumber) && 
                           s.UserId == user.Id)
                .ToListAsync();

            foreach (var seat in seats)
            {
                seat.IsOccupied = false;
                seat.UserId = null;
                seat.ReservationDate = null;
            }

            // Update trip sold tickets count
            ticket.Trip.TicketsSold -= ticket.SeatNumbers.Count;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Biletiniz başarıyla iptal edildi." });
        }

        private bool CanCancelTicket(Ticket ticket)
        {
            if (ticket.IsCancelled)
                return false;

            var tripDateTime = ticket.Trip.DepartureDate.Date + ticket.Trip.DepartureTime;
            var now = DateTime.Now;

            // Check if trip date has passed or if there's less than 1 hour to departure
            return tripDateTime > now.AddHours(1);
        }

        private (string, string) GetTicketStatus(Ticket ticket)
        {
            if (ticket.IsCancelled)
                return ("İptal Edildi", "danger");

            var tripDateTime = ticket.Trip.DepartureDate.Date + ticket.Trip.DepartureTime;
            var now = DateTime.Now;

            if (tripDateTime < now)
                return ("Tamamlandı", "success");
            else if (tripDateTime <= now.AddHours(1))
                return ("Yaklaşan", "warning");
            else
                return ("Aktif", "primary");
        }
    }
}



