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
        private readonly IConfiguration _configuration;

        public PaymentController(MotorcyclePartShopDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // 1. TẠO URL THANH TOÁN
        public async Task<IActionResult> CreatePaymentUrl(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            var vnPayModel = _configuration.GetSection("Vnpay");

            string vnp_TmnCode = vnPayModel["TmnCode"];
            string vnp_HashSecret = vnPayModel["HashSecret"];
            string vnp_Url = vnPayModel["BaseUrl"];

            // [FIXED] Lấy URL trả về từ file cấu hình (hoặc biến môi trường trên Render)
            // Thay vì dùng Url.Action() dễ bị load balancer của Render ép về http://
            string vnp_ReturnUrl = vnPayModel["PaymentBackReturnUrl"];

            VnPayLibrary vnpay = new VnPayLibrary();

            vnpay.AddRequestData("vnp_Version", vnPayModel["Version"]);
            vnpay.AddRequestData("vnp_Command", vnPayModel["Command"]);
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);

            long amount = (long)(order.TotalAmount * 100);
            vnpay.AddRequestData("vnp_Amount", amount.ToString());

            // VNPAY bắt buộc dùng giờ Việt Nam (GMT+7). 
            // Dùng UtcNow.AddHours(7) để luôn lấy đúng giờ VN bất kể server cloud đặt ở đâu.
            string createDateVnTime = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
            vnpay.AddRequestData("vnp_CreateDate", createDateVnTime);

            vnpay.AddRequestData("vnp_CurrCode", vnPayModel["CurrCode"]);
            vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnpay.AddRequestData("vnp_Locale", vnPayModel["Locale"]);

            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang #" + order.OrderId);
            vnpay.AddRequestData("vnp_OrderType", "other");

            vnpay.AddRequestData("vnp_TxnRef", order.OrderId.ToString());

            vnpay.AddRequestData("vnp_ReturnUrl", vnp_ReturnUrl);

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);

            return Redirect(paymentUrl);
        }

        // 2. XỬ LÝ KẾT QUẢ TRẢ VỀ (CALLBACK)
        public async Task<IActionResult> PaymentCallback()
        {
            var response = _configuration.GetSection("Vnpay");
            string vnp_HashSecret = response["HashSecret"];

            var vnpayData = Request.Query;
            VnPayLibrary vnpay = new VnPayLibrary();

            foreach (var s in vnpayData)
            {
                if (!string.IsNullOrEmpty(s.Key) && s.Key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(s.Key, s.Value);
                }
            }

            long orderId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef"));
            long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100;
            long vnpayTranId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
            string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
            string vnp_SecureHash = vnpay.GetResponseData("vnp_SecureHash");

            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

            if (checkSignature)
            {
                var order = await _context.Orders.FindAsync((int)orderId);

                if (vnp_ResponseCode == "00") // 1. THANH TOÁN THÀNH CÔNG
                {
                    if (order != null)
                    {
                        order.PaymentStatus = "Paid";
                        order.DeliveryStatus = "Pending";

                        _context.OrderTracking.Add(new Models.OrderTracking
                        {
                            OrderId = order.OrderId,
                            Status = "Payment Successful via VNPAY",
                            // Dùng UtcNow thay vì Now để chuẩn hóa với Postgres
                            UpdatedAt = DateTime.UtcNow
                        });

                        await _context.SaveChangesAsync();
                    }
                    ViewBag.Message = "Payment successful!";
                    ViewBag.IsSuccess = true;
                }
                else // 2. THANH TOÁN THẤT BẠI / HỦY GIỮA CHỪNG
                {
                    ViewBag.Message = "The transaction is not complete. Your order will be held for 10 minutes. Please re-pay in your Order History.";
                    ViewBag.IsSuccess = false;
                }
            }
            else
            {
                ViewBag.Message = "An error occurred (Incorrect security signature).";
                ViewBag.IsSuccess = false;
            }

            return View();
        }
    }
}