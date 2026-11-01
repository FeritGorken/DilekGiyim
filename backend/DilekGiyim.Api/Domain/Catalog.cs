namespace DilekGiyim.Api.Domain;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int SortOrder { get; set; }
    public List<Product> Products { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    /// <summary>Ürün kodu, örn. DG-TR-2601</summary>
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string? FabricAndCare { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>KDV dahil satış fiyatı</summary>
    public decimal Price { get; set; }
    /// <summary>KDV dahil indirimli fiyat; boşsa indirim yok</summary>
    public decimal? SalePrice { get; set; }
    /// <summary>KDV hariç birim maliyet</summary>
    public decimal Cost { get; set; }
    public decimal VatRate { get; set; } = 0.10m;

    public bool IsPublished { get; set; } = true;
    public bool IsNew { get; set; }
    public bool IsFeatured { get; set; }

    /// <summary>Bir varyantın stoğu bu sayıya eşit veya altındaysa "kritik" sayılır</summary>
    public int CriticalThreshold { get; set; } = 1;
    /// <summary>Sitede "Son X ürün" uyarısının gösterileceği stok seviyesi</summary>
    public int LowStockDisplay { get; set; } = 3;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ProductImage> Images { get; set; } = [];
    public List<ProductVariant> Variants { get; set; } = [];

    public decimal EffectivePrice => SalePrice is > 0 && SalePrice < Price ? SalePrice.Value : Price;
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Url { get; set; } = "";
    public string Alt { get; set; } = "";
    public int SortOrder { get; set; }
}

/// <summary>Renk + beden kombinasyonu. Stok bu seviyede tutulur.</summary>
public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ColorName { get; set; } = "";
    public string ColorHex { get; set; } = "#000000";
    public string Size { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Stock { get; set; }
}
