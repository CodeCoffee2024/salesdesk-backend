using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollapseStudioTierIntoPro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_free_trial",
                table: "workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The pricing matrix collapsed from three tiers (Free/Pro/Studio) down
            // to two (Free/Pro — "Full Access" in every user-facing label); Studio's
            // features folded into Pro rather than disappearing. SubscriptionTier no
            // longer has a Studio member, so any row still carrying that string
            // value (stored via HasConversion<string>()) would fail to deserialize
            // the moment the app reads it — reassign every one of them to Pro, the
            // tier its features now live under, rather than downgrading them to Free.
            migrationBuilder.Sql("UPDATE workspaces SET subscription_tier = 'Pro' WHERE subscription_tier = 'Studio';");
            migrationBuilder.Sql("UPDATE g_cash_payment_submissions SET tier = 'Pro' WHERE tier = 'Studio';");
            migrationBuilder.Sql("UPDATE subscription_upgrade_requests SET tier = 'Pro' WHERE tier = 'Studio';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Studio->Pro reassignment above is intentionally not reversed:
            // there's no way to tell a row that was genuinely Studio apart from one
            // that was always Pro once both read back as "Pro", and reintroducing
            // the Studio enum member is a code change this migration can't make.
            migrationBuilder.DropColumn(
                name: "is_free_trial",
                table: "workspaces");
        }
    }
}
