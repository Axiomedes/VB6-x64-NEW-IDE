# Diagnóstico Comparativo y Plan de Acción: Soporte ActiveX / OCX al 100% (Paridad VB6 x86)

Este documento contrasta la especificación técnica de [Manual_Tecnico_ActiveX_OCX_IDE_x86.txt](../Manual_Tecnico_ActiveX_OCX_IDE_x86.txt) con la implementación actual del IDE (`ViBa7.ActiveXHost`, `ViBa7.ProjectModel` y `ViBa7.IDE`), identificando las brechas técnicas y estableciendo la hoja de ruta de ingeniería para alcanzar el **Nivel H (100% de compatibilidad con VB6)**.

---

## 1. Diagnóstico Comparativo: Manual Técnico vs. Implementación Actual

> [!IMPORTANT]
> **Estado Global Actual**: El IDE se encuentra entre el **Nivel C y D (~42% de cobertura total)**. El proceso corre en `x86` STA, descubre controles en `WOW6432Node`, lee TypeLibs con liberación de memoria nativa, instancia OCX mediante `CoCreateInstance` en un contenedor estacionado offscreen (`_sharedParkingHwnd`) y captura snapshots visuales en modo diseño. Sin embargo, **carece de ejecución interactiva en tiempo real (Runtime), sumidero de eventos COM (`IConnectionPoint`), persistencia binaria `.frx` (`IPersistStreamInit`/`IPersistStorage`), contenedor extendido (`VBControlExtender`) y licencias (`IClassFactory2`)**.

### 1.1 Matriz de las 12 Áreas Funcionales (Sección 1 del Manual)

| # | Área Funcional (Manual) | Cobertura | Implementado Actualmente | Brecha para el 100% (Faltante) |
| :--- | :--- | :---: | :--- | :--- |
| **1** | **Descubrimiento y registro** | **85%** | `SystemActiveXRegistryScanner` escanea `HKCR\TypeLib`, `WOW6432Node\CLSID`, valida PE `IMAGE_FILE_MACHINE_I386` (`0x014C`), soporta `regsvr32` y filtra rutas inválidas. | Registro aislado (Reg-Free COM / manifiestos SxS) y caché persistente en disco con invalidación por timestamp/hash del `.ocx`. |
| **2** | **Introspección TypeLib** | **80%** | `OcxTypeLibReader` usa `LoadTypeLibEx(REGKIND_NONE)`, extrae `TKIND_COCLASS`, interfaz default, interfaz de eventos (`IMPLTYPEFLAG_FSOURCE`), `DISPID`, `TKIND_ENUM`, y libera `TLIBATTR`/`TYPEATTR`/`FUNCDESC`/`VARDESC`. `OcxToolboxBitmapExtractor` usa `LOAD_LIBRARY_AS_DATAFILE`. | Extracción de `FUNCFLAGS` (`FUNCFLAG_FIMMEDIATEBIND`, `FUNCFLAG_FDEFAULTBIND`, `FUNCFLAG_FDISPLAYBIND`, `FUNCFLAG_FHIDDEN`, `FUNCFLAG_FNONBROWSABLE`), `VARFLAGS`, subtipos `OLE_COLOR`, `OLE_OPTEXCLUSIVE`, `IFontDisp` e `IPictureDisp` para editores especializados. |
| **3** | **Inserción en el proyecto** | **75%** | `ComponentsWindow` y `ToolboxPanel` agregan herramientas con ícono nativo; `VbpParser` lee/escribe `Object={GUID}#ver#lcid; archivo.ocx`. | Validación de bloqueo al desmarcar un componente en uso en formularios abiertos y actualización automática de versión mayor/menor cuando el sistema tiene una versión superior compatible. |
| **4** | **Alojamiento visual en diseñador** | **55%** | `ActiveXInPlaceHost` y `DynamicActiveXControl` crean la instancia COM, aplican `MaskFpuExceptions` y `NativeDialogGuard`, y renderizan vía `OleDraw` + `PrintWindow` sobre `_sharedParkingHwnd`. | **No existe máquina de estados OLE formal** (`Created -> Sited -> Loaded -> Visible -> InPlaceActive -> UIActive`). Falta `IAdviseSink` / `IViewObject::SetAdvise` para repintado asíncrono, modo *UI-Active* en diseño (ej. cambiar pestañas en `SSTab` en diseño) y `ISimpleFrameSite` para OCX contenedores. |
| **5** | **Propiedades y páginas de propiedades** | **60%** | `PropertyGridPanel` muestra propiedades del TypeLib, y `DynamicActiveXControl.ShowPropertyPages` invoca `ISpecifyPropertyPages` + `OleCreatePropertyFrame`. | Falta `IPropertyNotifySink` (`OnChanged` / `OnRequestEdit`) para sincronizar cambios desde las páginas de propiedades nativas hacia el `PropertyGridPanel` y marcar el `.frm` como modificado; falta `IPerPropertyBrowsing` y el objeto extendido `VBControlExtender`. |
| **6** | **Persistencia `.vbp`/`.frm`/`.frx`** | **35%** | `FormDesignParser` lee cabeceras `Object =`, propiedades escalares (`CustomProperties`) y preserva controles no resueltos (`IsUnresolved`). `OlePropertyBag` lee/escribe escalares básicos. | **Brecha crítica**: `FormDesignSerializer` **no emite** líneas `Object = "{GUID}#ver#0"; "file.ocx"` al guardar el `.frm`. `FrxResourceFile` solo soporta imágenes (`0x0000746C`), **no persiste streams binarios de OCX** (`IPersistStreamInit::Load/Save` hacia `.frx`, `Object.zyx = "Form1.frx":0000`, `IPersistStorage`). |
| **7** | **Eventos COM** | **15%** | `OcxTypeLibReader` extrae las firmas de eventos (`OcxEventInfo`) del TypeLib. | **0% de conexión en vivo**: No existe implementación de `IConnectionPointContainer`, `IConnectionPoint::Advise` ni sumidero `IDispatch` (`ActiveXEventSink`). Tampoco se inyecta el parámetro `Index As Integer` en arreglos de controles OCX ni se propagan argumentos `ByRef` (`VT_BYREF`). |
| **8** | **Automatización / IntelliSense / Código** | **65%** | `VbControlMemberCatalog` fusiona miembros del TypeLib con propiedades de ubicación para autocompletado. | El doble clic sobre un OCX en el diseñador no genera el procedimiento del evento predeterminado (`IMPLTYPEFLAG_FDEFAULT` / `DISPID_VALUE` o primer evento de la interfaz source) con su firma exacta de parámetros VB6. Falta soporte de propiedades indexadas (`TextMatrix(r, c)`) y tipos `IFontDisp`/`IPictureDisp`. |
| **9** | **Modo ejecución dentro del IDE (F5)** | **10%** | `LiveFormWindow` muestra el formulario en ejecución y `LiveControlProxy` expone propiedades de controles intrínsecos al intérprete. | **Brecha crítica**: En modo ejecución (`F5`), los OCX caen en el `default:` de `LiveFormWindow.CreateLiveControlVisual`, mostrando una imagen estática de diseño sin HWND interactivo, sin `Ambient.UserMode = True`, sin eventos y sin puente `IDispatch` en `LiveControlProxy`. |
| **10** | **Compilación y salida ejecutable** | **25%** | `Vb6ProjectCompiler` traduce formularios y módulos a un proyecto intermedio. | El generador de código compilado no instancia los OCX por `CLSID` con su estado binario `.frx`, clave de licencia `IClassFactory2::CreateInstanceLic` ni enlaza sus eventos COM en el ejecutable final x86. |
| **11** | **Manejo de errores y aislamiento** | **55%** | `DynamicActiveXControl` implementa `MaskFpuExceptions` (`_control87`) y `NativeDialogGuard` (`WH_CBT`). | Múltiples bloques `catch { }` silenciosos ocultan el `HRESULT`, la etapa de fallo y `IErrorInfo`. Falta telemetría de fugas de referencias COM (`AddRef`/`Release`), handles GDI y `HWND` al cerrar formularios. |
| **12** | **Soporte `UserControl` (`.ctl`/`.ctx`)** | **25%** | El IDE abre archivos `.ctl` en el diseñador y los parsea. | Falta el motor de ciclo de vida de `UserControl` (`Initialize`, `InitProperties`, `ReadProperties`, `WriteProperties`, `Resize`, `Paint`, `Terminate`), objetos `Ambient` y `Extender`, persistencia en `.ctx` y compilación de proyectos `Type=Control` a `.ocx`. |

---

## 2. Arquitectura Objetivo para el 100% de Paridad VB6

```mermaid
flowchart TD
    subgraph ProjectLayer["Capa de Proyecto y Metadatos (ViBa7.ProjectModel)"]
        Scanner["SystemActiveXRegistryScanner (HKCR + WOW6432Node)"]
        TLib["OcxTypeLibReader (ITypeLib / ITypeInfo / FUNCFLAGS)"]
        Parser["FormDesignParser / Serializer (.frm + Object Headers)"]
        Frx["FrxResourceFile (Blobs Binarios .frx / .ctx)"]
    end

    subgraph HostLayer["Capa de Alojamiento OLE/COM x86 (ViBa7.ActiveXHost)"]
        Factory["ActiveXActivationService (CoGetClassObject / IClassFactory2 / Licencias)"]
        StateMachine["ActiveXInstanceStateMachine (Created -> Sited -> Loaded -> InPlace -> UIActive)"]
        Site["OleClientSite (IOleInPlaceSiteEx, IOleControlSite, ISimpleFrameSite, IAdviseSink, IPropertyNotifySink)"]
        Extender["VbControlExtender (Propiedades del Contenedor + Ambient IDispatch)"]
        Persist["ActiveXPersistenceAdapter (IPersistPropertyBag, IPersistStreamInit, IPersistStorage)"]
        EventSink["ActiveXEventSink (IConnectionPointContainer / IDispatch::Invoke / VT_BYREF)"]
        DispatchBridge["ActiveXDispatchInvoker (IFontDisp, IPictureDisp, OLE_COLOR, Propiedades Indexadas)"]
    end

    subgraph IdeLayer["Capa de IDE y Ejecución (ViBa7.IDE)"]
        DesignHost["ActiveXDesignerHost (Ambient.UserMode = False, Snapshot + Edición UIActive)"]
        RuntimeHost["ActiveXRuntimeHost (HwndHost Real, Ambient.UserMode = True, TranslateAccelerator)"]
        LiveProxy["LiveControlProxy (Puente Intérprete VB6 <-> ActiveXDispatchInvoker / EventSink)"]
    end

    Scanner --> TLib
    Parser <--> Frx
    Parser <--> Persist
    Factory --> StateMachine
    StateMachine --> Site
    Site --> Extender
    StateMachine --> Persist
    StateMachine --> EventSink
    StateMachine --> DispatchBridge
    DesignHost --> StateMachine
    RuntimeHost --> StateMachine
    LiveProxy <--> DispatchBridge
    EventSink --> LiveProxy
```

---

## 3. Plan de Acción Detallado por Fases (Hitos 1 al 8)

### Fase 1: Persistencia Fiel `.vbp` / `.frm` / `.frx` y Ciclo de Vida OLE (Hitos 2 y 3 del Manual)
1. **Emisión de Cabeceras `Object =` en `.frm`** (`FormDesignSerializer.cs`): Serializar al inicio del `.frm` todas las entradas `FormDesignDocument.RequiredObjects` y referencias `OcxComponentReference` activas con formato `Object = "{GUID}#major.minor#lcid"; "NombreArchivo.ocx"`.
2. **Soporte de Blobs Binarios ActiveX en `.frx` y `.ctx`** (`FrxResourceFile.cs`, `FormDesignParser.cs`): Leer y escribir bloques binarios arbitrarios referenciados mediante `Object.zyx = "Form1.frx":0000` y propiedades binarias.
3. **Adaptador de Persistencia COM Completo** (`DynamicActiveXControl.cs`, `Ole32Interop.cs`): Implementar `CreateStreamOnHGlobal`, `IPersistStreamInit::Load/Save`, `IPersistStream`, `IPersistStorage` y soporte de `SAFEARRAY`/`IFontDisp`/`IPictureDisp` en `OlePropertyBag`.
4. **Máquina de Estados OLE y Licencias (`IClassFactory2`)**: Soportar `GetLicInfo`, `RequestLicKey` y `CreateInstanceLic`, formalizando las transiciones `Created -> Sited -> Loaded -> Visible -> InPlaceActive -> UIActive -> Disposed`.

### Fase 2: Contenedor OLE Completo, `VBControlExtender`, Tipos COM y `PropertyNotifySink` (Hito 3 y Sección 5)
1. **`IPropertyNotifySink` e `IAdviseSink`**: Sincronizar cambios realizados desde páginas de propiedades nativas (`OleCreatePropertyFrame`) hacia el `PropertyGridPanel` y refrescar el snapshot ante repintados asíncronos (`IViewObject::SetAdvise`).
2. **Objeto Extendido (`VBControlExtender`) y `TransformCoords`**: Implementar `IOleControlSite::GetExtendedControl` y conversión matemática real entre `HIMETRIC` (`2540/in`) y `Twips` (`1440/in`).
3. **Propiedades Ambientales Completas**: Soportar `DISPID_AMBIENT_FONT (-703)` (`IFontDisp`), `DISPID_AMBIENT_PALETTE (-726)` y `OnAmbientPropertyChange`.
4. **Conversión Avanzada de Tipos COM**: Soportar `StdFont`/`IFontDisp`, `StdPicture`/`IPictureDisp`, `OLE_COLOR`, `SAFEARRAY` y propiedades indexadas (`TextMatrix(r, c)`).

### Fase 3: Alojamiento Interactivo en Modo Ejecución (`ActiveXRuntimeHost`) y Puente de Automatización (Hitos 4 y 5)
1. **Creación de `ActiveXRuntimeHost` (`HwndHost` Real)**: Alojar el `HWND` nativo del OCX en `LiveFormWindow` con `Ambient.UserMode = True`, transfiriendo el estado persistido en diseño y delegando teclado a `IOleInPlaceActiveObject::TranslateAccelerator`.
2. **Integración con `LiveFormWindow` y `LiveControlProxy`**: Enrutar `GetProperty`, `SetProperty` (incluyendo propiedades indexadas) e `InvokeMethod` al `IDispatch` del control y sus subobjetos COM (`Nodes`, `ListItems`, `ColumnHeaders`).
3. **Activación UI-Active en Diseño (`OLEIVERB_UIACTIVATE`) e `ISimpleFrameSite`**: Permitir edición interactiva in-place en diseño (ej. cambiar pestañas en `SSTab`) y contención de controles hijos.

### Fase 4: Eventos COM en Vivo (`IConnectionPoint` / `ActiveXEventSink`) (Hito 4)
1. **Implementación de `ActiveXEventSink`**: Conectar la interfaz `IMPLTYPEFLAG_FSOURCE` mediante `IConnectionPointContainer::FindConnectionPoint` y `Advise`, despachando llamadas `IDispatch::Invoke` por `DISPID`.
2. **Soporte de `VT_BYREF` e `Index`**: Propagar cambios en argumentos `ByRef` de vuelta al control nativo e inyectar `Index As Integer` en arreglos de controles OCX.
3. **Generación de Stubs de Eventos en el Editor**: Generar la firma exacta del evento predeterminado del TypeLib al hacer doble clic sobre un OCX en el diseñador.

### Fase 5: Compilación Nativa/Ejecutable y Soporte `UserControl` `.ctl`/`.ctx` (Hitos 6 y 7)
1. **Compilación de Proyectos con OCX (`Vb6ProjectCompiler.cs`)**: Incrustar estado `.frx` y licencias `RequestLicKey`, instanciar `ActiveXRuntimeHost` y enlazar eventos en el `.exe` compilado, con soporte opcional de manifiestos SxS Reg-Free COM.
2. **Motor Completo de `UserControl` (`.ctl`/`.ctx`)**: Ciclo de vida completo (`Initialize`, `InitProperties`, `ReadProperties`, `WriteProperties`, `Resize`, `Paint`, `Terminate`), `PropertyBag` real y `RaiseEvent`.

### Fase 6: Batería de Pruebas de Aceptación y Telemetría de Estabilidad (Hito 8 y Sección 19)
1. **Diagnóstico Estructurado y Monitor de Recursos**: Registro detallado de `{ Stage, Clsid, Iid, HResultHex, ErrorInfoDescription }` y verificación de cero fugas de objetos GDI/USER/COM tras ciclos repetidos de apertura/cierre.
