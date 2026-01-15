using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MotorcyclePartShop.Models
{
    public class PasswordResetToken
    {
        [Key]
        public int Id { get; set; }

        // Liên kết với bảng User để biết mã này của ai
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required]
        [StringLength(10)]
        public string OtpCode { get; set; } // Mã OTP (6 số)

        public DateTime ExpiryTime { get; set; } // Thời điểm hết hạn (Sau 1 phút)

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Thời điểm tạo (Dùng để tính cooldown 1p30s)

        public bool IsUsed { get; set; } = false; // Đánh dấu đã sử dụng chưa để tránh dùng lại
    }
}