using DilekGiyim.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DilekGiyim.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> Variants => Set<ProductVariant>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StoreSettings> Settings => Set<StoreSettings>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>().HasIndex(x => x.Slug).IsUnique();

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.SalePrice).HasPrecision(18, 2);
            e.Property(x => x.Cost).HasPrecision(18, 2);
            e.Property(x => x.VatRate).HasPrecision(5, 4);
            e.Ignore(x => x.EffectivePrice);
            e.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Variants).WithOne(x => x.Product).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductVariant>(e =>
        {
            e.HasIndex(x => x.Sku).IsUnique();
            // Aynı üründe aynı renk + beden iki kez olamaz
            e.HasIndex(x => new { x.ProductId, x.ColorName, x.Size }).IsUnique();
        });

        b.Entity<Order>(e =>
        {
            e.HasIndex(x => x.OrderNo).IsUnique();
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.Shipping).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.PaidAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.PaymentToken);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CustomerId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OrderLine>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Ignore(x => x.LineTotal);
        });

        b.Entity<Coupon>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Value).HasPrecision(18, 2);
            e.Property(x => x.MinCartTotal).HasPrecision(18, 2);
        });

        b.Entity<AdminUser>(e =>
        {
            e.HasIndex(x => x.UserName).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasMany(x => x.Addresses).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Favorite>(e =>
        {
            e.HasKey(x => new { x.CustomerId, x.ProductId });
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PasswordResetToken>().HasIndex(x => x.TokenHash).IsUnique();

        b.Entity<StoreSettings>(e =>
        {
            e.Property(x => x.FreeShippingThreshold).HasPrecision(18, 2);
            e.Property(x => x.ShippingFee).HasPrecision(18, 2);
            e.Property(x => x.MinOrderTotal).HasPrecision(18, 2);
        });
    }
}
