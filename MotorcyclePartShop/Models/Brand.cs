using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MotorcyclePartShop.Models
{
    [Table("Brands")] // Đảm bảo map đúng tên bảng trong SQL Server
    public class Brand
    {
        [Key]
        public int BrandId { get; set; }

        [Required(ErrorMessage = "Tên thương hiệu không được để trống")]
        [StringLength(100, ErrorMessage = "Tên thương hiệu không được vượt quá 100 ký tự")]
        public string BrandName { get; set; }

        public string Description { get; set; } // Nếu bạn chưa chạy lệnh SQL thêm cột này, hãy xóa dòng này

        public bool IsActive { get; set; } = true; // Mặc định là Active

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Mặc định lấy giờ hiện tại

        // ==========================================
        // Navigation Properties (Quan hệ)
        // ==========================================

        // Mối quan hệ 1-N: Một Brand có nhiều Product
        // Từ khóa 'virtual' giúp EF Core hỗ trợ Lazy Loading (nếu cần)
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}  