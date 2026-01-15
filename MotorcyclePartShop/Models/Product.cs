using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MotorcyclePartShop.Models
{
    [Table("Products")] // Đảm bảo ánh xạ đúng tên bảng trong SQL
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        // ============================
        // 1. Thông tin cơ bản
        // ============================
        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        [MaxLength(255)]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; }

        public string Description { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        [Column(TypeName = "decimal(18,2)")] // Định dạng tiền tệ chuẩn cho SQL
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho không hợp lệ")]
        public int Stock { get; set; }

        public string MainImage { get; set; }

        public bool IsFeatured { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ============================
        // 2. Khóa ngoại (Foreign Keys)
        // ============================

        // --- BRAND (Thương hiệu) ---
        // Để int? (Nullable) để nếu xóa Brand thì sản phẩm không bị xóa (Set Null)
        public int? BrandId { get; set; }

        [ForeignKey("BrandId")]
        public virtual Brand Brand { get; set; }

        // --- CATEGORY (Danh mục) ---
        // Category bắt buộc phải có (int), nếu xóa Category thì check ràng buộc (Restrict)
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        // ============================
        // 3. Navigation Properties (Danh sách liên quan)
        // ============================

        // Danh sách ảnh phụ
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        // Danh sách thông số kỹ thuật
        public virtual ICollection<ProductSpecification> Specifications { get; set; } = new List<ProductSpecification>();

        // Danh sách khuyến mãi (Quan hệ N-N)
        public virtual ICollection<PromotionProduct> PromotionProducts { get; set; } = new List<PromotionProduct>();

        // (Tùy chọn) Danh sách chi tiết đơn hàng đã mua sản phẩm này 
        // Giúp thống kê sản phẩm bán chạy dễ dàng hơn
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}