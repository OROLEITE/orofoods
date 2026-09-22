using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Authorization;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Storage;

namespace Orofoods.Web.Controllers;

[Route("media/products")]
public sealed class ProductMediaController(
    ApplicationDbContext db,
    IProductImageStorage imageStorage,
    IAuthorizationService authorizationService,
    ILogger<ProductMediaController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet("{imageId:int}", Name = "ProductMedia")]
    public async Task<IActionResult> Get(int imageId, CancellationToken cancellationToken)
    {
        var image = await db.ProductImages
            .AsNoTracking()
            .Include(item => item.Product)
            .SingleOrDefaultAsync(item => item.Id == imageId, cancellationToken);
        if (image?.Product is null)
        {
            return NotFound();
        }

        var product = image.Product;
        var isAdministrator = User.Identity?.IsAuthenticated == true && User.IsInRole("Administrador");
        if (isAdministrator)
        {
            Response.Headers.CacheControl = "private, max-age=300, must-revalidate";
        }
        else if (User.Identity?.IsAuthenticated == true)
        {
            var authorized = await authorizationService.AuthorizeAsync(User, OrofoodsPolicies.ApprovedCustomer);
            if (!authorized.Succeeded)
            {
                return Forbid();
            }

            if (!product.IsActive)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, max-age=300, must-revalidate";
        }
        else
        {
            if (!product.IsActive)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "public, max-age=300, must-revalidate";
        }

        try
        {
            var result = await imageStorage.OpenReadAsync(image.Url, cancellationToken);
            return result is null ? NotFound() : File(result.Content, result.ContentType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogWarning("Product image storage failed for image {ImageId}", imageId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}