using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionalPricingTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TASK-038: SubscriptionTier.Premium was renamed to Pro (a second paid
            // tier, Studio, was added alongside it, and "Premium" stopped being a
            // clear name once it wasn't the only paid option). The column is a
            // plain string conversion, so any existing "Premium" rows need a data
            // fix, not just a schema change — there's no shape difference here for
            // EF to generate on its own.
            migrationBuilder.Sql("UPDATE workspaces SET subscription_tier = 'Pro' WHERE subscription_tier = 'Premium';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE workspaces SET subscription_tier = 'Premium' WHERE subscription_tier = 'Pro';");
        }
    }
}
