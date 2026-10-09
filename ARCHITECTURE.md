# Arquitectura Técnica — VB7-x64 IDE

## 1. Visión General
VB7-x64 IDE es un entorno de desarrollo para Windows x64 desarrollado con C# y WPF sobre .NET 10 LTS.
Su objetivo primario (v1.0 MVP) es proporcionar un entorno de edición altamente confiable, reactivo e intuitivo para proyectos legados de Microsoft Visual Basic 6.0, garantizando la preservación estricta de formatos binarios y codificación de caracteres.

## 2. Principios de Diseño
1. **Desacoplamiento Estricto**: La UI no interactúa con el compilador ni con el runtime de manera sincrónica ni acoplada. Toda interacción futura se realizará a través de un protocolo de comunicación aislado (como JSON-RPC / Language Server Protocol).
2. **Conservación Quirúrgica de Archivos**:
   - Lectura con detección de codificación (ANSI con soporte de ACP local, UTF-8 con/sin BOM, UTF-16) y terminadores de línea (CRLF/LF).
   - Escritura segura a través de archivos atómicos temporales y reemplazo controlado, preservando permisos y metadatos.
   - Creación de backups automáticos (`.bak`) si está configurado.
   - Guardado no destructivo de formularios `.frm`: sólo se reescribe el bloque de código posterior a los atributos de formulario (`VB_Name`, etc.), preservando la definición de controles sin serializarla de nuevo.
3. **Seguridad en Tiempo de Edición**: Abrir proyectos o inspeccionar archivos nunca ejecuta código bajo ninguna circunstancia.

## 3. Componentes de la Arquitectura

### 3.1 `VB7x64.ProjectModel`
Biblioteca de clases (.NET 10 x64) independiente de la interfaz gráfica:
- **`VbpParser`**: Parser tolerante a fallos para archivos de proyecto `.vbp`. Retiene líneas no reconocidas, mantiene el orden original y resuelve rutas relativas/absolutas.
- **`VB6Project` & `ProjectItem`**: Modelo en memoria que representa los componentes (`Form`, `Module`, `ClassModule`, `UserControl`, referencias de tipo COM/ActiveX).
- **`EditorDocument`**: Modelo de abstracción de archivo en memoria:
  - Ruta de archivo, hash SHA-256 inicial y hash guardado.
  - Detección de encoding y presencia de BOM.
  - Convención de fin de línea detectada (`\r\n` o `\n`).
  - Estado sucio (`IsDirty`), solo lectura (`IsReadOnly`).
  - Detección de cambios externos en disco mediante `FileSystemWatcher` / chequeo de timestamp.
- **`SafeFileStorage`**: Mecanismo de lectura y escritura segura con archivo temporal intermedio y copias `.bak`.

### 3.2 `VB7x64.IDE`
Aplicación WPF para Windows x64:
- **Shell**: `MainWindow` con sistema de menús, barra de herramientas, área de acoplamiento de paneles y barra de estado (con información de línea, columna, codificación y estado de modificación).
- **Project Explorer**: Árbol de visualización del proyecto agrupando Formularios, Módulos, Módulos de Clase y Recursos.
- **Editor Host**: Contenedor de pestañas que aloja instancias de AvalonEdit con resaltador sintáctico personalizado para VB6 (`VB6SyntaxHighlighting`).
- **Settings & Logging**: Gestión de configuración de usuario y registro de eventos/errores.

## 4. Diagrama de Flujo de Trabajo

```text
[ Archivo en Disco (.vbp, .frm, .bas, .cls) ]
                     │
                     ▼
             [ SafeFileStorage ] ────> Detecta Encoding / CRLF / Hash original
                     │
                     ▼
            [ VB7x64.ProjectModel ]
             (VbpParser / EditorDocument)
                     │
                     ▼
             [ VB7x64.IDE (WPF) ]
         ┌───────────┴───────────┐
         ▼                       ▼
  [ ProjectExplorer ]     [ AvalonEdit ]
  (Árbol del proyecto)   (Editor con sintaxis VB6)
```
