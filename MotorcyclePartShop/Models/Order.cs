using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MotorcyclePartShop.Models
{
    public class Order
    {
        // --- CONSTRUCTOR: Khởi tạo danh sách để tránh lỗi NullReference ---
        public Order()
        {
            Items = new List<OrderItem>();
            Tracking = new List<OrderTracking>();
            OrderDate = DateTime.Now; // Mặc định lấy giờ hiện tại
        }

        [Key]
        public int OrderId { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public DateTime OrderDate { get; set; }

        // Định dạng kiểu tiền tệ cho SQL Server (tránh lỗi mất số lẻ)
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; }

        // === CÁC TRƯỜNG CHO PHÉP NULL ===
        public string? PaymentStatus { get; set; }
        public string? DeliveryStatus { get; set; }
        public string? ShippingAddress { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TrackingCode { get; set; }

        // === KHÓA NGOẠI MÃ GIẢM GIÁ (MỚI) ===
        public int? PromotionId { get; set; }

        [ForeignKey("PromotionId")]
        public virtual Promotion? Promotion { get; set; }

        // === QUAN HỆ ===
        public virtual ICollection<OrderItem> Items { get; set; }
        public virtual ICollection<OrderTracking> Tracking { get; set; }
    }
}