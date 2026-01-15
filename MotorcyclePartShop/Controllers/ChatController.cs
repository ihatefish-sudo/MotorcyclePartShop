using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MotorcyclePartShop.Controllers
{
    public class ChatController : Controller
    {
        private readonly MotorcyclePartShopDbContext _context;
        // Key của bạn
        private readonly string _apiKey = "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";
        private readonly string _apiUrl = "https://api.groq.com/openai/v1/chat/completions";

        public ChatController(MotorcyclePartShopDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string userMessage)
        {
            if (string.IsNullOrEmpty(userMessage))
                return Json(new { reply = "How can I help you today?" });

            try
            {
                // --- BƯỚC 1: TÌM KIẾM & CHẤM ĐIỂM (LOGIC "CẮT ĐUÔI") ---

                var allProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive)
                    .Select(p => new {
                        p.ProductId,
                        p.ProductName,
                        p.Description,
                        CategoryName = p.Category.CategoryName,
                        p.Price,
                        p.Stock,
                        p.MainImage
                    })
                    .ToListAsync();

                var stopWords = new[] { "i", "want", "to", "buy", "need", "find", "looking", "for", "please", "show", "me", "a", "an", "the", "is", "are", "have", "do", "you", "shop" };
                var keywords = userMessage.ToLower()
                    .Split(new[] { ' ', ',', '?', '.', '!', ';', '-' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 2 && !stopWords.Contains(w))
                    .ToList();

                var relatedProducts = new List<dynamic>();
                bool foundProduct = false;

                if (keywords.Any())
                {
                    // 1. Chấm điểm tất cả sản phẩm
                    var scoredList = allProducts.Select(p => {
                        int score = 0;
                        string name = p.ProductName.ToLower();
                        string cat = p.CategoryName?.ToLower() ?? "";
                        string desc = p.Description?.ToLower() ?? "";

                        foreach (var k in keywords)
                        {
                            if (name.Contains(k)) score += 10;       // Ưu tiên 1: Tên (10đ)
                            else if (cat.Contains(k)) score += 5;    // Ưu tiên 2: Danh mục (5đ)
                            // Lưu ý: Không cộng điểm Mô tả ở đây để tránh nhiễu, 
                            // hoặc chỉ cộng rất thấp nếu thực sự cần.
                        }
                        return new { Product = p, Score = score };
                    }).Where(x => x.Score > 0).ToList();

                    // 2. Logic "CẮT ĐUÔI" (Quan trọng)
                    // Nếu có bất kỳ sản phẩm nào điểm >= 10 (tức là khớp Tên), ta chỉ lấy nhóm đó.
                    // Bỏ qua các sản phẩm điểm thấp (chỉ khớp danh mục hoặc mô tả mờ nhạt).
                    if (scoredList.Any(x => x.Score >= 10))
                    {
                        relatedProducts = scoredList
                            .Where(x => x.Score >= 10) // Chỉ lấy hàng xịn
                            .OrderByDescending(x => x.Score)
                            .Take(5)
                            .Select(x => x.Product)
                            .ToList<dynamic>();
                    }
                    else
                    {
                        // Nếu không có hàng xịn, mới đành lấy hàng khớp danh mục
                        relatedProducts = scoredList
                            .OrderByDescending(x => x.Score)
                            .Take(3)
                            .Select(x => x.Product)
                            .ToList<dynamic>();
                    }

                    foundProduct = relatedProducts.Any();
                }

                // --- BƯỚC 2: TẠO HTML (Giữ nguyên) ---
                string productHtml = "";
                string productContextForAI = "";

                if (foundProduct)
                {
                    var sb = new StringBuilder();
                    sb.Append("<div class='chat-product-list mt-2'>");

                    foreach (var p in relatedProducts)
                    {
                        string imgUrl = p.MainImage.StartsWith("http") ? p.MainImage : $"/images/products/{p.MainImage}";
                        string price = p.Price.ToString("#,##0") + "đ";

                        sb.Append($@"
                            <div class='card mb-2 shadow-sm border-0'>
                                <div class='row g-0 align-items-center'>
                                    <div class='col-3 text-center p-1'>
                                        <img src='{imgUrl}' class='img-fluid rounded' style='max-height: 60px; object-fit: contain;'>
                                    </div>
                                    <div class='col-9'>
                                        <div class='card-body p-2'>
                                            <h6 class='card-title small mb-1 fw-bold text-truncate'>{p.ProductName}</h6>
                                            <div class='d-flex justify-content-between align-items-center'>
                                                <span class='text-danger small fw-bold'>{price}</span>
                                                <a href='/Product/Details/{p.ProductId}' class='btn btn-xs btn-danger text-white py-0 px-2' style='font-size: 0.75rem;'>
                                                    Buy now <i class='fas fa-arrow-right ms-1'></i>
                                                </a>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>");

                        productContextForAI += $"- Found: {p.ProductName} ({price})\n";
                    }
                    sb.Append("</div>");
                    productHtml = sb.ToString();
                }
                else
                {
                    productContextForAI = "No specific products found matching the user keywords.";
                }

                // --- BƯỚC 3: GỌI AI GROQ (Giữ nguyên) ---
                var systemPrompt = $@"
                    You are a helpful AI sales assistant for 'Moto Shop'.
                    
                    Situation:
                    The system searched for products based on user query: '{userMessage}'.
                    Result: 
                    {productContextForAI}
                    
                    Task:
                    1. If products are found: Respond with a short, polite introductory sentence (e.g. 'I found these products matching your request:').
                    2. If NO products found: Apologize and suggest contacting hotline 1900xxxx.
                    3. Keep it friendly and concise.
                ";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                    var requestBody = new
                    {
                        model = "llama-3.3-70b-versatile",
                        messages = new[] {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userMessage }
                        },
                        max_tokens = 200
                    };

                    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
                    var content = new StringContent(JsonSerializer.Serialize(requestBody, jsonOptions), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(_apiUrl, content);

                    string aiReply = "I'm checking...";
                    if (response.IsSuccessStatusCode)
                    {
                        var resultJson = await response.Content.ReadAsStringAsync();
                        var jsonNode = JsonNode.Parse(resultJson);
                        aiReply = jsonNode["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
                    }

                    return Json(new { reply = aiReply + productHtml });
                }
            }
            catch (Exception ex)
            {
                return Json(new { reply = "System Error: " + ex.Message });
            }
        }
    }
}