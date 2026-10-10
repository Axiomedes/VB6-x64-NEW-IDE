# VB7-x64 IDE

[![Build & Test](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows)](https://microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSES.md)
[![Tests](https://img.shields.io/badge/Tests-100%20Passing-success.svg)](tests/VB7x64.Tests)

Un entorno de desarrollo integrado (IDE) moderno para **Windows x64** inspirado en **Microsoft Visual Basic 6.0**, diseñado para abrir, explorar, editar y refactorizar proyectos clásicos de VB6 (`.vbp`, `.vbg`, `.bas`, `.cls`, `.frm`, `.ctl`, `.pag`) de forma segura y sin corromper sus formatos ni recursos binarios.

---

## 🌟 Características Principales

### 1. Editor de Código y Productividad Visual
* **Editor AvalonEdit Optimizado**: Resaltado sintáctico fiel para VB6 (keywords, tipos, literales de fecha `#mm/dd/yyyy#`, cadenas escapadas, comentarios con apóstrofe y `Rem`).
* **Resaltado de Línea Activa**: Resaltado sutil y configurable de la línea donde se ubica el cursor.
* **Guías de Estructura de Control (Branch Lines)**: Líneas verticales que conectan bloques `If...End If`, `Select Case`, `For...Next`, `Do...Loop`, `With...End With` y procedimientos.
* **Detalles de Fin de Bloque (Inlay Hints / Ghost Text)**: Anotaciones discretas al final de bloques extensos (ej. `End If  ' [If precio > 100]`).
* **Edición Rápida**: Comentar/descomentar en bloque (<kbd>Ctrl+K, Ctrl+C</kbd> / <kbd>Ctrl+K, Ctrl+U</kbd>), invertir asignaciones (`A = B` ➔ `B = A`) y conversión de mayúsculas/minúsculas.

### 2. Exploración y Navegación Avanzada
* **Explorador de Proyectos Dual**:
  * **Modo Estándar**: Árbol clásico purista de VB6 (Proyecto ➔ Carpetas de tipos ➔ Archivos).
  * **Modo Avanzado**: Expansión en profundidad de componentes internos (Sub, Function, Property, Eventos, Constantes, Variables de módulo, Enums y Types).
  * **Filtrado en Tiempo Real**: Cuadro de búsqueda dinámica instantánea para proyectos, archivos y miembros.
* **Búsqueda de Referencias y Usos (Find References - <kbd>Shift+F12</kbd>)**: Localiza apariciones del identificador en el archivo, proyecto o grupo `.vbg`, excluyendo comentarios y cadenas, con navegación por doble clic.
* **Historial de Navegación y Marcadores (Bookmarks)**:
  * Historial Atrás / Adelante (<kbd>Alt+←</kbd> / <kbd>Alt+→</kbd>).
  * Marcadores visuales en el margen (`🔖`) con navegación secuencial (<kbd>F2</kbd> / <kbd>Shift+F2</kbd>) y alternancia rápida (<kbd>Ctrl+F2</kbd>).
* **Explorador de Tareas (TODO / Task List)**: Escaneo automático de etiquetas `' TODO:`, `' BUG:`, `' FIXME:`, `' NOTE:` y `' HACK:` con priorización y salto a la línea.

### 3. Asistentes de Código y Snippets
* **Asistente de Propiedades (Property Builder)**: Generación automática de bloques `Property Get/Let` (tipos primitivos) y `Property Get/Set` (objetos) con variable de respaldo tipada.
* **Asistente de Procedimientos (Procedure Builder)**: Diálogo interactivo para declarar métodos con visibilidad, tipos de retorno, argumentos tipados (`ByVal`/`ByRef`/`Optional`) y manejo de errores.
* **Biblioteca de Snippets (AutoText con expansión por <kbd>Tab</kbd>)**: Plantillas para `if`, `ife`, `sel`, `for`, `fore`, `dow`, `dou`, `whi`, `with`, `prop`, `sub`, `fn`, `err`.
* **Portapapeles Múltiple (Clipboard Ring - <kbd>Ctrl+Shift+V</kbd>)**: Historial rotativo de los últimos fragmentos copiados con previsualización e inserción directa.

### 4. Mantenimiento y Calidad de Código
* **Formateador de Código VB6 (Code Formatter)**: Indentación automática según profundidad de anidación, alineación estética de cláusulas `As` en declaraciones `Dim` y normalización de líneas en blanco.
* **Ordenador de Procedimientos (Sort Procedures)**: Reorganización alfabética o por visibilidad preservando el bloque de declaraciones de módulo.
* **Comparador de Cambios (File Diff Viewer)**: Visor de diferencias lado a lado y unificado con coloreado de adiciones y supresiones frente a la versión en disco.
* **Estadísticas de Proyecto (Project Statistics)**: Dashboard analítico con cálculo de SLOC efectivo, volumen de comentarios, recuento de procedimientos y desglose por archivo.
* **Motor de Manejo de Errores (Error Handling Engine)**: Inserción y retiro masivo de esquemas configurables (`MsgBox`, `Debug.Print`, `HandleError`, `On Error Resume Next`) con soporte de macros dinámicas.
* **Inyección de Trazas (Tracing Engine)**: Inyección y retiro antes de producción de marcas de trazabilidad (`Debug.Print "--> Entrando a ..."`) con identificadores unívocos (`' [VB7_TRACE]`).
* **Diseñador de Orden de Tabulación (Tab Order Designer)**: Reordenación manual o auto-ordenación geográfica (Top ➔ Left) de la propiedad `TabIndex` en formularios.
* **Verificador de Teclas Rápidas (Hotkey Designer)**: Detección y auto-resolución de conflictos de aceleradores `&` en `Caption`.
* **Analizador de Calidad de Código (Code Analyzer)**: Auditoría estática que reporta falta de `Option Explicit`, código muerto en rutinas `Private` huérfanas, variables sin utilizar y procedimientos vacíos.

---

## 🛡️ Principios e Invariantes de Seguridad

1. **Cero Corrupción de Archivos**: Los archivos que no son modificados por el usuario se preservan byte a byte idénticos al original.
2. **Preservación Estricta de Cabeceras Visuales**: La edición de formularios `.frm`, controles `.ctl` y páginas `.pag` opera sobre el código de usuario, protegiendo las cabeceras `Begin...End` y metadatos de diseño.
3. **Codificación ANSI (Windows-1252) y Saltos CRLF**: Todo guardado mantiene rigurosamente la página de códigos Windows y terminadores `\r\n`.
4. **Protección de Recursos Binarios**: Archivos `.frx` y recursos binarios nunca son procesados como texto y se mantienen inalterados.
5. **Reemplazo Atómico y Copias de Respaldo**: Escritura mediante archivos temporales con reemplazo atómico y generación opcional de `.bak`.

---

## 🏗️ Estructura de la Solución

```text
VB7x64.sln
├── src/
│   ├── VB7x64.IDE/             # Shell WPF x64: Menús, Barra de herramientas, Editor, Diálogos y UI
│   └── VB7x64.ProjectModel/    # Modelo de proyectos, Parsers, Motores de refactorización y E/S segura
├── tests/
│   ├── VB7x64.Tests/           # Suite de pruebas unitarias xUnit (100 pruebas / 100% éxito)
│   └── fixtures/               # Proyectos de muestra de VB6 para pruebas de round-trip
└── docs/                       # Arquitectura y Decisiones de Diseño (ADRs)
```

---

## 🚀 Requisitos y Compilación

### Requisitos del Sistema
* **Sistema Operativo**: Windows 10 / Windows 11 (arquitectura 64-bit).
* **SDK**: [.NET 10.0 SDK LTS](https://dotnet.microsoft.com/download/dotnet/10.0).

### Compilación y Ejecución de Pruebas

```bash
# Clonar el repositorio
git clone https://github.com/Axiomedes/VB6-x64-NEW-IDE.git
cd VB6-x64-NEW-IDE

# Restaurar paquetes y compilar la solución
dotnet build

# Ejecutar la suite de pruebas unitarias automatizadas
dotnet test

# Iniciar el IDE
dotnet run --project src/VB7x64.IDE
```

---

## 🗺️ Próximos Pasos (Roadmap)

1. **Diseñador Visual de Formularios (Form Designer WYSIWYG)**:
   * Superficie de diseño interactiva con rejilla (*Grid / Snap to Grid*).
   * Renderizado gráfico fiel de controles de VB6.
   * Manipulación interactiva (8 sizing handles, arrastre y redimensión).
   * Caja de herramientas (*Toolbox*) y Ventana de propiedades (*Property Grid*).
   * Flujo dual Código ↔ Diseñador (<kbd>F7</kbd> / <kbd>Shift+F7</kbd>).
2. **Servicios de Lenguaje e IntelliSense**:
   * Auto List Members tras el punto (`.`) y <kbd>Ctrl+J</kbd>.
   * Auto Quick Info (<kbd>Ctrl+I</kbd>) para firmas de funciones del runtime y APIs de usuario.
   * Auto Complete Word (<kbd>Ctrl+Espacio</kbd>).

