using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Commercial;

public enum CommercialActivityType
{
    [Display(Name = "Ligar")]
    Call,
    [Display(Name = "WhatsApp")]
    WhatsApp,
    [Display(Name = "Visita")]
    Visit,
    [Display(Name = "Retorno")]
    Return,
    [Display(Name = "Pedido")]
    Order
}

public enum CommercialActivityStatus
{
    [Display(Name = "Agendado")]
    Scheduled,
    [Display(Name = "Em negociação")]
    InProgress,
    [Display(Name = "Concluído")]
    Completed,
    [Display(Name = "Cancelado")]
    Cancelled
}

public class CommercialActivity
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int? SalesRepresentativeId { get; set; }
    public SalesRepresentative? SalesRepresentative { get; set; }
    public string? AssignedUserId { get; set; }
    public ApplicationUser? AssignedUser { get; set; }
    public CommercialActivityType Type { get; set; }
    public CommercialActivityStatus Status { get; set; } = CommercialActivityStatus.Scheduled;
    public DateTime ScheduledAt { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsPriority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}