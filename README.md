# QMS FlowDoc - Sistema de Gestión de Calidad (MVP)

QMS FlowDoc es una plataforma integral diseñada para la gestión de calidad en laboratorios de citometría de flujo, facilitando el cumplimiento de la norma **ISO 15189** y los requisitos de validación técnica.

> **Arquitectura Local-First**: Esta aplicación funciona sin necesidad de un servidor central de base de datos. Cada PC tiene su propia base de datos local (SQLite) que se sincroniza automáticamente con un repositorio maestro en una carpeta de red compartida.

---

## 🛠️ Requerimientos del Sistema

### Entorno de Ejecución (Cliente)
- **Windows 10 v1809** o superior (x64).
- Acceso a una carpeta de red compartida (para el Repositorio Maestro).
- No requiere instalación de .NET Runtime (Versión Portable).

### Desarrollo
- .NET 8.0 SDK o superior.
- Visual Studio 2022 (Workload: Desarrollo de escritorio .NET + Windows App SDK).

---

## 🚀 Guía de Instalación y Despliegue

### Opción A: Versión Portable (Recomendada)
Esta versión no requiere instalador.

1.  **Preparar la Red**:
    *   Crea una carpeta compartida en tu servidor o NAS (ej. `Z:\QMS_Repo` o `\\Servidor\QMS_Repo`).
    *   Asegúrate de que los usuarios tengan permisos de **Lectura y Escritura**.

2.  **Desplegar Cliente**:
    *   Copia la carpeta de la aplicación (`publish/`) al PC del usuario (ej. `C:\QMSFlowDoc`).
    *   Ejecuta `QMSFlowDoc.Client.exe`.

3.  **Configuración Inicial**:
    *   Al abrir por primera vez, el asistente te pedirá:
        *   **Ruta Local**: Donde se guardarán los datos en este PC (ej. `C:\Datos_QMS`).
        *   **Ruta de Red**: La carpeta compartida creada en el paso 1.
    *   ¡Listo! El sistema sincronizará automáticamente la estructura de carpetas y bases de datos.

### Opción B: Compilación desde Código
```powershell
# Restaurar y compilar
dotnet restore
dotnet build -p:Platform=x64

# Publicar versión portable
dotnet publish -c Release -r win-x64 --self-contained -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:Platform=x64
```

---

## 🔄 Sincronización y Arquitectura de Red

El sistema utiliza un modelo de **Sincronización Bidireccional** con política *"Last Write Wins"*:

*   **Inicio**: Descarga cambios pendientes de la red.
*   **Cierre**: Sube los cambios locales a la red.
*   **Conflictos**: Si un archivo ha cambiado en ambos lados, se muestra un diálogo para que el usuario decida qué versión conservar.
*   **Logs**: Cada PC genera su propio log de sincronización en la red (`sync_YYYYMMDD_PCNAME.log`) para auditoría.

---

## 📖 Manual Básico de Usuario

### 1. Gestión Documental (Módulo A/C)
- Visualiza, crea y aprueba documentos (Procedimientos, Manuales).
- Control de versiones automático.
- Impresión de **Copias Controladas** con marca de agua y trazabilidad.

### 2. Inventario de Reactivos (Módulo D)
- Registro de reactivos, lotes y caducidades.
- **Informes de Consumo**: Trazabilidad de uso por paciente/causa, incluyendo fecha de caducidad.
- **Informes de Entradas**: Registro de recepciones con conteo de unidades.
- Alertas de Stock Mínimo y Caducidad.

### 3. Personal y Competencias (Módulo E) - *ISO 15189*
- Gestión de expedientes de personal.
- **Matriz de Competencias**: Planifica, evalúa y supervisa las competencias técnicas.
- Registro de autorizaciones vigentes y plan de formación continua.

### 4. Equipos y Mantenimiento (Módulo F)
- Inventario de equipamiento.
- Calendario de mantenimientos preventivos y registro de correctivos.

### 5. Calidad y Mejora (Módulo G/H/I)
- Auditorías internas.
- Gestión de No Conformidades y Acciones Correctivas (CAPA).
- Evaluación de Riesgos.

---

## 📂 Estructura de Carpetas

El sistema organiza la información automáticamente:

```
/ (Raíz del Repositorio)
 ├── Documentos/         # Procedimientos aprobados y borradores
 ├── Base_datos/         # Base de datos SQLite y Backups
 │    └── Logs/          # Historial de sincronización de cada PC
 ├── Informes/           # PDFs generados por el sistema
 ├── Personal/           # Evidencias de competencia y formación
 └── Inventario/         # Hojas de cálculo auxiliares o evidencias
```

---

## 🌐 QMSFlowDoc v3 (Web en red local)

Se ha iniciado la refactorización v3 para centralizar la aplicación en un único servidor accesible desde navegador dentro de la LAN. El nuevo proyecto `src/QMSFlowDoc.Web` reutiliza la capa de datos SQL Server y el almacenamiento documental central, expone login web, panel, listado documental, subida de borradores y descarga de versiones vigentes.

Consulta `docs/QMSFlowDoc-V3-architecture.md` para la arquitectura, configuración de servidor y roadmap de migración desde la versión de escritorio.

---

# Soporte
Para validar la instalación, revisa los logs de sincronización en `Base_datos/Logs` o utiliza la opción "Ver Log de Sincronización" en el menú de Configuración.
