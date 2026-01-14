# User Requirements Specification (URS) - QMS FlowDoc

## 1. Introducción
Este documento define los requisitos de usuario para el sistema QMS FlowDoc, diseñado para la gestión de calidad en laboratorios de citometría de flujo bajo la norma ISO 15189.

## 2. Requisitos Funcionales
- **UR-F01**: El sistema debe permitir la gestión de documentos controlados (ciclo de vida completo).
- **UR-F02**: El sistema debe permitir el registro de reactivos y control de lotes/caducidades.
- **UR-F03**: El sistema debe permitir el registro de mantenimiento de equipos y planes preventivos.
- **UR-F04**: El sistema debe permitir la gestión de capacitación del personal y evaluación de competencia.
- **UR-F05**: El sistema debe permitir el registro de no conformidades y acciones CAPA.
- **UR-F06**: El sistema debe soportar trabajo offline y sincronización cloud.

## 3. Requisitos de Seguridad e Integridad (GAMP 5)
- **UR-S01**: Acceso restringido por roles (RBAC).
- **UR-S02**: Audit Trail completo para todas las acciones críticas.
- **UR-S03**: Firmas electrónicas vinculadas al usuario (simples/avanzadas).

## 4. Requisitos de Infraestructura
- **UR-I01**: El cliente debe ser una aplicación Windows nativa.
- **UR-I02**: El servidor debe ser escalable (ASP.NET Core + PostgreSQL).
