using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MotorcyclePartShop.Services
{
    public class AutoCancelUnpaidOrdersService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public AutoCancelUnpaidOrdersService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessExpiredOrders(); // Đổi tên hàm cho đúng ngữ cảnh mới
                // Chờ 1 phút trước khi quét lại
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessExpiredOrders()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<MotorcyclePartShopDbContext>();

                // Lấy mốc thời gian 10 phút trước
                var thresholdTime = DateTime.Now.AddMinutes(-10);

                // Tìm các đơn hàng thỏa mãn điều kiện:
                var expiredOrders = await context.Orders
                    .Include(o => o.Items) // Load chi tiết để trả kho
                    .Where(o => o.PaymentMethod == "VNPAY"
                                && o.PaymentStatus == "Pending"
                                && o.DeliveryStatus != "Cancelled"       // Chưa bị hủy thủ công
                                && o.DeliveryStatus != "Payment Failed"  // Chưa bị đánh dấu thất bại trước đó
                                && o.OrderDate <= thresholdTime)
                    .ToListAsync();

                if (expiredOrders.Any())
                {
                    foreach (var order in expiredOrders)
                    {
                        // 1. Cập nhật trạng thái theo yêu cầu mới
                        order.DeliveryStatus = "Payment Failed"; // Thay đổi từ "Cancelled"
                        order.PaymentStatus = "Timeout";         // Thay đổi từ "Cancelled (Timeout)"

                        // 2. Vẫn cần trả lại số lượng tồn kho (Stock) 
                        // Vì đơn hàng coi như thất bại, hàng phải được bán cho người khác
                        foreach (var item in order.Items)
                        {
                            var product = await context.Products.FindAsync(item.ProductId);
                            if (product != null)
                            {
                                product.Stock += item.Quantity; // Cộng lại kho
                            }
                        }

                        // 3. Ghi log tracking với nội dung mới
                        context.OrderTracking.Add(new OrderTracking
                        {
                            OrderId = order.OrderId,
                            Status = "System: Payment timed out (10 mins). Status updated to Payment Failed.",
                            UpdatedAt = DateTime.Now
                        });
                    }

                    await context.SaveChangesAsync();
                }
            }
        }
    }
}