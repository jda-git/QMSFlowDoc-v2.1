# Qualification Protocol (IQ/OQ/PQ) - QMS FlowDoc

## 1. Calificación de Instalación (IQ)
- [ ] Base de Datos PostgreSQL instalada y accesible.
- [ ] Aplicación WinUI 3 instalada en cliente Windows 10/11.
- [ ] Conectividad con el servidor API verificada.
- [ ] Carpeta de caché local creada con permisos de lectura/escritura.

## 2. Calificación de Operación (OQ)
- [ ] Desafío de Autenticación: Ingreso con credenciales correctas e incorrectas.
- [ ] Desafío de RBAC: Intentar acceder a funciones de aprobación con rol de "Usuario".
- [ ] Desafío de Offline: Cargar un documento sin conexión y verificar que se guarda en caché.
- [ ] Desafío de Impresión: Verificar marcas de agua en PDF generado.

## 3. Calificación de Desempeño (PQ)
- [ ] Simulación de ciclo de vida completo de 10 documentos.
- [ ] Registro de 5 reactivos y ajuste de stock.
- [ ] Generación de reporte de riesgo y mitigación.
- [ ] Verificación de sincronización tras 24h de uso.
