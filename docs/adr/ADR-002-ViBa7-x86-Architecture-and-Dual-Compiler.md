# ADR-002: Transición a ViBa7-x86 y Arquitectura de Compiladores Satélites Desacoplados (x32 / x64)

## Estado
Aprobado (Supera y actualiza la premisa de proceso x64 único de ADR-001)

## Contexto
Durante el análisis de diseño del diseñador visual de controles y la compatibilidad con el ecosistema histórico de Visual Basic 6.0, se identificó que el 90% de las aplicaciones comerciales reales dependen de controles ActiveX (`.OCX`) de terceros compilados exclusivamente en binarios Win32 de 32 bits (PE32) cuyo código fuente no existe.

En la arquitectura de Windows, un proceso de 64 bits no puede cargar librerías de 32 bits en su espacio de memoria. Ejecutar el IDE como un proceso x64 puro imposibilita alojar controles OLE/ActiveX clásicos de forma directa en el diseñador visual, exigiendo capas frágiles de comunicación entre procesos (IPC surrogates).

Asimismo, se requiere la capacidad de compilar tanto proyectos legados 100% compatibles con VB6 SP6 (enlazados con `msvbvm60.dll`) como nuevos proyectos de 64 bits para entornos modernos.

## Decisiones

1. **Denominación y Solución:**
   - La solución adopta la denominación oficial **ViBa7-x86** (`ViBa7-x86.slnx`).
   - El título del entorno de desarrollo pasa a ser **ViBa7-x86 IDE**.

2. **Plataforma del IDE (Win-x86 Nativo en .NET 10 LTS):**
   - El proceso del IDE se compila y ejecuta como **32-bit nativo (x86)** con `LARGEADDRESSAWARE` habilitado (otorgando 4 GB de memoria virtual en Windows de 64 bits).
   - Esto permite el alojamiento in-process directo de controles ActiveX clásicos mediante las interfaces estándar COM/OLE (`IOleObject`, `IOleClientSite`, `IOleInPlaceSite`).

3. **Desacoplamiento Estricto de Compiladores (Toolchain Independiente):**
   - El IDE no contiene lógica interna de emisión de código máquina.
   - Se formaliza el contrato de toolchain CLI + JSON (`docs/TOOLCHAIN_SPEC.md`).
   - **Compilador x32 (`vbc32.exe`)**: Proyecto independiente para generar binarios PE32 100% compatibles con VB6 SP6 enlazados a `msvbvm60.dll`.
   - **Compilador x64 (`vbc64.exe`)**: Proyecto independiente para generar binarios PE64 modernos con soporte de tipos extendidos (VBA7: `LongPtr`, `LongLong`, `PtrSafe`) enlazados a un runtime x64 moderno.
   - Un proceso de 32 bits en Windows puede lanzar y comunicarse con procesos de 64 bits sin impedimento.

## Consecuencias
- Habilita el soporte visual 100% nativo de componentes `.ocx` clásicos en el diseñador sin capas de emulación lentas.
- El 100% del código gestionado actual en C# y WPF se mantiene intacto y pasa toda la batería de pruebas (262 tests superados).
- Los proyectos de compilador x32 y x64 pueden evolucionar con sus propios repositorios, ciclos de lanzamiento y tecnologías de backend (e.g. LLVM, C++, Rust).
