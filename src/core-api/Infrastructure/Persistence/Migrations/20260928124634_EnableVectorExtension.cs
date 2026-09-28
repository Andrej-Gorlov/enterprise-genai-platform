using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseGenAI.Core.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableVectorExtension : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS vector;");
        }
    }
}
