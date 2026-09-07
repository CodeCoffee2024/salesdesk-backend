using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only fixup, no schema change: Workspace.DocumentQuota's default at
    /// creation changed from 100 to null (see Workspace.cs), because
    /// CreateDocumentCommand now enforces `DocumentQuota ?? tier limit` instead of
    /// ignoring DocumentQuota entirely — leaving the old 100 default in place would
    /// have silently raised every existing Free-tier workspace's real cap from 5 to
    /// 100. SetWorkspaceQuotaCommand (the only way DocumentQuota was ever written)
    /// was a no-op until this fix, so every workspace still sitting at exactly 100 is
    /// definitionally the untouched default, not a deliberate admin choice — safe to
    /// reset to null (defer to tier limit) uniformly. See docs/feature/billing-plan-limits.md.
    /// </summary>
    public partial class ResetDefaultDocumentQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE workspaces SET document_quota = NULL WHERE document_quota = 100;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: workspaces that legitimately had a null override before
            // this migration (as opposed to an untouched 100 default) can't be told
            // apart from the ones this Up() just reset, so Down() can't restore 100
            // without risking overwriting an intentional null on some other workspace.
        }
    }
}
