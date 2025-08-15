using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiletSatisWebApp.Models
{
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        [Required]
        public int TripId { get; set; }

        [ForeignKey("TripId")]
        public Trip Trip { get; set; }

        [Required]
        public List<int> SeatNumbers { get; set; } = new List<int>();

        [Required]
        public string PassengerFirstName { get; set; }

        [Required]
        public string PassengerLastName { get; set; }

        [Required]
        public string PassengerEmail { get; set; }

        [Required]
        public string PassengerPhone { get; set; }

        [Required]
        public decimal TotalPrice { get; set; }

        [Required]
        public DateTime PurchaseDate { get; set; }

        public bool IsCancelled { get; set; } = false;

        public DateTime? CancellationDate { get; set; }

        [NotMapped]
        public string SeatNumbersString 
        { 
            get => string.Join(",", SeatNumbers);
            set => SeatNumbers = value?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(int.Parse).ToList() ?? new List<int>();
        }
    }
}