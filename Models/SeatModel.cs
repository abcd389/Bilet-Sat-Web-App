using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiletSatisWebApp.Models
{
    public class Seat
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TripId { get; set; }

        [ForeignKey("TripId")]
        public Trip Trip { get; set; }

        [Required]
        public int SeatNumber { get; set; }

        public bool IsOccupied { get; set; } = false;

        public string? UserId { get; set; } // Null if seat is not occupied

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        public DateTime? ReservationDate { get; set; }
    }
}