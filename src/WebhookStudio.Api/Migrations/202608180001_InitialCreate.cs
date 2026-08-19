using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WebhookStudio.Api.Migrations;

[DbContext(typeof(StudioDbContext))]
[Migration("202608180001_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS "Endpoints" (
  "Id" TEXT NOT NULL CONSTRAINT "PK_Endpoints" PRIMARY KEY,
  "Name" TEXT NOT NULL, "Slug" TEXT NOT NULL, "CreatedAtUtc" TEXT NOT NULL,
  "ResponseStatusCode" INTEGER NOT NULL DEFAULT 200, "ResponseContentType" TEXT NOT NULL DEFAULT 'application/json',
  "ResponseBody" TEXT NOT NULL DEFAULT '{"received":true}', "ResponseDelayMs" INTEGER NOT NULL DEFAULT 0, "RetentionLimit" INTEGER NOT NULL DEFAULT 500
);
CREATE TABLE IF NOT EXISTS "CapturedRequests" (
  "Id" TEXT NOT NULL CONSTRAINT "PK_CapturedRequests" PRIMARY KEY, "EndpointId" TEXT NOT NULL,
  "Method" TEXT NOT NULL, "PathAndQuery" TEXT NOT NULL, "HeadersJson" TEXT NOT NULL, "Body" BLOB NOT NULL,
  "ContentType" TEXT NULL, "RemoteIp" TEXT NULL, "ReceivedAtUtc" TEXT NOT NULL, "BodySize" INTEGER NOT NULL,
  "BodyText" TEXT NULL, "ResponseStatusCode" INTEGER NOT NULL DEFAULT 200,
  CONSTRAINT "FK_CapturedRequests_Endpoints_EndpointId" FOREIGN KEY ("EndpointId") REFERENCES "Endpoints" ("Id") ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS "ReplayAttempts" (
  "Id" TEXT NOT NULL CONSTRAINT "PK_ReplayAttempts" PRIMARY KEY, "CapturedRequestId" TEXT NOT NULL,
  "TargetUrl" TEXT NOT NULL, "StatusCode" INTEGER NULL, "DurationMs" INTEGER NOT NULL, "Succeeded" INTEGER NOT NULL,
  "Error" TEXT NULL, "CreatedAtUtc" TEXT NOT NULL,
  CONSTRAINT "FK_ReplayAttempts_CapturedRequests_CapturedRequestId" FOREIGN KEY ("CapturedRequestId") REFERENCES "CapturedRequests" ("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Endpoints_Slug" ON "Endpoints" ("Slug");
CREATE INDEX IF NOT EXISTS "IX_CapturedRequests_EndpointId_ReceivedAtUtc" ON "CapturedRequests" ("EndpointId", "ReceivedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_CapturedRequests_EndpointId_Method_ReceivedAtUtc" ON "CapturedRequests" ("EndpointId", "Method", "ReceivedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_ReplayAttempts_CapturedRequestId" ON "ReplayAttempts" ("CapturedRequestId");
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ReplayAttempts"); migrationBuilder.DropTable("CapturedRequests"); migrationBuilder.DropTable("Endpoints");
    }
}
