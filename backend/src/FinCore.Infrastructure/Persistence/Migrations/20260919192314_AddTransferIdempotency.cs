using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, collation: "C"),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsePayload = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.CheckConstraint("CK_IdempotencyRecords_Lifecycle", "(\"Status\" = 'Processing' AND \"TransactionId\" IS NULL AND \"ResponsePayload\" IS NULL AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Completed' AND \"TransactionId\" IS NOT NULL AND \"ResponsePayload\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_LedgerTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_TransactionId",
                table: "IdempotencyRecords",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRecords_Owner_Operation_Key",
                table: "IdempotencyRecords",
                columns: new[] { "OwnerId", "Operation", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");
        }
    }
}
