# VB7-x64 IDE

Un entorno de desarrollo integrado (IDE) moderno para Windows x64 inspirado en Microsoft Visual Basic 6.0, diseñado para abrir, explorar y editar proyectos clásicos de VB6 (`.vbp`, `.bas`, `.cls`, `.frm`) de forma segura y sin corromper sus formatos ni recursos.

## Principios y Objetivos del MVP (v1.0)

1. **Seguridad e Integridad Estricta de Archivos**:
   - Nunca reescribe archivos que no hayan sido modificados por el usuario (verificación byte a byte).
   - Preserva la codificación ANSI original (Windows Code Pages) y saltos de línea (CRLF).
   - Edición de formularios `.frm`: se delimita y edita exclusivamente la sección de código fuente, preservando intacto el bloque serializado de diseño, controles y propiedades.
   - Los archivos `.frx` son recursos binarios; nunca se leen ni escriben como texto y se mantienen inalterados.
   - Preserva líneas desconocidas y su orden dentro de proyectos `.vbp`.

2. **Desacoplamiento Arquitectónico**:
   - El IDE es una aplicación independiente de edición de código.
   - El compilador (`VB6 Open Compiler x64`) y el runtime x64 se desarrollan de forma independiente y se integrarán en etapas posteriores (vía protocolos estructurados / LSP). El MVP no compila ni ejecuta.

3. **Tecnología**:
   - Desarrollado en **C# / WPF** orientado nativamente a **Windows x64** sobre **.NET 10 LTS**.
   - Componente de edición basado en **AvalonEdit** con reglas de resaltado sintáctico específicas para VB6.

## Estructura de la Solución

```text
VB7x64.sln
├── src/
│   ├── VB7x64.IDE/             # Shell WPF, UI, Editores, Controles y Servicios del IDE
│   └── VB7x64.ProjectModel/    # Modelo de proyectos VB6, Parser de .vbp, Documentos y Manejo seguro de E/S
├── tests/
│   ├── VB7x64.Tests/           # Pruebas automatizadas de integridad, Round-trip y Parsers
│   └── fixtures/               # Muestras de proyectos VB6 clásicos para validación
└── docs/                       # Arquitectura y Decisiones de Diseño (ADRs)
```
