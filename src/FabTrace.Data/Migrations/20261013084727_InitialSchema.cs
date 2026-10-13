using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FabTrace.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lots",
                columns: table => new
                {
                    LotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LotNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.LotId);
                });

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    MachineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MachineType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Line = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApiKeyHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.MachineId);
                });

            migrationBuilder.CreateTable(
                name: "ProcessSteps",
                columns: table => new
                {
                    ProcessStepId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSteps", x => x.ProcessStepId);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.RecipeId);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.ShiftId);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    UnitId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SerialNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.UnitId);
                    table.ForeignKey(
                        name: "FK_Units_Lots_LotId",
                        column: x => x.LotId,
                        principalTable: "Lots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Alarms",
                columns: table => new
                {
                    AlarmId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    AlarmCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<byte>(type: "tinyint", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alarms", x => x.AlarmId);
                    table.CheckConstraint("CK_Alarms_ClearedAfterRaised", "[ClearedAt] IS NULL OR [ClearedAt] >= [RaisedAt]");
                    table.CheckConstraint("CK_Alarms_Severity", "[Severity] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Alarms_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "MachineId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineStateEvents",
                columns: table => new
                {
                    MachineStateEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<byte>(type: "tinyint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineStateEvents", x => x.MachineStateEventId);
                    table.CheckConstraint("CK_MachineStateEvents_State", "[State] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_MachineStateEvents_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "MachineId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeParameters",
                columns: table => new
                {
                    RecipeParameterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Target = table.Column<double>(type: "float", nullable: false),
                    LowerLimit = table.Column<double>(type: "float", nullable: true),
                    UpperLimit = table.Column<double>(type: "float", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeParameters", x => x.RecipeParameterId);
                    table.ForeignKey(
                        name: "FK_RecipeParameters_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Operators",
                columns: table => new
                {
                    OperatorId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BadgeNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operators", x => x.OperatorId);
                    table.ForeignKey(
                        name: "FK_Operators_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionResults",
                columns: table => new
                {
                    InspectionResultId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<long>(type: "bigint", nullable: false),
                    ProcessStepId = table.Column<int>(type: "int", nullable: false),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    OperatorId = table.Column<int>(type: "int", nullable: false),
                    Verdict = table.Column<byte>(type: "tinyint", nullable: false),
                    DefectCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    InspectedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CycleTimeMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionResults", x => x.InspectionResultId);
                    table.CheckConstraint("CK_InspectionResults_CycleTimeMs", "[CycleTimeMs] >= 0");
                    table.CheckConstraint("CK_InspectionResults_Verdict", "[Verdict] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_InspectionResults_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "MachineId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionResults_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "OperatorId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionResults_ProcessSteps_ProcessStepId",
                        column: x => x.ProcessStepId,
                        principalTable: "ProcessSteps",
                        principalColumn: "ProcessStepId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionResults_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "RecipeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionResults_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "UnitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestReadings",
                columns: table => new
                {
                    TestReadingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InspectionResultId = table.Column<long>(type: "bigint", nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Value = table.Column<double>(type: "float", nullable: false),
                    LowerLimit = table.Column<double>(type: "float", nullable: true),
                    UpperLimit = table.Column<double>(type: "float", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestReadings", x => x.TestReadingId);
                    table.ForeignKey(
                        name: "FK_TestReadings_InspectionResults_InspectionResultId",
                        column: x => x.InspectionResultId,
                        principalTable: "InspectionResults",
                        principalColumn: "InspectionResultId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_EventId",
                table: "Alarms",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alarms_MachineId",
                table: "Alarms",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_MachineId",
                table: "InspectionResults",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_OperatorId",
                table: "InspectionResults",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_ProcessStepId",
                table: "InspectionResults",
                column: "ProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_RecipeId",
                table: "InspectionResults",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_ResultId",
                table: "InspectionResults",
                column: "ResultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionResults_UnitId",
                table: "InspectionResults",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_LotNumber",
                table: "Lots",
                column: "LotNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_ApiKeyHash",
                table: "Machines",
                column: "ApiKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Code",
                table: "Machines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_EventId",
                table: "MachineStateEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineStateEvents_MachineId",
                table: "MachineStateEvents",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Operators_BadgeNo",
                table: "Operators",
                column: "BadgeNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operators_ShiftId",
                table: "Operators",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_Code",
                table: "ProcessSteps",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeParameters_RecipeId_Name",
                table: "RecipeParameters",
                columns: new[] { "RecipeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Code_Version",
                table: "Recipes",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Name",
                table: "Shifts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestReadings_InspectionResultId",
                table: "TestReadings",
                column: "InspectionResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_LotId",
                table: "Units",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_SerialNumber",
                table: "Units",
                column: "SerialNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alarms");

            migrationBuilder.DropTable(
                name: "MachineStateEvents");

            migrationBuilder.DropTable(
                name: "RecipeParameters");

            migrationBuilder.DropTable(
                name: "TestReadings");

            migrationBuilder.DropTable(
                name: "InspectionResults");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropTable(
                name: "Operators");

            migrationBuilder.DropTable(
                name: "ProcessSteps");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "Lots");
        }
    }
}