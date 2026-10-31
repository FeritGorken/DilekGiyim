using DilekGiyim.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DilekGiyim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    // Veritabanı bağlanana kadar örnek veri
    private static readonly Product[] Products =
    [
        new(1, "Basic Pamuklu Tişört", "Tişört", 349.90m, ["S", "M", "L", "XL"], null),
        new(2, "Keten Gömlek", "Gömlek", 899.90m, ["M", "L", "XL"], null),
        new(3, "Yüksek Bel Jean", "Pantolon", 1199.90m, ["36", "38", "40", "42"], null),
        new(4, "Triko Hırka", "Hırka", 1049.90m, ["S", "M", "L"], null),
    ];

    [HttpGet]
    public ActionResult<IEnumerable<Product>> GetAll() => Ok(Products);

    [HttpGet("{id:int}")]
    public ActionResult<Product> GetById(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        return product is null ? NotFound() : Ok(product);
    }
}
