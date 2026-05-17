# QMSFlowDoc.Web

Portal web de QMSFlowDoc v3 para centralizar la aplicación en un servidor de red local.

## Ejecutar en desarrollo

```powershell
dotnet run --project src/QMSFlowDoc.Web/QMSFlowDoc.Web.csproj
```

Abrir desde cualquier cliente de la LAN:

```text
http://<nombre-o-ip-del-servidor>:5080
```

## Configuración

Los valores principales están en `appsettings.json`:

- `ConnectionStrings:QMSFlowDoc`: base de datos SQL Server central.
- `Server:Urls`: direcciones donde escucha Kestrel; por defecto `http://0.0.0.0:5080`.
- `DocumentStorage:RootPath`: carpeta local del servidor donde se guardan documentos, adjuntos, informes y logs.
- `Authentication`: nombre y duración de la cookie de sesión.

## Estado funcional

Este proyecto es la base de la refactorización v3. El primer vertical disponible es gestión documental básica: login, panel, listado, subida de borradores y descarga de versión vigente.
