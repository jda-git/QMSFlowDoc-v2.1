using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentMetrologyAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentDailyQC");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextDueDate",
                table: "MaintenancePlans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresStop",
                table: "MaintenancePlans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresVerification",
                table: "MaintenancePlans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Responsible",
                table: "MaintenancePlans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "MaintenancePlans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToleranceDays",
                table: "MaintenancePlans",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivitiesPerformed",
                table: "MaintenanceEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviationReason",
                table: "MaintenanceEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EndStatus",
                table: "MaintenanceEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDeviation",
                table: "MaintenanceEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "MaintenanceEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PerformedByUserName",
                table: "MaintenanceEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "MaintenanceEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAdditionalAction",
                table: "MaintenanceEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresVerification",
                table: "MaintenanceEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                table: "MaintenanceEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VerificationPerformed",
                table: "MaintenanceEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptanceDate",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Aptitude",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AreaLaboratorio",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CentrifugeHasTimer",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CentrifugeRcfRange",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CentrifugeRotor",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CentrifugeRpmRange",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CentrifugeSpecificMaintenance",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CentrifugeType",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ColdHasAlarm",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ColdHasBackup",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ColdSensorAssociated",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ColdTempMax",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ColdTempMin",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColdTempRecordType",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Criticidad",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CytoAcquisitionConfig",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoAcquisitionSoftware",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoAcquisitionSoftwareVersion",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoAssociatedComputer",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CytoDetectorsCount",
                table: "Equipments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CytoFcsExport",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CytoFcsExportPath",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoFilters",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CytoLasersCount",
                table: "Equipments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoNotes",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoOS",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoOpticalConfig",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoParameters",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoQcConfig",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoType",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CytoWavelengths",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecommissionDate",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMaintenanceContract",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HospitalInventoryNumber",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactAnalysis",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactBiosecurity",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactContinuity",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactPreparation",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactPreservation",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactResult",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImpactTraceability",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntendedUse",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PipetteCriticalUse",
                table: "Equipments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PipetteEmpLimit",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PipetteIntendedUse",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PipetteNominalVolume",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PipetteResolution",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PipetteType",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PipetteVolumeRange",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Restrictions",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServiceEntryDate",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftComputerInstalled",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftFunction",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoftInstallationDate",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftLicense",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftManufacturer",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftName",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftValidationState",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftVersion",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalServiceInfo",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyUntil",
                table: "Equipments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidencePath",
                table: "EquipmentHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EquipmentHistory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentAcceptances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceptionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceptionCondition = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PackagingCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    VisualDamage = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessoriesReceived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReceptionNotes = table.Column<string>(type: "TEXT", nullable: true),
                    ReceptionEvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    InstallationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InstalledBy = table.Column<string>(type: "TEXT", nullable: true),
                    AmbientConditionsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConnectionsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    InitialPowerOnCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    SoftwareCommunicationCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstallationNotes = table.Column<string>(type: "TEXT", nullable: true),
                    InstallationEvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    AcceptanceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CriteriaDefined = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriteriaMet = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AcceptanceOutcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AcceptanceRestrictions = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceEntryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcceptanceEvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    AcceptanceNotes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentAcceptances_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentAlerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentName = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentAlerts_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentCalibrationPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    ControlledMagnitude = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    FrequencyMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    LastCalibrationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextCalibrationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Tolerance = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ProviderOrMethod = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    RequiresCertificate = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentCalibrationPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentCalibrationPlans_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentCalibrationRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    PlanId = table.Column<string>(type: "TEXT", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    PerformedByUserName = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Magnitude = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    ObservedError = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    MaxPermissibleError = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Uncertainty = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    CertificatePath = table.Column<string>(type: "TEXT", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Restrictions = table.Column<string>(type: "TEXT", nullable: true),
                    ImpactAssessmentRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    VolumeNominal = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    VolumeTested = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SystematicError = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RandomError = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AcceptableLimit = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PointsResultsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentCalibrationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentCalibrationRecords_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentDecommissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PreviousStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    NewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresDecontamination = table.Column<bool>(type: "INTEGER", nullable: false),
                    DecontaminationPerformed = table.Column<bool>(type: "INTEGER", nullable: false),
                    DecontaminationEvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    Destination = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ValidatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    ValidatedByUserName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentDecommissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentDecommissions_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentFunctionalQC",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    PerformedByUserName = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    LotNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ParametersEvaluated = table.Column<string>(type: "TEXT", nullable: true),
                    AcceptanceCriteria = table.Column<string>(type: "TEXT", nullable: true),
                    ObtainedValues = table.Column<string>(type: "TEXT", nullable: true),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPass = table.Column<bool>(type: "INTEGER", nullable: false),
                    EvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ActionTaken = table.Column<string>(type: "TEXT", nullable: true),
                    EquipmentEndStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentFunctionalQC", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentFunctionalQC_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentImpactAssessments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentId = table.Column<string>(type: "TEXT", nullable: true),
                    RepairId = table.Column<string>(type: "TEXT", nullable: true),
                    LastConformingVerificationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProbableStartDateOfProblem = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PotentiallyAffectedPeriod = table.Column<string>(type: "TEXT", nullable: true),
                    ImpactType = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    RequiresExternalReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExternalNCId = table.Column<string>(type: "TEXT", nullable: true),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Justification = table.Column<string>(type: "TEXT", nullable: false),
                    EndStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentImpactAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentImpactAssessments_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentIncidents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IncidentType = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ImmediateStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ImmediateAction = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresRemoval = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresRepair = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresImpactAssessment = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresNotification = table.Column<bool>(type: "INTEGER", nullable: false),
                    EvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    IncidentStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Conclusion = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentIncidents_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentIndicatorSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IndicatorKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentIndicatorSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentRepairRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    DetectionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProblemDescription = table.Column<string>(type: "TEXT", nullable: false),
                    DetectedDuring = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EquipmentRemovedFromService = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemovedFromServiceDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TechnicalServiceNotified = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InterventionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InterventionDescription = table.Column<string>(type: "TEXT", nullable: true),
                    PartsReplaced = table.Column<string>(type: "TEXT", nullable: true),
                    ConfigurationModified = table.Column<bool>(type: "INTEGER", nullable: false),
                    SoftwareUpdated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    VerificationRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    VerificationPerformed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReactivationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    PerformedBy = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRepairRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentRepairRecords_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OldStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    NewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    EvidencePath = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentStatusHistories_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentAcceptances_EquipmentId",
                table: "EquipmentAcceptances",
                column: "EquipmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentAlerts_EquipmentId",
                table: "EquipmentAlerts",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentCalibrationPlans_EquipmentId",
                table: "EquipmentCalibrationPlans",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentCalibrationRecords_EquipmentId",
                table: "EquipmentCalibrationRecords",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDecommissions_EquipmentId",
                table: "EquipmentDecommissions",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFunctionalQC_EquipmentId_PerformedAt",
                table: "EquipmentFunctionalQC",
                columns: new[] { "EquipmentId", "PerformedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentImpactAssessments_EquipmentId",
                table: "EquipmentImpactAssessments",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentIncidents_EquipmentId",
                table: "EquipmentIncidents",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRepairRecords_EquipmentId",
                table: "EquipmentRepairRecords",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStatusHistories_EquipmentId",
                table: "EquipmentStatusHistories",
                column: "EquipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentAcceptances");

            migrationBuilder.DropTable(
                name: "EquipmentAlerts");

            migrationBuilder.DropTable(
                name: "EquipmentCalibrationPlans");

            migrationBuilder.DropTable(
                name: "EquipmentCalibrationRecords");

            migrationBuilder.DropTable(
                name: "EquipmentDecommissions");

            migrationBuilder.DropTable(
                name: "EquipmentFunctionalQC");

            migrationBuilder.DropTable(
                name: "EquipmentImpactAssessments");

            migrationBuilder.DropTable(
                name: "EquipmentIncidents");

            migrationBuilder.DropTable(
                name: "EquipmentIndicatorSnapshots");

            migrationBuilder.DropTable(
                name: "EquipmentRepairRecords");

            migrationBuilder.DropTable(
                name: "EquipmentStatusHistories");

            migrationBuilder.DropColumn(
                name: "NextDueDate",
                table: "MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "RequiresStop",
                table: "MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "RequiresVerification",
                table: "MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "Responsible",
                table: "MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ToleranceDays",
                table: "MaintenancePlans");

            migrationBuilder.DropColumn(
                name: "ActivitiesPerformed",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "DeviationReason",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "EndStatus",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "HasDeviation",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "PerformedByUserName",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "RequiresAdditionalAction",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "RequiresVerification",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "VerificationPerformed",
                table: "MaintenanceEvents");

            migrationBuilder.DropColumn(
                name: "AcceptanceDate",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "Aptitude",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "AreaLaboratorio",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CentrifugeHasTimer",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CentrifugeRcfRange",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CentrifugeRotor",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CentrifugeRpmRange",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CentrifugeSpecificMaintenance",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CentrifugeType",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ColdHasAlarm",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ColdHasBackup",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ColdSensorAssociated",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ColdTempMax",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ColdTempMin",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ColdTempRecordType",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "Criticidad",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoAcquisitionConfig",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoAcquisitionSoftware",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoAcquisitionSoftwareVersion",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoAssociatedComputer",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoDetectorsCount",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoFcsExport",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoFcsExportPath",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoFilters",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoLasersCount",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoNotes",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoOS",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoOpticalConfig",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoParameters",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoQcConfig",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoType",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "CytoWavelengths",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "DecommissionDate",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "HasMaintenanceContract",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "HospitalInventoryNumber",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ImpactAnalysis",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ImpactBiosecurity",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ImpactContinuity",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ImpactPreparation",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ImpactPreservation",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ImpactResult",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ImpactTraceability",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "IntendedUse",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PipetteCriticalUse",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PipetteEmpLimit",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PipetteIntendedUse",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PipetteNominalVolume",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PipetteResolution",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PipetteType",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PipetteVolumeRange",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "Restrictions",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "ServiceEntryDate",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftComputerInstalled",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftFunction",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftInstallationDate",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftLicense",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftManufacturer",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftName",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftValidationState",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "SoftVersion",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "TechnicalServiceInfo",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "WarrantyUntil",
                table: "Equipments");

            migrationBuilder.DropColumn(
                name: "EvidencePath",
                table: "EquipmentHistory");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EquipmentHistory");

            migrationBuilder.CreateTable(
                name: "EquipmentDailyQC",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: false),
                    IsPass = table.Column<bool>(type: "INTEGER", nullable: false),
                    LotNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentDailyQC", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentDailyQC_Equipments_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDailyQC_EquipmentId_PerformedAt",
                table: "EquipmentDailyQC",
                columns: new[] { "EquipmentId", "PerformedAt" });
        }
    }
}
