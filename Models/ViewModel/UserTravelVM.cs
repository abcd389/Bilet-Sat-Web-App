using BiletSatisWebApp.Models;

namespace BiletSatisWebApp.Models.ViewModel
{
    public class UserTravelVM
    {
        public int TicketId { get; set; }
        public Trip Trip { get; set; }
        public List<int> SeatNumbers { get; set; }
        public string PassengerName { get; set; }
        public string PassengerEmail { get; set; }
        public string PassengerPhone { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime PurchaseDate { get; set; }
        public bool IsCancelled { get; set; }
        public DateTime? CancellationDate { get; set; }
        public bool CanCancel { get; set; } // Based on time restrictions
        public string StatusText { get; set; }
        public string StatusColor { get; set; }
    }

    public class ProfileTravelVM
    {
        public string Ad { get; set; }
        public string Soyad { get; set; }
        public string Email { get; set; }
        public string UserType { get; set; }
        public List<UserTravelVM> Tickets { get; set; } = new List<UserTravelVM>();
    }
}