using HelloApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace HelloApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    // In-memory store — static so it survives between requests
    private static readonly List<Product> _products = new()
    {
        new Product { Id = 1, Name = "Laptop",     Price = 999.99m,  Category = "Electronics" },
        new Product { Id = 2, Name = "Desk Chair", Price = 249.00m,  Category = "Furniture"   },
        new Product { Id = 3, Name = "Notebook",   Price = 3.99m,    Category = "Stationery"  },
    };

    private static int _nextId = 4;

    // GET api/products
    [HttpGet]
    public ActionResult<IEnumerable<Product>> GetAll()
    {
        return Ok(_products);
    }

    // GET api/products/1
    [HttpGet("{id:int}")]
    public ActionResult<Product> GetById(int id)
    {
        var product = _products.Find(p => p.Id == id);
        if (product == null)
            return NotFound($"Product with id {id} not found.");
        return Ok(product);
    }

    // POST api/products
    [HttpPost]
    public ActionResult<Product> Create(Product product)
    {
        product.Id = _nextId++;
        _products.Add(product);

        // 201 Created — includes Location header pointing to the new resource
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // PUT api/products/1
    [HttpPut("{id:int}")]
    public IActionResult Update(int id, Product updated)
    {
        var product = _products.Find(p => p.Id == id);
        if (product == null)
            return NotFound($"Product with id {id} not found.");

        product.Name = updated.Name;
        product.Price = updated.Price;
        product.Category = updated.Category;

        return NoContent(); // 204 — success, nothing to return
    }

    // DELETE api/products/1
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var product = _products.Find(p => p.Id == id);
        if (product == null)
            return NotFound($"Product with id {id} not found.");

        _products.Remove(product);
        return NoContent(); // 204
    }
}