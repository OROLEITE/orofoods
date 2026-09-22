using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Orofoods.Web.Authorization;
using Orofoods.Web.Controllers;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Services.Storage;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Controllers;

public sealed class ProductMediaControllerTests
{
    [Fact]
    public async Task Anonymous_active_product_returns_stream_and_public_cache()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = new Product { Sku = "ACT-01", Name = "Ativo", Brand = "Orofoods", IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var image = new ProductImage { ProductId = product.Id, Url = "/uploads/products/image.jpg" };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var storage = CreateStorage(new ProductImageReadResult(new MemoryStream([1, 2]), "image/jpeg", "image.jpg"));
        var controller = CreateController(db, storage.Object, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.Get(image.Id, CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.Equal("public, max-age=300, must-revalidate", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Anonymous_inactive_product_returns_not_found()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = new Product { Sku = "OLD-01", Name = "Inativo", Brand = "Orofoods", IsActive = false };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var image = new ProductImage { ProductId = product.Id, Url = "/uploads/products/image.jpg" };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var storage = CreateStorage(new ProductImageReadResult(new MemoryStream([1]), "image/jpeg", "image.jpg"));
        var controller = CreateController(db, storage.Object, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.Get(image.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        storage.Verify(item => item.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Administrator_can_read_inactive_product_with_private_cache()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = new Product { Sku = "OLD-02", Name = "Inativo", Brand = "Orofoods", IsActive = false };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var image = new ProductImage { ProductId = product.Id, Url = "/uploads/products/image.png" };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var storage = CreateStorage(new ProductImageReadResult(new MemoryStream([1]), "image/png", "image.png"));
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "admin"), new Claim(ClaimTypes.Role, "Administrador")], "test"));
        var controller = CreateController(db, storage.Object, user);

        var result = await controller.Get(image.Id, CancellationToken.None);

        Assert.IsType<FileStreamResult>(result);
        Assert.Equal("private, max-age=300, must-revalidate", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Approved_customer_can_read_active_product_with_private_cache()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = new Product { Sku = "ACT-02", Name = "Ativo", Brand = "Orofoods", IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var image = new ProductImage { ProductId = product.Id, Url = "/uploads/products/image.webp" };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var storage = CreateStorage(new ProductImageReadResult(new MemoryStream([1]), "image/webp", "image.webp"));
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(item => item.AuthorizeAsync(
            It.IsAny<ClaimsPrincipal>(),
            It.IsAny<object?>(),
            OrofoodsPolicies.ApprovedCustomer))
            .ReturnsAsync(AuthorizationResult.Success());
        var controller = CreateController(db, storage.Object, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "customer")], "test")), authorization.Object);

        var result = await controller.Get(image.Id, CancellationToken.None);

        Assert.IsType<FileStreamResult>(result);
        Assert.Equal("private, max-age=300, must-revalidate", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Unapproved_authenticated_user_cannot_read_an_active_product()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = new Product { Sku = "ACT-04", Name = "Ativo", Brand = "Orofoods", IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var image = new ProductImage { ProductId = product.Id, Url = "/uploads/products/image.jpg" };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var storage = CreateStorage(new ProductImageReadResult(new MemoryStream([1]), "image/jpeg", "image.jpg"));
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(item => item.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                OrofoodsPolicies.ApprovedCustomer))
            .ReturnsAsync(AuthorizationResult.Failed());
        var controller = CreateController(db, storage.Object, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "pending")], "test")), authorization.Object);

        var result = await controller.Get(image.Id, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        storage.Verify(item => item.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_blob_reference_returns_not_found()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = new Product { Sku = "ACT-03", Name = "Ativo", Brand = "Orofoods", IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var image = new ProductImage { ProductId = product.Id, Url = "invalid-reference" };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var storage = CreateStorage(null);
        var controller = CreateController(db, storage.Object, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.Get(image.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Storage_failure_returns_sanitized_service_unavailable_response()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var product = new Product { Sku = "ACT-05", Name = "Ativo", Brand = "Orofoods", IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var image = new ProductImage { ProductId = product.Id, Url = "/uploads/products/image.jpg" };
        db.ProductImages.Add(image);
        await db.SaveChangesAsync();

        var storage = new Mock<IProductImageStorage>();
        storage.Setup(item => item.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("internal storage detail"));
        var controller = CreateController(db, storage.Object, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.Get(image.Id, CancellationToken.None);

        var response = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public void Product_image_consumers_use_the_application_route_instead_of_storage_urls()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var consumers = new[]
        {
            "Orofoods.Web/Views/Home/Index.cshtml",
            "Orofoods.Web/Views/Home/Products.cshtml",
            "Orofoods.Web/Views/Home/Product.cshtml",
            "Orofoods.Web/Views/Portal/Catalog.cshtml",
            "Orofoods.Web/Views/Portal/Product.cshtml",
            "Orofoods.Web/Areas/Admin/Views/Products/Index.cshtml",
            "Orofoods.Web/Areas/Admin/Views/Products/Edit.cshtml"
        };

        foreach (var relativePath in consumers)
        {
            var source = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.Contains("ProductMedia", source);
            Assert.DoesNotContain("image.Url", source, StringComparison.Ordinal);
            Assert.DoesNotContain("primaryImage.Url", source, StringComparison.Ordinal);
            Assert.DoesNotContain("blob.core.windows.net", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sig=", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static Mock<IProductImageStorage> CreateStorage(ProductImageReadResult? result)
    {
        var storage = new Mock<IProductImageStorage>();
        storage.Setup(item => item.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
        return storage;
    }

    private static ProductMediaController CreateController(
        Orofoods.Web.Data.ApplicationDbContext db,
        IProductImageStorage storage,
        ClaimsPrincipal user,
        IAuthorizationService? authorization = null)
    {
        var controller = new ProductMediaController(
            db,
            storage,
            authorization ?? Mock.Of<IAuthorizationService>(),
            NullLogger<ProductMediaController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }
}
