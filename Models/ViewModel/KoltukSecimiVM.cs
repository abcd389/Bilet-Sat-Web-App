using BiletSatisWebApp.Models;
namespace BiletSatisWebApp.Models.ViewModel
{

    public class KoltukSecimiVM
    {
        public Trip Trip { get; set; }
        public List<int> SelectedSeats { get; set; } = new List<int>();
        public List<int> SoldSeats { get; set; } = new List<int>(); // Seats that are already sold
        public string TransportType { get; set; } // Otobüs, Uçak, Tren
        public int MaxSeats { get; set; } // Maximum number of seats based on transport type
    }

}