using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orofoods.Web.Data.MigrationsPostgreSql;

public partial class AddPointPaymentAuditEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PointPaymentAuditEvents",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrderId = table.Column<int>(type: "integer", nullable: false),
                PaymentId = table.Column<int>(type: "integer", nullable: false),
                DriverId = table.Column<int>(type: "integer", nullable: true),
                PaymentTerminalId = table.Column<int>(type: "integer", nullable: true),
                AssignmentId = table.Column<int>(type: "integer", nullable: true),
                AdminUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                ExternalOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PreviousStatus = table.Column<int>(type: "integer", nullable: true),
                NewStatus = table.Column<int>(type: "integer", nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                ResultCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ResultSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PointPaymentAuditEvents", x => x.Id);
                table.ForeignKey("FK_PointPaymentAuditEvents_AspNetUsers_AdminUserId", x => x.AdminUserId, "AspNetUsers", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_PointPaymentAuditEvents_DriverPaymentTerminalAssignments_AssignmentId", x => x.AssignmentId, "DriverPaymentTerminalAssignments", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_PointPaymentAuditEvents_Drivers_DriverId", x => x.DriverId, "Drivers", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_PointPaymentAuditEvents_Orders_OrderId", x => x.OrderId, "Orders", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_PointPaymentAuditEvents_PaymentTerminals_PaymentTerminalId", x => x.PaymentTerminalId, "PaymentTerminals", "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_PointPaymentAuditEvents_Payments_PaymentId", x => x.PaymentId, "Payments", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_PointPaymentAuditEvents_AdminUserId", "PointPaymentAuditEvents", "AdminUserId");
        migrationBuilder.CreateIndex("IX_PointPaymentAuditEvents_AssignmentId", "PointPaymentAuditEvents", "AssignmentId");
        migrationBuilder.CreateIndex("IX_PointPaymentAuditEvents_DriverId", "PointPaymentAuditEvents", "DriverId");
        migrationBuilder.CreateIndex("IX_PointPaymentAuditEvents_OrderId_OccurredAt", "PointPaymentAuditEvents", new[] { "OrderId", "OccurredAt" });
        migrationBuilder.CreateIndex("IX_PointPaymentAuditEvents_PaymentId", "PointPaymentAuditEvents", "PaymentId");
        migrationBuilder.CreateIndex("IX_PointPaymentAuditEvents_PaymentTerminalId", "PointPaymentAuditEvents", "PaymentTerminalId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PointPaymentAuditEvents");
    }
}
