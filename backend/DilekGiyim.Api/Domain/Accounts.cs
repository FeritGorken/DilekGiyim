namespace DilekGiyim.Api.Domain;

/// <summary>Yönetim paneli kullanıcısı</summary>
public class AdminUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
    /// <summary>Şifre değişince yenilenir; eski oturumlar geçersiz olur</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Mağaza müşterisi (üye)</summary>
public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    /// <summary>Küçük harfe çevrilmiş e-posta (benzersiz)</summary>
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    public bool MarketingConsent { get; set; }
    public DateTime KvkkAcceptedAt { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CustomerAddress> Addresses { get; set; } = [];
}

public class CustomerAddress
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    /// <summary>"Ev", "İş" …</summary>
    public string Title { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public string Line { get; set; } = "";
    public bool IsDefault { get; set; }
}

public class Favorite
{
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AccountType
{
    Admin,
    Customer
}

/// <summary>Şifre sıfırlama bağlantısı. Token'ın kendisi saklanmaz, yalnızca SHA-256 özeti.</summary>
public class PasswordResetToken
{
    public int Id { get; set; }
    public AccountType AccountType { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
