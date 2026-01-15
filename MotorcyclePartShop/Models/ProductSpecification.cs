using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MotorcyclePartShop.Models
{
    public class ProductSpecification
    {
        [Key]
        public int SpecId { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        public string SpecName { get; set; }  // Ví dụ: "Loại động cơ"
        public string SpecValue { get; set; } // Ví dụ: "4 kỳ, 1 xi lanh, làm mát bằng dung dịch"
    }
}