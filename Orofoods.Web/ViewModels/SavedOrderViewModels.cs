using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Services.Orders;

namespace Orofoods.Web.ViewModels;

public sealed class SavedOrderEditViewModel
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public List<SavedOrderLine> Items { get; set; } = [];
}
