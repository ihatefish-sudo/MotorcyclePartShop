using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MotorcyclePartShop.Models
{
    public class ReturnRequest
    {
        [Key]
        public int ReturnId { get; set; }

        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }

        public string Reason { get; set; }

        public string Note { get; set; }

        public string ImageEvidence { get; set; }

        public string Status { get; set; } = "Pending";

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        // --- SỬA LỖI TẠI ĐÂY ---
        // Thêm dấu ? vào sau DateTime để cho phép Null
        public DateTime? ResolvedAt { get; set; }
        // -----------------------

        public string? AdminNote { get; set; } // Nên để string? cho AdminNote luôn
    }
}