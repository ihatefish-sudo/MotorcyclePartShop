using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MotorcyclePartShop.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; }

        public bool IsActive { get; set; } = true;

        // --- BỔ SUNG DÒNG NÀY ĐỂ HẾT LỖI COMPILE ---
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}