using BiletSatisWebApp.Models;
using System.ComponentModel.DataAnnotations;

namespace BiletSatisWebApp.Models.ViewModel
{
    public class PaymentVM
    {
        public Trip Trip { get; set; }
        public List<int> SelectedSeats { get; set; }
        public decimal TotalPrice { get; set; }

        [Required(ErrorMessage = "İsim alanı zorunludur")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Soyisim alanı zorunludur")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "E-posta alanı zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Telefon alanı zorunludur")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
        public string Phone { get; set; }
    }
}