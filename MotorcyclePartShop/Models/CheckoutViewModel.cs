using System.ComponentModel.DataAnnotations;

namespace MotorcyclePartShop.Models
{
    public class CheckoutViewModel
    {
        public List<CartItem> CartItems { get; set; }

        // --- SỬA LẠI: Thêm { get; set; } ---
        // Để Controller có thể gán giá trị sau khi tính toán
        public decimal TotalAmount { get; set; }

        public string? CouponCode { get; set; }
        public decimal DiscountAmount { get; set; } = 0;

        // --- SỬA LẠI: Thêm { get; set; } ---
        // Để Controller có toàn quyền quyết định số tiền cuối cùng (sau khi cộng ship, trừ mã...)
        public decimal FinalAmount { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        public string Address { get; set; }

        public string? Note { get; set; }
        public string Province { get; set; }
        public decimal ShippingFee { get; set; } = 0;
        public string PaymentMethod { get; set; } = "COD";
    }
}