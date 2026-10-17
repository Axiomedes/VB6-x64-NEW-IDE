# Arquitectura Técnica — ViBa7-x86 IDE

## 1. Visión General
**ViBa7-x86 IDE** es un entorno de desarrollo integrado para Windows (proceso nativo x86 con `LARGEADDRESSAWARE`) desarrollado con C# y WPF sobre .NET 10 LTS.
Su objetivo es proporcionar compatibilidad total con proyectos de Microsoft Visual Basic 6.0 SP6 (`.vbp`, `.vbg`, `.frm`, `.frx`, `.ctl`, `.ctx`, `.bas`, `.cls`), alojando controles ActiveX/OCX de 32 bits directamente en memoria (*in-process*) y orquestando compiladores satélites independientes para 32 bits (`vbc32.exe`) y 64 bits (`vbc64.exe`).

## 2. Principios de Diseño
1. **Proceso x86 Nativo para Alojamiento COM/OLE In-Process**: Permite cargar directamente controles `.ocx` y bibliotecas de tipos de 32 bits mediante `CoCreateInstance`, `IOleObject`, `IOleInPlaceSite` y `LoadTypeLibEx` sin necesidad de procesos sustitutos IPC.
2. **Desacoplamiento Estricto de Compiladores (Dual Toolchain)**: La UI se comunica con los compiladores `vbc32.exe` (PE32 enlazado a `msvbvm60.dll`) y `vbc64.exe` (PE64 moderno con soporte `LongPtr`/`LongLong`/`PtrSafe`) mediante el contrato CLI/JSON definido en [docs/TOOLCHAIN_SPEC.md](docs/TOOLCHAIN_SPEC.md).
3. **Conservación Quirúrgica de Archivos**:
   - Lectura con detección de codificación (ANSI con soporte de ACP local, UTF-8 con/sin BOM, UTF-16) y terminadores de línea (`CRLF`/`LF`).
   - Escritura segura a través de archivos atómicos temporales y reemplazo controlado, preservando permisos y metadatos.
   - Preservación íntegra de controles ActiveX no resueltos (`IsUnresolved`) y recursos binarios `.frx`.
4. **Seguridad en Tiempo de Edición**: La introspección de componentes se realiza con `LoadTypeLibEx(..., REGKIND_NONE)` y `LoadLibraryEx(..., LOAD_LIBRARY_AS_DATAFILE)`, mientras que la instanciación en diseño aplica máscara de excepciones FPU (`_control87`) y guardia de diálogos modales (`WH_CBT`).

## 3. Componentes de la Arquitectura

### 3.1 `ViBa7.ProjectModel`
Biblioteca de dominio independiente de la interfaz gráfica:
- **`VbpParser` & `VbgParser`**: Parsers tolerantes a fallos para proyectos `.vbp` y grupos `.vbg`, preservando referencias COM y directivas `Object={GUID}#ver#lcid; archivo.ocx`.
- **`FormDesignParser` & `FormDesignSerializer`**: Motor de lectura y escritura de formularios `.frm` y controles de usuario `.ctl` con soporte de contenedores anidados, menús, conversión `HiMetric <-> Twips` y recursos `.frx` (`FrxResourceFile`).
- **Subsistema ActiveX (`OcxTypeLibReader`, `SystemActiveXRegistryScanner`, `OcxToolboxBitmapExtractor`)**: Escaneo del registro (`HKCR` y `WOW6432Node`), validación de cabeceras PE32 (`IMAGE_FILE_MACHINE_I386`), extracción de CoClasses, interfaces default y de eventos (`IMPLTYPEFLAG_FSOURCE`), `DISPID`, `TKIND_ENUM` y bitmaps `ToolboxBitmap32`.
- **Servicios de Lenguaje (`VbControlMemberCatalog`, `VbRuntimeCatalog`, `IntelliSenseContextDetector`)**: Autocompletado contextual, información de parámetros, formateador y analizador estático VB6.
- **Intérprete en Caliente (`LiveExecutionHost`, `VbStatementInterpreter`, `VbExpressionEvaluator`)**: Ejecución inmediata (`F5`) e inspección en la Ventana Inmediata.

### 3.2 `ViBa7.ActiveXHost`
Anfitrión COM/OLE x86 para controles ActiveX:
- **`ActiveXInPlaceHost` & `DynamicActiveXControl`**: Activación COM en hilo STA, implementación de `IOleClientSite`, `IOleInPlaceSite`, `IOleInPlaceFrame`, `IOleControlSite`, propiedades ambientales (`DISPID_AMBIENT_*`), persistencia `IPersistPropertyBag` / `IPersistStreamInit`, páginas de propiedades nativas (`ISpecifyPropertyPages` + `OleCreatePropertyFrame`) y captura visual mediante `OleDraw` / `PrintWindow`.

### 3.3 `ViBa7.IDE`
Aplicación WPF (`net10.0-windows`, `PlatformTarget=x86`):
- **Shell Principal (`MainWindow`)**: Menús, barra de herramientas, acoplamiento de paneles, explorador de proyectos dual y barra de estado.
- **Diseñador Visual (`FormDesignSurface`, `ControlVisualRenderer`, `PropertyGridPanel`, `ToolboxPanel`)**: Superficie WYSIWYG con grilla magnética, selección múltiple, portapapeles, gotero de color (`ScreenColorDropper`) y editor de menús (`MenuEditorWindow`).
- **Editor de Código (`AvalonEdit`, `VB6FoldingStrategy`, `BranchLinesRenderer`, `IntelliSenseController`)**: Resaltado sintáctico VB6, plegado de código, guías estructurales, marcadores y corrector automático.

### 3.4 `LIBRARY VB6` (Base de Conocimiento Condensada)
Repositorio indexado de ingeniería VB6 / COM / Win32 (`VB6_LIBRARY_INDEX.md`, `VB6_LIBRARY_DATABASE.json`, `TYPELIB_AND_RUNTIME_SYMBOLS.json`, `CODE_SAMPLES_INDEX.json` y `query_library.py`) que consolida la especificación de `msvbvm60.dll`, `VB6.OLB`, arquitectura COM/ActiveX y patrones de migración x86/x64.
