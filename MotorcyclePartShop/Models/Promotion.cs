using System;
using System.Collections.Generic;

namespace MotorcyclePartShop.Models
{
    public class Promotion
    {
        public int PromotionId { get; set; }
        public string PromoCode { get; set; }

        public double DiscountPercent { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }
        public int UsageLimitPerUser { get; set; } = 1;
        public ICollection<PromotionProduct> PromotionProducts { get; set; }
    }
}
