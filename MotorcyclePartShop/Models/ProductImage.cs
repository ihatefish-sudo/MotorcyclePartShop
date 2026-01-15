namespace MotorcyclePartShop.Models
{
    public class ProductImage
    {
        public int ImageId { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; }

        public string ImageUrl { get; set; }
    }
}
