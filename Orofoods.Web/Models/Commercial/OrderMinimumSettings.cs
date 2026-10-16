using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Commercial;

public sealed class OrderMinimumSettings
{
    public const int SingletonId = 1;
    public const decimal InitialMinimum = 160m;

    [Key]
    public int Id { get; set; } = SingletonId;

    [Range(typeof(decimal), "0", "999999999")]
    public decimal MinimumAmount { get; set; } = InitialMinimum;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(450)] public string? UpdatedByUserId { get; set; }
}
