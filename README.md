# QMS FlowDoc - Sistema de Gestión de Calidad (MVP)

QMS FlowDoc es una plataforma integral diseñada para la gestión de calidad en laboratorios de citometría de flujo, facilitando el cumplimiento de la norma **ISO 15189** y los requisitos de validación técnica.

---

## 🛠️ Requerimientos del Sistema

Para compilar y ejecutar este proyecto, necesitas:

### Desarrollo / Servidor
- **.NET 9 SDK** (mínimo v9.0)
- **PostgreSQL 16+** (Servidor de base de datos principal)
- **Visual Studio 2022** (con carga de trabajo de desarrollo de Windows App SDK) o **JetBrains Rider**.

### Cliente (Usuario Final)
- **Windows 10 v1809** o superior.
- Conectividad a internet (para sincronización cloud) o modo offline (SQLite interno).

---

## 🚀 Guía de Instalación y Configuración

### 1. Base de Datos
1. Abre tu gestor de base de datos (p.ej., pgAdmin).
2. Crea una base de datos llamada `qmsflowdoc`.
3. Ejecuta el script de esquema inicial ubicado en: `docs/schema.sql`.

### 2. Configuración del Backend (API)
1. Navega a `src/QMSFlowDoc.Api/appsettings.json`.
2. Configura la cadena de conexión en `ConnectionStrings:DefaultConnection` con tus credenciales de PostgreSQL.
3. Asegúrate de configurar una clave secreta para JWT en la sección `Jwt:Key` (debe tener al menos 32 caracteres).

### 3. Compilación
Desde una terminal en la raíz del proyecto o desde Visual Studio:

```powershell
# Restaurar dependencias
dotnet restore

# Compilar la solución
dotnet build QMSFlowDoc.sln
```

---

## 🏃 Cómo Ejecutar la Aplicación

### Opción A: Usar el Lanzador Automático (Recomendado)
He creado una herramienta de configuración y lanzamiento automático en `src/QMSFlowDoc.Launcher`. Esta herramienta se encarga de:
1. Comprobar .NET 9 y PostgreSQL.
2. Instalar requisitos faltantes vía `winget`.
3. Crear la base de datos y cargar el esquema.
4. Generar claves de seguridad JWT.
5. Compilar y arrancar tanto la API como el Cliente.

**Para usarlo:**
1. Abre una terminal en la raíz.
2. Ejecuta: `dotnet run --project src/QMSFlowDoc.Launcher`
3. Sigue las instrucciones en pantalla.

### Opción B: Ejecución Manual
#### Paso 1: Iniciar el Servidor (API)
```powershell
dotnet run --project src/QMSFlowDoc.Api
```
*La API se ejecutará por defecto en `https://localhost:5001`.*

### Paso 2: Iniciar el Cliente (WinUI 3)
Desde Visual Studio, establece `QMSFlowDoc.Client` como proyecto de inicio y presiona **F5**.
O mediante terminal:
```powershell
dotnet run --project src/QMSFlowDoc.Client
```

---

## 📖 Manual Básico de Usuario

### 1. Acceso (Login)
- Al iniciar, la aplicación te pedirá credenciales.
- Si es la primera vez, el sistema permite registrar un usuario inicial (Administrador).
- Una vez dentro, verás el **Dashboard** con los estados críticos del laboratorio.

### 2. Gestión Documental (Módulo A/C)
- Ve a la sección **Documentos** para ver el listado maestro.
- **Flujo**: Un documento nace como *Borrador*, se envía a *Revisión* y finalmente es *Aprobado*.
- **Impresión**: En la vista de detalles, usa el botón de imprimir para generar un PDF con marca de agua "CONTROLADO" y pie de página de trazabilidad.

### 3. Inventario de Reactivos (Módulo D)
- En **Inventario**, puedes registrar nuevos reactivos y sus lotes correspondientes.
- Los reactivos con stock igual o inferior al mínimo aparecerán resaltados en el Dashboard.

### 4. Equipos y Mantenimiento (Módulo F)
- Registra tus citómetros y equipos de soporte en la sección **Equipos**.
- Registra cada mantenimiento preventivo o correctivo para mantener la trazabilidad.

### 5. Personal y Capacitación (Módulo E)
- Crea fichas de personal en la sección **Personal**.
- Registra cursos de formación y evaluaciones de competencia para asegurar el cumplimiento normativo.

### 6. Mejora Continua (Módulo G/H/I)
- **Riesgos**: Evalúa riesgos por probabilidad e impacto.
- **Incidencias**: Registra no conformidades y asocia acciones CAPA.
- **Auditorías**: Planifica auditorías internas y registra los hallazgos.

---

## ☁️ Sincronización y Modo Offline

- La aplicación detecta automáticamente la pérdida de conexión.
- En modo offline, puedes seguir consultando documentos y datos guardados en la caché local.
- Un indicador en el pie de página de la aplicación muestra el estado de sincronización ("Al día" o "Offline").

---

# Soporte y Validación
Para consultar la documentación técnica de validación (URS, FRS, IQ/OQ/PQ), revisa la carpeta `docs/validation/`.
