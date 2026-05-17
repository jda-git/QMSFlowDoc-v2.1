# QMSFlowDoc v3 - Arquitectura web centralizada

## Objetivo

QMSFlowDoc v3 transforma la aplicación de escritorio instalada en cada PC en un portal web de red local. Un único ordenador actúa como servidor y concentra:

- La aplicación web ASP.NET Core (`QMSFlowDoc.Web`).
- La base de datos SQL Server/SQL Server Express.
- El repositorio físico del gestor documental.
- La autenticación y auditoría de accesos.

Los equipos cliente no instalan la aplicación: acceden desde el navegador a `http://<servidor>:5080` dentro de la LAN.

## Reutilización del código v2

La versión web conserva las capas portables existentes:

- `QMSFlowDoc.Shared`: modelos de dominio, enums y DTOs.
- `QMSFlowDoc.Data`: `QmsFlowDocDbContext` y mapeo EF Core contra SQL Server.
- `QMSFlowDoc.DocumentStorage`: almacenamiento central con rutas relativas y protección contra traversal.

La capa que se sustituye es `QMSFlowDoc.Client` WinUI/WPF. Sus pantallas se migran progresivamente a endpoints/páginas web dentro de `QMSFlowDoc.Web`.

## Primer alcance implementado

El primer vertical funcional de v3 incluye:

1. Servidor HTTP escuchando por defecto en `0.0.0.0:5080`.
2. Login web con las cuentas y hashes BCrypt existentes.
3. Cookie de sesión para navegadores de la red local.
4. Dashboard con métricas de documentos, equipos, no conformidades y revisiones.
5. Listado de documentos y descarga de la versión vigente.
6. Alta de borradores con subida de archivo al repositorio documental central.
7. Endpoint `/health` para verificar el estado básico del servidor.

## Configuración mínima del servidor

Editar `src/QMSFlowDoc.Web/appsettings.json` antes de publicar:

```json
{
  "ConnectionStrings": {
    "QMSFlowDoc": "Server=localhost\\SQLEXPRESS;Database=QMSFlowDoc;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Server": {
    "Urls": [ "http://0.0.0.0:5080" ]
  },
  "DocumentStorage": {
    "RootPath": "C:\\QMSFlowDoc-V3\\Repository"
  }
}
```

Recomendaciones de despliegue LAN:

- Reservar IP o nombre DNS local para el servidor.
- Abrir el puerto TCP 5080 en el firewall del servidor.
- Ubicar `DocumentStorage:RootPath` en un disco local del servidor con backup.
- Mantener SQL Server en el servidor; los clientes no necesitan conexión directa a la base de datos.
- Publicar con Kestrel o detrás de IIS si se quiere arranque automático como servicio Windows.

## Roadmap de migración

1. Migrar permisos finos de `QMSFlowDoc.Client.Services.AuthorizationService` a políticas ASP.NET Core.
2. Convertir los módulos restantes a páginas web: inventario, personal/competencias, equipos, auditorías, CAPA/EQA y configuración.
3. Añadir antiforgery tokens y cabeceras de seguridad antes de abrir fuera de una LAN controlada.
4. Crear herramienta de migración asistida desde instalaciones v2/local-first hacia base de datos y repositorio v3 centralizados.
5. Empaquetar publicación como servicio Windows con backup programado y restauración documentada.
