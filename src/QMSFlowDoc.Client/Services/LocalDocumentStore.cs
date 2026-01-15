using Microsoft.Data.Sqlite;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.DTOs; // Added DTOs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

/// <summary>
/// Local document storage using SQLite (no PostgreSQL dependency)
/// </summary>
public class LocalDocumentStore
{
    private readonly string _dbPath;
    private readonly NetworkConfigStore _networkConfig;

    public LocalDocumentStore(NetworkConfigStore networkConfig)
    {
        _networkConfig = networkConfig;
        
        // Try to use Workspace path, fallback to AppData
        // Use Task.Run to avoid potential deadlock on UI thread
        string basePath;
        try
        {
            var config = Task.Run(() => networkConfig.LoadAsync()).Result;
            basePath = config.LocalBasePath;
        }
        catch
        {
            basePath = null;
        }
        
        if (string.IsNullOrWhiteSpace(basePath))
        {
            // Fallback to AppData if Workspace not configured
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "QMSFlowDoc");
        }
        
        var dbFolder = Path.Combine(basePath, "Base_datos");
        Directory.CreateDirectory(dbFolder);
        _dbPath = Path.Combine(dbFolder, "qmsflowdoc.db");
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Folders (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ParentFolderId TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Documents (
                Id TEXT PRIMARY KEY,
                DocCode TEXT NOT NULL UNIQUE,
                Title TEXT NOT NULL,
                DocumentTypeId TEXT,
                FolderId TEXT,
                Area TEXT,
                Process TEXT,
                OwnerUserId TEXT,
                Status TEXT NOT NULL DEFAULT 'DRAFT',
                ReviewIntervalMonths INTEGER,
                NextReviewDue TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS DocumentVersions (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NOT NULL,
                VersionMajor INTEGER NOT NULL,
                VersionMinor INTEGER NOT NULL,
                VersionLabel TEXT NOT NULL,
                ChangeSummary TEXT,
                CreatedByUserId TEXT,
                CreatedAt TEXT NOT NULL,
                EffectiveFrom TEXT,
                LocalFilePath TEXT,
                FileName TEXT,
                MimeType TEXT,
                Sha256 TEXT,
                IsCurrent INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS DocumentTypes (
                Id TEXT PRIMARY KEY,
                TypeCode TEXT,
                Name TEXT NOT NULL,
                Description TEXT
            );

            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT NOT NULL,
                UserId TEXT,
                UserName TEXT,
                Action TEXT NOT NULL,
                EntityType TEXT,
                EntityId TEXT,
                Details TEXT,
                MachineName TEXT
            );

            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                FullName TEXT,
                Email TEXT,
                Role TEXT NOT NULL DEFAULT 'Usuario',
                IsActive INTEGER NOT NULL DEFAULT 1,
                FailedLoginAttempts INTEGER DEFAULT 0,
                IsLocked INTEGER DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                LastLoginAt TEXT
            );

            CREATE TABLE IF NOT EXISTS StaffProfiles (
                Id TEXT PRIMARY KEY,
                UserId TEXT,
                FullName TEXT, -- Computed or cached
                Position TEXT,
                Department TEXT,
                HireDate TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                Role TEXT DEFAULT 'Usuario',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS Suppliers (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ContactName TEXT,
                Email TEXT,
                Phone TEXT,
                Notes TEXT
            );

            CREATE TABLE IF NOT EXISTS StorageLocations (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT
            );

            CREATE TABLE IF NOT EXISTS ReagentTypes (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT
            );

            CREATE TABLE IF NOT EXISTS Reagents (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Manufacturer TEXT,
                SupplierId TEXT,
                ManufacturerCode TEXT,
                InternalCode TEXT,
                Fluorescence TEXT,
                ReagentType TEXT,
                Reference TEXT,
                Classification TEXT,
                StorageConditions TEXT,
                DefaultLocationId TEXT,
                OpenShelfLifeDays INTEGER,
                Status INTEGER NOT NULL DEFAULT 0,
                MinStock REAL,
                TargetStock REAL,
                ReorderQty REAL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS ReagentLots (
                Id TEXT PRIMARY KEY,
                ReagentId TEXT NOT NULL,
                LotNumber TEXT NOT NULL,
                ExpiryDate TEXT NOT NULL,
                ReceivedDate TEXT NOT NULL,
                ReceivedQty REAL NOT NULL,
                AvailableQty REAL NOT NULL,
                LocationId TEXT,
                Status INTEGER NOT NULL DEFAULT 1,
                OpenedDate TEXT,
                OpenExpiryDate TEXT,
                PanelId TEXT,
                ReleaseByUserId TEXT
            );

            CREATE TABLE IF NOT EXISTS InventoryMovements (
                Id TEXT PRIMARY KEY,
                ReagentId TEXT NOT NULL,
                LotId TEXT,
                Type INTEGER NOT NULL,
                Qty REAL NOT NULL,
                Date TEXT NOT NULL,
                UserId TEXT NOT NULL,
                Notes TEXT
            );

            CREATE TABLE IF NOT EXISTS Equipments (
                Id TEXT PRIMARY KEY,
                AssetTag TEXT,
                Name TEXT NOT NULL,
                Manufacturer TEXT,
                Model TEXT,
                SerialNumber TEXT,
                SoftwareVersion TEXT,
                FirmwareVersion TEXT,
                Location TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                InstalledAt TEXT,
                Notes TEXT
            );

            CREATE TABLE IF NOT EXISTS MaintenanceEvents (
                Id TEXT PRIMARY KEY,
                EquipmentId TEXT NOT NULL,
                PlanId TEXT,
                PerformedAt TEXT NOT NULL,
                PerformedByUserId TEXT,
                EventType INTEGER NOT NULL,
                Outcome TEXT,
                Notes TEXT,
                HasIssues INTEGER DEFAULT 0,
                NextMaintenanceMonth INTEGER,
                NextMaintenanceYear INTEGER
            );
        ";

        // Execute first batch (including MaintenanceEvents)
        using (var c = new SqliteCommand(createTableSql, connection)) { await c.ExecuteNonQueryAsync(); }

        // Migration for existing databases
        try { using var c = new SqliteCommand("ALTER TABLE MaintenanceEvents ADD COLUMN HasIssues INTEGER DEFAULT 0", connection); await c.ExecuteNonQueryAsync(); } catch {}
        try { using var c = new SqliteCommand("ALTER TABLE MaintenanceEvents ADD COLUMN NextMaintenanceMonth INTEGER", connection); await c.ExecuteNonQueryAsync(); } catch {}
        try { using var c = new SqliteCommand("ALTER TABLE MaintenanceEvents ADD COLUMN NextMaintenanceYear INTEGER", connection); await c.ExecuteNonQueryAsync(); } catch {}

        // Start second batch
        createTableSql = @"
            CREATE TABLE IF NOT EXISTS MaintenancePlans (
                Id TEXT PRIMARY KEY,
                EquipmentId TEXT NOT NULL,
                PlanName TEXT NOT NULL,
                FrequencyDays INTEGER NOT NULL,
                ChecklistJson TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS EquipmentDailyQC (
                Id TEXT PRIMARY KEY,
                EquipmentId TEXT NOT NULL,
                LotNumber TEXT,
                IsPass INTEGER NOT NULL,
                Notes TEXT,
                PerformedByUserId TEXT NOT NULL,
                PerformedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Incidents (
                Id TEXT PRIMARY KEY,
                DetectedAt TEXT NOT NULL,
                DetectedByUserId TEXT,
                Title TEXT NOT NULL,
                Description TEXT,
                Severity INTEGER NOT NULL,
                ImpactPatient INTEGER NOT NULL DEFAULT 0,
                Containment TEXT,
                Origin TEXT,
                RootCauseAnalysis TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                UpdatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS IncidentActions (
                Id TEXT PRIMARY KEY,
                NCId TEXT NOT NULL,
                ActionType INTEGER NOT NULL,
                Description TEXT,
                OwnerUserId TEXT,
                DueDate TEXT,
                CompletedAt TEXT,
                EffectivenessCheck TEXT,
                Status INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS TrainingActivities (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Provider TEXT,
                TrainingTypeId TEXT,
                Modality TEXT,
                StartDate TEXT,
                EndDate TEXT,
                Hours REAL,
                Credits TEXT,
                Description TEXT,
                IsInternal INTEGER,
                InternalDepartment TEXT,
                Status TEXT,
                CreatedByUserId TEXT,
                CreatedAt TEXT,
                UpdatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS StaffTrainings (
                Id TEXT PRIMARY KEY,
                StaffId TEXT NOT NULL,
                TrainingActivityId TEXT,
                Title TEXT, -- For legacy or flat records
                ParticipationRole TEXT,
                Result TEXT,
                Score TEXT,
                CompletionDate TEXT,
                CertificateDocId TEXT,
                Notes TEXT,
                Status TEXT,
                CreatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS CompetencyEvaluations (
                Id TEXT PRIMARY KEY,
                StaffId TEXT NOT NULL,
                CompetencyName TEXT,
                Area TEXT,
                EvaluationDate TEXT,
                ValidUntil TEXT,
                Outcome TEXT,
                Evidence TEXT,
                EvaluatorName TEXT
            );

            CREATE TABLE IF NOT EXISTS Authorizations (
                Id TEXT PRIMARY KEY,
                Code TEXT,
                Name TEXT NOT NULL,
                Description TEXT
            );

            CREATE TABLE IF NOT EXISTS StaffAuthorizations (
                Id TEXT PRIMARY KEY,
                StaffId TEXT NOT NULL,
                TaskName TEXT,
                Description TEXT,
                ValidFrom TEXT,
                ValidUntil TEXT,
                GrantedAt TEXT,
                Status TEXT,
                GrantedByName TEXT
            );

            CREATE TABLE IF NOT EXISTS Risks (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Description TEXT,
                Category TEXT,
                Likelihood INTEGER NOT NULL,
                Impact INTEGER NOT NULL,
                MitigationPlan TEXT,
                OwnerUserId TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS AuditPlans (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                ScheduledDate TEXT NOT NULL,
                Scope TEXT,
                LeadAuditor TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                SummaryReport TEXT,
                ReportDocumentId TEXT
            );

            CREATE TABLE IF NOT EXISTS AuditFindings (
                Id TEXT PRIMARY KEY,
                AuditPlanId TEXT NOT NULL,
                Description TEXT,
                IsoRequirement TEXT,
                Type INTEGER NOT NULL,
                RelatedNCId TEXT
            );

            CREATE TABLE IF NOT EXISTS ManagementReviews (
                Id TEXT PRIMARY KEY,
                ReviewDate TEXT NOT NULL,
                Participants TEXT,
                Agenda TEXT,
                Summary TEXT,
                Actions TEXT,
                MinutesDocumentId TEXT
            );

            CREATE TABLE IF NOT EXISTS EQAPrograms (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Provider TEXT,
                CycleFrequency TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                Notes TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT
            );

            CREATE TABLE IF NOT EXISTS EQAResults (
                Id TEXT PRIMARY KEY,
                ProgramId TEXT NOT NULL,
                CycleIdentifier TEXT NOT NULL,
                ReceiptDate TEXT,
                ProcessingDate TEXT,
                SubmissionDate TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                Score REAL,
                Performance INTEGER NOT NULL DEFAULT 3,
                Notes TEXT,
                EvidenceDocId TEXT,
                ReviewerUserId TEXT,
                ReviewDate TEXT,
                FOREIGN KEY (ProgramId) REFERENCES EQAPrograms(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_documents_code ON Documents(DocCode);
            CREATE INDEX IF NOT EXISTS idx_versions_document ON DocumentVersions(DocumentId);
            CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON AuditLogs(Timestamp);
            CREATE INDEX IF NOT EXISTS idx_users_username ON Users(Username);
            CREATE INDEX IF NOT EXISTS idx_staff_name ON StaffProfiles(FullName);
        ";

        using var command = new SqliteCommand(createTableSql, connection);
        await command.ExecuteNonQueryAsync();

        // Migration: Add columns to existing tables if missing
        try
        {
            using var colCmd = new SqliteCommand("ALTER TABLE Documents ADD COLUMN Area TEXT", connection);
            await colCmd.ExecuteNonQueryAsync();
        }
        catch { /* Column already exists */ }

        try
        {
            using var colCmd = new SqliteCommand("ALTER TABLE Documents ADD COLUMN Process TEXT", connection);
            await colCmd.ExecuteNonQueryAsync();
        }
        catch { /* Column already exists */ }

        try
        {
            using var colCmd = new SqliteCommand("ALTER TABLE StaffProfiles ADD COLUMN UserId TEXT", connection);
            await colCmd.ExecuteNonQueryAsync();
        }
        catch { /* Column already exists */ }

        // Seed Document Types
        await SeedDocumentTypesAsync(connection);
        
        // Seed default admin user if no users exist
        await SeedDefaultUserAsync(connection);
    }

    private async Task SeedDefaultUserAsync(SqliteConnection connection)
    {
        var countSql = "SELECT COUNT(*) FROM Users";
        using var countCmd = new SqliteCommand(countSql, connection);
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        
        if (count == 0)
        {
            // Create default admin user: admin / Admin123!
            var adminId = Guid.NewGuid().ToString();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
            
            var insertSql = @"
                INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                VALUES ($id, $username, $hash, $fullname, $role, 1, $created)";
            using var insertCmd = new SqliteCommand(insertSql, connection);
            insertCmd.Parameters.AddWithValue("$id", adminId);
            insertCmd.Parameters.AddWithValue("$username", "admin");
            insertCmd.Parameters.AddWithValue("$hash", passwordHash);
            insertCmd.Parameters.AddWithValue("$fullname", "Administrador");
            insertCmd.Parameters.AddWithValue("$role", "Administrador");
            insertCmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
            await insertCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<(bool Success, string? UserId, string? FullName, string Role)> ValidateUserAsync(string username, string password)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT Id, PasswordHash, FullName, Role, IsActive, IsLocked FROM Users WHERE Username = $username";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$username", username);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var isActive = reader.GetInt32(4) == 1;
            var isLocked = reader.GetInt32(5) == 1;
            
            if (!isActive || isLocked)
                return (false, null, null, "");
            
            var storedHash = reader.GetString(1);
            if (BCrypt.Net.BCrypt.Verify(password, storedHash))
            {
                var userId = reader.GetString(0);
                var fullName = reader.IsDBNull(2) ? null : reader.GetString(2);
                var role = reader.GetString(3);
                
                // Update last login
                var updateSql = "UPDATE Users SET LastLoginAt = $now WHERE Id = $id";
                using var updateCmd = new SqliteCommand(updateSql, connection);
                updateCmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
                updateCmd.Parameters.AddWithValue("$id", userId);
                await updateCmd.ExecuteNonQueryAsync();
                
                return (true, userId, fullName, role);
            }
        }
        
        return (false, null, null, "");
    }

    public async Task<bool> AnyUsersExistAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT COUNT(*) FROM Users";
        using var cmd = new SqliteCommand(sql, connection);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<Guid> CreateUserAsync(string username, string password, string? fullName, string? email, string role)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        // Check if user exists
        var checkSql = "SELECT Id FROM Users WHERE Username = $username";
        using var checkCmd = new SqliteCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("$username", username);
        var existingIdObj = await checkCmd.ExecuteScalarAsync();

        if (existingIdObj != null)
        {
            var existingId = existingIdObj.ToString();

            // Check if orphan (no StaffProfile)
            var profileSql = "SELECT COUNT(*) FROM StaffProfiles WHERE UserId = $uid";
            using var profileCmd = new SqliteCommand(profileSql, connection);
            profileCmd.Parameters.AddWithValue("$uid", existingId);
            var count = Convert.ToInt32(await profileCmd.ExecuteScalarAsync());

            if (count > 0)
            {
                throw new Exception($"El usuario '{username}' ya existe y tiene ficha activa.");
            }

            // Orphan user found. Reclaim it!
            var passwordHashReclaim = BCrypt.Net.BCrypt.HashPassword(password);
            var updateSql = @"UPDATE Users 
                              SET PasswordHash = $hash, FullName = $fullname, Email = $email, Role = $role, IsActive = 1, CreatedAt = $created
                              WHERE Id = $id";
            using var updateCmd = new SqliteCommand(updateSql, connection);
            updateCmd.Parameters.AddWithValue("$hash", passwordHashReclaim);
            updateCmd.Parameters.AddWithValue("$fullname", fullName ?? "");
            updateCmd.Parameters.AddWithValue("$email", email ?? "");
            updateCmd.Parameters.AddWithValue("$role", role);
            updateCmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
            updateCmd.Parameters.AddWithValue("$id", existingId);
            await updateCmd.ExecuteNonQueryAsync();

            return Guid.Parse(existingId);
        }

        // Create new user
        var id = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var sql = @"INSERT INTO Users (Id, Username, PasswordHash, FullName, Email, Role, IsActive, CreatedAt)
                    VALUES ($id, $username, $hash, $fullname, $email, $role, 1, $created)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.AddWithValue("$hash", passwordHash);
        cmd.Parameters.AddWithValue("$fullname", fullName ?? "");
        cmd.Parameters.AddWithValue("$email", email ?? "");
        cmd.Parameters.AddWithValue("$role", role);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
        
        return id;
    }

    // Staff Profile CRUD methods
    public async Task<IEnumerable<StaffListDto>> GetStaffProfilesAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"
            SELECT s.Id, 
                   COALESCE(u.FullName, s.FullName, 'Sin Nombre') as FullName, 
                   s.Position, 
                   s.Department, 
                   s.IsActive 
            FROM StaffProfiles s
            LEFT JOIN Users u ON s.UserId = u.Id";

        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<StaffListDto>();
        while (await reader.ReadAsync())
        {
            list.Add(new StaffListDto
            {
                Id = Guid.Parse(reader.GetString(0)),
                FullName = reader.IsDBNull(1) ? "Sin Nombre" : reader.GetString(1),
                PositionTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                Department = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActive = reader.GetInt32(4) == 1
            });
        }
        return list;
    }

    public async Task<StaffProfileDetailDto?> GetStaffProfileDetailsAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"
            SELECT s.Id, s.UserId, s.Position, s.Department, s.HireDate, s.IsActive,
                   u.Username, u.FullName, u.Email, u.Role
            FROM StaffProfiles s
            LEFT JOIN Users u ON s.UserId = u.Id
            WHERE s.Id = $id";
            
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new StaffProfileDetailDto(
                Id: Guid.Parse(reader["Id"].ToString()),
                UserId: Guid.Parse(reader["UserId"].ToString()),
                Username: reader["Username"] == DBNull.Value ? "" : reader["Username"].ToString(),
                FullName: reader["FullName"] == DBNull.Value ? "" : reader["FullName"].ToString(),
                Email: reader["Email"] == DBNull.Value ? "" : reader["Email"].ToString(),
                PositionTitle: reader["Position"] == DBNull.Value ? "" : reader["Position"].ToString(),
                Department: reader["Department"] == DBNull.Value ? "" : reader["Department"].ToString(),
                HiredAt: DateTime.TryParse(reader["HireDate"].ToString(), out var hd) ? hd : DateTime.MinValue,
                RoleName: reader["Role"] == DBNull.Value ? "Usuario" : reader["Role"].ToString(),
                IsActive: Convert.ToInt32(reader["IsActive"]) == 1
            );
        }
        return null;
    }

    public async Task<StaffProfile?> GetStaffProfileByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"
            SELECT s.Id, s.UserId, s.Position, s.Department, s.HireDate, s.IsActive,
                   u.Username, u.FullName, u.Email, u.Role
            FROM StaffProfiles s
            LEFT JOIN Users u ON s.UserId = u.Id
            WHERE s.Id = $id";
            
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        StaffProfile? profile = null;
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                profile = new StaffProfile
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    UserId = reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)),
                    PositionTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Department = reader.IsDBNull(3) ? null : reader.GetString(3),
                    HiredAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                    IsActive = reader.GetInt32(5) == 1
                };
                
                // Populate User object if UserId exists
                if (profile.UserId.HasValue)
                {
                    profile.User = new User
                    {
                        Id = profile.UserId.Value,
                        Username = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        FullName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        Email = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Roles = new List<Role> { new Role { RoleName = reader.IsDBNull(9) ? "Usuario" : reader.GetString(9) } }
                    };
                }
            }
        }
        
        if (profile != null)
        {
            // Load Trainings
            var trainingSql = "SELECT Id, Title, Provider, Hours, CompletedAt, Result, Notes FROM StaffTrainings WHERE StaffId = $sid";
            using var trainCmd = new SqliteCommand(trainingSql, connection);
            trainCmd.Parameters.AddWithValue("$sid", profile.Id.ToString());
            using var trainReader = await trainCmd.ExecuteReaderAsync();
            
            profile.Trainings = new List<StaffTraining>();
            while (await trainReader.ReadAsync())
            {
                profile.Trainings.Add(new StaffTraining
                {
                    Id = Guid.Parse(trainReader.GetString(0)),
                    TrainingActivity = new TrainingActivity
                    {
                        Title = trainReader.GetString(1),
                        Provider = trainReader.IsDBNull(2) ? null : trainReader.GetString(2),
                        Hours = trainReader.GetDecimal(3)
                    },
                    CompletionDate = trainReader.IsDBNull(4) ? null : DateTime.Parse(trainReader.GetString(4)),
                    Result = trainReader.IsDBNull(5) ? null : trainReader.GetString(5),
                    Notes = trainReader.IsDBNull(6) ? null : trainReader.GetString(6)
                });
            }
        }
        
        return profile;
    }

    public async Task CreateStaffProfileAsync(StaffProfile profile)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"INSERT INTO StaffProfiles (Id, UserId, FullName, Position, Department, HireDate, IsActive, CreatedAt)
                    VALUES ($id, $userid, $fullname, $position, $dept, $hiredate, $active, $created)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", profile.Id.ToString());
        cmd.Parameters.AddWithValue("$userid", profile.UserId?.ToString() ?? "");
        cmd.Parameters.AddWithValue("$fullname", ""); // Will be filled from User table
        cmd.Parameters.AddWithValue("$position", profile.PositionTitle ?? "");
        cmd.Parameters.AddWithValue("$dept", profile.Department ?? "");
        cmd.Parameters.AddWithValue("$hiredate", profile.HiredAt?.ToString("o") ?? "");
        cmd.Parameters.AddWithValue("$active", profile.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateStaffProfileAsync(StaffProfile profile)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"UPDATE StaffProfiles SET Position = $position, 
                    Department = $dept, HireDate = $hiredate, IsActive = $active, UpdatedAt = $updated WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", profile.Id.ToString());
        cmd.Parameters.AddWithValue("$position", profile.PositionTitle ?? "");
        cmd.Parameters.AddWithValue("$dept", profile.Department ?? "");
        cmd.Parameters.AddWithValue("$hiredate", profile.HiredAt?.ToString("o") ?? "");
        cmd.Parameters.AddWithValue("$active", profile.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DeleteStaffProfileAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "DELETE FROM StaffProfiles WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    // Inventory CRUD methods
    public async Task<IEnumerable<ReagentType>> GetReagentTypesAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT Id, Name, Description FROM ReagentTypes";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<ReagentType>();
        while (await reader.ReadAsync())
        {
            list.Add(new ReagentType
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return list;
    }

    public async Task<ReagentType> CreateReagentTypeAsync(ReagentType type)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "INSERT OR REPLACE INTO ReagentTypes (Id, Name, Description) VALUES ($id, $name, $desc)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", type.Id.ToString());
        cmd.Parameters.AddWithValue("$name", type.Name);
        cmd.Parameters.AddWithValue("$desc", type.Description ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
        return type;
    }

    public async Task<bool> DeleteReagentTypeAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        using var cmd = new SqliteCommand("DELETE FROM ReagentTypes WHERE Id = $id", connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Reagents WHERE 1=1";
        if (isActive.HasValue)
        {
            sql += isActive.Value ? " AND Status != 2 AND Status != 3" : " AND (Status = 2 OR Status = 3)"; // 2=Obsoleto, 3=Bloqueado as inactive? Need logic check. ReagentStatus enum: 0=ACTIVO, 1=EN_CAMBIO, 2=OBSOLETO, 3=BLOQUEADO
            // Actually usually 'Active' means Status 0 or 1.
            if (isActive.Value) sql = "SELECT * FROM Reagents WHERE Status IN (0, 1)";
            else sql = "SELECT * FROM Reagents WHERE Status IN (2, 3)";
        }
        
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();

        var reagents = new List<ReagentListDto>();
        while (await reader.ReadAsync())
        {
            var r = new ReagentListDto
            {
                Id = Guid.Parse(reader["Id"].ToString()),
                Name = reader["Name"].ToString(),
                Manufacturer = reader["Manufacturer"] == DBNull.Value ? null : reader["Manufacturer"].ToString(),
                ReagentType = reader["ReagentType"] == DBNull.Value ? "" : reader["ReagentType"].ToString(),
                Reference = reader["Reference"] == DBNull.Value ? "" : reader["Reference"].ToString(),
                Status = (ReagentStatus)Convert.ToInt32(reader["Status"]),
                MinStock = Convert.ToDecimal(reader["MinStock"]),
                TargetStock = Convert.ToDecimal(reader["TargetStock"]),
                Fluorescence = reader["Fluorescence"] == DBNull.Value ? "" : reader["Fluorescence"].ToString(),
                InternalCode = reader["InternalCode"] == DBNull.Value ? "" : reader["InternalCode"].ToString(),
                AvailableLots = new List<LotSummaryDto>()
            };
            reagents.Add(r);
        }
        reader.Close();

        // Fill Lots and Calculate Stock
        foreach (var r in reagents)
        {
            var lotSql = "SELECT Id, LotNumber, ExpiryDate, AvailableQty FROM ReagentLots WHERE ReagentId = $rid AND AvailableQty > 0 AND Status = 1"; // 1=RELEASED
            using var lotCmd = new SqliteCommand(lotSql, connection);
            lotCmd.Parameters.AddWithValue("$rid", r.Id.ToString());
            using var lotReader = await lotCmd.ExecuteReaderAsync();
            
            decimal total = 0;
            var now = DateTime.UtcNow;
            var nearest = DateTime.MaxValue;

            while (await lotReader.ReadAsync())
            {
                var qty = Convert.ToDecimal(lotReader["AvailableQty"]);
                var expiry = DateTime.Parse(lotReader["ExpiryDate"].ToString());
                
                total += qty;
                if (expiry < nearest) nearest = expiry;
                
                r.AvailableLots.Add(new LotSummaryDto
                {
                    Id = Guid.Parse(lotReader["Id"].ToString()),
                    LotNumber = lotReader["LotNumber"].ToString(),
                    ExpiryDate = expiry,
                    Qty = qty
                });
            }
            r.TotalStock = total;
            r.NearestExpiry = nearest == DateTime.MaxValue ? null : nearest;
            
            // Calc Expiry Status
            if (r.NearestExpiry.HasValue)
            {
                var days = (r.NearestExpiry.Value - now).TotalDays;
                if (days < 0) r.ExpiryStatus = 2; // Expired
                else if (days < 60) r.ExpiryStatus = 1; // Warning
                else r.ExpiryStatus = 0;
            }
        }
        
        if (isLowStock.HasValue && isLowStock.Value)
        {
            return reagents.Where(r => r.TotalStock <= r.MinStock);
        }

        return reagents;
    }

    public async Task<Reagent?> GetReagentByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM Reagents WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var r = new Reagent
            {
                Id = Guid.Parse(reader["Id"].ToString()),
                Name = reader["Name"].ToString(),
                Manufacturer = reader["Manufacturer"] == DBNull.Value ? null : reader["Manufacturer"].ToString(),
                ReagentType = reader["ReagentType"].ToString(),
                Reference = reader["Reference"].ToString(),
                Status = (ReagentStatus)Convert.ToInt32(reader["Status"]),
                MinStock = Convert.ToDecimal(reader["MinStock"]),
                TargetStock = Convert.ToDecimal(reader["TargetStock"]),
                ReorderQty = Convert.ToDecimal(reader["ReorderQty"]),
                Fluorescence = reader["Fluorescence"].ToString(),
                InternalCode = reader["InternalCode"] == DBNull.Value ? "" : reader["InternalCode"].ToString(),
                Classification = reader["Classification"] == DBNull.Value ? null : reader["Classification"].ToString(),
                StorageConditions = reader["StorageConditions"] == DBNull.Value ? null : reader["StorageConditions"].ToString(),
                OpenShelfLifeDays = reader["OpenShelfLifeDays"] == DBNull.Value ? null : Convert.ToInt32(reader["OpenShelfLifeDays"]),
                CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                Lots = new List<ReagentLot>()
            };
            return r;
        }
        return null;
    }

    public async Task CreateReagentAsync(Reagent reagent)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"INSERT INTO Reagents (Id, Name, Manufacturer, ReagentType, Reference, Classification, StorageConditions, OpenShelfLifeDays, Status, MinStock, TargetStock, ReorderQty, CreatedAt, Fluorescence, InternalCode, ManufacturerCode)
                    VALUES ($id, $name, $manu, $type, $ref, $class, $store, $open, $status, $min, $target, $reorder, $created, $fluor, $int, $mancode)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", reagent.Id.ToString());
        cmd.Parameters.AddWithValue("$name", reagent.Name);
        cmd.Parameters.AddWithValue("$manu", reagent.Manufacturer ?? "");
        cmd.Parameters.AddWithValue("$type", reagent.ReagentType ?? "");
        cmd.Parameters.AddWithValue("$ref", reagent.Reference ?? "");
        cmd.Parameters.AddWithValue("$class", reagent.Classification ?? "");
        cmd.Parameters.AddWithValue("$store", reagent.StorageConditions ?? "");
        cmd.Parameters.AddWithValue("$open", reagent.OpenShelfLifeDays ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)reagent.Status);
        cmd.Parameters.AddWithValue("$min", reagent.MinStock);
        cmd.Parameters.AddWithValue("$target", reagent.TargetStock);
        cmd.Parameters.AddWithValue("$reorder", reagent.ReorderQty);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$fluor", reagent.Fluorescence ?? "");
        cmd.Parameters.AddWithValue("$int", reagent.InternalCode ?? "");
        cmd.Parameters.AddWithValue("$mancode", reagent.ManufacturerCode ?? "");
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"UPDATE Reagents SET Name=$name, Manufacturer=$manu, ReagentType=$type, Reference=$ref, 
                    Classification=$class, StorageConditions=$store, OpenShelfLifeDays=$open, 
                    MinStock=$min, TargetStock=$target, ReorderQty=$reorder, 
                    Fluorescence=$fluor, InternalCode=$int, ManufacturerCode=$mancode, UpdatedAt=$updated 
                    WHERE Id=$id";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$name", request.Name);
        cmd.Parameters.AddWithValue("$manu", request.Manufacturer ?? "");
        cmd.Parameters.AddWithValue("$type", request.ReagentType ?? "");
        cmd.Parameters.AddWithValue("$ref", request.Reference ?? "");
        cmd.Parameters.AddWithValue("$class", request.Classification ?? "");
        cmd.Parameters.AddWithValue("$store", request.StorageConditions ?? "");
        cmd.Parameters.AddWithValue("$open", request.OpenShelfLifeDays ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$min", request.MinStock);
        cmd.Parameters.AddWithValue("$target", request.TargetStock);
        cmd.Parameters.AddWithValue("$reorder", request.ReorderQty);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$fluor", request.Fluorescence ?? "");
        cmd.Parameters.AddWithValue("$int", request.InternalCode ?? "");
        cmd.Parameters.AddWithValue("$mancode", request.ManufacturerCode ?? "");

        // DTO check: CreateReagentRequest Definition?
        // Let's assume request properties match. If not, build will fail and I'll fix it.
        
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> UpdateReagentStatusAsync(Guid id, int status)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "UPDATE Reagents SET Status = $status, UpdatedAt = $updated WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteReagentAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        // Transaction to delete reagent and lots
        using var transaction = connection.BeginTransaction();
        try
        {
            var sqlLots = "DELETE FROM ReagentLots WHERE ReagentId = $id";
            using var cmdLots = new SqliteCommand(sqlLots, connection, transaction);
            cmdLots.Parameters.AddWithValue("$id", id.ToString());
            await cmdLots.ExecuteNonQueryAsync();

            var sql = "DELETE FROM Reagents WHERE Id = $id";
            using var cmd = new SqliteCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("$id", id.ToString());
            var rows = await cmd.ExecuteNonQueryAsync();
            
            await transaction.CommitAsync();
            return rows > 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<List<ReagentLot>> RegisterLotAsync(RegisterLotRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var lotId = Guid.NewGuid().ToString();
        var sql = @"INSERT INTO ReagentLots (Id, ReagentId, LotNumber, ExpiryDate, ReceivedDate, ReceivedQty, AvailableQty, Status, LocationId)
                    VALUES ($id, $rid, $lot, $expiry, $received, $qty, $avail, 1, $loc)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", lotId);
        cmd.Parameters.AddWithValue("$rid", request.ReagentId.ToString());
        cmd.Parameters.AddWithValue("$lot", request.LotNumber);
        cmd.Parameters.AddWithValue("$expiry", request.ExpiryDate.ToString("o"));
        cmd.Parameters.AddWithValue("$received", request.ReceivedDate.ToString("o"));
        cmd.Parameters.AddWithValue("$qty", request.ReceivedQty);
        cmd.Parameters.AddWithValue("$avail", request.ReceivedQty);
        cmd.Parameters.AddWithValue("$loc", request.LocationId?.ToString() ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();

        // Register Movement
        var moveSql = @"INSERT INTO InventoryMovements (Id, ReagentId, LotId, Type, Qty, Date, UserId, Notes)
                        VALUES ($id, $rid, $lotid, 0, $qty, $date, $user, 'Entrada inicial')";
        using var moveCmd = new SqliteCommand(moveSql, connection);
        moveCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        moveCmd.Parameters.AddWithValue("$rid", request.ReagentId.ToString());
        moveCmd.Parameters.AddWithValue("$lotid", lotId);
        moveCmd.Parameters.AddWithValue("$qty", request.ReceivedQty);
        moveCmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
        moveCmd.Parameters.AddWithValue("$user", request.UserId?.ToString() ?? Guid.Empty.ToString());
        
        await moveCmd.ExecuteNonQueryAsync();
        
        // Return all lots for this reagent
        var lots = new List<ReagentLot>();
        var getSql = "SELECT * FROM ReagentLots WHERE ReagentId = $rid AND AvailableQty > 0";
        using var getCmd = new SqliteCommand(getSql, connection);
        getCmd.Parameters.AddWithValue("$rid", request.ReagentId.ToString());
        using var reader = await getCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lots.Add(new ReagentLot
            {
                Id = Guid.Parse(reader["Id"].ToString()),
                ReagentId = Guid.Parse(reader["ReagentId"].ToString()),
                LotNumber = reader["LotNumber"].ToString(),
                ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString()),
                AvailableQty = Convert.ToDecimal(reader["AvailableQty"]),
                Status = (LotStatus)Convert.ToInt32(reader["Status"])
            });
        }
        return lots;
    }

    public async Task<bool> AdjustStockAsync(AdjustStockRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        // Determine delta based on movement type
        decimal delta = request.Qty;
        switch (request.MovementType)
        {
            case InventoryMovementType.OUT:
            case InventoryMovementType.WASTE:
            case InventoryMovementType.TRANSFER:
                delta = -Math.Abs(request.Qty);
                break;
            case InventoryMovementType.IN:
            case InventoryMovementType.RETURN:
                delta = Math.Abs(request.Qty);
                break;
            // ADJUST trusts the sign sent by the client
        }

        // Update Lot Qty
        var sql = "UPDATE ReagentLots SET AvailableQty = AvailableQty + $delta WHERE Id = $lotId";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$delta", delta); 
        cmd.Parameters.AddWithValue("$lotId", request.ReagentLotId?.ToString() ?? "");
        var rows = await cmd.ExecuteNonQueryAsync();


        // Check if Lot is empty and update Status to CONSUMED
        if (rows > 0)
        {
            var checkSql = "SELECT AvailableQty FROM ReagentLots WHERE Id = $lotId";
            using var checkCmd = new SqliteCommand(checkSql, connection);
            checkCmd.Parameters.AddWithValue("$lotId", request.ReagentLotId?.ToString() ?? "");
            var newQty = Convert.ToDecimal(await checkCmd.ExecuteScalarAsync());
            
            if (newQty <= 0)
            {
                var statusSql = "UPDATE ReagentLots SET Status = 20, AvailableQty = 0 WHERE Id = $lotId"; // 20 = CONSUMED
                using var statusCmd = new SqliteCommand(statusSql, connection);
                statusCmd.Parameters.AddWithValue("$lotId", request.ReagentLotId?.ToString() ?? "");
                await statusCmd.ExecuteNonQueryAsync();
            }
        }
        
        if (rows > 0)
        {
            // Register Movement
            var moveSql = @"INSERT INTO InventoryMovements (Id, ReagentId, LotId, Type, Qty, Date, UserId, Notes)
                            VALUES ($id, $rid, $lotid, $type, $qty, $date, $user, $notes)";
            using var moveCmd = new SqliteCommand(moveSql, connection);
            moveCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            moveCmd.Parameters.AddWithValue("$rid", request.ReagentId.ToString());
            moveCmd.Parameters.AddWithValue("$lotid", request.ReagentLotId?.ToString() ?? (object)DBNull.Value);
            moveCmd.Parameters.AddWithValue("$type", (int)request.MovementType);
            moveCmd.Parameters.AddWithValue("$qty", Math.Abs(request.Qty));
            moveCmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
            moveCmd.Parameters.AddWithValue("$user", request.UserId?.ToString() ?? Guid.Empty.ToString());
            moveCmd.Parameters.AddWithValue("$notes", request.Reason ?? request.Notes ?? "");
            
            await moveCmd.ExecuteNonQueryAsync();
            return true;
        }
        return false;
    }

    public async Task<List<InventoryMovementDto>> GetMovementsAsync(DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"SELECT m.Id, m.ReagentId, r.Name, r.Manufacturer, r.Fluorescence, 
                           m.LotId, l.LotNumber, l.ExpiryDate, m.Type, m.Qty, m.Date, m.UserId, u.Username, m.Notes
                    FROM InventoryMovements m
                    LEFT JOIN Reagents r ON m.ReagentId = r.Id
                    LEFT JOIN ReagentLots l ON m.LotId = l.Id
                    LEFT JOIN Users u ON m.UserId = u.Id
                    WHERE 1=1";
        
        if (from.HasValue) sql += " AND m.Date >= $from";
        if (to.HasValue) sql += " AND m.Date <= $to";
        if (type.HasValue) sql += " AND m.Type = $type";
        if (reagentId.HasValue) sql += " AND m.ReagentId = $rid";
        
        sql += " ORDER BY m.Date DESC";
        
        using var cmd = new SqliteCommand(sql, connection);
        if (from.HasValue) cmd.Parameters.AddWithValue("$from", from.Value.ToString("o"));
        if (to.HasValue) cmd.Parameters.AddWithValue("$to", to.Value.ToString("o"));
        if (type.HasValue) cmd.Parameters.AddWithValue("$type", (int)type.Value);
        if (reagentId.HasValue) cmd.Parameters.AddWithValue("$rid", reagentId.ToString());
        
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<InventoryMovementDto>();
        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader["Id"].ToString());
            var date = DateTime.Parse(reader["Date"].ToString());
            var userName = reader["Username"] == DBNull.Value ? "System" : reader["Username"].ToString();
            var reagentName = reader["Name"] == DBNull.Value ? "Unknown" : reader["Name"].ToString();
            var manufacturer = reader["Manufacturer"] == DBNull.Value ? null : reader["Manufacturer"].ToString();
            var fluorescence = reader["Fluorescence"] == DBNull.Value ? null : reader["Fluorescence"].ToString();
            var adjustmentType = ((InventoryMovementType)Convert.ToInt32(reader["Type"])).ToString();
            var quantity = Convert.ToDecimal(reader["Qty"]);
            var lotNumber = reader["LotNumber"] == DBNull.Value ? null : reader["LotNumber"].ToString();
            DateTime? expiryDate = reader["ExpiryDate"] == DBNull.Value ? null : DateTime.Parse(reader["ExpiryDate"].ToString());
            var reason = reader["Notes"] == DBNull.Value ? "" : reader["Notes"].ToString();

            list.Add(new InventoryMovementDto(
                id,
                date,
                userName,
                reagentName,
                manufacturer,
                fluorescence,
                adjustmentType,
                quantity,
                lotNumber,
                expiryDate,
                reason
            ));
        }
        return list;
    }

    // Equipment CRUD methods
    public async Task<IEnumerable<EquipmentListDto>> GetEquipmentAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM Equipments";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<EquipmentListDto>();
        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader["Id"].ToString());
            var dto = new EquipmentListDto
            {
                Id = id,
                AssetTag = reader["AssetTag"] == DBNull.Value ? null : reader["AssetTag"].ToString(),
                Name = reader["Name"].ToString(),
                Model = reader["Model"] == DBNull.Value ? null : reader["Model"].ToString(),
                SoftwareVersion = reader["SoftwareVersion"] == DBNull.Value ? null : reader["SoftwareVersion"].ToString(),
                FirmwareVersion = reader["FirmwareVersion"] == DBNull.Value ? null : reader["FirmwareVersion"].ToString(),
                Location = reader["Location"] == DBNull.Value ? null : reader["Location"].ToString(),
                Status = (EquipmentStatus)Convert.ToInt32(reader["Status"])
            };
            list.Add(dto);
        }
        reader.Close();

        foreach (var eq in list)
        {
            // Last Maintenance
            // Last Maintenance + Next Due (manual override)
            var lastMaintSql = "SELECT Id, PerformedAt, EventType, Outcome, NextMaintenanceMonth, NextMaintenanceYear FROM MaintenanceEvents WHERE EquipmentId = $eid ORDER BY PerformedAt DESC LIMIT 1";
            using var lastCmd = new SqliteCommand(lastMaintSql, connection);
            lastCmd.Parameters.AddWithValue("$eid", eq.Id.ToString());
            using var lastReader = await lastCmd.ExecuteReaderAsync();
            int? manualNextMonth = null;
            int? manualNextYear = null;

            if (await lastReader.ReadAsync())
            {
                eq.LastMaintenanceEventId = Guid.Parse(lastReader["Id"].ToString());
                eq.LastMaintenanceAt = DateTime.Parse(lastReader["PerformedAt"].ToString());
                eq.LastEventType = ((MaintenanceEventType)Convert.ToInt32(lastReader["EventType"])).ToString();
                eq.LastOutcome = lastReader["Outcome"] == DBNull.Value ? null : lastReader["Outcome"].ToString();
                
                if (lastReader["NextMaintenanceMonth"] != DBNull.Value && lastReader["NextMaintenanceYear"] != DBNull.Value)
                {
                    manualNextMonth = Convert.ToInt32(lastReader["NextMaintenanceMonth"]);
                    manualNextYear = Convert.ToInt32(lastReader["NextMaintenanceYear"]);
                }
            }
            lastReader.Close();

            // QC Today
            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            var qcSql = "SELECT IsPass FROM EquipmentDailyQC WHERE EquipmentId = $eid AND date(PerformedAt) = $today ORDER BY PerformedAt DESC LIMIT 1";
            using var qcCmd = new SqliteCommand(qcSql, connection);
            qcCmd.Parameters.AddWithValue("$eid", eq.Id.ToString());
            qcCmd.Parameters.AddWithValue("$today", today);
            var result = await qcCmd.ExecuteScalarAsync();
            if (result != null)
            {
                var pass = Convert.ToInt32(result) == 1;
                eq.TodayQCStatus = pass ? "PASS" : "FAIL";
                eq.TodayQCColor = pass ? "Green" : "Red";
            }
            else
            {
                eq.TodayQCStatus = "PENDING";
                eq.TodayQCColor = "Gray";
            }
            
            // Next Maintenance Calculation
            DateTime? nextDue = null;

            // 1. Try Manual Input first (highest priority if set recently)
            if (manualNextMonth.HasValue && manualNextYear.HasValue)
            {
                try {
                    // Default to 1st of month? Or end? Let's say 1st for safety or user intent.
                    nextDue = new DateTime(manualNextYear.Value, manualNextMonth.Value, 1);
                } catch { } // Invalid date
            }

            // 2. If no manual override, try Maintenance Plans
            if (nextDue == null)
            {
                var planSql = "SELECT FrequencyDays, PlanName FROM MaintenancePlans WHERE EquipmentId = $eid AND IsActive = 1";
                using var planCmd = new SqliteCommand(planSql, connection);
                planCmd.Parameters.AddWithValue("$eid", eq.Id.ToString());
                using var planReader = await planCmd.ExecuteReaderAsync();
                
                while (await planReader.ReadAsync())
                {
                    var days = Convert.ToInt32(planReader["FrequencyDays"]);
                    var baseDate = eq.LastMaintenanceAt;
                    if (!baseDate.HasValue) 
                    {
                        var instSql = "SELECT InstalledAt FROM Equipments WHERE Id = $eid";
                        using var instCmd = new SqliteCommand(instSql, connection);
                        instCmd.Parameters.AddWithValue("$eid", eq.Id.ToString());
                        var inst = await instCmd.ExecuteScalarAsync();
                        if (inst != null && inst != DBNull.Value) baseDate = DateTime.Parse(inst.ToString());
                    }

                    if (baseDate.HasValue)
                    {
                        var due = baseDate.Value.AddDays(days);
                        if (nextDue == null || due < nextDue) nextDue = due;
                    }
                }
            }
            if (nextDue.HasValue)
            {
                eq.NextMaintenanceDue = nextDue.Value.ToString("dd/MM/yyyy");
                // TODO: Color logic for due/overdue
            }
        }
        return list;
    }

    public async Task<Equipment?> GetEquipmentByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM Equipments WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return new Equipment
            {
                Id = Guid.Parse(reader["Id"].ToString()),
                AssetTag = reader["AssetTag"] == DBNull.Value ? null : reader["AssetTag"].ToString(),
                Name = reader["Name"].ToString(),
                Manufacturer = reader["Manufacturer"] == DBNull.Value ? null : reader["Manufacturer"].ToString(),
                Model = reader["Model"] == DBNull.Value ? null : reader["Model"].ToString(),
                SerialNumber = reader["SerialNumber"] == DBNull.Value ? null : reader["SerialNumber"].ToString(),
                SoftwareVersion = reader["SoftwareVersion"] == DBNull.Value ? null : reader["SoftwareVersion"].ToString(),
                FirmwareVersion = reader["FirmwareVersion"] == DBNull.Value ? null : reader["FirmwareVersion"].ToString(),
                Location = reader["Location"] == DBNull.Value ? null : reader["Location"].ToString(),
                Status = (EquipmentStatus)Convert.ToInt32(reader["Status"]),
                InstalledAt = reader["InstalledAt"] == DBNull.Value ? null : DateTime.Parse(reader["InstalledAt"].ToString()),
                Notes = reader["Notes"] == DBNull.Value ? null : reader["Notes"].ToString()
            };
        }
        return null;
    }

    public async Task CreateEquipmentAsync(Equipment equipment)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"INSERT INTO Equipments (Id, AssetTag, Name, Manufacturer, Model, SerialNumber, SoftwareVersion, FirmwareVersion, Location, Status, InstalledAt, Notes)
                    VALUES ($id, $tag, $name, $manu, $model, $serial, $soft, $firm, $loc, $status, $inst, $notes)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", equipment.Id.ToString());
        cmd.Parameters.AddWithValue("$tag", equipment.AssetTag ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$name", equipment.Name);
        cmd.Parameters.AddWithValue("$manu", equipment.Manufacturer ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$model", equipment.Model ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$serial", equipment.SerialNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$soft", equipment.SoftwareVersion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$firm", equipment.FirmwareVersion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$loc", equipment.Location ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)equipment.Status);
        cmd.Parameters.AddWithValue("$inst", equipment.InstalledAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", equipment.Notes ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateEquipmentAsync(UpdateEquipmentRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"UPDATE Equipments SET AssetTag=$tag, Name=$name, Manufacturer=$manu, Model=$model, SerialNumber=$serial, 
                    SoftwareVersion=$soft, FirmwareVersion=$firm, Location=$loc, InstalledAt=$inst 
                    WHERE Id=$id";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", request.Id.ToString());
        cmd.Parameters.AddWithValue("$tag", request.AssetTag ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$name", request.Name);
        cmd.Parameters.AddWithValue("$manu", request.Manufacturer ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$model", request.Model ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$serial", request.SerialNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$soft", request.SoftwareVersion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$firm", request.FirmwareVersion ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$loc", request.Location ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$inst", request.InstalledAt?.ToString("o") ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DeleteEquipmentAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "DELETE FROM Equipments WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task UpdateMaintenanceAsync(UpdateMaintenanceRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"UPDATE MaintenanceEvents 
                    SET EventType=$type, Outcome=$outcome, Notes=$notes, PerformedAt=$date, PerformedByUserId=$user,
                        HasIssues=$issue, NextMaintenanceMonth=$nm, NextMaintenanceYear=$ny
                    WHERE Id=$id";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", request.Id.ToString());
        cmd.Parameters.AddWithValue("$type", (int)request.EventType);
        cmd.Parameters.AddWithValue("$outcome", request.Outcome ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$date", request.PerformedAt?.ToString("o") ?? DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$user", request.PerformedByUserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$issue", (request.HasIssues ?? false) ? 1 : 0);
        cmd.Parameters.AddWithValue("$nm", request.NextMaintenanceMonth ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ny", request.NextMaintenanceYear ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
    }


    // Dashboard Data
    public async Task<DashboardDataDto> GetDashboardDataAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var stats = new DashboardStatsDto();
        
        // 1. Documents
        stats.TotalDocuments = Convert.ToInt32(await new SqliteCommand("SELECT COUNT(*) FROM Documents", connection).ExecuteScalarAsync());
        stats.PendingReviewDocs = Convert.ToInt32(await new SqliteCommand("SELECT COUNT(*) FROM Documents WHERE Status = 'DRAFT'", connection).ExecuteScalarAsync());
        stats.PendingApprovalDocs = Convert.ToInt32(await new SqliteCommand("SELECT COUNT(*) FROM Documents WHERE Status = 'REVIEW'", connection).ExecuteScalarAsync());

        // 2. Inventory (Low Stock)
        stats.LowStockReagents = Convert.ToInt32(await new SqliteCommand("SELECT COUNT(*) FROM Reagents WHERE CurrentStock <= MinimumStock", connection).ExecuteScalarAsync());

        // 3. Equipment (Due Maintenance)
        stats.DueEquipmentMaintenance = Convert.ToInt32(await new SqliteCommand("SELECT COUNT(*) FROM Maintenances WHERE DueDate <= date('now') AND (Status = 0 OR Status IS NULL)", connection).ExecuteScalarAsync());

        // 4. Risks (High Risks)
        // Note: RiskScore is derived, so we check Likelihood * Impact >= 15
        stats.OpenHighRisks = Convert.ToInt32(await new SqliteCommand("SELECT COUNT(*) FROM Risks WHERE Status = 0 AND (Likelihood * Impact) >= 15", connection).ExecuteScalarAsync());

        // 5. Staff
        stats.ActiveStaffCount = Convert.ToInt32(await new SqliteCommand("SELECT COUNT(*) FROM StaffProfiles WHERE Status = 'ACTIVO'", connection).ExecuteScalarAsync());

        // Recent Activity (Audit Logs)
        var logs = new List<DashboardRecentActivityDto>();
        var logSql = "SELECT Action, EntityType, Details, Timestamp, UserName FROM AuditLogs ORDER BY Timestamp DESC LIMIT 10";
        using var cmd = new SqliteCommand(logSql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new DashboardRecentActivityDto
            {
                Type = reader.GetString(0),
                Description = $"{reader.GetString(1)}: {reader.GetString(2)}",
                Timestamp = DateTime.Parse(reader.GetString(3)),
                UserName = reader.GetString(4)
            });
        }

        return new DashboardDataDto(stats, logs);
    }

    // Global Search
    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string query)
    {
        var results = new List<SearchResultDto>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Search Documents
        var docSql = "SELECT Id, DocCode, Title FROM Documents WHERE DocCode LIKE $q OR Title LIKE $q LIMIT 20";
        using (var cmd = new SqliteCommand(docSql, connection))
        {
            cmd.Parameters.AddWithValue("$q", $"%{query}%");
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SearchResultDto(
                    Id: Guid.Parse(reader.GetString(0)), 
                    EntityType: "DOCUMENT", 
                    Title: reader.GetString(1), 
                    Subtitle: reader.GetString(2), 
                    Route: $"/documents/{reader.GetString(0)}"));
            }
        }

        // Search Reagents
        var reagentSql = "SELECT Id, Name, CatalogNumber FROM Reagents WHERE Name LIKE $q OR CatalogNumber LIKE $q LIMIT 20";
        using (var cmd = new SqliteCommand(reagentSql, connection))
        {
            cmd.Parameters.AddWithValue("$q", $"%{query}%");
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SearchResultDto(
                    Id: Guid.Parse(reader.GetString(0)), 
                    EntityType: "REAGENT", 
                    Title: reader.GetString(1), 
                    Subtitle: reader.IsDBNull(2) ? "" : reader.GetString(2), 
                    Route: $"/inventory/reagents/{reader.GetString(0)}"));
            }
        }

        return results;
    }

    // Training Activities
    public async Task<IEnumerable<TrainingActivityDto>> GetTrainingActivitiesAsync()
    {
        var activities = new List<TrainingActivityDto>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM TrainingActivities ORDER BY CreatedAt DESC";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            activities.Add(new TrainingActivityDto(
                Id: Guid.Parse(reader.GetString(0)),
                Title: reader.GetString(1),
                Provider: reader.IsDBNull(2) ? null : reader.GetString(2),
                TrainingTypeName: "General", 
                StartDate: reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                Hours: reader.GetDecimal(7),
                Status: reader.IsDBNull(13) ? "ACTIVO" : reader.GetString(13)
            ));
        }
        return activities;
    }

    public async Task<TrainingActivity> CreateTrainingActivityAsync(TrainingActivity activity)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO TrainingActivities (Id, Title, Provider, TrainingTypeId, Modality, StartDate, EndDate, Hours, Credits, Description, IsInternal, InternalDepartment, Status, CreatedByUserId, CreatedAt, UpdatedAt)
            VALUES ($id, $title, $provider, $typeId, $modality, $start, $end, $hours, $credits, $desc, $internal, $dept, $status, $userId, $created, $updated)
        ";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", activity.Id.ToString());
        cmd.Parameters.AddWithValue("$title", activity.Title);
        cmd.Parameters.AddWithValue("$provider", activity.Provider ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$typeId", activity.TrainingTypeId.ToString());
        cmd.Parameters.AddWithValue("$modality", activity.Modality);
        cmd.Parameters.AddWithValue("$start", activity.StartDate?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$end", activity.EndDate?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$hours", activity.Hours);
        cmd.Parameters.AddWithValue("$credits", activity.Credits ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$desc", activity.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$internal", activity.IsInternal ? 1 : 0);
        cmd.Parameters.AddWithValue("$dept", activity.InternalDepartment ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", activity.Status);
        cmd.Parameters.AddWithValue("$userId", activity.CreatedByUserId.ToString());
        cmd.Parameters.AddWithValue("$created", activity.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updated", activity.UpdatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
        return activity;
    }

    public async Task<MaintenanceEvent?> GetLastMaintenanceAsync(Guid equipmentId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM MaintenanceEvents WHERE EquipmentId = $eid ORDER BY PerformedAt DESC LIMIT 1";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$eid", equipmentId.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new MaintenanceEvent
            {
                Id = Guid.Parse(reader["Id"].ToString()),
                EquipmentId = Guid.Parse(reader["EquipmentId"].ToString()),
                PerformedAt = DateTime.Parse(reader["PerformedAt"].ToString()),
                EventType = (MaintenanceEventType)Convert.ToInt32(reader["EventType"]),
                Outcome = reader["Outcome"] == DBNull.Value ? null : reader["Outcome"].ToString(),
                Notes = reader["Notes"] == DBNull.Value ? null : reader["Notes"].ToString(),
                PerformedByUserId = reader["PerformedByUserId"] == DBNull.Value ? null : Guid.Parse(reader["PerformedByUserId"].ToString()),
                HasIssues = reader["HasIssues"] != DBNull.Value && Convert.ToInt32(reader["HasIssues"]) == 1,
                NextMaintenanceMonth = reader["NextMaintenanceMonth"] == DBNull.Value ? null : Convert.ToInt32(reader["NextMaintenanceMonth"]),
                NextMaintenanceYear = reader["NextMaintenanceYear"] == DBNull.Value ? null : Convert.ToInt32(reader["NextMaintenanceYear"])
            };
        }
        return null;
    }

    public async Task RegisterMaintenanceAsync(RegisterMaintenanceRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"INSERT INTO MaintenanceEvents (Id, EquipmentId, PlanId, PerformedAt, PerformedByUserId, EventType, Outcome, Notes, HasIssues, NextMaintenanceMonth, NextMaintenanceYear)
                    VALUES ($id, $eid, $pid, $at, $user, $type, $outcome, $notes, $issue, $nm, $ny)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$eid", request.EquipmentId.ToString());
        cmd.Parameters.AddWithValue("$pid", request.PlanId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$at", request.PerformedAt?.ToString("o") ?? DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$user", request.UserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$type", (int)request.EventType);
        cmd.Parameters.AddWithValue("$outcome", request.Outcome ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$issue", (request.HasIssues ?? false) ? 1 : 0);
        cmd.Parameters.AddWithValue("$nm", request.NextMaintenanceMonth ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ny", request.NextMaintenanceYear ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
    }
    
    // RegisterDailyQCAsync etc.
    public async Task RegisterDailyQCAsync(CreateDailyQCRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"INSERT INTO EquipmentDailyQC (Id, EquipmentId, LotNumber, IsPass, Notes, PerformedByUserId, PerformedAt)
                    VALUES ($id, $eid, $lot, $pass, $notes, $user, $at)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$eid", request.EquipmentId.ToString());
        cmd.Parameters.AddWithValue("$lot", request.LotNumber ?? "");
        cmd.Parameters.AddWithValue("$pass", request.IsPass ? 1 : 0);
        cmd.Parameters.AddWithValue("$notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$user", request.UserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$at", request.PerformedAt.ToString("o"));
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<EquipmentDailyQCDto>> GetDailyQCAsync(Guid equipmentId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"SELECT qc.*, u.Username FROM EquipmentDailyQC qc
                    LEFT JOIN Users u ON qc.PerformedByUserId = u.Id
                    WHERE EquipmentId = $eid ORDER BY PerformedAt DESC";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$eid", equipmentId.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<EquipmentDailyQCDto>();
        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader["Id"].ToString());
            var eqId = Guid.Parse(reader["EquipmentId"].ToString());
            var lot = reader["LotNumber"].ToString();
            var pass = Convert.ToInt32(reader["IsPass"]) == 1;
            var notes = reader["Notes"] == DBNull.Value ? null : reader["Notes"].ToString();
            var at = DateTime.Parse(reader["PerformedAt"].ToString());
            var by = reader["Username"] == DBNull.Value ? "Unknown" : reader["Username"].ToString();

            // Positional record constructor check:
            // public record EquipmentDailyQCDto(Guid Id, Guid EquipmentId, string LotNumber, bool IsPass, string? Notes, DateTime PerformedAt, string PerformedByName);
            list.Add(new EquipmentDailyQCDto(id, eqId, lot, pass, notes, at, by));
        }
        return list;
    }

    // Quality CRUD methods
    public async Task<IEnumerable<NCListDto>> GetNonconformitiesAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM Incidents ORDER BY DetectedAt DESC";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<NCListDto>();
        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader["Id"].ToString());
            
            // Count actions
            var actionSql = "SELECT COUNT(*) FROM IncidentActions WHERE NCId = $id";
            using var actionCmd = new SqliteCommand(actionSql, connection);
            actionCmd.Parameters.AddWithValue("$id", id.ToString());
            var count = Convert.ToInt32(await actionCmd.ExecuteScalarAsync());

            list.Add(new NCListDto(
                id,
                DateTime.Parse(reader["DetectedAt"].ToString()),
                reader["Title"].ToString(),
                (NCSeverity)Convert.ToInt32(reader["Severity"]),
                (NCStatus)Convert.ToInt32(reader["Status"]),
                Convert.ToInt32(reader["ImpactPatient"]) == 1,
                count,
                reader["Origin"] == DBNull.Value ? null : reader["Origin"].ToString(),
                reader["RootCauseAnalysis"] == DBNull.Value ? null : reader["RootCauseAnalysis"].ToString()
            ));
        }
        return list;
    }

    public async Task<Nonconformity?> GetNCByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM Incidents WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            var nc = new Nonconformity
            {
                Id = Guid.Parse(reader["Id"].ToString()),
                DetectedAt = DateTime.Parse(reader["DetectedAt"].ToString()),
                DetectedByUserId = reader["DetectedByUserId"] == DBNull.Value ? null : Guid.Parse(reader["DetectedByUserId"].ToString()),
                Title = reader["Title"].ToString(),
                Description = reader["Description"].ToString(),
                Severity = (NCSeverity)Convert.ToInt32(reader["Severity"]),
                ImpactPatient = Convert.ToInt32(reader["ImpactPatient"]) == 1,
                Containment = reader["Containment"] == DBNull.Value ? null : reader["Containment"].ToString(),
                Origin = reader["Origin"] == DBNull.Value ? null : reader["Origin"].ToString(),
                RootCauseAnalysis = reader["RootCauseAnalysis"] == DBNull.Value ? null : reader["RootCauseAnalysis"].ToString(),
                Status = (NCStatus)Convert.ToInt32(reader["Status"]),
                UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString())
            };
            reader.Close();
            
            // Load Actions
            var actionSql = "SELECT * FROM IncidentActions WHERE NCId = $id";
            using var actionCmd = new SqliteCommand(actionSql, connection);
            actionCmd.Parameters.AddWithValue("$id", id.ToString());
            using var actionReader = await actionCmd.ExecuteReaderAsync();
            while (await actionReader.ReadAsync())
            {
                nc.Actions.Add(new CapaAction
                {
                    Id = Guid.Parse(actionReader["Id"].ToString()),
                    NCId = id,
                    ActionType = (CAPAActionType)Convert.ToInt32(actionReader["ActionType"]),
                    Description = actionReader["Description"].ToString(),
                    OwnerUserId = actionReader["OwnerUserId"] == DBNull.Value ? null : Guid.Parse(actionReader["OwnerUserId"].ToString()),
                    DueDate = actionReader["DueDate"] == DBNull.Value ? null : DateTime.Parse(actionReader["DueDate"].ToString()),
                    CompletedAt = actionReader["CompletedAt"] == DBNull.Value ? null : DateTime.Parse(actionReader["CompletedAt"].ToString()),
                    EffectivenessCheck = actionReader["EffectivenessCheck"] == DBNull.Value ? null : actionReader["EffectivenessCheck"].ToString(),
                    Status = (CAPAStatus)Convert.ToInt32(actionReader["Status"])
                });
            }
            
            return nc;
        }
        return null;
    }

    public async Task CreateNCAsync(Nonconformity nc)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"INSERT INTO Incidents (Id, DetectedAt, DetectedByUserId, Title, Description, Severity, ImpactPatient, Containment, Origin, RootCauseAnalysis, Status, UpdatedAt)
                    VALUES ($id, $at, $user, $title, $desc, $sev, $impact, $cont, $orig, $rca, $stat, $updated)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", nc.Id.ToString());
        cmd.Parameters.AddWithValue("$at", nc.DetectedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$user", nc.DetectedByUserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$title", nc.Title);
        cmd.Parameters.AddWithValue("$desc", nc.Description);
        cmd.Parameters.AddWithValue("$sev", (int)nc.Severity);
        cmd.Parameters.AddWithValue("$impact", nc.ImpactPatient ? 1 : 0);
        cmd.Parameters.AddWithValue("$cont", nc.Containment ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$orig", nc.Origin ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$rca", nc.RootCauseAnalysis ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$stat", (int)nc.Status);
        cmd.Parameters.AddWithValue("$updated", nc.UpdatedAt.ToString("o"));
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateNCAsync(Guid id, CreateNCRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"UPDATE Incidents SET Title=$title, Description=$desc, Severity=$sev, Status=$stat, 
                    ImpactPatient=$impact, Containment=$cont, Origin=$orig, RootCauseAnalysis=$rca, UpdatedAt=$updated
                    WHERE Id=$id";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$title", request.Title);
        cmd.Parameters.AddWithValue("$desc", request.Description);
        cmd.Parameters.AddWithValue("$sev", (int)request.Severity);
        cmd.Parameters.AddWithValue("$stat", (int)(request.Status ?? NCStatus.OPEN));
        cmd.Parameters.AddWithValue("$impact", request.ImpactPatient ? 1 : 0);
        cmd.Parameters.AddWithValue("$cont", request.Containment ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$orig", request.Origin ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$rca", request.RootCauseAnalysis ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateNCStatusAsync(Guid id, int status)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "UPDATE Incidents SET Status=$stat, UpdatedAt=$updated WHERE Id=$id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$stat", status);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task CreateCAPAAsync(CapaAction action)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"INSERT INTO IncidentActions (Id, NCId, ActionType, Description, OwnerUserId, DueDate, CompletedAt, EffectivenessCheck, Status)
                    VALUES ($id, $ncid, $type, $desc, $owner, $due, $comp, $check, $stat)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", action.Id.ToString());
        cmd.Parameters.AddWithValue("$ncid", action.NCId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$type", (int)action.ActionType);
        cmd.Parameters.AddWithValue("$desc", action.Description);
        cmd.Parameters.AddWithValue("$owner", action.OwnerUserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$due", action.DueDate?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$comp", action.CompletedAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$check", action.EffectivenessCheck ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$stat", (int)action.Status);
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CreateAuditLogAsync(string action, string resource, string details, string userId, string userName)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"INSERT INTO AuditLogs (Id, Action, Resource, Details, Timestamp, UserId, UserName)
                    VALUES ($id, $action, $res, $det, $time, $uid, $uname)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$action", action);
        cmd.Parameters.AddWithValue("$res", resource ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$det", details ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$uid", userId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$uname", userName ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
    }
    private async Task SeedDocumentTypesAsync(SqliteConnection connection)
    {
        var types = new List<(Guid Id, string Code, string Name)>
        {
            (Guid.Parse("362678f2-3e2b-4d40-b88a-268e364660e2"), "REP", "Reporte"),
            (Guid.Parse("7f1f3a2c-9a1d-4f4e-9b6f-78e7f8e7f8e7"), "INS", "Instructivo"),
            (Guid.Parse("8ca6789a-0b2c-4d3e-9f8a-7e6d5c4b3a21"), "FOR", "Formulario"),
            (Guid.Parse("b4c5d6e7-f8a9-4b0c-bd1d-2e3f4a5b6c7d"), "MAN", "Manual"),
            (Guid.Parse("e8f9a0b1-c2d3-4e4f-95a6-b7c8d9e0f1a2"), "EXT", "Externo"),
            (Guid.Parse("f1e2d3c4-b5a6-4078-9e0f-1a2b3c4d5e6f"), "PRO", "Procedimiento")
        };

        foreach (var t in types)
        {
            var checkSql = "SELECT COUNT(*) FROM DocumentTypes WHERE Id = $id";
            using var checkCmd = new SqliteCommand(checkSql, connection);
            checkCmd.Parameters.AddWithValue("$id", t.Id.ToString());
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (count == 0)
            {
                var insertSql = "INSERT INTO DocumentTypes (Id, TypeCode, Name) VALUES ($id, $code, $name)";
                using var insertCmd = new SqliteCommand(insertSql, connection);
                insertCmd.Parameters.AddWithValue("$id", t.Id.ToString());
                insertCmd.Parameters.AddWithValue("$code", t.Code);
                insertCmd.Parameters.AddWithValue("$name", t.Name);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<Document> CreateDocumentAsync(Document document)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO Documents (Id, DocCode, Title, DocumentTypeId, FolderId, Area, Process, OwnerUserId, Status, ReviewIntervalMonths, NextReviewDue, CreatedAt, UpdatedAt)
            VALUES ($id, $code, $title, $typeId, $folderId, $area, $process, $ownerId, $status, $interval, $nextReview, $created, $updated)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", document.Id.ToString());
        command.Parameters.AddWithValue("$code", document.DocCode);
        command.Parameters.AddWithValue("$title", document.Title);
        command.Parameters.AddWithValue("$typeId", document.DocumentTypeId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$folderId", document.FolderId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$area", document.Area ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$process", document.Process ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$ownerId", document.OwnerUserId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", document.Status.ToString());
        command.Parameters.AddWithValue("$interval", document.ReviewIntervalMonths ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$nextReview", document.NextReviewDue?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$created", document.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updated", document.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync();
        return document;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        try
        {
            // Get Folder Name for trash path
            var folderSql = "SELECT f.Name FROM Documents d LEFT JOIN Folders f ON d.FolderId = f.Id WHERE d.Id = $id";
            string folderName = "General";
            using (var cmd = new SqliteCommand(folderSql, connection))
            {
                cmd.Parameters.AddWithValue("$id", id.ToString());
                var result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value) folderName = result.ToString()!;
            }

            // Get file paths to delete physical files
            var versionsSql = "SELECT LocalFilePath FROM DocumentVersions WHERE DocumentId = $id";
            var filePaths = new List<string>();
            
            using (var cmd = new SqliteCommand(versionsSql, connection))
            {
                cmd.Parameters.AddWithValue("$id", id.ToString());
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        var path = reader.GetString(0);
                        if (!string.IsNullOrEmpty(path)) filePaths.Add(path);
                    }
                }
            }

            // Delete from database (cascade will delete versions)
            var deleteSql = "DELETE FROM Documents WHERE Id = $id";
            using (var cmd = new SqliteCommand(deleteSql, connection))
            {
                cmd.Parameters.AddWithValue("$id", id.ToString());
                var affected = await cmd.ExecuteNonQueryAsync();
                
                if (affected > 0)
                {
                    // Move files to Trash
                    var config = await _networkConfig.LoadAsync();
                    var datePrefix = DateTime.UtcNow.ToString("yyyy-MM-dd");
                    
                    foreach (var filePath in filePaths)
                    {
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                var fileName = Path.GetFileName(filePath);
                                var trashLocalDir = Path.Combine(config.LocalBasePath, "_Trash", "local", datePrefix);
                                Directory.CreateDirectory(trashLocalDir);
                                
                                var newTrashPath = Path.Combine(trashLocalDir, $"{id}_{fileName}");
                                File.Move(filePath, newTrashPath, true);

                                // Try move network too if applicable
                                if (config.UseNetworkStorage)
                                {
                                    var oldNetworkPath = Path.Combine(config.NetworkBasePath, "Documentos", folderName, fileName);
                                    if (File.Exists(oldNetworkPath))
                                    {
                                        var trashNetworkDir = Path.Combine(config.NetworkBasePath, "_Trash", "network", datePrefix);
                                        Directory.CreateDirectory(trashNetworkDir);
                                        var newNetworkTrashPath = Path.Combine(trashNetworkDir, $"{id}_{fileName}");
                                        File.Move(oldNetworkPath, newNetworkTrashPath, true);
                                    }
                                }
                            }
                        }
                        catch 
                        { 
                            // Continue even if file move fails
                        }
                    }
                }
                return affected > 0;
            }
        }
        catch (Exception)
        {
            // Log error
            return false;
        }
    }

    public async Task<bool> UpdateDocumentAsync(Guid id, QMSFlowDoc.Shared.DTOs.CreateDocumentRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Get Current FolderId before update
        Guid? oldFolderId = null;
        var existingSql = "SELECT FolderId FROM Documents WHERE Id = $id";
        using (var checkCmd = new SqliteCommand(existingSql, connection))
        {
            checkCmd.Parameters.AddWithValue("$id", id.ToString());
            var result = await checkCmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value) oldFolderId = Guid.Parse(result.ToString()!);
        }

        var sql = @"
            UPDATE Documents 
            SET DocCode = $code, 
                Title = $title, 
                DocumentTypeId = $typeId, 
                FolderId = $folderId, 
                Area = $area, 
                Process = $process, 
                Status = $status,
                ReviewIntervalMonths = $interval,
                UpdatedAt = $updated
            WHERE Id = $id
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$code", request.DocCode);
        command.Parameters.AddWithValue("$title", request.Title);
        command.Parameters.AddWithValue("$typeId", request.DocumentTypeId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$folderId", request.FolderId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$area", request.Area ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$process", request.Process ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$status", request.Status?.ToString() ?? DocumentStatus.DRAFT.ToString());
        command.Parameters.AddWithValue("$interval", request.ReviewIntervalMonths);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));

        var affected = await command.ExecuteNonQueryAsync();

        if (affected > 0)
        {
            // Detect Folder Change
            if (oldFolderId != request.FolderId)
            {
                await HandleFolderMoveAsync(id, oldFolderId, request.FolderId);
            }

            // Also update Version Label if provided
            if (!string.IsNullOrWhiteSpace(request.VersionLabel))
            {
                var versionSql = "UPDATE DocumentVersions SET VersionLabel = $label WHERE DocumentId = $id AND IsCurrent = 1";
                using var versionCmd = new SqliteCommand(versionSql, connection);
                versionCmd.Parameters.AddWithValue("$label", request.VersionLabel);
                versionCmd.Parameters.AddWithValue("$id", id.ToString());
                await versionCmd.ExecuteNonQueryAsync();
            }
        }

        return affected > 0;
    }

    public async Task<bool> UpdateDocumentStatusAsync(Guid id, DocumentStatus status)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "UPDATE Documents SET Status = $status, UpdatedAt = $updated WHERE Id = $id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private async Task HandleFolderMoveAsync(Guid docId, Guid? oldFolderId, Guid? newFolderId)
    {
        var config = await _networkConfig.LoadAsync();
        var oldFolderName = oldFolderId.HasValue ? await GetFolderNameByIdAsync(oldFolderId.Value) : "General";
        var newFolderName = newFolderId.HasValue ? await GetFolderNameByIdAsync(newFolderId.Value) : "General";

        if (string.IsNullOrEmpty(oldFolderName)) oldFolderName = "General";
        if (string.IsNullOrEmpty(newFolderName)) newFolderName = "General";

        var versions = await GetVersionsAsync(docId);
        foreach (var version in versions)
        {
            if (string.IsNullOrEmpty(version.LocalFilePath)) continue;

            try
            {
                // 1. Local Move
                var fileName = Path.GetFileName(version.LocalFilePath);
                var newLocalPath = Path.Combine(config.LocalBasePath, "Documentos", newFolderName, fileName);
                
                if (File.Exists(version.LocalFilePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newLocalPath)!);
                    File.Move(version.LocalFilePath, newLocalPath, true);
                }

                // 2. Network Move
                if (config.UseNetworkStorage)
                {
                    var oldNetworkPath = Path.Combine(config.NetworkBasePath, "Documentos", oldFolderName, fileName);
                    var newNetworkPath = Path.Combine(config.NetworkBasePath, "Documentos", newFolderName, fileName);
                    
                    if (File.Exists(oldNetworkPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newNetworkPath)!);
                        File.Move(oldNetworkPath, newNetworkPath, true);
                    }
                }

                // 3. Update DB Path
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();
                var updateSql = "UPDATE DocumentVersions SET LocalFilePath = $path WHERE Id = $id";
                using var cmd = new SqliteCommand(updateSql, connection);
                cmd.Parameters.AddWithValue("$path", newLocalPath);
                cmd.Parameters.AddWithValue("$id", version.Id.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Log or handle error? For now continue
                System.Diagnostics.Debug.WriteLine($"Error moving file for version {version.Id}: {ex.Message}");
            }
        }
    }

    public async Task<Document?> GetDocumentByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Documents WHERE Id = $id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var doc = MapDocument(reader);
            doc.Versions = await GetVersionsAsync(doc.Id);
            return doc;
        }
        return null;
    }

    public async Task<List<Document>> GetAllDocumentsAsync(bool includeObsolete = false)
    {
        var documents = new List<Document>();
        
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM Documents";
        if (!includeObsolete)
        {
            sql += " WHERE Status <> 'OBSOLETE'";
        }
        sql += " ORDER BY UpdatedAt DESC";

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var doc = MapDocument(reader);
            doc.Versions = await GetVersionsAsync(doc.Id); // Load versions for each doc
            documents.Add(doc);
        }

        return documents;
    }

    public async Task LogAuditAsync(AuditLog log)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO AuditLogs (Id, Timestamp, UserId, UserName, Action, EntityType, EntityId, Details, MachineName)
            VALUES ($id, $ts, $userId, $userName, $action, $entityType, $entityId, $details, $machine)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", log.Id.ToString());
        command.Parameters.AddWithValue("$ts", log.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$userId", log.UserId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$userName", log.UserName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$action", log.Action);
        command.Parameters.AddWithValue("$entityType", log.EntityType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$entityId", log.EntityId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$details", log.Details ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$machine", log.MachineName ?? Environment.MachineName);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DocumentVersion> AddVersionAsync(DocumentVersion version)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO DocumentVersions 
            (Id, DocumentId, VersionMajor, VersionMinor, VersionLabel, ChangeSummary, CreatedByUserId, CreatedAt, EffectiveFrom, LocalFilePath, FileName, MimeType, Sha256, IsCurrent)
            VALUES ($id, $docId, $major, $minor, $label, $summary, $userId, $created, $effective, $path, $fileName, $mime, $sha256, $current)
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", version.Id.ToString());
        command.Parameters.AddWithValue("$docId", version.DocumentId.ToString());
        command.Parameters.AddWithValue("$major", version.VersionMajor);
        command.Parameters.AddWithValue("$minor", version.VersionMinor);
        command.Parameters.AddWithValue("$label", version.VersionLabel);
        command.Parameters.AddWithValue("$summary", version.ChangeSummary ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$userId", version.CreatedByUserId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$created", version.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$effective", version.EffectiveFrom?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$path", version.LocalFilePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$fileName", version.FileName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$mime", version.MimeType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sha256", version.Sha256 ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$current", version.IsCurrent ? 1 : 0);

        await command.ExecuteNonQueryAsync();
        return version;
    }

    /// <summary>
    /// Create document with file - copies to local/network folders automatically
    /// </summary>
    public async Task<(Document Document, DocumentVersion Version)> CreateDocumentWithFileAsync(
        string docCode,
        string title,
        DocumentStatus status,
        Guid? documentTypeId,
        int? reviewIntervalMonths,
        string versionLabel,
        string? area,                   // Added
        string? process,                // Added
        byte[] fileBytes,
        string fileName,
        string subFolderName)
    {
        // 0. PDF Restriction
        if (!Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se permiten archivos .PDF en el gestor documental.");
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // 1. Resolve FolderId
        Guid? folderId = null;
        if (!string.IsNullOrWhiteSpace(subFolderName))
        {
            folderId = await FindOrCreateFolderIdAsync(subFolderName);
        }

        // Calculate Next Review
        DateTime? nextReview = null;
        if (reviewIntervalMonths.HasValue && reviewIntervalMonths.Value > 0)
        {
            nextReview = DateTime.UtcNow.AddMonths(reviewIntervalMonths.Value);
        }

        // 2. Create document
        var document = new Document
        {
            Id = Guid.NewGuid(),
            DocCode = docCode,
            Title = title,
            Status = status,
            FolderId = folderId,
            DocumentTypeId = documentTypeId,     // Set Type
            Area = area,                         // Set Area
            Process = process,                   // Set Process
            ReviewIntervalMonths = reviewIntervalMonths ?? 12, // Set Interval
            NextReviewDue = nextReview,          // Set Next Review
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Check if document already exists
        var existingDoc = await GetDocumentByIdAsync(document.Id);
        if (existingDoc == null)
        {
            // Try find by code if ID is new (e.g. from UI that doesn't know ID yet)
            var sqlFind = "SELECT Id FROM Documents WHERE DocCode = $code";
            using (var cmdFind = new SqliteCommand(sqlFind, connection))
            {
                cmdFind.Parameters.AddWithValue("$code", docCode);
                var result = await cmdFind.ExecuteScalarAsync();
                if (result != null) existingDoc = await GetDocumentByIdAsync(Guid.Parse(result.ToString()!));
            }
        }

        if (existingDoc != null)
        {
            document.Id = existingDoc.Id; // Sync IDs
            await ArchiveOldVersionsAsync(existingDoc.Id, connection);
            
            // Update metadata
            var sqlUpdate = "UPDATE Documents SET Title=$title, DocumentTypeId=$typeId, FolderId=$folderId, Area=$area, Process=$process, UpdatedAt=$updated WHERE Id=$id";
            using var cmdUpdate = new SqliteCommand(sqlUpdate, connection);
            cmdUpdate.Parameters.AddWithValue("$id", document.Id.ToString());
            cmdUpdate.Parameters.AddWithValue("$title", title);
            cmdUpdate.Parameters.AddWithValue("$typeId", documentTypeId?.ToString() ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$folderId", folderId?.ToString() ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$area", area ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$process", process ?? (object)DBNull.Value);
            cmdUpdate.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O"));
            await cmdUpdate.ExecuteNonQueryAsync();
        }
        else
        {
            await CreateDocumentAsync(document);
        }

        // 2. Determine file paths
        var config = await _networkConfig.LoadAsync();
        string localPath, networkPath;

        if (config != null && config.UseNetworkStorage)
        {
            // Use configured paths with subfolder
            // config.LocalBasePath is typically: C:\...\QMS
            // We want: QMS\Documentos\[category]\[file]
            var category = !string.IsNullOrWhiteSpace(subFolderName) ? subFolderName : "General";
            var fileNameSafe = $"{docCode}_v1.0{Path.GetExtension(fileName)}";

            localPath = Path.Combine(config.LocalBasePath, "Documentos", category, fileNameSafe);
            networkPath = Path.Combine(config.NetworkBasePath, "Documentos", category, fileNameSafe);
        }
        else
        {
            // Fallback to default location
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var defaultFolder = Path.Combine(localAppData, "QMSFlowDoc", "Files");
            Directory.CreateDirectory(defaultFolder);
            localPath = Path.Combine(defaultFolder, $"{docCode}_v1.0{Path.GetExtension(fileName)}");
            networkPath = localPath; // Same as local if no network
        }

        // 3. Copy file to local folder
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await File.WriteAllBytesAsync(localPath, fileBytes);

        // 4. Copy to network if different
        if (localPath != networkPath && config?.UseNetworkStorage == true)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(networkPath)!);
                await File.WriteAllBytesAsync(networkPath, fileBytes);
            }
            catch
            {
                // Network copy failed - will sync later
            }
        }

        // 5. Create version record
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionMajor = 1, 
            VersionMinor = 0,
            VersionLabel = !string.IsNullOrWhiteSpace(versionLabel) ? versionLabel : "1.0", // Use user version
            ChangeSummary = "Initial version via Local Mode",
            CreatedAt = DateTime.UtcNow,
            FileName = fileName,
            LocalFilePath = localPath,
            MimeType = "application/pdf",
            IsCurrent = true
        };

        await AddVersionAsync(version);

        return (document, version);
    }

    private async Task ArchiveOldVersionsAsync(Guid documentId, SqliteConnection connection)
    {
        var versions = await GetVersionsAsync(documentId);
        var config = await _networkConfig.LoadAsync();
        var dateStr = DateTime.Now.ToString("dd-MM-yy");

        foreach (var v in versions)
        {
            if (v.IsCurrent && !string.IsNullOrEmpty(v.LocalFilePath) && File.Exists(v.LocalFilePath))
            {
                try
                {
                    var oldFileName = v.FileName ?? Path.GetFileName(v.LocalFilePath);
                    var archivedName = $"RETIRADO_{dateStr}_{oldFileName}";
                    var archiveLocalDir = Path.Combine(config.LocalBasePath, "Documentos", "VERSIONES ANTIGUAS");
                    Directory.CreateDirectory(archiveLocalDir);

                    var newPath = Path.Combine(archiveLocalDir, archivedName);
                    File.Move(v.LocalFilePath, newPath, true);

                    // Update DB record
                    var sql = "UPDATE DocumentVersions SET LocalFilePath = $path, IsCurrent = 0 WHERE Id = $id";
                    using var cmd = new SqliteCommand(sql, connection);
                    cmd.Parameters.AddWithValue("$path", newPath);
                    cmd.Parameters.AddWithValue("$id", v.Id.ToString());
                    await cmd.ExecuteNonQueryAsync();

                    // Optional: Try archive network too
                    if (config.UseNetworkStorage && !string.IsNullOrEmpty(config.NetworkBasePath))
                    {
                         try
                         {
                             // Calculate the relative path from local base to the old file
                             var relPath = Path.GetRelativePath(config.LocalBasePath, v.LocalFilePath);
                             var oldNetworkPath = Path.Combine(config.NetworkBasePath, relPath);

                             if (File.Exists(oldNetworkPath))
                             {
                                 var archiveNetworkDir = Path.Combine(config.NetworkBasePath, "Documentos", "VERSIONES ANTIGUAS");
                                 Directory.CreateDirectory(archiveNetworkDir);
                                 var newNetworkPath = Path.Combine(archiveNetworkDir, archivedName);
                                 File.Move(oldNetworkPath, newNetworkPath, true);
                             }
                         }
                         catch (Exception netEx)
                         {
                             System.Diagnostics.Debug.WriteLine($"Network archival error: {netEx.Message}");
                         }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Archival error: {ex.Message}");
                }
            }
            else if (v.IsCurrent)
            {
                // Mark as not current even if file missing
                var sql = "UPDATE DocumentVersions SET IsCurrent = 0 WHERE Id = $id";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("$id", v.Id.ToString());
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<Guid> FindOrCreateFolderIdAsync(string folderName)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        // Try find existing
        var selectSql = "SELECT Id FROM Folders WHERE Name = $name COLLATE NOCASE LIMIT 1";
        using (var cmd = new SqliteCommand(selectSql, connection))
        {
            cmd.Parameters.AddWithValue("$name", folderName);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return Guid.Parse(result.ToString()!);
            }
        }

        // Create new
        var newId = Guid.NewGuid();
        var insertSql = "INSERT INTO Folders (Id, Name, CreatedAt) VALUES ($id, $name, $created)";
        using (var cmd = new SqliteCommand(insertSql, connection))
        {
            cmd.Parameters.AddWithValue("$id", newId.ToString());
            cmd.Parameters.AddWithValue("$name", folderName);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        return newId;
    }

    private Document MapDocument(SqliteDataReader reader)
    {
        return new Document
        {
            Id = Guid.Parse(reader.GetString(0)),
            DocCode = reader.GetString(1),
            Title = reader.GetString(2),
            DocumentTypeId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
            FolderId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
            Area = reader.IsDBNull(5) ? null : reader.GetString(5),
            Process = reader.IsDBNull(6) ? null : reader.GetString(6),
            OwnerUserId = reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)),
            Status = Enum.Parse<DocumentStatus>(reader.GetString(8)),
            ReviewIntervalMonths = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            NextReviewDue = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10)),
            CreatedAt = DateTime.Parse(reader.GetString(11)),
            UpdatedAt = DateTime.Parse(reader.GetString(12))
        };
    }

    public async Task<List<DocumentType>> GetDocumentTypesAsync()
    {
        var types = new List<DocumentType>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM DocumentTypes ORDER BY Name";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            types.Add(new DocumentType
            {
                Id = Guid.Parse(reader.GetString(0)),
                TypeCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        return types;
    }

    private async Task<List<DocumentVersion>> GetVersionsAsync(Guid docId)
    {
        var versions = new List<DocumentVersion>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = "SELECT * FROM DocumentVersions WHERE DocumentId = $docId ORDER BY CreatedAt DESC";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$docId", docId.ToString());

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            versions.Add(new DocumentVersion
            {
                Id = Guid.Parse(reader.GetString(0)),
                DocumentId = Guid.Parse(reader.GetString(1)),
                VersionMajor = reader.GetInt32(2),
                VersionMinor = reader.GetInt32(3),
                VersionLabel = reader.GetString(4),
                ChangeSummary = reader.IsDBNull(5) ? "" : reader.GetString(5),
                CreatedByUserId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                EffectiveFrom = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                LocalFilePath = reader.IsDBNull(9) ? "" : reader.GetString(9),
                FileName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                MimeType = reader.IsDBNull(11) ? "" : reader.GetString(11),
                Sha256 = reader.IsDBNull(12) ? "" : reader.GetString(12),
                IsCurrent = reader.GetInt32(13) == 1
            });
        }
        return versions;
    }
    public async Task<List<FolderDto>> GetFoldersAsync()
    {
        var folders = new List<FolderDto>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"
            SELECT f.Id, f.Name, f.ParentFolderId, 
            (SELECT COUNT(*) FROM Folders sub WHERE sub.ParentFolderId = f.Id) as SubCount,
            (SELECT COUNT(*) FROM Documents d WHERE d.FolderId = f.Id) as DocCount
            FROM Folders f
            ORDER BY f.Name";
            
        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            folders.Add(new FolderDto(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
                reader.GetInt32(3),
                reader.GetInt32(4)
            ));
        }
        return folders;
    }

    public async Task<bool> CreateFolderAsync(string name, Guid? parentId)
    {
        // 1. Check if folder already exists (read-only check)
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        // Find existing ID
        var checkSql = "SELECT Id FROM Folders WHERE Name = $name COLLATE NOCASE LIMIT 1";
        Guid? existingId = null;
        using (var cmd = new SqliteCommand(checkSql, connection))
        {
            cmd.Parameters.AddWithValue("$name", name);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                existingId = Guid.Parse(result.ToString()!);
            }
        }

        if (existingId.HasValue)
        {
             // Already exists. Return true as "success" (idempotent) or false if we want to signal "already existed"
             // For UI "Create Folder", usually we don't want to create duplicates. 
             // Returning true implies "folder is there now".
             return true; 
        }

        // 2. Create new
        var id = Guid.NewGuid();
        var insertSql = "INSERT INTO Folders (Id, Name, ParentFolderId, CreatedAt) VALUES ($id, $name, $parent, $created)";
        
        using (var cmd = new SqliteCommand(insertSql, connection))
        {
            cmd.Parameters.AddWithValue("$id", id.ToString());
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$parent", parentId?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }

    public async Task<bool> RenameFolderAsync(Guid id, string newName)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "UPDATE Folders SET Name = $name WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$name", newName);
        
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteFolderAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "DELETE FROM Folders WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
    // Document Types
    public async Task<DocumentType?> GetDocumentTypeByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM DocumentTypes WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new DocumentType
            {
                Id = Guid.Parse(reader.GetString(0)),
                TypeCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            };
        }
        return null;
    }

    public async Task<string?> GetFolderNameByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var sql = "SELECT Name FROM Folders WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    // Training CRUD
    public async Task RegisterTrainingAsync(Guid staffId, string title, string? provider, decimal hours, DateTime completed, string result, string? notes)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"INSERT INTO StaffTrainings (Id, StaffId, Title, Provider, Hours, CompletedAt, Result, Notes)
                    VALUES ($id, $sid, $title, $prov, $hours, $comp, $res, $notes)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$sid", staffId.ToString());
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$prov", provider ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$hours", hours);
        cmd.Parameters.AddWithValue("$comp", completed.ToString("o"));
        cmd.Parameters.AddWithValue("$res", result);
        cmd.Parameters.AddWithValue("$notes", notes ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DeleteTrainingAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        using var cmd = new SqliteCommand("DELETE FROM StaffTrainings WHERE Id = $id", connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // Competency CRUD
    public async Task<IEnumerable<CompetencyEvaluationDto>> GetStaffEvaluationsAsync(Guid staffId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT Id, CompetencyName, Area, EvaluationDate, ValidUntil, Outcome, EvaluatorName FROM CompetencyEvaluations WHERE StaffId = $sid";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$sid", staffId.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<CompetencyEvaluationDto>();
        while (await reader.ReadAsync())
        {
            list.Add(new CompetencyEvaluationDto(
                Guid.Parse(reader.GetString(0)),
                Guid.Empty,
                reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? (DateTime?)null : DateTime.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? "Pending" : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)
            ));
        }
        return list;
    }

    public async Task AssessCompetencyAsync(AssessCompetencyRequest req)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"INSERT INTO CompetencyEvaluations (Id, StaffId, CompetencyName, Area, EvaluationDate, ValidUntil, Outcome, Evidence, EvaluatorName)
                    VALUES ($id, $sid, $name, $area, $date, $valid, $out, $evid, $eval)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$sid", req.StaffId.ToString());
        cmd.Parameters.AddWithValue("$name", req.CompetencyName);
        cmd.Parameters.AddWithValue("$area", req.Area);
        cmd.Parameters.AddWithValue("$date", req.EvaluationDate.ToString("o"));
        cmd.Parameters.AddWithValue("$valid", req.ValidUntil?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$out", req.Outcome.ToString());
        cmd.Parameters.AddWithValue("$evid", req.Evidence ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$eval", "Local Admin");
        await cmd.ExecuteNonQueryAsync();
    }
    
    public async Task<bool> DeleteEvaluationAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = new SqliteCommand("DELETE FROM CompetencyEvaluations WHERE Id = $id", connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // Authorization CRUD
    public async Task<IEnumerable<StaffAuthorizationDto>> GetStaffAuthorizationsAsync(Guid staffId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT Id, TaskName, Description, ValidFrom, ValidUntil, GrantedAt, Status, GrantedByName FROM StaffAuthorizations WHERE StaffId = $sid";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$sid", staffId.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<StaffAuthorizationDto>();
        while (await reader.ReadAsync())
        {
            list.Add(new StaffAuthorizationDto(
                Guid.Parse(reader.GetString(0)),
                Guid.Empty,
                reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? (DateTime?)null : DateTime.Parse(reader.GetString(4)),
                DateTime.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? "Active" : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)
            ));
        }
        return list;
    }

    public async Task GrantAuthorizationAsync(GrantAuthorizationRequest req)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"INSERT INTO StaffAuthorizations (Id, StaffId, TaskName, Description, ValidFrom, ValidUntil, GrantedAt, Status, GrantedByName)
                    VALUES ($id, $sid, $name, $desc, $from, $until, $granted, $status, $by)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$sid", req.StaffId.ToString());
        cmd.Parameters.AddWithValue("$name", req.TaskName);
        cmd.Parameters.AddWithValue("$desc", req.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$from", req.ValidFrom.ToString("o"));
        cmd.Parameters.AddWithValue("$until", req.ValidUntil?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$granted", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$status", "Active");
        cmd.Parameters.AddWithValue("$by", "Local Admin");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> DeleteAuthorizationAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = new SqliteCommand("DELETE FROM StaffAuthorizations WHERE Id = $id", connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
    // Risk CRUD
    public async Task<IEnumerable<RiskListDto>> GetRisksAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT Id, Title, Description, Category, Likelihood, Impact, Status, OwnerUserId FROM Risks";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<RiskListDto>();
        while (await reader.ReadAsync())
        {
            var l = (RiskLikelihood)reader.GetInt32(4);
            var i = (RiskImpact)reader.GetInt32(5);
            list.Add(new RiskListDto(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(3) ? "OPERATIONAL" : reader.GetString(3),
                (int)l * (int)i, // Score
                (RiskStatus)reader.GetInt32(6),
                l,
                i
            ));
        }
        return list;
    }

    public async Task<Risk?> GetRiskByIdAsync(Guid id)
    {
         using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM Risks WHERE Id = $id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return new Risk
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Category = reader.IsDBNull(3) ? "OPERATIONAL" : reader.GetString(3),
                Likelihood = (RiskLikelihood)reader.GetInt32(4),
                Impact = (RiskImpact)reader.GetInt32(5),
                MitigationPlan = reader.IsDBNull(6) ? null : reader.GetString(6),
                OwnerUserId = reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7)),
                Status = (RiskStatus)reader.GetInt32(8),
                CreatedAt = DateTime.Parse(reader.GetString(9)),
                UpdatedAt = reader.IsDBNull(10) ? DateTime.MinValue : DateTime.Parse(reader.GetString(10))
            };
        }
        return null;
    }

    public async Task<Risk> CreateRiskAsync(CreateRiskRequest req)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var id = Guid.NewGuid();
        var sql = @"INSERT INTO Risks (Id, Title, Description, Category, Likelihood, Impact, MitigationPlan, OwnerUserId, Status, CreatedAt, UpdatedAt)
                    VALUES ($id, $title, $desc, $cat, $like, $imp, $mit, $owner, $stat, $created, $updated)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$title", req.Title);
        cmd.Parameters.AddWithValue("$desc", req.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$cat", req.Category);
        cmd.Parameters.AddWithValue("$like", (int)req.Likelihood);
        cmd.Parameters.AddWithValue("$imp", (int)req.Impact);
        cmd.Parameters.AddWithValue("$mit", req.MitigationPlan ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$owner", req.OwnerUserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$stat", 0); // Active
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        
        await cmd.ExecuteNonQueryAsync();
        
        return new Risk
        {
            Id = id,
            Title = req.Title,
            Description = req.Description ?? "",
            Category = req.Category,
            Likelihood = req.Likelihood,
            Impact = req.Impact,
            MitigationPlan = req.MitigationPlan,
            OwnerUserId = req.OwnerUserId,
            Status = RiskStatus.ACTIVE
        };
    }

    public async Task<bool> UpdateRiskAsync(Guid id, CreateRiskRequest req)
    {
         using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = @"UPDATE Risks SET Title=$title, Description=$desc, Category=$cat, Likelihood=$like, Impact=$imp, MitigationPlan=$mit, OwnerUserId=$owner, UpdatedAt=$updated WHERE Id=$id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$title", req.Title);
        cmd.Parameters.AddWithValue("$desc", req.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$cat", req.Category);
        cmd.Parameters.AddWithValue("$like", (int)req.Likelihood);
        cmd.Parameters.AddWithValue("$imp", (int)req.Impact);
        cmd.Parameters.AddWithValue("$mit", req.MitigationPlan ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$owner", req.OwnerUserId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

     public async Task<bool> UpdateRiskStatusAsync(Guid id, int status)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = new SqliteCommand("UPDATE Risks SET Status=$stat, UpdatedAt=$up WHERE Id=$id", connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$stat", status);
        cmd.Parameters.AddWithValue("$up", DateTime.UtcNow.ToString("o"));
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // Audit CRUD
    public async Task<IEnumerable<AuditListDto>> GetAuditsAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT Id, Title, ScheduledDate, Status, LeadAuditor, ReportDocumentId FROM AuditPlans";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        
        var list = new List<AuditListDto>();
        while (await reader.ReadAsync())
        {
            list.Add(new AuditListDto(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2)),
                (AuditStatus)reader.GetInt32(3),
                0, // Finding count dummy for list
                reader.IsDBNull(5) ? null : Guid.Parse(reader.GetString(5))
            ));
        }
        return list;
    }

    public async Task<AuditPlan?> GetAuditByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT * FROM AuditPlans WHERE Id=$id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            var plan = new AuditPlan
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                ScheduledDate = DateTime.Parse(reader.GetString(2)),
                Scope = reader.IsDBNull(3) ? "" : reader.GetString(3),
                LeadAuditor = reader.IsDBNull(4) ? null : reader.GetString(4),
                Status = (AuditStatus)reader.GetInt32(5),
                SummaryReport = reader.IsDBNull(6) ? null : reader.GetString(6),
                ReportDocumentId = reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7))
            };
            
            // Load Findings
            reader.Close();
            var fSql = "SELECT Id, Description, IsoRequirement, Type, RelatedNCId FROM AuditFindings WHERE AuditPlanId=$pid";
            using var fCmd = new SqliteCommand(fSql, connection);
            fCmd.Parameters.AddWithValue("$pid", id.ToString());
            using var fReader = await fCmd.ExecuteReaderAsync();
            
            while (await fReader.ReadAsync())
            {
                plan.Findings.Add(new AuditFinding
                {
                    Id = Guid.Parse(fReader.GetString(0)),
                    AuditPlanId = id,
                    Description = fReader.GetString(1),
                    IsoRequirement = fReader.IsDBNull(2) ? null : fReader.GetString(2),
                    Type = (FindingType)fReader.GetInt32(3),
                    RelatedNCId = fReader.IsDBNull(4) ? null : Guid.Parse(fReader.GetString(4))
                });
            }
            return plan;
        }
        return null;
    }

    public async Task<AuditPlan> CreateAuditAsync(CreateAuditRequest req)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var id = Guid.NewGuid();
        var sql = @"INSERT INTO AuditPlans (Id, Title, ScheduledDate, Scope, LeadAuditor, Status, ReportDocumentId)
                    VALUES ($id, $title, $date, $scope, $lead, 0, $rep)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$title", req.Title);
        cmd.Parameters.AddWithValue("$date", req.ScheduledDate.ToString("o"));
        cmd.Parameters.AddWithValue("$scope", req.Scope ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$lead", req.LeadAuditor ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$rep", req.ReportDocumentId?.ToString() ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
        
        return new AuditPlan
        {
            Id = id,
            Title = req.Title,
            ScheduledDate = req.ScheduledDate,
            Scope = req.Scope ?? "",
            LeadAuditor = req.LeadAuditor,
            Status = AuditStatus.PLANNED,
            ReportDocumentId = req.ReportDocumentId
        };
    }

    public async Task<bool> UpdateAuditAsync(Guid id, CreateAuditRequest req)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "UPDATE AuditPlans SET Title=$title, ScheduledDate=$date, Scope=$scope, LeadAuditor=$lead, ReportDocumentId=$rep WHERE Id=$id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$title", req.Title);
        cmd.Parameters.AddWithValue("$date", req.ScheduledDate.ToString("o"));
        cmd.Parameters.AddWithValue("$scope", req.Scope ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$lead", req.LeadAuditor ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$rep", req.ReportDocumentId?.ToString() ?? (object)DBNull.Value);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<AuditFinding> RegisterFindingAsync(RegisterFindingRequest req)
    {
         using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var id = Guid.NewGuid();
        var sql = @"INSERT INTO AuditFindings (Id, AuditPlanId, Description, IsoRequirement, Type)
                    VALUES ($id, $pid, $desc, $iso, $type)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$pid", req.AuditPlanId.ToString());
        cmd.Parameters.AddWithValue("$desc", req.Description);
        cmd.Parameters.AddWithValue("$iso", req.IsoRequirement ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$type", (int)req.Type);
        
        await cmd.ExecuteNonQueryAsync();
        
        return new AuditFinding
        {
             Id = id,
             AuditPlanId = req.AuditPlanId,
             Description = req.Description,
             IsoRequirement = req.IsoRequirement,
             Type = req.Type
        };
    }

    public async Task<bool> DeleteAuditAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = new SqliteCommand("DELETE FROM AuditPlans WHERE Id=$id", connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // Review CRUD
    public async Task<IEnumerable<ManagementReviewListDto>> GetReviewsAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        
        var sql = "SELECT Id, ReviewDate, Summary, MinutesDocumentId FROM ManagementReviews";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<ManagementReviewListDto>();
        while(await reader.ReadAsync())
        {
            list.Add(new ManagementReviewListDto(
                Guid.Parse(reader.GetString(0)),
                DateTime.Parse(reader.GetString(1)),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3))
            ));
        }
        return list;
    }

    public async Task<ManagementReview?> GetReviewByIdAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var sql = "SELECT * FROM ManagementReviews WHERE Id=$id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ManagementReview
            {
                Id = Guid.Parse(reader.GetString(0)),
                ReviewDate = DateTime.Parse(reader.GetString(1)),
                Participants = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Agenda = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Summary = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Actions = reader.IsDBNull(5) ? null : reader.GetString(5),
                MinutesDocumentId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6))
            };
        }
        return null;
    }

    public async Task<ManagementReview> CreateReviewAsync(CreateManagementReviewRequest req)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
         var id = Guid.NewGuid();
         var sql = @"INSERT INTO ManagementReviews (Id, ReviewDate, Participants, Agenda, Summary, Actions, MinutesDocumentId)
                     VALUES ($id, $date, $part, $agenda, $sum, $act, $mins)";
         using var cmd = new SqliteCommand(sql, connection);
         cmd.Parameters.AddWithValue("$id", id.ToString());
         cmd.Parameters.AddWithValue("$date", req.ReviewDate.ToString("o"));
         cmd.Parameters.AddWithValue("$part", req.Participants);
         cmd.Parameters.AddWithValue("$agenda", req.Agenda);
         cmd.Parameters.AddWithValue("$sum", req.Summary);
         cmd.Parameters.AddWithValue("$act", req.Actions ?? (object)DBNull.Value);
         cmd.Parameters.AddWithValue("$mins", req.MinutesDocumentId?.ToString() ?? (object)DBNull.Value);
         await cmd.ExecuteNonQueryAsync();
         
         return new ManagementReview
         {
             Id = id,
             ReviewDate = req.ReviewDate,
             Participants = req.Participants,
             Agenda = req.Agenda,
             Summary = req.Summary,
             Actions = req.Actions,
             MinutesDocumentId = req.MinutesDocumentId
         };
    }

    public async Task<bool> UpdateReviewAsync(Guid id, CreateManagementReviewRequest req)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var sql = "UPDATE ManagementReviews SET ReviewDate=$date, Participants=$part, Agenda=$agenda, Summary=$sum, Actions=$act, MinutesDocumentId=$mins WHERE Id=$id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$date", req.ReviewDate.ToString("o"));
        cmd.Parameters.AddWithValue("$part", req.Participants);
        cmd.Parameters.AddWithValue("$agenda", req.Agenda);
        cmd.Parameters.AddWithValue("$sum", req.Summary);
        cmd.Parameters.AddWithValue("$act", req.Actions ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$mins", req.MinutesDocumentId?.ToString() ?? (object)DBNull.Value);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteReviewAsync(Guid id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        using var cmd = new SqliteCommand("DELETE FROM ManagementReviews WHERE Id=$id", connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        return await cmd.ExecuteNonQueryAsync() > 0;
    }


    // --- EQA Module ---

    public async Task<EQAProgram> CreateEQAProgramAsync(CreateEQAProgramRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow.ToString("o");

        var sql = @"INSERT INTO EQAPrograms (Id, Name, Provider, CycleFrequency, Status, Notes, CreatedAt, UpdatedAt)
                    VALUES ($id, $name, $prov, $freq, $status, $notes, $created, $updated)";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$name", request.Name);
        cmd.Parameters.AddWithValue("$prov", request.Provider ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$freq", request.Frequency ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)EQAStatus.ACTIVE);
        cmd.Parameters.AddWithValue("$notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);

        await cmd.ExecuteNonQueryAsync();

        return new EQAProgram
        {
            Id = id,
            Name = request.Name,
            Provider = request.Provider,
            CycleFrequency = request.Frequency,
            Status = EQAStatus.ACTIVE,
            Notes = request.Notes
        };
    }

    public async Task UpdateEQAProgramAsync(UpdateEQAProgramRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"UPDATE EQAPrograms 
                    SET Name=$name, Provider=$prov, CycleFrequency=$freq, Status=$status, Notes=$notes, UpdatedAt=$updated
                    WHERE Id=$id";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", request.Id.ToString());
        cmd.Parameters.AddWithValue("$name", request.Name);
        cmd.Parameters.AddWithValue("$prov", request.Provider ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$freq", request.Frequency ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (int)request.Status);
        cmd.Parameters.AddWithValue("$notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<EQAProgramDto>> GetEQAProgramsAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var list = new List<EQAProgramDto>();
        // Get base programs first
        var sql = "SELECT * FROM EQAPrograms WHERE Status != 2 ORDER BY Name";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new EQAProgramDto(
                Guid.Parse(reader["Id"].ToString()),
                reader["Name"].ToString(),
                reader["Provider"] == DBNull.Value ? null : reader["Provider"].ToString(),
                reader["CycleFrequency"] == DBNull.Value ? null : reader["CycleFrequency"].ToString(),
                (EQAStatus)Convert.ToInt32(reader["Status"]),
                null, 
                "Gray",
                0
            ));
        }
        reader.Close();

        // Enrich with stats
        var enrichedList = new List<EQAProgramDto>();
        foreach (var p in list)
        {
            var lastSql = "SELECT Performance, CycleIdentifier FROM EQAResults WHERE ProgramId=$pid AND Status=2 ORDER BY SubmissionDate DESC LIMIT 1"; 
            using var lastCmd = new SqliteCommand(lastSql, connection);
            lastCmd.Parameters.AddWithValue("$pid", p.Id.ToString());
            using var lastReader = await lastCmd.ExecuteReaderAsync();
            string lastRes = "No Data";
            string color = "Gray";
            if (await lastReader.ReadAsync())
            {
                var perf = (EQAPerformance)Convert.ToInt32(lastReader["Performance"]);
                var cycle = lastReader["CycleIdentifier"].ToString();
                color = perf == EQAPerformance.SATISFACTORY ? "Green" : (perf == EQAPerformance.UNSATISFACTORY ? "Red" : "Orange");
                lastRes = $"{perf} ({cycle})";
            }
            lastReader.Close();

            var pendingSql = "SELECT COUNT(*) FROM EQAResults WHERE ProgramId=$pid AND Status IN (0, 1)"; 
            using var pendCmd = new SqliteCommand(pendingSql, connection);
            pendCmd.Parameters.AddWithValue("$pid", p.Id.ToString());
            int pending = Convert.ToInt32(await pendCmd.ExecuteScalarAsync());

            enrichedList.Add(p with { LastResult = lastRes, LastResultColor = color, PendingCount = pending });
        }

        return enrichedList;
    }

    public async Task RegisterEQAResultAsync(RegisterEQAResultRequest request)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var sql = @"INSERT INTO EQAResults (Id, ProgramId, CycleIdentifier, ReceiptDate, ProcessingDate, SubmissionDate, Status, Notes, Performance)
                    VALUES ($id, $pid, $cycle, $receipt, $process, $submit, $status, $notes, $perf)";
        
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("$pid", request.ProgramId.ToString());
        cmd.Parameters.AddWithValue("$cycle", request.CycleIdentifier);
        cmd.Parameters.AddWithValue("$receipt", request.ReceiptDate?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$process", request.ProcessingDate?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$submit", request.SubmissionDate?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", request.SubmissionDate.HasValue ? 1 : 0);
        cmd.Parameters.AddWithValue("$notes", request.Notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$perf", (int)EQAPerformance.NOT_EVALUATED);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<EQAResultDto>> GetEQAResultsAsync(Guid programId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var list = new List<EQAResultDto>();
        var sql = "SELECT * FROM EQAResults WHERE ProgramId = $pid ORDER BY CycleIdentifier DESC";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("$pid", programId.ToString());
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var perf = (EQAPerformance)Convert.ToInt32(reader["Performance"]);
            var color = perf == EQAPerformance.SATISFACTORY ? "Green" : (perf == EQAPerformance.UNSATISFACTORY ? "Red" : "Orange");
            if (perf == EQAPerformance.NOT_EVALUATED) color = "Gray";

            list.Add(new EQAResultDto(
                Guid.Parse(reader["Id"].ToString()),
                Guid.Parse(reader["ProgramId"].ToString()),
                reader["CycleIdentifier"].ToString(),
                ((EQAResultStatus)Convert.ToInt32(reader["Status"])).ToString(),
                perf.ToString(),
                color,
                reader["SubmissionDate"] == DBNull.Value ? null : DateTime.Parse(reader["SubmissionDate"].ToString()),
                reader["Score"] == DBNull.Value ? null : Convert.ToDecimal(reader["Score"])
            ));
        }
        return list;
    }
}

