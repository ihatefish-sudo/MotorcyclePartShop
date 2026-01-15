using System;

namespace MotorcyclePartShop.Models
{
    public class OrderTracking
    {
        public int TrackingId { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; }

        public string Status { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
