namespace MotorcyclePartShop.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string MainImage { get; set; }

        // Tính tổng tiền cho item này (Giá * Số lượng)
        public decimal Total => Price * Quantity;
    }
}