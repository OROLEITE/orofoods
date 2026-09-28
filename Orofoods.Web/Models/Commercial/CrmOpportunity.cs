using System.ComponentModel.DataAnnotations;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Orders;

namespace Orofoods.Web.Models.Commercial;

public enum CrmOpportunityType { Repurchase, NewProduct, CrossSell, CustomerRecovery, Proposal, Other }
public enum CrmOpportunityStage { Open, Contacted, Proposal, Negotiation, Won, Lost, Cancelled }
public enum CrmOpportunitySource { Manual, RepurchaseAlert, NoPurchaseAlert, FirstContact, OrderFollowUp }
public enum CrmOpportunityEventType { Created, StageChanged, ProposalSent, Won, Lost, OrderRelated }

public class CrmOpportunity
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string? AssignedUserId { get; set; }
    public ApplicationUser? AssignedUser { get; set; }
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public int? RelatedOrderId { get; set; }
    public Order? RelatedOrder { get; set; }
    [MaxLength(160)] public string Title { get; set; } = "";
    public CrmOpportunityType Type { get; set; }
    public CrmOpportunityStage Stage { get; set; } = CrmOpportunityStage.Open;
    public CrmOpportunitySource Source { get; set; } = CrmOpportunitySource.Manual;
    public decimal? EstimatedValue { get; set; }
    public DateTime? ExpectedCloseAt { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    [MaxLength(80)] public string? LostReason { get; set; }
    [MaxLength(500)] public string? Outcome { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public List<CrmOpportunityHistory> History { get; set; } = [];
}

public class CrmOpportunityHistory
{
    public int Id { get; set; }
    public int OpportunityId { get; set; }
    public CrmOpportunity Opportunity { get; set; } = null!;
    public string? ChangedByUserId { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }
    public CrmOpportunityEventType EventType { get; set; }
    public CrmOpportunityStage? FromStage { get; set; }
    public CrmOpportunityStage? ToStage { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
