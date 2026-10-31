namespace DilekGiyim.Api.Models;

public record Product(
    int Id,
    string Name,
    string Category,
    decimal Price,
    string[] Sizes,
    string? ImageUrl);
