# Functional Requirements Specification (FRS) - QMS FlowDoc

## 1. Diseño del Sistema
QMS FlowDoc es una arquitectura cliente-servidor con sincronización eventual.

## 2. Especificaciones de Funciones
### 2.1 Gestión Documental
- El sistema debe soportar archivos PDF.
- El sistema debe añadir pies de página con metadatos: Usuario, Fecha, Hora, ID de copia.
- El sistema debe mostrar marcas de agua: "BORRADOR", "EN REVISIÓN", "CONTROLADO", "OBSOLETO".

### 2.2 Inventario y Reactivos
- El sistema debe calcular el stock disponible sumando lotes "RELEASED".
- El sistema debe avisar cuando el stock sea <= MinStock.

## 3. Interfaces
- **API**: Basada en REST (JSON).
- **Base de Datos**: PostgreSQL 16+.
- **Caché Local**: SQLite 3.

## 4. Requisitos de Rendimiento
- Tiempo de respuesta de búsqueda < 2 segundos.
- Capacidad de documentos > 10,000 archivos.
