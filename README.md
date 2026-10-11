# VB7-x64 IDE

[![Build & Test](https://img.shields.io/badge/.NET-10.0%20LTS-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows)](https://microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSES.md)
[![Tests](https://img.shields.io/badge/Tests-144%20Passing-success.svg)](tests/VB7x64.Tests)

Un entorno de desarrollo integrado (IDE) moderno para **Windows x64** inspirado en **Microsoft Visual Basic 6.0**, diseñado para abrir, explorar, editar, diseñar y refactorizar proyectos clásicos de VB6 (`.vbp`, `.vbg`, `.bas`, `.cls`, `.frm`, `.ctl`, `.pag`) de forma nativa en sistemas de 64 bits, con total seguridad y sin corromper sus formatos ni recursos binarios.

<img width="800" height="600" alt="{B3C140AD-5074-4806-9E99-97C02727499A}" src="https://github.com/user-attachments/assets/c0e8be77-11b8-4247-b2a9-55ed0bca5943" />

---

## 🌟 Características Implementadas

### 1. Diseñador Visual de Formularios (Form Designer WYSIWYG)
* **Superficie de Diseño Bidireccional**: Renderizado fiel del formulario visual a partir de la cabecera `Begin VB.Form...End` de archivos `.frm`.
* **Soporte de Contenedores y Jerarquías**: Capacidad para dibujar controles directamente sobre o dentro de contenedores (e.g. botones o cajas de texto dentro de un `Frame` o `PictureBox`), manteniendo su pertenencia en la jerarquía visual.
* **Portapapeles en el Diseñador**: Operaciones completas de **Copiar**, **Cortar** y **Pegar** (<kbd>Ctrl+C</kbd>, <kbd>Ctrl+X</kbd>, <kbd>Ctrl+V</kbd>) que permiten duplicar controles con nombres únicos autogenerados, mover controles entre contenedores o pegarlos en otros formularios.
* **Diferenciación entre Form y UserControl**: Detección del tipo de raíz; los formularios disponen de barra de título y borde estándar de Windows, mientras que los `UserControl` se presentan como lienzos puros sin barra superior.
* **Controles No Visibles en Ejecución**: Controles como `Timer` mantienen un tamaño fijo (32×32 px / 480×480 twips) no redimensionable en diseño.
* **Rejilla y Ajuste (Grid & Snap to Grid)**: Rejilla de puntos visual personalizable con paso ajustable y alineación magnética automática.
* **Alternancia Rápida Código ↔ Objeto**: Botones dedicados en la barra de herramientas y atajos estándar (<kbd>F7</kbd> para Código, <kbd>Shift+F7</kbd> para Diseñador visual).
  <img width="600" height="510" alt="{79B68E45-75E7-44B6-8E14-6BA66121F5E1}" src="https://github.com/user-attachments/assets/55b15372-5360-405d-8ef9-a94e40fea98f" />

### 2. Editor de Menús Clásico de VB6 (<kbd>Ctrl+E</kbd>)
* **Diálogo de Edición Jerárquica de Menús**: Réplica moderna del editor de menús de VB6 accesible con <kbd>Ctrl+E</kbd> o desde el menú de herramientas.
* **Estructura en Árbol con Puntos de Nivel**: Visualización de niveles de profundidad mediante puntos guía (`....Menú`) al utilizar los botones de sangría (**►** / **◄**).
* **Propiedades de Menú**: Configuración de `Caption`, `Name`, `Index`, `Shortcut` (atajos de teclado estándar como <kbd>Ctrl+O</kbd>, <kbd>F1</kbd>, etc.), `Checked`, `Enabled` y `Visible`.
* **Reordenación Dinámica**: Botones de navegación (Subir **▲**, Bajar **▼**, Insertar, Eliminar, Siguiente).
* **Sincronización Automática**: Serialización y deserialización transparente de las estructuras `Begin VB.Menu...End` en el archivo `.frm`.
<img width="590" height="370" alt="{BB9529BE-444A-4D98-8047-FA142D1CC46C}" src="https://github.com/user-attachments/assets/7f7ff856-be5b-47a5-9062-19128916caef" />

### 3. Servicios de Lenguaje e IntelliSense
* **Auto List Members (<kbd>Ctrl+J</kbd> / `.`)**: Despliegue automático de listas de miembros con iconos descriptivos para propiedades, métodos y eventos al escribir el punto (`.`) o de forma manual.
* **Auto Parameter Info (<kbd>Ctrl+I</kbd>)**: Tooltip interactivo con firmas completas de argumentos para funciones del runtime y métodos de objetos, con resaltado dinámico en negrita del parámetro actualmente activo.
* **Auto Complete Word (<kbd>Ctrl+Espacio</kbd>)**: Autocompletado inteligente de palabras clave, nombres de controles y variables locales/globales con filtrado incremental.
* **Catálogo Exhaustivo de VB6**: Biblioteca preconfigurada con todas las funciones intrínsecas (`Mid`, `MsgBox`, `InStr`, etc.), constantes de sistema (`vbCrLf`, `vbInformation`, `vbNormal`, etc.), tipos de datos y miembros de todos los controles estándar.
<img width="1741" height="520" alt="{48815C18-517A-4B98-B869-3923965FB4BE}" src="https://github.com/user-attachments/assets/050198c3-ea1c-491c-bd68-390b7abf3557" />

### 4. Soporte para Componentes ActiveX / OCX en Tiempo de Diseño
* **Lector Nativo TypeLib COM x64**: Motor P/Invoke a `oleaut32.dll!LoadTypeLibEx` con modo `REGKIND_NONE` que lee metadatos COM directamente desde archivos `.ocx`, `.dll` o `.tlb` en procesos x64 **sin requerir registro en Windows ni permisos de administrador**.
* **Diálogo de Componentes (<kbd>Ctrl+T</kbd>)**:
  * Accesible desde `Proyecto ➔ Componentes...` o con el atajo clásico <kbd>Ctrl+T</kbd>.
  * Exploración de disco mediante el botón **Examinar...** para cargar cualquier archivo `.ocx` o `.tlb`.
  * Visualización de ruta, biblioteca, GUID, versión y controles expuestos.
  * Persistencia automática de directivas `Object={GUID}#Ver#0; Archivo.ocx` en el archivo de proyecto `.vbp`.
  * Carga y registro automático de componentes al abrir cualquier `.vbp` o `.vbg`.
* **Integración en la Caja de Herramientas (*Toolbox*)**: Los controles del OCX se incorporan dinámicamente como herramientas seleccionables para dibujarse en el formulario.
* **Expansión Dinámica de IntelliSense**: Al instanciar un control OCX o declarar una variable tipada (e.g. `Dim c As axJColCombo`), IntelliSense ofrece todas sus propiedades, métodos, firmas y eventos en tiempo real.
* **Renderizado Adaptativo en Diseñador**: Los controles OCX se representan gráficamente en el formulario según su categoría (controles de lista/combo se visualizan interactivos y controles generales muestran el distintivo `🧩`).

### 5. Editor de Código y Productividad Visual
* **Editor AvalonEdit Optimizado**: Resaltado sintáctico fiel para VB6 (keywords, tipos, literales de fecha `#mm/dd/yyyy#`, cadenas escapadas, comentarios con apóstrofe y `Rem`).
* **Resaltado de Línea Activa**: Resaltado sutil y configurable de la línea donde se ubica el cursor.
* **Guías de Estructura de Control (Branch Lines)**: Líneas verticales que conectan bloques `If...End If`, `Select Case`, `For...Next`, `Do...Loop`, `With...End With` y procedimientos.
* **Detalles de Fin de Bloque (Inlay Hints / Ghost Text)**: Anotaciones discretas al final de bloques extensos (ej. `End If  ' [If precio > 100]`).
* **Edición Rápida**: Comentar/descomentar en bloque (<kbd>Ctrl+K, Ctrl+C</kbd> / <kbd>Ctrl+K, Ctrl+U</kbd>), invertir asignaciones (`A = B` ⇄ `B = A`) y conversión de mayúsculas/minúsculas.

### 6. Exploración y Navegación Avanzada
* **Explorador de Proyectos Dual**:
  * **Modo Estándar**: Árbol clásico purista de VB6 (Proyecto ➔ Carpetas de tipos ➔ Archivos).
  * **Modo Avanzado**: Expansión en profundidad de componentes internos (Sub, Function, Property, Eventos, Constantes, Variables de módulo, Enums y Types).
  * **Filtrado en Tiempo Real**: Cuadro de búsqueda dinámica instantánea para proyectos, archivos y miembros.
* **Búsqueda de Referencias y Usos (Find References - <kbd>Shift+F12</kbd>)**: Localiza apariciones del identificador en el archivo, proyecto o grupo `.vbg`, excluyendo comentarios y cadenas, con navegación por doble clic.
* **Historial de Navegación y Marcadores (Bookmarks)**:
  * Historial Atrás / Adelante (<kbd>Alt+←</kbd> / <kbd>Alt+→</kbd>).
  * Marcadores visuales en el margen (`🔖`) con navegación secuencial (<kbd>F2</kbd> / <kbd>Shift+F2</kbd>) y alternancia rápida (<kbd>Ctrl+F2</kbd>).
* **Explorador de Tareas (TODO / Task List)**: Escaneo automático de etiquetas `' TODO:`, `' BUG:`, `' FIXME:`, `' NOTE:` y `' HACK:` con priorización y salto a la línea.

### 7. Asistentes de Código y Snippets
* **Asistente de Propiedades (Property Builder)**: Generación automática de bloques `Property Get/Let` (tipos primitivos) y `Property Get/Set` (objetos) con variable de respaldo tipada.
* **Asistente de Procedimientos (Procedure Builder)**: Diálogo interactivo para declarar métodos con visibilidad, tipos de retorno, argumentos tipados (`ByVal`/`ByRef`/`Optional`) y manejo de errores.
* **Biblioteca de Snippets (AutoText con expansión por <kbd>Tab</kbd>)**: Plantillas para `if`, `ife`, `sel`, `for`, `fore`, `dow`, `dou`, `whi`, `with`, `prop`, `sub`, `fn`, `err`.
* **Portapapeles Múltiple (Clipboard Ring - <kbd>Ctrl+Shift+V</kbd>)**: Historial rotativo de los últimos fragmentos copiados con previsualización e inserción directa.

### 8. Mantenimiento y Calidad de Código
* **Formateador de Código VB6 (Code Formatter)**: Indentación automática según profundidad de anidación, alineación estética de cláusulas `As` en declaraciones `Dim` y normalización de líneas en blanco.
* **Ordenador de Procedimientos (Sort Procedures)**: Reorganización alfabética o por visibilidad preservando el bloque de declaraciones de módulo.
* **Comparador de Cambios (File Diff Viewer)**: Visor de diferencias lado a lado y unificado con coloreado de adiciones y supresiones frente a la versión en disco.
* **Estadísticas de Proyecto (Project Statistics)**: Dashboard analítico con cálculo de SLOC efectivo, volumen de comentarios, recuento de procedimientos y desglose por archivo.
* **Motor de Manejo de Errores (Error Handling Engine)**: Inserción y retiro masivo de esquemas configurables (`MsgBox`, `Debug.Print`, `HandleError`, `On Error Resume Next`) con soporte de macros dinámicas.
* **Inyección de Trazas (Tracing Engine)**: Inyección y retiro antes de producción de marcas de trazabilidad (`Debug.Print "--> Entrando a ..."`) con identificadores unívocos (`' [VB7_TRACE]`).
* **Diseñador de Orden de Tabulación (Tab Order Designer)**: Reordenación manual o auto-ordenación geográfica (Top ➔ Left) de la propiedad `TabIndex` en formularios.
* **Verificador de Teclas Rápidas (Hotkey Designer)**: Detección y auto-resolución de conflictos de aceleradores `&` en `Caption`.
* **Analizador de Calidad de Código (Code Analyzer)**: Auditoría estática que reporta falta de `Option Explicit`, código muerto en rutinas `Private` huérfanas, variables sin utilizar y procedimientos vacíos.

### 9. Ejecución en Caliente (Hot-Run / <kbd>F5</kbd>) y Ventana Inmediata
* **Ejecución Instantánea en Memoria**: Lanzamiento inmediato de formularios y módulos de código sin compilar a disco, utilizando el motor de evaluación e interpretación con semántica exacta de VB6.
* **Anfitrión Visual Interactivo (`LiveFormWindow`)**: Instanciación dinámica de controles nativos (`CommandButton`, `TextBox`, `Label`, `CheckBox`, `OptionButton`, `ListBox`, `ComboBox`, `Frame`, `Timer`, `Menu`) con proxies bidireccionales (`LiveControlProxy` y `LiveFormProxy`).
* **Despacho Reactivo de Eventos**: Soporte completo para `Form_Load`, `Click`, `Change`, `Timer`, `DblClick` y `Form_Unload`.
* **Ventana Inmediata (Immediate Window - <kbd>Ctrl+G</kbd>)**:
  * Evaluación instantánea de expresiones mediante `? expr` o `Print expr`.
  * Ejecución directa de sentencias (`Variable = Valor`, llamadas a procedimientos).
  * Consola de salida de `Debug.Print` en tiempo de ejecución.
  * Historial de comandos navegable con las flechas <kbd>↑</kbd> y <kbd>↓</kbd>.
* **Controles de Depuración**:
  * Iniciar (<kbd>F5</kbd> / Botón ▶️ en Toolbar).
  * Interrumpir / Pausa (<kbd>Ctrl+Break</kbd> / Botón ⏸️).
  * Detener / Terminar (<kbd>Shift+F5</kbd> / Botón ⏹️).
  * Paso a paso por instrucciones (<kbd>F8</kbd>) y por procedimientos (<kbd>Shift+F8</kbd>).
  * Gestión de Puntos de Interrupción (<kbd>F9</kbd> / <kbd>Ctrl+Shift+F9</kbd>).

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
│   ├── VB7x64.IDE/             # Shell WPF x64: Menús, Toolbar, Editor, Diseñador, Hot-Run, Immediate Window y UI
│   └── VB7x64.ProjectModel/    # Modelo de proyectos, Parsers, Motor Hot-Run (Execution), ActiveX y E/S segura
├── tests/
│   ├── VB7x64.Tests/           # Suite de pruebas unitarias xUnit (154 pruebas / 100% éxito)
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

# Ejecutar la suite de pruebas unitarias automatizadas (154 pruebas)
dotnet test

# Iniciar el IDE
dotnet run --project src/VB7x64.IDE
```

---

## 🗺️ Roadmap de Milestones

| Milestone | Estado | Descripción |
| :--- | :---: | :--- |
| **M1: Núcleo y Seguridad de Archivos** | ✅ Completado | Modelos `.vbp`/`.vbg`, manejo ANSI/CRLF, preservación `.frx`, pruebas de round-trip. |
| **M2: Productividad Visual y Refactorización** | ✅ Completado | Branch lines, Inlay hints, Property/Procedure Builder, AutoText, Tab Order, Hotkey Checker. |
| **M3: Diseñador de Formularios (Form Designer)** | ✅ Completado | WYSIWYG bidireccional, contenedores (`Frame`/`PictureBox`), portapapeles, Grid Snap, UserControls. |
| **M4: Editor de Menús (<kbd>Ctrl+E</kbd>)** | ✅ Completado | Editor jerárquico de menús, indentación por puntos `....`, atajos de teclado y sincronización `.frm`. |
| **M5: IntelliSense y Carga de OCX en Diseño** | ✅ Completado | List Members, Parameter Info, Complete Word, lector COM TypeLib nativo x64, diálogo Componentes (<kbd>Ctrl+T</kbd>). |
| **M6: Ejecución en Caliente (Hot-Run / <kbd>F5</kbd>)** | ✅ Completado | Motor de ejecución en memoria sin compilar a disco, ventana en vivo interactiva (`LiveFormWindow`), breakpoints (<kbd>F9</kbd>), paso a paso (<kbd>F8</kbd>), Ventana Inmediata (<kbd>Ctrl+G</kbd>, `?` / `Print`) y `Debug.Print` en tiempo real. |
| **M7: Compilación Nativa x32 (MSVBVM60.DLL)** | ⏳ **Próximo** | Emisor nativo de ejecutables PE32 (x86) independientes con cabeceras VB6 clásicas y enlace directo al runtime del sistema `msvbvm60.dll` (Opción B). |
| **M8: Generación y Compilación Nativa x64** | ⏳ **Planificado** | Compilador / emisor nativo a ejecutables y librerías de 64 bits para arquitectura Windows x64 moderna. |
