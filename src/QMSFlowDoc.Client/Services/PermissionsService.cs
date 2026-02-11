using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IPermissionsService
{
    Task EnsureSeedDataAsync();
    Task<List<Role>> GetAllRolesAsync();
    Task<List<Permission>> GetAllPermissionsAsync();
    Task<List<Permission>> GetPermissionsForRoleAsync(Guid roleId);
    Task UpdateRolePermissionsAsync(Guid roleId, List<Guid> permissionIds);
    Task<bool> HasPermissionAsync(string roleName, string permissionKey);
}

public class PermissionsService : IPermissionsService
{
    private readonly LocalDocumentStore _store;

    public PermissionsService(LocalDocumentStore store)
    {
        _store = store;
    }

    public async Task<List<Role>> GetAllRolesAsync() => await _store.GetAllRolesAsync();
    public async Task<List<Permission>> GetAllPermissionsAsync() => await _store.GetAllPermissionsAsync();
    public async Task<List<Permission>> GetPermissionsForRoleAsync(Guid roleId) => await _store.GetPermissionsForRoleAsync(roleId);
    public async Task UpdateRolePermissionsAsync(Guid roleId, List<Guid> permissionIds) => await _store.UpdateRolePermissionsAsync(roleId, permissionIds);

    public async Task<bool> HasPermissionAsync(string roleName, string permissionKey)
    {
        // Admin always has all permissions
        if (roleName.Equals("Administrador", StringComparison.OrdinalIgnoreCase)) return true;

        var role = await _store.GetRoleByNameAsync(roleName);
        if (role == null) return false;

        var perms = await _store.GetPermissionsForRoleAsync(role.Id);
        return perms.Any(p => p.PermissionKey == permissionKey);
    }

    public async Task EnsureSeedDataAsync()
    {
        // 1. Ensure Roles
        var roles = new[] { "Administrador", "Consultor", "Manager", "Quality", "Staff" };
        foreach (var roleName in roles)
        {
            var exists = await _store.GetRoleByNameAsync(roleName);
            if (exists == null)
            {
                await _store.CreateRoleAsync(new Role 
                { 
                    Id = Guid.NewGuid(), 
                    RoleName = roleName, 
                    Description = $"Rol de {roleName}" 
                });
            }
        }

        // 2. Ensure Permissions
        var permissions = GetDefaultPermissions();
        var dbPerms = await _store.GetAllPermissionsAsync();
        
        foreach (var p in permissions)
        {
            if (!dbPerms.Any(dp => dp.PermissionKey == p.PermissionKey))
            {
                p.Id = Guid.NewGuid(); // Ensure ID is set
                await _store.CreatePermissionAsync(p);
            }
        }

        // 3. Seed Matrix (Only if role has NO permissions assigned yet)
        await SeedMatrixAsync();
    }

    private List<Permission> GetDefaultPermissions()
    {
        return new List<Permission>
        {
            // Dashboard
            new() { PermissionKey = "Dashboard.View", Description = "Ver Dashboard" },

            // Documentos
            new() { PermissionKey = "Documents.View", Description = "Ver Documentos" },
            new() { PermissionKey = "Documents.Create", Description = "Crear Documentos" },
            new() { PermissionKey = "Documents.CreateFolder", Description = "Crear Carpetas" },
            new() { PermissionKey = "Documents.Edit", Description = "Editar Documentos" },
            new() { PermissionKey = "Documents.Delete", Description = "Eliminar Documentos" },
            new() { PermissionKey = "Documents.ViewObsolete", Description = "Ver Obsoletos" },
            new() { PermissionKey = "Documents.RenameFolder", Description = "Renombrar Carpetas" },
            new() { PermissionKey = "Documents.DeleteFolder", Description = "Eliminar Carpetas" },

            // Inventario
            new() { PermissionKey = "Inventory.View", Description = "Ver Inventario" },
            new() { PermissionKey = "Inventory.CreateReagent", Description = "Nuevo Reactivo" },
            new() { PermissionKey = "Inventory.EditReagent", Description = "Modificar Artículo" },
            new() { PermissionKey = "Inventory.Entry", Description = "Entrada (Lote)" },
            new() { PermissionKey = "Inventory.Exit", Description = "Salida/Consumo" },
            new() { PermissionKey = "Inventory.Export", Description = "Exportar Excel" },
            new() { PermissionKey = "Inventory.Print", Description = "Imprimir" },
            new() { PermissionKey = "Inventory.Delete", Description = "Borrar Registro" },
            new() { PermissionKey = "Inventory.FilterActive", Description = "Ver Solo Activos" },
            new() { PermissionKey = "Inventory.FilterLowStock", Description = "Ver Stock Bajo" },

            // Proveedores
            new() { PermissionKey = "Suppliers.View", Description = "Ver Proveedores" },
            new() { PermissionKey = "Suppliers.Create", Description = "Nuevo Proveedor" },
            new() { PermissionKey = "Suppliers.Edit", Description = "Editar Proveedor" },
            new() { PermissionKey = "Suppliers.Evaluate", Description = "Evaluar Proveedor" },
            new() { PermissionKey = "Suppliers.Delete", Description = "Eliminar Proveedor" },

            // Equipos
            new() { PermissionKey = "Equipment.View", Description = "Ver Equipos" },
            new() { PermissionKey = "Equipment.Create", Description = "Nuevo Equipo" },
            new() { PermissionKey = "Equipment.Edit", Description = "Editar Equipo" },
            new() { PermissionKey = "Equipment.Delete", Description = "Eliminar Equipo" },
            new() { PermissionKey = "Equipment.Maintenance", Description = "Registrar Mantenimiento" },
            new() { PermissionKey = "Equipment.DailyQC", Description = "Registrar QC Diario" },
            new() { PermissionKey = "Equipment.Export", Description = "Exportar Excel" },

            // Personal
            new() { PermissionKey = "Staff.View", Description = "Ver Personal" },
            new() { PermissionKey = "Staff.Create", Description = "Nueva Ficha" },
            new() { PermissionKey = "Staff.Edit", Description = "Editar Ficha" },
            new() { PermissionKey = "Staff.Export", Description = "Exportar Excel" },
            new() { PermissionKey = "Staff.Delete", Description = "Borrar Ficha" },
            new() { PermissionKey = "Staff.Clear", Description = "Limpiar Registros" },

            // Competencias
            new() { PermissionKey = "Competency.Catalog", Description = "Catálogo Competencias" },
            new() { PermissionKey = "Competency.Matrix", Description = "Matriz Visual" },
            new() { PermissionKey = "Competency.Training", Description = "Formación" },
            new() { PermissionKey = "Competency.Auth", Description = "Autorizaciones" },

            // Otros Módulos
            new() { PermissionKey = "EQA.View", Description = "Control Calidad EQA" },
            new() { PermissionKey = "IQC.View", Description = "Control Interno Calidad" },
            new() { PermissionKey = "Methods.View", Description = "Métodos" },
            new() { PermissionKey = "Improvements.View", Description = "Mejoras" },
            new() { PermissionKey = "Audits.View", Description = "Auditoría" },
            new() { PermissionKey = "Incidents.View", Description = "Incidencias" },
            new() { PermissionKey = "Complaints.View", Description = "Quejas" },
            new() { PermissionKey = "Auth.ChangePassword", Description = "Cambiar Contraseña" },
            new() { PermissionKey = "Configuration.View", Description = "Configuración" },
        };
    }

    private async Task SeedMatrixAsync()
    {
        var allRoles = await _store.GetAllRolesAsync();
        var allPerms = await _store.GetAllPermissionsAsync();

        foreach (var role in allRoles)
        {
            // Skip seeding if role already has permissions
            var existingPerms = await _store.GetPermissionsForRoleAsync(role.Id);
            if (existingPerms.Any()) continue;

            var permsToAdd = new List<Guid>();

            // Helper for adding permission by key
            void Add(string key)
            {
                var p = allPerms.FirstOrDefault(x => x.PermissionKey == key);
                if (p != null) permsToAdd.Add(p.Id);
            }

            // Consultor
            if (role.RoleName == "Consultor")
            {
                Add("Dashboard.View");
                Add("Documents.View");
                
                Add("Inventory.View");
                Add("Inventory.Entry");
                Add("Inventory.Exit");
                Add("Inventory.Export");
                Add("Inventory.Print");
                Add("Inventory.FilterActive");
                Add("Inventory.FilterLowStock");

                Add("Suppliers.View");
                
                Add("Equipment.View");
                Add("Equipment.Maintenance");
                Add("Equipment.DailyQC");
                Add("Equipment.Export");

                // Personal: Table says "Personal" -> No, but "Exportar excel" -> Si.
                // Assuming View is needed to Export.
                Add("Staff.View"); 
                Add("Staff.Export");

                Add("Auth.ChangePassword");
                
                // Config: No
            }
            // Manager & Quality: All Yes
            else if (role.RoleName == "Manager" || role.RoleName == "Quality" || role.RoleName == "Administrador")
            {
                 permsToAdd.AddRange(allPerms.Select(p => p.Id));
            }
            // Staff
            else if (role.RoleName == "Staff")
            {
                Add("Dashboard.View");
                Add("Documents.View");

                Add("Inventory.View");
                Add("Inventory.CreateReagent");
                Add("Inventory.EditReagent");
                Add("Inventory.Entry");
                Add("Inventory.Exit");
                Add("Inventory.Export");
                Add("Inventory.Print");
                Add("Inventory.FilterActive");
                Add("Inventory.FilterLowStock");

                Add("Suppliers.View");

                Add("Equipment.View");
                Add("Equipment.Maintenance");
                Add("Equipment.DailyQC");
                Add("Equipment.Export");

                // Personal: Table says "Personal" -> No, but "Exportar excel" -> Si.
                Add("Staff.View");
                Add("Staff.Export");

                // Competencias: No

                Add("EQA.View");
                Add("IQC.View");
                Add("Methods.View");
                Add("Improvements.View");
                Add("Audits.View");
                Add("Incidents.View");
                Add("Complaints.View");
                Add("Auth.ChangePassword");
                Add("Configuration.View");
            }

            if (permsToAdd.Any())
            {
                await _store.UpdateRolePermissionsAsync(role.Id, permsToAdd);
            }
        }
    }
}
