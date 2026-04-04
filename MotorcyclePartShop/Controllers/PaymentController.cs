using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using MotorcyclePartShop.Utilities;
using System;
using System.Threading.Tasks;

namespace MotorcyclePartShop.Controllers
{
    public class PaymentController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;

        public PaymentController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        // ========================================================
        // 1. TẠO URL THANH TOÁN (GỬI YÊU CẦU LÊN VNPAY)
        // ========================================================
        public async Task<IActionResult> CreatePaymentUrl(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            // Gắn cứng toàn bộ cấu hình để né hoàn toàn lỗi từ server Render
            string vnp_TmnCode = "XKY2ST3F";
            string vnp_HashSecret = "GJYOOVLZX5K1ISUSMGQW6QCZXT6EN18A";
            string vnp_Url = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            string vnp_ReturnUrl = "https://motorcyclepartshop.onrender.com/Payment/PaymentCallback";

            VnPayLibrary vnpay = new VnPayLibrary();

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);

            long amount = (long)(order.TotalAmount * 100);
            vnpay.AddRequestData("vnp_Amount", amount.ToString());

            // Giờ Việt Nam (Bắt buộc)
            string createDateVnTime = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
            vnpay.AddRequestData("vnp_CreateDate", createDateVnTime);

            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", "127.0.0.1"); // Ép IP cứng
            vnpay.AddRequestData("vnp_Locale", "vn");

            // Xóa sạch khoảng trắng để chữ ký lúc đi và lúc về khớp 100%
            vnpay.AddRequestData("vnp_OrderInfo", "Order_" + order.OrderId);
            vnpay.AddRequestData("vnp_OrderType", "other");

            vnpay.AddRequestData("vnp_TxnRef", order.OrderId.ToString());
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_ReturnUrl);

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);

            return Redirect(paymentUrl);
        }

        // ========================================================
        // 2. XỬ LÝ KẾT QUẢ TRẢ VỀ TỪ VNPAY (CALLBACK)
        // ========================================================
        public async Task<IActionResult> PaymentCallback()
        {
            // [QUAN TRỌNG NHẤT]: Gắn cứng HashSecret y hệt như lúc đi
            // Tuyệt đối không đọc từ Configuration nữa để tránh lỗi Render
            string vnp_HashSecret = "GJYOOVLZX5K1ISUSMGQW6QCZXT6EN18A";

            var vnpayData = Request.Query;
            VnPayLibrary vnpay = new VnPayLibrary();

            // Đọc toàn bộ dữ liệu VNPAY trả về trên thanh URL
            foreach (var s in vnpayData)
            {
                if (!string.IsNullOrEmpty(s.Key) && s.Key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(s.Key, s.Value);
                }
            }

            // Lấy các tham số quan trọng
            long orderId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef"));
            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_SecureHash = vnpay.GetResponseData("vnp_SecureHash");

            // Kiểm tra chữ ký (Validate Signature)
            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

            if (checkSignature)
            {
                var order = await _context.Orders.FindAsync((int)orderId);

                // 00 = Giao dịch thành công
                if (vnp_ResponseCode == "00")
                {
                    if (order != null)
                    {
                        order.PaymentStatus = "Paid";
                        order.DeliveryStatus = "Pending";

                        _context.OrderTracking.Add(new Models.OrderTracking
                        {
                            OrderId = order.OrderId,
                            Status = "Payment Successful via VNPAY",
                            // Dùng UtcNow để không bị crash trên PostgreSQL
                            UpdatedAt = DateTime.UtcNow
                        });

                        await _context.SaveChangesAsync();
                    }
                    ViewBag.Message = "Payment successful!";
                    ViewBag.IsSuccess = true;
                }
                else
                {
                    ViewBag.Message = "The transaction is not complete. Your order will be held for 10 minutes. Please re-pay in your Order History.";
                    ViewBag.IsSuccess = false;
                }
            }
            else
            {
                // Sai chữ ký (Lỗi 70)
                ViewBag.Message = "An error occurred (Incorrect security signature).";
                ViewBag.IsSuccess = false;
            }

            return View();
        }
    }
}