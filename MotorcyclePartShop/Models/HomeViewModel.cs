using MotorcyclePartShop.Models;

namespace MotorcyclePartShop.Models
{
    public class HomeViewModel
    {
        public List<Category> Categories { get; set; }
        public List<Product> FeaturedProducts { get; set; } // Sản phẩm nổi bật
        public List<Product> NewProducts { get; set; }      // Sản phẩm mới
        public List<Product> DiscountedProducts { get; set; } // Sản phẩm đang giảm giá
    }
}