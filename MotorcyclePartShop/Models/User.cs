using System;
using System.Collections.Generic;

namespace MotorcyclePartShop.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public string Phone { get; set; }
        public string Address { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<UserRole> UserRoles { get; set; }
    }
}
