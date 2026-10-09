# ADR-001: Decisiones de Arquitectura y Alcance del MVP para VB7-x64 IDE

## Estado
Aprobado

## Contexto
Se requiere construir un IDE moderno para Windows x64 inspirado en Microsoft Visual Basic 6.0 que permita a los desarrolladores abrir, navegar y editar proyectos clásicos existentes de VB6 sin alterar ni corromper su formato ni sus recursos binarios.

## Decisiones

1. **Plataforma y Framework**:
   - Se utilizará **C# y WPF** sobre **.NET 10 LTS**, configurando explícitamente el target de compilación a `x64`.
   - Se emplea WPF por su madurez, fidelidad gráfica en Windows, aceleración por hardware y amplio ecosistema para controles de desarrollo.

2. **Componente de Editor**:
   - Se adopta **AvalonEdit** bajo licencia MIT.
   - Se implementarán analizadores léxicos y definiciones de resaltado de sintaxis específicas para VB6 (incluyendo keywords, literales de fecha `#mm/dd/yyyy#`, cadenas escapadas, comentarios con apóstrofe y `Rem`).

3. **Separación de Componentes**:
   - El IDE es completamente independiente del compilador y del runtime.
   - En el MVP (versión 1.0) no se incluye compilación en caliente, ejecución directa ni diseñador visual interactivo de controles.
   - Futuras capacidades de análisis y compilación se consumirán como servicios externos desacoplados (e.g., vía JSON-RPC/LSP).

4. **Estrategia de Preservación de Archivos (Seguridad de Archivos)**:
   - Los archivos que no sufren cambios no se guardan ni tocan.
   - La codificación ANSI se preserva utilizando la Windows Code Page adecuada sin forzar silenciosamente UTF-8.
   - Los archivos `.frm` dividen estrictamente la sección de definición de controles/propiedades y la sección de código. Al guardar, solo se modifica la sección de código.
   - Los archivos `.frx` nunca se procesan como texto plano.
   - Se generan archivos temporales y reemplazo atómico para evitar corrupción en cortes de energía o fallos de E/S.

## Consecuencias
- Cero riesgo de corrupción de proyectos VB6 legados.
- Desarrollo ágil enfocado en la experiencia de edición y navegación.
- Preparado para conectarse al compilador x64 cuando esté listo sin rehacer la arquitectura del IDE.
