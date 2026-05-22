using ManiApi.Data;
using ManiApi.Models;
using ManiApi.DTOs.Products;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Products;

public class VersionService
{
    private readonly AppDbContext _db;

    public VersionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object?> GetContent(int versionId)
{
    var version = await _db.ProductVersions
        .AsNoTracking()
        .Where(v => v.Id == versionId)
        .Select(v => new
        {
            v.Id,
            v.VersionName,
            v.VersionRasejums,
            v.VersionDate,
            v.VersionComment,
            v.ProductId,
            v.ProductionModel
        })
        .FirstOrDefaultAsync();

    if (version is null)
        return null;

    var product = await _db.Products
        .AsNoTracking()
        .Where(p => p.Id == version.ProductId)
        .Select(p => new
        {
            p.ProductName,
            p.ProductCode,
            p.CategoryId
        })
        .FirstOrDefaultAsync();

    if (product is null)
        return null;

    var categoryName = await _db.Categories
        .AsNoTracking()
        .Where(c => c.Id == product.CategoryId && c.IsActive)
        .Select(c => c.CategoryName)
        .FirstOrDefaultAsync();

    return new
    {
        VersionId = version.Id,
        ProductionModel = version.ProductionModel,
        CategoryName = categoryName,
        ProductName = product.ProductName,
        ProductCode = product.ProductCode,
        VersionName = version.VersionName,
        VersionRasejums = version.VersionRasejums,
        VersionDate = version.VersionDate,
        VersionComment = version.VersionComment
    };
}

public async Task<object> GetListSimple()
{
    
    var rows = await _db.Products.AsNoTracking()
        .Where(p => p.IsActive)
        .Select(p => new
        {
            p.Id,
            p.ProductCode,
            p.ProductName,
            CategoryName = _db.Categories
                .Where(c => c.Id == p.CategoryId && c.IsActive)
                .Select(c => c.CategoryName)
                .FirstOrDefault()
        })
        .ToListAsync();

    var result = rows.Select(x => new
    {
        x.Id,
        x.ProductCode,
        x.ProductName,
        x.CategoryName
    });

    return result;
}

public async Task<List<ProductDetailDto>> GetDetailsByVersion(int versionId)
{
    var rows = await _db.ProductTopParts.AsNoTracking()
        .Where(pt => pt.VersionId == versionId && pt.IsActive)
        .Join(_db.TopParts.Where(tp => tp.IsActive),
              pt => pt.TopPartId,
              tp => tp.Id,
              (pt, tp) => new ProductDetailDto
              {
                  TopPartId = pt.TopPartId,
                  TopPartName = tp.TopPartName,
                  TopPartCode = tp.TopPartCode,
                  Stage = tp.Stage,
                  Quantity = pt.QtyPerProduct,
                  ProductToPartId = pt.Id
              })
        .ToListAsync();

    return rows;
}

public async Task<object> Create(CreateProductRequest dto)
        {
            Console.WriteLine($"[CREATE] Name={dto.ProductName}, Code={dto.ProductCode}, Cat={dto.CategoryId}, " +
                              $"VerName={dto.VersionName}, VerRasejums={dto.VersionRasejums}, VerDate={dto.VersionDate}, VerComment={dto.VersionComment}");
            
                if (string.IsNullOrWhiteSpace(dto.ProductName) || string.IsNullOrWhiteSpace(dto.ProductCode))
                    throw new Exception("Nosaukums un kods ir obligāti.");

                var product = new Product
                {
                    ProductName = dto.ProductName,
                    ProductCode = dto.ProductCode,
                    CategoryId = dto.CategoryId,
                    IsActive = true
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                int? versionId = null;

                // Izveidojam versiju, ja ir vismaz viens versijas lauks
                if (!string.IsNullOrWhiteSpace(dto.VersionName)
                    || !string.IsNullOrWhiteSpace(dto.VersionRasejums)
                    || !string.IsNullOrWhiteSpace(dto.VersionDate)
                    || !string.IsNullOrWhiteSpace(dto.VersionComment))
                {
                    var parsedDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                    if (!string.IsNullOrWhiteSpace(dto.VersionDate)
                        && DateOnly.TryParse(dto.VersionDate, out var d))
                    {
                        parsedDate = d;
                    }

                    var ver = new ProductVersion
                    {
                        ProductId = product.Id,
                        VersionName = dto.VersionName ?? "",
                        VersionRasejums = dto.VersionRasejums ?? "",
                        VersionDate = parsedDate,
                        VersionComment = dto.VersionComment ?? "",
                        IsActive = true,

                        ProductionModel = dto.ProductionModel
                    };

                    _db.ProductVersions.Add(ver);
                    await _db.SaveChangesAsync();
                    versionId = ver.Id;
                }

                return new { product.Id, VersionId = versionId };            
        }

}