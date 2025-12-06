using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobSystem.Manager.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QueueType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QueueConnectionString = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QueueName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstanceSize = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UseSpotInstances = table.Column<bool>(type: "bit", nullable: false),
                    ScaleMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinWorkers = table.Column<int>(type: "int", nullable: false),
                    MaxWorkers = table.Column<int>(type: "int", nullable: false),
                    QueueItemTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    QueuePollingIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    GitRepoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    GitBranch = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "main"),
                    S3CodePath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CodeSyncHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GitCommitHash = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeSyncHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeSyncHistory_JobDefinitions_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalTable: "JobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScalingEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PreviousWorkerCount = table.Column<int>(type: "int", nullable: false),
                    NewWorkerCount = table.Column<int>(type: "int", nullable: false),
                    QueueDepth = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScalingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScalingEvents_JobDefinitions_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalTable: "JobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkerInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    JobDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ec2InstanceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PrivateIpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InstanceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSpotInstance = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LaunchedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastHeartbeatAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TerminatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerInstances_JobDefinitions_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalTable: "JobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkerStatusReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkerInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ItemId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProgressPercent = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerStatusReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkerStatusReports_WorkerInstances_WorkerInstanceId",
                        column: x => x.WorkerInstanceId,
                        principalTable: "WorkerInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeSyncHistory_JobDefinitionId",
                table: "CodeSyncHistory",
                column: "JobDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_JobDefinitions_Name",
                table: "JobDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScalingEvents_JobDefinitionId",
                table: "ScalingEvents",
                column: "JobDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerInstances_JobDefinitionId",
                table: "WorkerInstances",
                column: "JobDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerStatusReports_WorkerInstanceId",
                table: "WorkerStatusReports",
                column: "WorkerInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeSyncHistory");

            migrationBuilder.DropTable(
                name: "ScalingEvents");

            migrationBuilder.DropTable(
                name: "WorkerStatusReports");

            migrationBuilder.DropTable(
                name: "WorkerInstances");

            migrationBuilder.DropTable(
                name: "JobDefinitions");
        }
    }
}
