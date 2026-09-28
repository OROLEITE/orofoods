using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Models.Commercial;

public enum UserNotificationType { TaskAssigned, AppointmentReminder, OverdueReturn, RepurchaseDue, CustomerNoPurchase, FirstContact, OpportunityAssigned, OpportunityDue, OpportunityWon, OrderCreated, WhatsAppMessageReceived }

public class UserNotification
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? OpportunityId { get; set; }
    public CrmOpportunity? Opportunity { get; set; }
    public int? ActivityId { get; set; }
    public CommercialActivity? Activity { get; set; }
    public UserNotificationType Type { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(500)] public string Message { get; set; } = "";
    [MaxLength(500)] public string? ActionUrl { get; set; }
    [MaxLength(200)] public string? DeduplicationKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}