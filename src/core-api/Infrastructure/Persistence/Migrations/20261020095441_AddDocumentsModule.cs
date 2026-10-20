using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseGenAI.Core.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    TemporaryObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    ProcessingVersion = table.Column<int>(type: "integer", nullable: false),
                    ReadyProcessingVersion = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UploadExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.DocumentId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_ObjectKey",
                table: "documents",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_OwnerId",
                table: "documents",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_Status",
                table: "documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_documents_TemporaryObjectKey",
                table: "documents",
                column: "TemporaryObjectKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documents");
        }
    }
}
