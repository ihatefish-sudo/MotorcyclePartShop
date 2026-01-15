using Microsoft.EntityFrameworkCore;
using MotorcyclePartShop.Models;

namespace MotorcyclePartShop.Data
{
    public class MotorcyclePartShopDbContext : DbContext
    {
        public MotorcyclePartShopDbContext(DbContextOptions<MotorcyclePartShopDbContext> options)
            : base(options)
        {
        }

        // ===============================
        // 1. Database Tables (DbSet)
        // ===============================
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductSpecification> ProductSpecifications { get; set; }

        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<PromotionProduct> PromotionProducts { get; set; }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderTracking> OrderTracking { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<ReturnRequest> ReturnRequests { get; set; }

        // ===============================
        // 2. Fluent API Mapping (Cấu hình chi tiết)
        // ===============================
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------------------------
            // A. Cấu hình Kiểu dữ liệu (Decimal) cho SQL Server
            // ---------------------------
            // Định dạng tiền tệ chính xác để tránh lỗi làm tròn
            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasColumnType("decimal(18,2)");

            // ---------------------------
            // B. User & Roles (Many-to-Many)
            // ---------------------------
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId }); // Khóa chính phức hợp

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            // ---------------------------
            // C. Product Relationships (QUAN TRỌNG: SỬA LỖI BrandId1 TẠI ĐÂY)
            // ---------------------------

            // 1. Product - Category
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products) // <--- Phải chỉ rõ 'c.Products' để map danh sách ngược lại
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Chặn xóa Category nếu còn sản phẩm

            // 2. Product - Brand
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Brand)
                .WithMany(b => b.Products) // <--- Phải chỉ rõ 'b.Products' để tránh lỗi BrandId1
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.SetNull); // Nếu xóa Brand, set BrandId = NULL (Yêu cầu Product.BrandId là int?)

            // 3. Product - Images
            modelBuilder.Entity<ProductImage>()
                .HasKey(pi => pi.ImageId);

            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Product thì xóa luôn ảnh

            // 4. Product - Specifications
            modelBuilder.Entity<ProductSpecification>()
                .HasKey(ps => ps.SpecId);

            modelBuilder.Entity<ProductSpecification>()
                .HasOne(ps => ps.Product)
                .WithMany(p => p.Specifications)
                .HasForeignKey(ps => ps.ProductId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Product thì xóa luôn thông số kỹ thuật

            // ---------------------------
            // D. Promotions (Many-to-Many)
            // ---------------------------
            modelBuilder.Entity<PromotionProduct>()
                .HasKey(pp => new { pp.ProductId, pp.PromotionId });

            modelBuilder.Entity<PromotionProduct>()
                .HasOne(pp => pp.Product)
                .WithMany(p => p.PromotionProducts)
                .HasForeignKey(pp => pp.ProductId);

            modelBuilder.Entity<PromotionProduct>()
                .HasOne(pp => pp.Promotion)
                .WithMany(p => p.PromotionProducts)
                .HasForeignKey(pp => pp.PromotionId);

            // ---------------------------
            // E. Order & OrderItems
            // ---------------------------
            modelBuilder.Entity<OrderItem>()
                .HasKey(oi => oi.OrderItemId);

            // OrderItem thuộc về 1 Order
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Order thì xóa luôn chi tiết đơn hàng

            // OrderItem tham chiếu tới 1 Product
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems) // Map với danh sách OrderItems bên Product (nếu có)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict); // Không cho xóa Product nếu đã có người mua (giữ lịch sử)

            // ---------------------------
            // F. OrderTracking
            // ---------------------------
            modelBuilder.Entity<OrderTracking>()
                .HasKey(ot => ot.TrackingId);

            modelBuilder.Entity<OrderTracking>()
                .HasOne(ot => ot.Order)
                .WithMany(o => o.Tracking)
                .HasForeignKey(ot => ot.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------------------------
            // G. ReturnRequest
            // ---------------------------
            modelBuilder.Entity<ReturnRequest>()
                .HasKey(r => r.ReturnId);

            modelBuilder.Entity<ReturnRequest>()
                .HasOne(r => r.Order)
                .WithMany() // Một đơn hàng có thể có ReturnRequest (1-N hoặc 1-1 tùy logic, để trống là 1-N mặc định)
                .HasForeignKey(r => r.OrderId);
        }
    }
}