using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.Models;
using System.Security.Cryptography;
using System.Text;

namespace QMSFlowDoc.Api.Data;

public static class DbInitializer
{
    public static async Task Initialize(ApplicationDbContext context)
    {
        try 
        {
            await context.Database.EnsureCreatedAsync();

            // Handle schema update for Fluorescence field
            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Reagents\" ADD COLUMN IF NOT EXISTS \"Fluorescence\" text;");
            }
            catch { }

            // Handle schema update for Unit -> Reference
            try
            {
                // Safe rename for PostgreSQL using an anonymous block
                var sql = @"
                    DO $$ 
                    BEGIN 
                        IF EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='Reagents' AND column_name='Unit') 
                        AND NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                       WHERE table_name='Reagents' AND column_name='Reference') THEN
                            ALTER TABLE ""Reagents"" RENAME COLUMN ""Unit"" TO ""Reference"";
                        END IF;
                    END $$;";
                await context.Database.ExecuteSqlRawAsync(sql);
            }
            catch { }

            // Ensure Reference exists (in case it wasn't renamed or was new)
            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Reagents\" ADD COLUMN IF NOT EXISTS \"Reference\" text DEFAULT '';");
            }
            catch { }
            
            // Handle schema update for ReagentType
             try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Reagents\" ADD COLUMN IF NOT EXISTS \"ReagentType\" text DEFAULT 'Químico';");
            }
            catch { }

            // Handle schema update for InternalCode
            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Reagents\" ADD COLUMN IF NOT EXISTS \"InternalCode\" text;");
            }
            catch { }

            // Handle schema updates for MaintenanceEvent new fields
            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"MaintenanceEvents\" ADD COLUMN IF NOT EXISTS \"HasIssues\" boolean;");
            }
            catch { }

            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"MaintenanceEvents\" ADD COLUMN IF NOT EXISTS \"NextMaintenanceMonth\" integer;");
            }
            catch { }

            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"MaintenanceEvents\" ADD COLUMN IF NOT EXISTS \"NextMaintenanceYear\" integer;");
            }
            catch { }

            // ISO 15189 Phase 4 Schema Updates
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Equipment\" ADD COLUMN IF NOT EXISTS \"SoftwareVersion\" text;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Equipment\" ADD COLUMN IF NOT EXISTS \"FirmwareVersion\" text;"); } catch { }
            
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"DocumentVersions\" ADD COLUMN IF NOT EXISTS \"ApprovedByUserId\" uuid;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"DocumentVersions\" ADD COLUMN IF NOT EXISTS \"ApprovalDate\" timestamp with time zone;"); } catch { }
            
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Nonconformities\" ADD COLUMN IF NOT EXISTS \"Origin\" text;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Nonconformities\" ADD COLUMN IF NOT EXISTS \"RootCauseAnalysis\" text;"); } catch { }
            
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Reagents\" ADD COLUMN IF NOT EXISTS \"Classification\" text;"); } catch { }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"ReagentLots\" ADD COLUMN IF NOT EXISTS \"PanelId\" uuid;"); } catch { }

            // Create ISO 15189 Tables if missing
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""TrainingTypeCatalogs"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""Code"" text NOT NULL,
                        ""Name"" text NOT NULL,
                        ""IsActive"" boolean NOT NULL DEFAULT true
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""TrainingActivities"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""Title"" text NOT NULL,
                        ""Provider"" text,
                        ""TrainingTypeId"" uuid NOT NULL,
                        ""Modality"" text NOT NULL DEFAULT 'PRESENCIAL',
                        ""StartDate"" timestamp with time zone,
                        ""EndDate"" timestamp with time zone,
                        ""Hours"" numeric NOT NULL,
                        ""Credits"" text,
                        ""Description"" text,
                        ""IsInternal"" boolean NOT NULL DEFAULT false,
                        ""InternalDepartment"" text,
                        ""Status"" text NOT NULL DEFAULT 'ACTIVO',
                        ""AnnulReason"" text,
                        ""CreatedByUserId"" uuid NOT NULL,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""StaffTrainings"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""StaffId"" uuid NOT NULL,
                        ""TrainingActivityId"" uuid NOT NULL,
                        ""CompletionDate"" timestamp with time zone NOT NULL,
                        ""ParticipationRole"" text,
                        ""Result"" text,
                        ""Score"" text,
                        ""CertificateDocId"" uuid,
                        ""Notes"" text,
                        ""Status"" text NOT NULL DEFAULT 'ACTIVO',
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""AnnulReason"" text
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""CompetencyCatalogs"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""Code"" text NOT NULL,
                        ""Name"" text NOT NULL,
                        ""Description"" text,
                        ""RoleScope"" text NOT NULL DEFAULT 'AMBOS',
                        ""Area"" text NOT NULL,
                        ""SubArea"" text,
                        ""DefaultReassessmentMonths"" integer NOT NULL DEFAULT 12,
                        ""IsActive"" boolean NOT NULL DEFAULT true,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""CreatedByUserId"" uuid NOT NULL
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""CompetencyEvaluations"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""StaffId"" uuid NOT NULL,
                        ""CompetencyId"" uuid NOT NULL,
                        ""TemplateId"" uuid,
                        ""EvaluationDate"" timestamp with time zone NOT NULL,
                        ""ValidUntil"" timestamp with time zone,
                        ""NextDueDate"" timestamp with time zone,
                        ""EvaluatorStaffId"" uuid NOT NULL,
                        ""Outcome"" text NOT NULL,
                        ""Findings"" text,
                        ""CorrectiveActions"" text,
                        ""EvidenceDocId"" uuid,
                        ""Status"" text NOT NULL DEFAULT 'ACTIVO',
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""AnnulReason"" text
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""StaffCompetencyStatuses"" (
                        ""StaffId"" uuid NOT NULL,
                        ""CompetencyId"" uuid NOT NULL,
                        ""CurrentStatus"" text NOT NULL,
                        ""LastEvaluationDate"" timestamp with time zone,
                        ""LastEvaluationId"" uuid,
                        ""NextDueDate"" timestamp with time zone,
                        ""UpdatedAt"" timestamp with time zone NOT NULL,
                        PRIMARY KEY (""StaffId"", ""CompetencyId"")
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""AuthorizationCatalogs"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""Code"" text NOT NULL,
                        ""Name"" text NOT NULL,
                        ""Description"" text,
                        ""RoleScope"" text NOT NULL DEFAULT 'AMBOS',
                        ""RequiresCompetency"" boolean NOT NULL DEFAULT false,
                        ""ValidityMonths"" integer,
                        ""IsActive"" boolean NOT NULL DEFAULT true,
                        ""CreatedAt"" timestamp with time zone NOT NULL
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""StaffAuthorizations"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""StaffId"" uuid NOT NULL,
                        ""AuthorizationId"" uuid NOT NULL,
                        ""GrantedByUserId"" uuid NOT NULL,
                        ""GrantedAt"" timestamp with time zone NOT NULL,
                        ""ValidFrom"" timestamp with time zone NOT NULL,
                        ""ValidUntil"" timestamp with time zone,
                        ""Status"" text NOT NULL DEFAULT 'VIGENTE',
                        ""RevocationReason"" text,
                        ""EvidenceDocId"" uuid,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL
                    );");

                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""AuthorizationRequiredCompetency"" (
                        ""AuthorizationId"" uuid NOT NULL,
                        ""CompetencyId"" uuid NOT NULL,
                        PRIMARY KEY (""AuthorizationId"", ""CompetencyId"")
                    );");

                // Ensure columns exist in tables that might have been created without them
                await context.Database.ExecuteSqlRawAsync(@"
                    DO $$ 
                    DECLARE
                        pk_constraint_name text;
                    BEGIN 
                        -- StaffTrainings checks
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='StaffTrainings') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='Id') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""Id"" uuid NOT NULL DEFAULT gen_random_uuid();
                                
                                -- Find and drop existing PK dynamically
                                SELECT conname INTO pk_constraint_name 
                                FROM pg_constraint 
                                WHERE conrelid = '""StaffTrainings""'::regclass AND contype = 'p';
                                
                                IF pk_constraint_name IS NOT NULL THEN
                                    EXECUTE 'ALTER TABLE ""StaffTrainings"" DROP CONSTRAINT ""' || pk_constraint_name || '""';
                                END IF;

                                ALTER TABLE ""StaffTrainings"" ADD PRIMARY KEY (""Id"");
                            END IF;

                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='StaffId') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""StaffId"" uuid NOT NULL;
                            END IF;
                            -- Handle legacy TrainingId column - rename to TrainingActivityId if needed
                            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='TrainingId')
                               AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='TrainingActivityId') THEN
                                ALTER TABLE ""StaffTrainings"" RENAME COLUMN ""TrainingId"" TO ""TrainingActivityId"";
                            END IF;
                            -- Drop legacy TrainingId if TrainingActivityId already exists
                            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='TrainingId')
                               AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='TrainingActivityId') THEN
                                ALTER TABLE ""StaffTrainings"" DROP COLUMN ""TrainingId"";
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='TrainingActivityId') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""TrainingActivityId"" uuid NOT NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='CompletionDate') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""CompletionDate"" timestamp with time zone NOT NULL DEFAULT NOW();
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='ParticipationRole') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""ParticipationRole"" text DEFAULT 'ASISTENTE';
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='Result') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""Result"" text DEFAULT 'APTO';
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='Score') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""Score"" text;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='CertificateDocId') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""CertificateDocId"" uuid;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='Notes') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""Notes"" text;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='Status') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""Status"" text NOT NULL DEFAULT 'ACTIVO';
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='CreatedAt') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW();
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='StaffTrainings' AND column_name='AnnulReason') THEN
                                ALTER TABLE ""StaffTrainings"" ADD COLUMN ""AnnulReason"" text;
                            END IF;
                        END IF;

                        -- CompetencyEvaluations checks
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='CompetencyEvaluations') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='CompetencyEvaluations' AND column_name='Id') THEN
                                ALTER TABLE ""CompetencyEvaluations"" ADD COLUMN ""Id"" uuid NOT NULL DEFAULT gen_random_uuid();
                                
                                -- Find and drop existing PK dynamically
                                SELECT conname INTO pk_constraint_name 
                                FROM pg_constraint 
                                WHERE conrelid = '""CompetencyEvaluations""'::regclass AND contype = 'p';
                                
                                IF pk_constraint_name IS NOT NULL THEN
                                    EXECUTE 'ALTER TABLE ""CompetencyEvaluations"" DROP CONSTRAINT ""' || pk_constraint_name || '""';
                                END IF;

                                ALTER TABLE ""CompetencyEvaluations"" ADD PRIMARY KEY (""Id"");
                            END IF;
                        END IF;

                        -- CompetencyCatalogs checks
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='CompetencyCatalogs') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='CompetencyCatalogs' AND column_name='SubArea') THEN
                                ALTER TABLE ""CompetencyCatalogs"" ADD COLUMN ""SubArea"" text;
                            END IF;
                        END IF;
                    END $$;");

                // SEPARATE Block for TrainingActivities to ensure visibility in logs and correct execution
                await context.Database.ExecuteSqlRawAsync(@"
                    DO $$ 
                    BEGIN 
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='TrainingActivities') THEN
                            -- Modality
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='Modality') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""Modality"" text NOT NULL DEFAULT 'PRESENCIAL';
                            END IF;
                            -- StartDate
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='StartDate') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""StartDate"" timestamp with time zone;
                            END IF;
                            -- EndDate
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='EndDate') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""EndDate"" timestamp with time zone;
                            END IF;
                            -- Credits
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='Credits') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""Credits"" text;
                            END IF;
                            -- IsInternal
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='IsInternal') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""IsInternal"" boolean NOT NULL DEFAULT false;
                            END IF;
                            -- InternalDepartment
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='InternalDepartment') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""InternalDepartment"" text;
                            END IF;
                            -- Status
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='Status') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""Status"" text NOT NULL DEFAULT 'ACTIVO';
                            END IF;
                            -- AnnulReason
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='AnnulReason') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""AnnulReason"" text;
                            END IF;
                             -- CreatedByUserId (add as nullable first, then update, then set NOT NULL)
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='CreatedByUserId') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""CreatedByUserId"" uuid;
                                UPDATE ""TrainingActivities"" SET ""CreatedByUserId"" = (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" = 'admin' LIMIT 1) WHERE ""CreatedByUserId"" IS NULL;
                                UPDATE ""TrainingActivities"" SET ""CreatedByUserId"" = '00000000-0000-0000-0000-000000000000' WHERE ""CreatedByUserId"" IS NULL;
                                ALTER TABLE ""TrainingActivities"" ALTER COLUMN ""CreatedByUserId"" SET NOT NULL;
                            END IF;
                            -- CreatedAt (Ensure it exists, though it should)
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='CreatedAt') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW();
                            END IF;
                            -- UpdatedAt
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='UpdatedAt') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW();
                            END IF;

                            -- TrainingTypeId (FK) - add as nullable first, then update, then set NOT NULL
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='TrainingActivities' AND column_name='TrainingTypeId') THEN
                                ALTER TABLE ""TrainingActivities"" ADD COLUMN ""TrainingTypeId"" uuid;
                                UPDATE ""TrainingActivities"" SET ""TrainingTypeId"" = (SELECT ""Id"" FROM ""TrainingTypeCatalogs"" LIMIT 1) WHERE ""TrainingTypeId"" IS NULL;
                                -- If still null (no types exist), we'll handle this after seeding
                            END IF;
                        END IF;
                    END $$;");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error creating/updating ISO tables: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash the whole init
            Console.WriteLine($"DB Schema update warning: {ex.Message}");
        }

        if (await context.Users.AnyAsync())
        {
            return; 
        }

        // Create Document Types
        if (!await context.DocumentTypes.AnyAsync())
        {
            var types = new List<DocumentType>
            {
                new DocumentType { Id = Guid.NewGuid(), Name = "Manual", Description = "Manuales de Calidad" },
                new DocumentType { Id = Guid.NewGuid(), Name = "Procedimiento", Description = "Procedimientos Operativos" },
                new DocumentType { Id = Guid.NewGuid(), Name = "Instructivo", Description = "Instructivos de Trabajo" },
                new DocumentType { Id = Guid.NewGuid(), Name = "Formulario", Description = "Formatos y Registros" },
                new DocumentType { Id = Guid.NewGuid(), Name = "Reporte", Description = "Informes y Reportes" },
                new DocumentType { Id = Guid.NewGuid(), Name = "Externo", Description = "Documentos Externos" }
            };
            context.DocumentTypes.AddRange(types);
            await context.SaveChangesAsync();
        }

        // Create Reagent Types
        if (!await context.ReagentTypes.AnyAsync())
        {
            var rTypes = new List<ReagentType>
            {
                new ReagentType { Id = Guid.NewGuid(), Name = "Químico", Description = "Reactivos químicos generales" },
                new ReagentType { Id = Guid.NewGuid(), Name = "Biológico", Description = "Reactivos biológicos" },
                new ReagentType { Id = Guid.NewGuid(), Name = "Consumible", Description = "Material fungible" },
                new ReagentType { Id = Guid.NewGuid(), Name = "Estándar", Description = "Estándares de calibración" },
                new ReagentType { Id = Guid.NewGuid(), Name = "Monoclonal", Description = "Anticuerpos monoclonales" }
            };
            context.ReagentTypes.AddRange(rTypes);
            await context.SaveChangesAsync();
        }

        // Create Roles
        var adminRole = new Role { Id = Guid.NewGuid(), RoleName = "Administrador", Description = "Acceso total al sistema" };
        var consultRole = new Role { Id = Guid.NewGuid(), RoleName = "Consultor", Description = "Acceso de consulta y lectura" };
        var managerRole = new Role { Id = Guid.NewGuid(), RoleName = "Manager", Description = "Management and oversight" };
        var qualityRole = new Role { Id = Guid.NewGuid(), RoleName = "Quality", Description = "Quality assurance and audits" };
        var staffRole = new Role { Id = Guid.NewGuid(), RoleName = "Staff", Description = "Basic documentation access" };

        context.Roles.AddRange(adminRole, consultRole, managerRole, qualityRole, staffRole);

        // Create Admin User
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            FullName = "System Administrator",
            Email = "admin@qmsflowdoc.local",
            PasswordHash = HashPassword("admin123"),
            Roles = new List<Role> { adminRole }
        };

        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Seed ISO 15189 Modules
        if (!await context.CompetencyCatalogs.AnyAsync())
        {
            var competencies = new List<CompetencyCatalog>
            {
                new CompetencyCatalog { Name = "Manejo de Citómetro de Flujo", Code = "COMP-001", Area = "Analítica", Description = "Operación básica del equipo y resolución de problemas.", CreatedByUserId = adminUser.Id },
                new CompetencyCatalog { Name = "Preparación de Muestras Sanguíneas", Code = "COMP-002", Area = "Preanalítica", Description = "Lisis osmótica y tinción de superficie.", CreatedByUserId = adminUser.Id },
                new CompetencyCatalog { Name = "Interpretación de Inmunofenotipo", Code = "COMP-003", Area = "Postanalítica", Description = "Análisis de poblaciones celulares en dot plots.", CreatedByUserId = adminUser.Id }
            };
            context.CompetencyCatalogs.AddRange(competencies);
            await context.SaveChangesAsync();
        }

        if (!await context.TrainingTypeCatalogs.AnyAsync())
        {
            var types = new List<TrainingTypeCatalog>
            {
                new TrainingTypeCatalog { Code = "CURSO", Name = "Curso" },
                new TrainingTypeCatalog { Code = "CONGRESO", Name = "Congreso" },
                new TrainingTypeCatalog { Code = "SESION", Name = "Sesión Interna" }
            };
            context.TrainingTypeCatalogs.AddRange(types);
            await context.SaveChangesAsync();
        }

        if (!await context.TrainingActivities.AnyAsync())
        {
            var courseType = await context.TrainingTypeCatalogs.FirstAsync(t => t.Code == "CURSO");
            var activities = new List<TrainingActivity>
            {
                new TrainingActivity { Title = "Curso de Bioseguridad en el Laboratorio", Provider = "Inscripciones Internas", Hours = 10, Description = "Gestión de residuos y riesgos biológicos.", TrainingTypeId = courseType.Id, CreatedByUserId = adminUser.Id },
                new TrainingActivity { Title = "Taller de Calidad ISO 15189", Provider = "AENOR", Hours = 20, Description = "Requisitos de gestión y técnicos.", TrainingTypeId = courseType.Id, CreatedByUserId = adminUser.Id }
            };
            context.TrainingActivities.AddRange(activities);
        }

        if (!await context.AuthorizationCatalogs.AnyAsync())
        {
            var auths = new List<AuthorizationCatalog>
            {
                new AuthorizationCatalog { Name = "Validación Técnica de Resultados", Code = "AUTH-VAL-TECH", Description = "Autorización para validar técnicamente resultados de citometría." },
                new AuthorizationCatalog { Name = "Mantenimiento Preventivo de Equipos", Code = "AUTH-MAINT", Description = "Autorización para realizar limpiezas y calibraciones periódicas." },
                new AuthorizationCatalog { Name = "Extracción de Muestras", Code = "AUTH-EXT", Description = "Autorización para toma de muestras biológicas." }
            };
            context.AuthorizationCatalogs.AddRange(auths);
        }

        await context.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}
