# -*- coding: utf-8 -*-
"""
Builds the complete Quick-Access Index & Knowledge Base for VB6 / COM / Win32 Library.
Generates:
  1. TYPELIB_AND_RUNTIME_SYMBOLS.json
  2. CODE_SAMPLES_INDEX.json
  3. VB6_LIBRARY_DATABASE.json
  4. query_library.py
  5. VB6_LIBRARY_INDEX.md
"""

import os
import sys
import json
import re

LIB_DIR = r"C:\Programming\IDE VB6 x64\LIBRARY VB6"

def parse_com_reports():
    print("Parsing COM reports...")
    msvbvm_list_file = os.path.join(LIB_DIR, "Reporte_Listado_COM_msvbvm60.dll.txt")
    vb6olb_list_file = os.path.join(LIB_DIR, "Reporte_Listado_COM_vb6.olb.txt")
    msvbvm_dll_file = os.path.join(LIB_DIR, "Reporte_DLL_msvbvm60.dll.txt")
    vb6olb_idl_file = os.path.join(LIB_DIR, "COM_TypeLib_VB6.OLB.txt")

    msvbvm_items = []
    if os.path.exists(msvbvm_list_file):
        with open(msvbvm_list_file, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                parts = [p.strip() for p in line.split("|")]
                if len(parts) >= 2 and parts[0].startswith("{"):
                    msvbvm_items.append({
                        "guid": parts[0],
                        "name": parts[1],
                        "threading": parts[2] if len(parts) > 2 else "",
                        "binary": parts[3] if len(parts) > 3 else ""
                    })

    vb6olb_items = []
    if os.path.exists(vb6olb_list_file):
        with open(vb6olb_list_file, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                parts = [p.strip() for p in line.split("|")]
                if len(parts) >= 2 and parts[0].startswith("{"):
                    vb6olb_items.append({
                        "guid": parts[0],
                        "name": parts[1],
                        "threading": parts[2] if len(parts) > 2 else "",
                        "binary": parts[3] if len(parts) > 3 else ""
                    })

    # Extract IDL details for VB6 controls
    controls_idl = {}
    if os.path.exists(vb6olb_idl_file):
        with open(vb6olb_idl_file, "r", encoding="utf-8", errors="ignore") as f:
            idl_text = f.read()

        # Find interfaces
        interface_blocks = re.findall(r'interface\s+(_?\w+)\s*:\s*IUnknown\s*\{([^}]*)\};', idl_text)
        for iface_name, iface_body in interface_blocks:
            methods = []
            for line in iface_body.split('\n'):
                line = line.strip()
                if line.startswith('HRESULT') or line.startswith('void'):
                    m = re.match(r'(?:HRESULT|void)\s+(\w+)\s*\((.*?)\);', line)
                    if m:
                        methods.append({"name": m.group(1), "args": m.group(2).strip()})
            controls_idl[iface_name] = methods

    # Extract Enums and Module info from msvbvm60 report
    runtime_enums = {}
    if os.path.exists(msvbvm_dll_file):
        with open(msvbvm_dll_file, "r", encoding="utf-8", errors="ignore") as f:
            dll_text = f.read()

        enum_blocks = re.findall(r'typedef\s+\[v1_enum\]\s+enum\s*\{([^}]*)\}\s*(\w+);', dll_text)
        for enum_body, enum_name in enum_blocks:
            members = {}
            for line in enum_body.split('\n'):
                line = line.strip().rstrip(',')
                if '=' in line:
                    k, v = line.split('=', 1)
                    members[k.strip()] = v.strip()
            runtime_enums[enum_name] = members

    symbols_data = {
        "msvbvm60_runtime": {
            "typelib_guid": "{000204ef-0000-0000-c000-000000000046}",
            "version": "6.0",
            "name": "VBA",
            "components_count": len(msvbvm_items),
            "components": msvbvm_items,
            "enums": runtime_enums
        },
        "vb6_controls_typelib": {
            "typelib_guid": "{fcfb3d2e-a0fa-1068-a738-08002b3371b5}",
            "version": "6.0",
            "name": "VB",
            "components_count": len(vb6olb_items),
            "components": vb6olb_items,
            "interfaces_methods": controls_idl
        }
    }

    out_file = os.path.join(LIB_DIR, "TYPELIB_AND_RUNTIME_SYMBOLS.json")
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump(symbols_data, f, indent=2)
    print(f"Saved {out_file}")
    return symbols_data

def parse_code_repository():
    print("Parsing Rockford Lhotka code repository...")
    base_dir = os.path.join(LIB_DIR, "VB6 Business  Distributed Objects")
    code_index = {
        "summary": {
            "total_files": 0,
            "class_modules_cls": 0,
            "forms_frm": 0,
            "standard_modules_bas": 0,
            "projects_vbp": 0,
            "project_groups_vbg": 0,
            "other_files": 0
        },
        "projects": [],
        "classes": [],
        "modules": [],
        "forms": []
    }

    for root, dirs, files in os.walk(base_dir):
        for f in files:
            code_index["summary"]["total_files"] += 1
            fpath = os.path.join(root, f)
            relpath = os.path.relpath(fpath, base_dir)
            ext = os.path.splitext(f)[1].lower()

            if ext == ".cls":
                code_index["summary"]["class_modules_cls"] += 1
                try:
                    with open(fpath, "r", encoding="latin1") as fl:
                        content = fl.read()
                    methods = re.findall(r'Public\s+(Sub|Function|Property\s+Get|Property\s+Let|Property\s+Set)\s+(\w+)', content, re.IGNORECASE)
                    implements = re.findall(r'^\s*Implements\s+(\w+)', content, re.IGNORECASE | re.MULTILINE)
                    instancing = re.findall(r'Instancing\s*=\s*(\d+)', content)
                    code_index["classes"].append({
                        "file": f,
                        "relative_path": relpath,
                        "class_name": os.path.splitext(f)[0],
                        "implements": implements,
                        "instancing": instancing[0] if instancing else "1",
                        "methods_count": len(methods),
                        "methods": [f"{m[0]} {m[1]}" for m in methods]
                    })
                except Exception:
                    pass
            elif ext == ".frm":
                code_index["summary"]["forms_frm"] += 1
                code_index["forms"].append({
                    "file": f,
                    "relative_path": relpath,
                    "form_name": os.path.splitext(f)[0]
                })
            elif ext == ".bas":
                code_index["summary"]["standard_modules_bas"] += 1
                try:
                    with open(fpath, "r", encoding="latin1") as fl:
                        content = fl.read()
                    methods = re.findall(r'Public\s+(Sub|Function)\s+(\w+)', content, re.IGNORECASE)
                    code_index["modules"].append({
                        "file": f,
                        "relative_path": relpath,
                        "module_name": os.path.splitext(f)[0],
                        "methods_count": len(methods),
                        "methods": [f"{m[0]} {m[1]}" for m in methods]
                    })
                except Exception:
                    pass
            elif ext == ".vbp":
                code_index["summary"]["projects_vbp"] += 1
                try:
                    with open(fpath, "r", encoding="latin1") as fl:
                        content = fl.read()
                    proj_type = re.findall(r'Type\s*=\s*(\w+)', content)
                    proj_name = re.findall(r'Title\s*=\s*\"([^\"]+)\"', content)
                    code_index["projects"].append({
                        "file": f,
                        "relative_path": relpath,
                        "project_type": proj_type[0] if proj_type else "Exe",
                        "project_title": proj_name[0] if proj_name else os.path.splitext(f)[0]
                    })
                except Exception:
                    pass
            elif ext == ".vbg":
                code_index["summary"]["project_groups_vbg"] += 1
            else:
                code_index["summary"]["other_files"] += 1

    out_file = os.path.join(LIB_DIR, "CODE_SAMPLES_INDEX.json")
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump(code_index, f, indent=2)
    print(f"Saved {out_file}")
    return code_index

def build_master_database(symbols_data, code_index):
    print("Building master knowledge base database...")
    db = {
        "metadata": {
            "title": "VB6 / COM / Win32 Quick-Access Engineering Library",
            "version": "1.0",
            "created": "2026-10-15",
            "location": LIB_DIR,
            "target_ecosystems": [
                "VB x32/x64 Compiler (Rust parser, AST, codegen, vtables, linking)",
                "VB7-x64 IDE (.NET C# forms, syntax highlighting, IntelliSense, COM browser)",
                "VB x32/x64 New Runtime (msvbvm60 replacement, BSTR, SAFEARRAY, VARIANT, ErrObject, Collection)",
                "Win32 API x64 Migration (LongPtr, LongLong, 64-bit alignment, Subclassing, WndProc)"
            ]
        },
        "books": [
            {
                "id": "curland",
                "title": "Advanced Visual Basic 6: Power Techniques for Everyday Programs",
                "author": "Matthew Curland",
                "publisher": "Addison-Wesley",
                "year": 2000,
                "pages": 514,
                "file": "Advanced-Visual-Basic-6-Power-Techniques-for-Everyday-Programs.pdf",
                "importance": "CRITICAL / FOUNDATIONAL",
                "summary": "The ultimate authority on the internal plumbing of VB6. Details how the VB6 runtime represents memory, object references, virtual method tables (vtables), array descriptors (SAFEARRAY), strings (BSTR), and thread apartments.",
                "relevance": {
                    "compiler_x64": "Blueprint for implementing COM vtable layout, direct interface implementation without dual interface overhead, function pointers via AddressOf, and ASM call thunks.",
                    "runtime_x64": "Crucial details for reimplementing SAFEARRAY descriptors, BSTR length-prefixed strings, Variant memory layouts, weak reference management, and memory manipulation (VarPtr, StrPtr, ObjPtr).",
                    "ide_x64": "Understanding type library generation, interface introspection, early vs late binding mechanics.",
                    "win32_api_x64": "Window subclassing, hooking, function pointer dispatch, and thunks."
                },
                "chapters": [
                    {"number": 1, "title": "Building Blocks", "page": 1, "topics": ["Living Pointers", "VBoost Objects", "CopyMemory", "VarPtr, StrPtr, ObjPtr", "Memory Layout"]},
                    {"number": 2, "title": "Leveraging Arrays", "page": 11, "topics": ["SAFEARRAY Descriptors", "Reading Array Variables", "Writing to Array Variables", "Wrapping C Arrays", "Multidimensional Redim"]},
                    {"number": 3, "title": "IUnknown: An Unknown Quantity", "page": 41, "topics": ["VB and IUnknown", "Calling IUnknown Functions Directly", "QueryInterface Rules", "VTable Layout"]},
                    {"number": 4, "title": "Binding Functions to Objects", "page": 53, "topics": ["Early vs Late Binding", "Name Binding at Runtime (IDispatch)", "VTable-Binding Custom Control Interfaces"]},
                    {"number": 5, "title": "Object Design Constructs", "page": 73, "topics": ["Implements Abstraction", "Interface Polymorphism", "Pluggability", "Aggregation and Reuse"]},
                    {"number": 6, "title": "Circular References", "page": 101, "topics": ["Reference Counting Pitfalls", "Intermediate Objects", "Weak References", "Collection Cleanups"]},
                    {"number": 7, "title": "External Object Creation", "page": 119, "topics": ["CoCreateInstance", "IClassFactory", "Custom Class Activation"]},
                    {"number": 8, "title": "Lightweight COM Objects", "page": 143, "topics": ["COM Objects without Class Modules", "Direct Memory VTables", "Zero-Overhead COM Entities"]},
                    {"number": 9, "title": "Large Systems of Objects", "page": 183, "topics": ["Complex Object Graphs", "Identity Maintenance", "Flyweight Design"]},
                    {"number": 10, "title": "VB Objects and the Running Object Table", "page": 203, "topics": ["ROT (Running Object Table)", "Monikers", "ActiveX EXE Shared Objects"]},
                    {"number": 11, "title": "Calling Function Pointers", "page": 227, "topics": ["Dynamic Function Dispatch", "CallWindowProc Hack", "Machine Code Thunks", "stdcall vs cdecl"]},
                    {"number": 12, "title": "Overriding Functions", "page": 261, "topics": ["VTable Patching", "Function Hooking", "Crash-Proof Subclassing"]},
                    {"number": 13, "title": "Threads in VB", "page": 279, "topics": ["Apartment Threading (STA)", "CoInitialize", "Cross-Apartment Marshaling", "Worker Threads"]},
                    {"number": 14, "title": "Strings in VB", "page": 357, "topics": ["BSTR Memory Architecture", "Unicode / ANSI Conversions", "SysAllocString & SysFreeString"]}
                ]
            },
            {
                "id": "mckinney",
                "title": "Hardcore Visual Basic",
                "author": "Bruce McKinney",
                "publisher": "Microsoft Press",
                "year": 1997,
                "pages": 768,
                "file": "hardcorevisualbasic5.pdf",
                "importance": "HIGH / PRACTICAL LOW-LEVEL",
                "summary": "Classic practical guide to low-level Windows programming in Visual Basic. Unveils the C-like power hidden behind Basic: pointers, memory copies, subclassing, Windows API, and COM type libraries.",
                "relevance": {
                    "compiler_x64": "Shows exact behavior of AddressOf, callback signatures, type aliasing in Declare, and type coercion rules.",
                    "runtime_x64": "Detailed behavior of BSTR strings, byte arrays, ANSI-to-Unicode conversion layers in Declare calls, error raising and Win32 error translation.",
                    "ide_x64": "Techniques for project structuring, resource management, and debugging tools.",
                    "win32_api_x64": "HWND handling, Windows messaging architecture, subclassing traps, memory handles (HGLOBAL, GDI)."
                },
                "chapters": [
                    {"number": 1, "title": "The Spirit of Basic", "page": 1, "topics": ["Language Philosophy", "Language Purification", "Control Flow"]},
                    {"number": 2, "title": "Variables, Types, and Pointers", "page": 47, "topics": ["CopyMemory Tricks", "VarPtr, StrPtr, ObjPtr", "Pointers to Variables", "User-Defined Types (UDT)"]},
                    {"number": 3, "title": "Objects, Interfaces, and Collections", "page": 109, "topics": ["Implements Polymorphism", "Polymorphic Filters", "Custom Collection Classes"]},
                    {"number": 4, "title": "Strings and Memory", "page": 181, "topics": ["BSTR Internals", "Unicode / ANSI String Bridging", "Byte Array Manipulation"]},
                    {"number": 5, "title": "Error Handling and Debugging", "page": 247, "topics": ["Raising and Trapping Errors", "Translating Win32 Errors to VB Errors", "Assertions"]},
                    {"number": 6, "title": "Windows Architecture and API", "page": 315, "topics": ["HWND Handles", "Process IDs", "Windows Messages Traps", "Window Subclassing"]},
                    {"number": 7, "title": "Resources, Dialogs, and Bitmaps", "page": 389, "topics": ["Windows Resource Files (.res)", "GDI Bitmaps", "Custom Dialogs"]},
                    {"number": 8, "title": "Controls and Windows Extensions", "page": 471, "topics": ["Common Dialog Extensions", "Shell Integration", "Custom Control Behaviors"]},
                    {"number": 9, "title": "COM Behind the Curtain", "page": 539, "topics": ["COM Terminology", "COM Registry Layout", "Type Libraries (TLB)", "Direct VTables"]},
                    {"number": 10, "title": "Files, Directories, and Shell Operations", "page": 657, "topics": ["File Times", "Directory Walking", "Shell Namespace"]}
                ]
            },
            {
                "id": "balena",
                "title": "Programming Microsoft Visual Basic 6.0",
                "author": "Francesco Balena",
                "publisher": "Microsoft Press",
                "year": 1999,
                "pages": 1318,
                "file": "Programming VB6.pdf",
                "importance": "NORMATIVE STANDARD / COMPLETE LANGUAGE SPECIFICATION",
                "summary": "The definitive, comprehensive reference for VB6 syntax, controls, events, and runtime functions. Defines the baseline expected language behavior for the compiler, form designer, and runtime.",
                "relevance": {
                    "compiler_x64": "Serves as the golden specification for AST parsing, statement semantics, operator precedence, scoping, variable declaration, arrays, and classes.",
                    "runtime_x64": "Full functional specification for VBA runtime modules: Strings (Mid, InStr, Replace), Conversion (CStr, CLng, Val), Math, DateTime, FileSystem, ErrObject, and Collection.",
                    "ide_x64": "Complete blueprint for form files (.frm), control properties, events, menus, toolbars, user controls, and data binding.",
                    "win32_api_x64": "Includes comprehensive Appendix of Win32 API functions adapted to VB6."
                },
                "chapters": [
                    {"number": 1, "title": "First Steps with Microsoft Visual Basic 6", "page": 3, "topics": ["IDE Architecture", "Project Types", "Compilation Models"]},
                    {"number": 2, "title": "Introduction to Forms", "page": 35, "topics": ["Form Lifecycle", "Load, Show, Activate, QueryUnload, Unload", "MDI Forms"]},
                    {"number": 3, "title": "Intrinsic Controls", "page": 91, "topics": ["TextBox, Label, CommandButton, CheckBox, OptionButton, ListBox, ComboBox, Timer, ScrollBars"]},
                    {"number": 4, "title": "Variables and Procedures", "page": 151, "topics": ["Data Types", "ByVal / ByRef Parameters", "Optional & ParamArray", "Scoping & Static"]},
                    {"number": 5, "title": "The Visual Basic for Applications Library", "page": 211, "topics": ["Strings, Conversion, Math, DateTime, FileSystem, Interaction, Information"]},
                    {"number": 6, "title": "Classes and Objects", "page": 275, "topics": ["Class Modules (.cls)", "Property Get/Let/Set", "Class_Initialize & Class_Terminate"]},
                    {"number": 7, "title": "Events, Polymorphism, and Inheritance", "page": 333, "topics": ["Custom Events", "RaiseEvent & WithEvents", "Implements Interface Polymorphism"]},
                    {"number": 8, "title": "Advanced Forms and Dialogs", "page": 449, "topics": ["Dynamic Control Arrays", "Menus", "System Dialogs"]},
                    {"number": 9, "title": "Windows Common Controls: Part I", "page": 499, "topics": ["ImageList, ToolBar, StatusBar, ProgressBar, Slider"]},
                    {"number": 10, "title": "Windows Common Controls: Part II", "page": 561, "topics": ["TreeView, ListView, TabStrip, CoolBar"]},
                    {"number": 11, "title": "ActiveX Components (DLL & EXE)", "page": 813, "topics": ["In-Process vs Out-of-Process Servers", "Threading Models", "Instancing Options"]},
                    {"number": 12, "title": "ActiveX Controls (UserControls)", "page": 905, "topics": ["UserControl Lifecycle", "PropertyBag Persistence", "Ambient & Extender Properties"]},
                    {"number": 13, "title": "Appendix: Windows API Functions", "page": 1189, "topics": ["Declare Syntax", "Core Win32 APIs for VB6", "Structures and Constants"]}
                ]
            },
            {
                "id": "appleman_win32",
                "title": "Visual Basic Programmer's Guide to the Win32 API",
                "author": "Daniel Appleman",
                "publisher": "Ziff-Davis Press / Macmillan",
                "year": 1999,
                "pages": 1556,
                "file": "VB Programmer Guide Win32 API.pdf",
                "importance": "ESSENTIAL WIN32 / 64-BIT PORTING BIBLE",
                "summary": "The monumental guide to calling the Windows Win32 API from Visual Basic. Details parameter passing conventions, memory layouts, pointers, handles, structures, callbacks, and Windows messages.",
                "relevance": {
                    "compiler_x64": "Crucial for designing 64-bit Declare syntax, LongPtr / LongLong types, ByVal/ByRef marshaling, struct padding/alignment, and callback calling conventions.",
                    "runtime_x64": "Memory allocation APIs (GlobalAlloc, LocalAlloc, HeapAlloc), ANSI/Unicode BSTR marshaling, and translating API errors.",
                    "ide_x64": "API declaration text viewer and validator.",
                    "win32_api_x64": "Complete reference for HWND, HDC, HBITMAP handles, GDI drawing, Windows messaging, and process/thread management."
                },
                "chapters": [
                    {"number": 1, "title": "What is the Win32 API?", "page": 3, "topics": ["Win32 Subsystem", "DLL Architecture", "16-bit to 32-bit Evolution"]},
                    {"number": 2, "title": "Handles and Objects", "page": 27, "topics": ["HWND, HDC, HPEN, HBRUSH, HGLOBAL", "Handle Lifetimes", "Kernel vs User vs GDI"]},
                    {"number": 3, "title": "The Visual Basic-Windows Interface", "page": 40, "topics": ["Declare Statement", "ByVal vs ByRef", "Strings (LPSTR, LPWSTR, BSTR)", "Any Keyword", "Callbacks & AddressOf"]},
                    {"number": 4, "title": "Real World API Programming", "page": 76, "topics": ["Translating C Headers (.h) to VB", "Structure Packing & Alignment", "Error Handling (GetLastError)"]},
                    {"number": 5, "title": "Windows Control and Information Functions", "page": 134, "topics": ["Window Hierarchy", "GetWindowLong / SetWindowLong (GetWindowLongPtr in x64)", "FindWindow", "SetWindowPos"]},
                    {"number": 6, "title": "Hardware and System Functions", "page": 250, "topics": ["System Metrics", "Time, Timers", "Registry Access"]},
                    {"number": 7, "title": "Device Contexts", "page": 360, "topics": ["HDC Creation", "GetDC, ReleaseDC", "Mapping Modes"]},
                    {"number": 8, "title": "Drawing Functions", "page": 472, "topics": ["GDI Primitives", "Pens, Brushes", "Paths, Regions"]},
                    {"number": 9, "title": "Bitmaps, Icons, and Raster Operations", "page": 620, "topics": ["DIB & DDB Bitmaps", "BitBlt, StretchBlt", "Icons, Cursors"]},
                    {"number": 10, "title": "Working with Menus", "page": 720, "topics": ["HMENU", "Dynamic Menu Modification", "TrackPopupMenu"]},
                    {"number": 11, "title": "Text and Fonts", "page": 786, "topics": ["LOGFONT", "CreateFontIndirect", "TextOut, DrawText"]},
                    {"number": 12, "title": "Printing", "page": 862, "topics": ["Printer DC", "Print Spooler API", "DEVMODE"]},
                    {"number": 13, "title": "File Operations", "page": 956, "topics": ["CreateFile, ReadFile, WriteFile", "File Mapping", "File Security"]},
                    {"number": 14, "title": "Processes and Threads", "page": 1090, "topics": ["CreateProcess", "Thread Management", "Synchronization (Mutex, Semaphore, Event)"]},
                    {"number": 15, "title": "Memory, Strings, Structures, and Resources", "page": 1192, "topics": ["VirtualAlloc", "GlobalAlloc", "CopyMemory", "RtlMoveMemory", "Structure Alignment"]},
                    {"number": 16, "title": "General Windows Messages", "page": 1270, "topics": ["SendMessage, PostMessage", "Window Lifecycle Messages (WM_CREATE, WM_DESTROY, WM_PAINT)"]},
                    {"number": 17, "title": "Edit Control Messages", "page": 1334, "topics": ["EM_GETSEL, EM_SETSEL, EM_REPLACESEL, EM_LIMITTEXT"]},
                    {"number": 18, "title": "List Box, Combo Box, and Button Messages", "page": 1362, "topics": ["LB_ADDSTRING, CB_ADDSTRING, BM_CLICK"]}
                ]
            },
            {
                "id": "appleman_com",
                "title": "Developing COM/ActiveX Components with Visual Basic 6: A Guide to the Perplexed",
                "author": "Dan Appleman",
                "publisher": "Sams",
                "year": 1998,
                "pages": 892,
                "file": "Developing-COMActiveX-components-VB6.pdf",
                "importance": "ESSENTIAL FOR ACTIVEX / IDE & COMPILER",
                "summary": "Deep exploration of how VB6 creates, compiles, and hosts COM components: ActiveX DLLs, ActiveX EXEs, ActiveX Controls (OCX), and ActiveX Documents.",
                "relevance": {
                    "compiler_x64": "Rules for generating COM interfaces, IDispatch implementations, connection points (IConnectionPointContainer), and binary compatibility verification.",
                    "runtime_x64": "Component activation, ClassFactory stubs, threading models (Single, Apartment, Multi-threaded), and object persistence (PropertyBag).",
                    "ide_x64": "UserControl designer, Ambient and Extender properties, Property Pages, and ActiveX control hosting.",
                    "win32_api_x64": "In-process DLL loading vs Out-of-process EXE marshaling."
                },
                "chapters": [
                    {"number": 1, "title": "ActiveX Myths and Fundamentals", "page": 14, "topics": ["Component Software", "ActiveX Architecture"]},
                    {"number": 3, "title": "Objects and Visual Basic", "page": 42, "topics": ["VB Object Model", "Encapsulation, State, Lifetime"]},
                    {"number": 4, "title": "The Component Object Model", "page": 66, "topics": ["Interfaces, Automation (IDispatch), Type Libraries, GUIDs"]},
                    {"number": 5, "title": "Aggregation and Polymorphism", "page": 96, "topics": ["Interface Reuse", "Implements mechanics", "Containment"]},
                    {"number": 6, "title": "The Life and Times of an ActiveX Component", "page": 124, "topics": ["Registration (regsvr32)", "In-Process DLL vs Out-of-Process EXE", "Unloading"]},
                    {"number": 10, "title": "Code and Classes: Beyond the Manuals", "page": 230, "topics": ["Instancing Properties: Private, PublicNotCreatable, SingleUse, MultiUse, GlobalSingleUse, GlobalMultiUse"]},
                    {"number": 11, "title": "Events and Connection Points", "page": 264, "topics": ["IConnectionPoint", "IConnectionPointContainer", "WithEvents wiring"]},
                    {"number": 14, "title": "Multithreading", "page": 354, "topics": ["Thread Pools in ActiveX DLL/EXE", "Apartment Threading (STA)", "Cross-Thread Marshaling"]},
                    {"number": 16, "title": "ActiveX Control Fundamentals", "page": 440, "topics": ["OCX Architecture", "IOleObject, IOleControl, IViewObject"]},
                    {"number": 17, "title": "The UserControl Object", "page": 468, "topics": ["UserControl Events: Initialize, InitProperties, ReadProperties, WriteProperties, Terminate"]},
                    {"number": 18, "title": "The Extender and Ambient Objects", "page": 512, "topics": ["Host-provided Properties (Top, Left, Visible, Enabled, Name)", "AmbientProperties (UserMode, BackColor)"]},
                    {"number": 19, "title": "The Wonderful World of Properties", "page": 540, "topics": ["Property Procedures", "PropertyBag Object", "Binary vs Text Serialization"]},
                    {"number": 25, "title": "Versioning and Binary Compatibility", "page": 760, "topics": ["No Compatibility vs Project vs Binary Compatibility", "GUID preservation rules"]}
                ]
            },
            {
                "id": "don_box",
                "title": "Essential COM",
                "author": "Don Box",
                "publisher": "Addison-Wesley",
                "year": 1998,
                "pages": 464,
                "file": "Don Box - Essential COM.pdf",
                "importance": "THEORETICAL COM FOUNDATION",
                "summary": "The definitive theoretical guide to the Component Object Model. Explains COM at the binary and ABI level: vtables, IUnknown, QueryInterface identity rules, IClassFactory, Apartments, and Marshaling.",
                "relevance": {
                    "compiler_x64": "Authoritative reference for COM ABI in x64 Windows (64-bit vtables, 64-bit pointer offsets, calling conventions).",
                    "runtime_x64": "Reimplementing IUnknown, IDispatch, IClassFactory, IEnumVARIANT, IMalloc, and IErrorInfo from scratch in 64 bits.",
                    "ide_x64": "Understanding type library generation and COM registration.",
                    "win32_api_x64": "CoInitializeEx, CoCreateInstance, CoRegisterClassObject."
                },
                "chapters": [
                    {"number": 1, "title": "COM as a Better C++", "page": 1, "topics": ["Software Distribution", "Binary Interfaces", "VTable Layout", "Dynamic Linking"]},
                    {"number": 2, "title": "Interfaces", "page": 37, "topics": ["Interface Definition Language (IDL)", "HRESULT Structure", "IUnknown", "QueryInterface Axioms (Reflexive, Symmetric, Transitive)", "AddRef / Release"]},
                    {"number": 3, "title": "Classes", "page": 85, "topics": ["IClassFactory", "CoCreateInstance", "Activation Logic", "Monikers"]},
                    {"number": 4, "title": "Objects", "page": 139, "topics": ["COM Object Identity", "Encapsulation", "Containment", "Aggregation Rules"]},
                    {"number": 5, "title": "Apartments", "page": 193, "topics": ["Threading Models", "Single-Threaded Apartments (STA)", "Multi-Threaded Apartments (MTA)", "Thread Neutral Apartments (TNA)", "Message Pumps"]},
                    {"number": 6, "title": "Applications", "page": 259, "topics": ["Marshaling", "Type Libraries (ITypeLib, ITypeInfo)", "Dual Interfaces", "IDispatch Automation"]},
                    {"number": 7, "title": "Miscellanea", "page": 323, "topics": ["IConnectionPoint / IConnectionPointContainer", "Memory Management (IMalloc)", "Rich Error Handling (IErrorInfo, ICreateErrorInfo)"]}
                ]
            },
            {
                "id": "lhotka",
                "title": "Professional VB6 Business Objects & Distributed Objects Codebase",
                "author": "Rockford Lhotka",
                "publisher": "Wrox Press",
                "year": 1998,
                "pages": 0,
                "file": "VB6 Business  Distributed Objects",
                "importance": "REAL-WORLD ENTERPRISE ARCHITECTURE BENCHMARK",
                "summary": "Complete source code collection (418 files, 197 classes) from Rockford Lhotka's landmark books on N-Tier object-oriented architecture in VB6. Precursor to the CSLA framework.",
                "relevance": {
                    "compiler_x64": "The ultimate test suite for the compiler! Contains massive class hierarchies, deep Implements usage, custom collections, and serialization.",
                    "runtime_x64": "Tests PropertyBag persistence, Variant serialization, collection enumeration (_NewEnum), and MTS/COM+ transaction context.",
                    "ide_x64": "Large complex multi-project workspace (.vbg) loader test.",
                    "win32_api_x64": "Distributed DCOM / MTS transactional state transfer."
                },
                "chapters": [
                    {"number": 4, "title": "Chapter 04: Object Persistence & BrokenRules", "page": 0, "topics": ["BrokenRules Object", "Person & PersonManager", "State Serialization", "ApplyEdit / CancelEdit"]},
                    {"number": 5, "title": "Chapter 05: Business Object Foundation", "page": 0, "topics": ["Business Base Class", "Validation Rules", "Parent-Child Collections"]},
                    {"number": 8, "title": "Chapter 08: VideoObjects Domain Model", "page": 0, "topics": ["VideoObjects Project", "Complete Business Logic Tier", "Persistence Layer"]},
                    {"number": 10, "title": "Chapter 10: 2-Tier and 3-Tier Distributed Objects", "page": 0, "topics": ["VideoServer In-Process / Out-of-Process", "Data Separation across Tiers"]},
                    {"number": 12, "title": "Chapter 12-15: MTS / COM+ Transactional Objects", "page": 0, "topics": ["VideoObjectsMTS", "VideoServerMTS", "ObjectContext, SetComplete, SetAbort", "Database Transactions"]},
                    {"number": 16, "title": "Distributed Objects: Multi-Tier Clusters", "page": 0, "topics": ["Cluster Server", "Worker Server", "Client Workstation", "ASP & RDS Web Integration"]}
                ]
            },
            {
                "id": "msdn_ref_es",
                "title": "Referencia de Visual Basic 6 (MSDN Oficial en Español)",
                "author": "Microsoft Corporation",
                "publisher": "Microsoft Press / MSDN",
                "year": 1999,
                "pages": 909,
                "file": "Referencia Visual Basic 6.pdf",
                "importance": "OFFICIAL SPANISH LANGUAGE & RUNTIME REFERENCE",
                "summary": "909-page complete alphabetical MSDN reference for Visual Basic 6 in Spanish. Details syntax, parameters, return types, constants, and error codes for every VB6 keyword and function.",
                "relevance": {
                    "compiler_x64": "Exact grammar syntax, parameter rules, and keyword reserved list.",
                    "runtime_x64": "Detailed behavior of all standard functions, enums, constants, and error conditions.",
                    "ide_x64": "Ideal source for Spanish IntelliSense tooltips and documentation.",
                    "win32_api_x64": "Constants and type descriptions."
                },
                "chapters": [
                    {"number": 1, "title": "Referencia Alfabética de Funciones e Instrucciones", "page": 1, "topics": ["Abs hasta Year", "Declaraciones Dim, ReDim, Sub, Function, Property, Type, Enum", "Operadores y Constantes"]}
                ]
            },
            {
                "id": "balena_api_appendix",
                "title": "Appendix: Windows API Functions",
                "author": "Francesco Balena",
                "publisher": "Microsoft Press",
                "year": 1999,
                "pages": 46,
                "file": "Programming Win32 API.pdf",
                "importance": "QUICK WIN32 API REFERENCE",
                "summary": "46-page condensed reference of Win32 API declarations, structures, and constants specifically crafted and tested for Visual Basic.",
                "relevance": {
                    "compiler_x64": "Quick declaration tests for API Declare parser.",
                    "runtime_x64": "Direct mapping of standard Windows API calls.",
                    "ide_x64": "API declaration library snippets.",
                    "win32_api_x64": "Standard API lookup table."
                },
                "chapters": [
                    {"number": 1, "title": "Windows API Declarations Reference", "page": 1189, "topics": ["Window Management", "GDI Graphics", "File Operations", "System Information"]}
                ]
            },
            {
                "id": "mcsd_guide",
                "title": "Visual Basic 6 The Complete Reference / MCSD Training Guide",
                "author": "New Riders Publishing",
                "publisher": "New Riders",
                "year": 1999,
                "pages": 1245,
                "file": "Visual Basic 6 The Complete Reference.pdf",
                "importance": "COMPREHENSIVE TRAINING & DESIGN PATTERNS",
                "summary": "1245-page exhaustive guide covering desktop and distributed VB6 enterprise architectures, COM+, MTS, ADO, deployment, and security.",
                "relevance": {
                    "compiler_x64": "Enterprise application compatibility benchmark.",
                    "runtime_x64": "Validation of transaction contexts and enterprise APIs.",
                    "ide_x64": "Project wizard templates and testing patterns.",
                    "win32_api_x64": "Deployment packaging and registry dependencies."
                },
                "chapters": [
                    {"number": 1, "title": "Desktop Application Development", "page": 1, "topics": ["Forms, Controls, Menus, Validation"]},
                    {"number": 2, "title": "Distributed Application Architecture", "page": 400, "topics": ["COM Components, MTS, COM+ Transactions, ADO"]}
                ]
            }
        ],
        "core_topics_cross_index": {
            "vtable_and_com_abi": {
                "title": "Virtual Method Tables (VTables) & COM ABI",
                "description": "How VB6 and COM represent objects in memory, table of pointers to functions, IUnknown base (QueryInterface, AddRef, Release), and 64-bit alignment.",
                "references": [
                    {"book": "curland", "chapter": 3, "pages": "41-52", "notes": "VB's internal IUnknown, calling vtable functions directly"},
                    {"book": "curland", "chapter": 4, "pages": "53-72", "notes": "VTable-binding custom control interfaces"},
                    {"book": "curland", "chapter": 8, "pages": "143-182", "notes": "Creating lightweight COM objects with custom raw vtables"},
                    {"book": "curland", "chapter": 12, "pages": "261-278", "notes": "VTable patching and method interception"},
                    {"book": "don_box", "chapter": 1, "pages": "14-25", "notes": "Abstract base classes as binary interfaces & vtables"},
                    {"book": "don_box", "chapter": 2, "pages": "48-72", "notes": "IUnknown implementation, vtable layout, QueryInterface"},
                    {"book": "mckinney", "chapter": 9, "pages": "539-570", "notes": "COM behind the curtain, vtable inspection"}
                ],
                "compiler_relevance": "Emit 64-bit pointer vtables for Class modules and Implements interfaces. Offset 0=QI, Offset 8=AddRef, Offset 16=Release, Offset 24+ = Custom methods (or IDispatch for dual).",
                "runtime_relevance": "Object headers must point to the vtable pointer. Implement IUnknown and IDispatch vtable stubs."
            },
            "safearray_memory_descriptors": {
                "title": "Array Descriptors & SAFEARRAY Memory Layout",
                "description": "The exact internal memory structure of VB6 arrays: SAFEARRAY structure, SAFEARRAYBOUND, cDims, cbElements, pvData, and bounds checking.",
                "references": [
                    {"book": "curland", "chapter": 2, "pages": "11-40", "notes": "Complete breakdown of SAFEARRAY descriptor, reading/writing array variables, wrapping raw C pointers in SAFEARRAY without copying"},
                    {"book": "curland", "chapter": 1, "pages": "1-10", "notes": "Using VarPtrArray and living pointers to array headers"},
                    {"book": "balena", "chapter": 4, "pages": "165-178", "notes": "Dynamic arrays, ReDim Preserve behavior, Erase rules"},
                    {"book": "appleman_win32", "chapter": 3, "pages": "67-68", "notes": "Passing arrays to Win32 DLLs, array pointers"}
                ],
                "compiler_relevance": "Array indexing syntax array(i, j) calculation based on SAFEARRAY bounds. Generating calls to SafeArrayGetElement / SafeArrayAccessData or raw pointer arithmetic.",
                "runtime_relevance": "In 64 bits: cbElements and pvData are 64-bit aligned. SafeArrayCreate, SafeArrayDestroy, SafeArrayRedim implementation."
            },
            "bstr_unicode_strings": {
                "title": "BSTR Strings & Unicode/ANSI Bridging",
                "description": "BSTR memory model: 4-byte length prefix before the string pointer, UTF-16 wchar_t data, null-terminated. Handling automatic ANSI conversion in Win32 Declare statements.",
                "references": [
                    {"book": "curland", "chapter": 14, "pages": "357-390", "notes": "Strings in VB: BSTR internals, SysAllocString, SysFreeString, StrPtr vs VarPtr"},
                    {"book": "mckinney", "chapter": 4, "pages": "181-246", "notes": "Deep dive into VB string memory, Unicode vs ANSI bridging, byte arrays (StrConv)"},
                    {"book": "appleman_win32", "chapter": 3, "pages": "43-46, 63-65", "notes": "How VB converts strings to ANSI during API calls, LPSTR vs LPWSTR"},
                    {"book": "balena", "chapter": 5, "pages": "211-230", "notes": "VBA String functions: Left, Right, Mid, InStr, Replace, StrConv"}
                ],
                "compiler_relevance": "Distinguish between Declare Function ... (ByVal s As String) [auto ANSI] and modern x64 Unicode declarations. StrPtr returns pointer to raw UTF-16 data.",
                "runtime_relevance": "Fast UTF-16 string engine, SysAllocStringByteLen, SysAllocStringLen, SysFreeString, BSTR concatenation with pre-allocated buffer."
            },
            "variant_and_type_coercion": {
                "title": "VARIANT & VARIANTARG Memory Layout",
                "description": "16-byte union in 32-bit (24-byte in 64-bit due to 8-byte alignment): VARTYPE vt, wReserved1-3, data union (llVal, lVal, bstrVal, pdispVal, punkVal, parray, etc.).",
                "references": [
                    {"book": "curland", "chapter": 1, "pages": "3-9", "notes": "Variant internals, living pointers into Variants, VarPtr on Variants"},
                    {"book": "don_box", "chapter": 6, "pages": "285-305", "notes": "IDispatch, VARIANT types, DISPPARAMS marshaling"},
                    {"book": "mckinney", "chapter": 2, "pages": "47-70", "notes": "Variant overhead, internal subtypes, VariantCopyInd"},
                    {"book": "balena", "chapter": 4, "pages": "154-162", "notes": "Variant type coercion rules, IsMissing, Empty vs Null"}
                ],
                "compiler_relevance": "64-bit VARIANT size is 24 bytes (alignment padding). Subtyping rules, implicit Variant conversion.",
                "runtime_relevance": "VariantInit, VariantClear, VariantCopy, VariantChangeType routines in 64 bits."
            },
            "win32_api_64bit_migration": {
                "title": "Win32 API Declarations & 64-Bit Migration (LongPtr & LongLong)",
                "description": "Porting Declare statements from 32-bit to 64-bit: replacing 32-bit Long with LongPtr for handles (HWND, HDC, HINSTANCE) and pointers; LongLong for 64-bit integers; struct alignment.",
                "references": [
                    {"book": "appleman_win32", "chapter": 3, "pages": "40-75", "notes": "Visual Basic-Windows Interface: Declare, ByVal/ByRef, Types, Any, Callbacks"},
                    {"book": "appleman_win32", "chapter": 4, "pages": "76-133", "notes": "Translating C headers (.h) to VB, struct packing rules"},
                    {"book": "appleman_win32", "chapter": 5, "pages": "134-249", "notes": "GetWindowLong/SetWindowLong -> Must use GetWindowLongPtr/SetWindowLongPtr in x64"},
                    {"book": "appleman_win32", "chapter": 15, "pages": "1192-1269", "notes": "Memory, structures, CopyMemory, structure alignment in 64 bits"}
                ],
                "compiler_relevance": "Introduce LongPtr keyword (resolves to 64-bit integer on x64, 32-bit on x86). Introduce LongLong keyword (^ suffix). Adjust UDT struct padding to 8-byte boundaries.",
                "runtime_relevance": "Foreign Function Interface (FFI) call generator using x64 Windows calling convention (RCX, RDX, R8, R9 + stack shadow space of 32 bytes)."
            },
            "subclassing_and_wndproc": {
                "title": "Safe Window Subclassing, Hooking & CallWindowProc",
                "description": "Intercepting Windows messages (WM_*) for VB forms and controls without IDE crashes. Machine-code thunks vs SetWindowLongPtr.",
                "references": [
                    {"book": "curland", "chapter": 11, "pages": "227-260", "notes": "Calling function pointers, CallWindowProc hack, ASM thunks"},
                    {"book": "curland", "chapter": 12, "pages": "261-278", "notes": "Overriding functions, safe subclassing"},
                    {"book": "mckinney", "chapter": 6, "pages": "344-362", "notes": "Receiving messages, encapsulating message traps"},
                    {"book": "appleman_win32", "chapter": 16, "pages": "1270-1333", "notes": "Window lifecycle messages, message queues"}
                ],
                "compiler_relevance": "AddressOf operator returning function pointers that adhere to the x64 Windows ABI for callback procedures.",
                "runtime_relevance": "Integration with SetWindowSubclass (comctl32.dll) for safe, crash-free subclassing."
            },
            "error_handling_errobject": {
                "title": "ErrObject, On Error, and HRESULT Exception Handling",
                "description": "How VB6 traps runtime errors and HRESULTs from COM calls. ErrObject interface (_ErrObject), Number, Source, Description, Raise, Clear.",
                "references": [
                    {"book": "balena", "chapter": 4, "pages": "188-210", "notes": "On Error GoTo, On Error Resume Next, Err object methods"},
                    {"book": "curland", "chapter": 3, "pages": "45-50", "notes": "How VB6 converts failing HRESULTs to Err.Raise"},
                    {"book": "mckinney", "chapter": 5, "pages": "247-270", "notes": "When and how to raise errors, Win32 error translation"},
                    {"book": "don_box", "chapter": 7, "pages": "342-358", "notes": "ISupportErrorInfo, IErrorInfo, ICreateErrorInfo"}
                ],
                "compiler_relevance": "Implement On Error GoTo / Resume Next jump tables and exception handlers in generated code.",
                "runtime_relevance": "Implement ErrObject coclass with GUID {a4c46654-499f-101b-bb78-00aa00383cbb} and exact dispids: Number(0), Source(0x60020002), Description(0x60020004), Raise(0x6002000a), Clear(0x6002000b)."
            },
            "collections_and_for_each": {
                "title": "Collection Object & For Each Enumeration (_NewEnum)",
                "description": "Internal implementation of VB.Collection: Add, Remove, Count, Item, and the hidden _NewEnum (dispid -4) returning IEnumVARIANT for For Each loops.",
                "references": [
                    {"book": "balena", "chapter": 6, "pages": "300-320", "notes": "Using Collection, Key hashing, 1-based indexing"},
                    {"book": "curland", "chapter": 6, "pages": "110-118", "notes": "Weak references and Collections, avoiding circular leaks"},
                    {"book": "appleman_com", "chapter": 12, "pages": "286-315", "notes": "Building custom strongly-typed collection classes, wrapping _NewEnum"},
                    {"book": "don_box", "chapter": 7, "pages": "330-340", "notes": "IEnumVARIANT specification: Next, Skip, Reset, Clone"}
                ],
                "compiler_relevance": "Compile For Each var In col into: call col._NewEnum -> QueryInterface IEnumVARIANT -> call Next(1, var, fetched) -> loop.",
                "runtime_relevance": "Implement Collection coclass with GUID {a4c4671c-499f-101b-bb78-00aa00383cbb} and exact dispids: Item(0), Add(1), Count(2), Remove(3), _NewEnum(-4)."
            },
            "usercontrols_and_ocx": {
                "title": "ActiveX Controls (UserControl) & PropertyBag Persistence",
                "description": "UserControl architecture, AmbientProperties, ExtenderProperties, PropertyBag binary/text serialization, and Form designer integration.",
                "references": [
                    {"book": "appleman_com", "chapter": 16, "pages": "440-467", "notes": "ActiveX Control Fundamentals, OLE Control architecture"},
                    {"book": "appleman_com", "chapter": 17, "pages": "468-511", "notes": "The UserControl Object, lifecycle events"},
                    {"book": "appleman_com", "chapter": 18, "pages": "512-539", "notes": "Extender and Ambient Objects"},
                    {"book": "appleman_com", "chapter": 19, "pages": "540-587", "notes": "Properties, PropertyBag, ReadProperties / WriteProperties"},
                    {"book": "balena", "chapter": 12, "pages": "905-978", "notes": "Step-by-step UserControl development in VB6"}
                ],
                "compiler_relevance": "Compile UserControl into an OCX / COM component exposing IOleObject, IOleControl, IViewObject, IProvideClassInfo.",
                "runtime_relevance": "PropertyBag object implementation (ReadProperty, WriteProperty).",
                "ide_x64": "Form Designer hosting UserControls, resolving AmbientProperties (UserMode, BackColor) and ExtenderProperties (Left, Top, Width, Height, Name)."
            },
            "enterprise_business_objects_csla": {
                "title": "Enterprise Business Objects & N-Tier Distributed Architecture",
                "description": "Rockford Lhotka's architecture for domain objects: State preservation, BrokenRules validation, BeginEdit/ApplyEdit/CancelEdit, and transaction handling.",
                "references": [
                    {"book": "lhotka", "chapter": 4, "pages": "All", "notes": "Object Persistence, Person, PersonManager, BrokenRules"},
                    {"book": "lhotka", "chapter": 5, "pages": "All", "notes": "Business base class, validation engine"},
                    {"book": "lhotka", "chapter": 8, "pages": "All", "notes": "Complete VideoObjects project, real enterprise domain model"},
                    {"book": "lhotka", "chapter": 12, "pages": "All", "notes": "VideoObjectsMTS: Transactional COM+ / MTS components"},
                    {"book": "mcsd_guide", "chapter": 2, "pages": "400-600", "notes": "Multi-tier enterprise systems, MTS/COM+ architecture"}
                ],
                "compiler_relevance": "Benchmark for large complex enterprise VB6 applications.",
                "runtime_relevance": "Compatibility with COM+ transaction context and multi-tier state marshalling.",
                "ide_x64": "Multi-project group (.vbg) support."
            }
        }
    }

    out_file = os.path.join(LIB_DIR, "VB6_LIBRARY_DATABASE.json")
    with open(out_file, "w", encoding="utf-8") as f:
        json.dump(db, f, indent=2)
    print(f"Saved {out_file}")
    return db

def generate_cli_query_tool():
    print("Generating query_library.py CLI search tool...")
    code = '''# -*- coding: utf-8 -*-
"""
VB6 / COM / Win32 Library Quick-Access Query Tool
Allows instant CLI search and lookup across all books, chapters, COM typelibs,
runtime symbols, and code samples.

Usage:
  python query_library.py search <keyword>
  python query_library.py topic <topic_key>
  python query_library.py book <curland|mckinney|balena|appleman_win32|appleman_com|don_box|lhotka>
  python query_library.py target <compiler|ide|runtime|api64>
  python query_library.py com <class_or_method_name>
  python query_library.py code <class_or_method_name>
  python query_library.py stats
"""

import os
import sys
import json
import re

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DB_FILE = os.path.join(SCRIPT_DIR, "VB6_LIBRARY_DATABASE.json")
SYMBOLS_FILE = os.path.join(SCRIPT_DIR, "TYPELIB_AND_RUNTIME_SYMBOLS.json")
CODE_FILE = os.path.join(SCRIPT_DIR, "CODE_SAMPLES_INDEX.json")

def load_json(path):
    if not os.path.exists(path):
        print(f"Error: Database file not found: {path}")
        sys.exit(1)
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)

def cmd_stats():
    db = load_json(DB_FILE)
    symbols = load_json(SYMBOLS_FILE)
    code = load_json(CODE_FILE)

    print("=" * 70)
    print("  VB6 / COM / WIN32 QUICK-ACCESS KNOWLEDGE BASE STATISTICS")
    print("=" * 70)
    print(f"Total Reference Volumes / Books: {len(db['books'])}")
    total_pages = sum(b.get('pages', 0) for b in db['books'])
    print(f"Total Documented Pages:         {total_pages:,}")
    print(f"Core Indexed Topics:            {len(db['core_topics_cross_index'])}")
    print(f"msvbvm60 Runtime Components:    {symbols['msvbvm60_runtime']['components_count']}")
    print(f"msvbvm60 Enums Defined:         {len(symbols['msvbvm60_runtime']['enums'])}")
    print(f"VB6.OLB Controls & Interfaces:  {symbols['vb6_controls_typelib']['components_count']}")
    print(f"Rockford Lhotka Code Files:     {code['summary']['total_files']}")
    print(f"  - Class Modules (.cls):       {code['summary']['class_modules_cls']}")
    print(f"  - Form Modules (.frm):        {code['summary']['forms_frm']}")
    print(f"  - Standard Modules (.bas):    {code['summary']['standard_modules_bas']}")
    print(f"  - Project Files (.vbp):       {code['summary']['projects_vbp']}")
    print(f"  - Project Groups (.vbg):      {code['summary']['project_groups_vbg']}")
    print("=" * 70)

def cmd_search(term):
    db = load_json(DB_FILE)
    term_low = term.lower()
    print(f"=== SEARCH RESULTS FOR: '{term}' ===\\n")

    # Search in books & chapters
    book_hits = 0
    for b in db["books"]:
        matched_chaps = []
        for ch in b.get("chapters", []):
            title_match = term_low in ch["title"].lower()
            topic_match = any(term_low in t.lower() for t in ch.get("topics", []))
            if title_match or topic_match:
                matched_chaps.append(ch)

        summary_match = term_low in b.get("summary", "").lower()
        if matched_chaps or summary_match:
            book_hits += 1
            print(f"[BOOK] {b['title']} (by {b['author']})")
            print(f"       File: {b['file']} ({b['pages']} pages)")
            for ch in matched_chaps:
                print(f"       -> Ch {ch['number']}: {ch['title']} (p. {ch['page']})")
                print(f"          Topics: {', '.join(ch.get('topics', []))}")
            print()

    # Search in topics
    topic_hits = 0
    for tk, tdata in db["core_topics_cross_index"].items():
        if term_low in tk or term_low in tdata["title"].lower() or term_low in tdata["description"].lower():
            topic_hits += 1
            print(f"[TOPIC] {tdata['title']} (key: {tk})")
            print(f"        {tdata['description']}")
            print(f"        Compiler: {tdata.get('compiler_relevance', '')}")
            print(f"        Runtime:  {tdata.get('runtime_relevance', '')}")
            print()

    print(f"Total matching books: {book_hits}, matching topics: {topic_hits}")

def cmd_topic(topic_key):
    db = load_json(DB_FILE)
    topics = db["core_topics_cross_index"]
    # exact or substring match
    matched = None
    for k, v in topics.items():
        if k.lower() == topic_key.lower() or topic_key.lower() in k.lower():
            matched = (k, v)
            break

    if not matched:
        print(f"Topic '{topic_key}' not found.")
        print("Available topics:")
        for k in sorted(topics.keys()):
            print(f"  - {k} ({topics[k]['title']})")
        return

    k, data = matched
    print("=" * 70)
    print(f"TOPIC: {data['title']} (Key: {k})")
    print("=" * 70)
    print(f"Description: {data['description']}\\n")
    print("BOOK REFERENCES:")
    for ref in data.get("references", []):
        print(f"  * Book: {ref['book'].upper()} | Chapter {ref['chapter']} (Pages {ref['pages']})")
        print(f"    Notes: {ref['notes']}")
    print()
    print(f"COMPILER x64 RELEVANCE:\\n  {data.get('compiler_relevance', 'N/A')}\\n")
    print(f"RUNTIME x64 RELEVANCE:\\n  {data.get('runtime_relevance', 'N/A')}\\n")

def cmd_book(book_id):
    db = load_json(DB_FILE)
    b = next((x for x in db["books"] if x["id"].lower() == book_id.lower()), None)
    if not b:
        print(f"Book '{book_id}' not found.")
        print("Available book IDs:")
        for x in db["books"]:
            print(f"  - {x['id']}: {x['title']}")
        return

    print("=" * 70)
    print(f"{b['title']}")
    print(f"Author: {b['author']} | Publisher: {b['publisher']} ({b['year']}) | Pages: {b['pages']}")
    print(f"Importance: {b.get('importance', 'Standard')}")
    print(f"File: {b['file']}")
    print("=" * 70)
    print(f"Summary:\\n{b['summary']}\\n")
    print("RELEVANCE BY SUBSYSTEM:")
    for sub, rel in b.get("relevance", {}).items():
        print(f"  [{sub.upper()}]: {rel}")
    print("\\nCHAPTERS:")
    for ch in b.get("chapters", []):
        topics_str = f" - Topics: {', '.join(ch['topics'])}" if ch.get('topics') else ""
        print(f"  Ch {ch['number']:2d}: {ch['title']:<45} (p. {ch['page']:4d}){topics_str}")

def cmd_target(target_name):
    db = load_json(DB_FILE)
    t = target_name.lower()
    mapping = {
        "compiler": "compiler_x64",
        "compiler_x64": "compiler_x64",
        "ide": "ide_x64",
        "ide_x64": "ide_x64",
        "runtime": "runtime_x64",
        "runtime_x64": "runtime_x64",
        "api64": "win32_api_x64",
        "win32_api_x64": "win32_api_x64"
    }
    key = mapping.get(t)
    if not key:
        print("Invalid target. Choose from: compiler, ide, runtime, api64")
        return

    print("=" * 70)
    print(f"RESOURCES AND BLUEPRINTS FOR: {key.upper()}")
    print("=" * 70)
    for b in db["books"]:
        rel = b.get("relevance", {}).get(key)
        if rel:
            print(f"[BOOK] {b['title']} (by {b['author']})")
            print(f"       File: {b['file']}")
            print(f"       Guidance: {rel}\\n")

    print("RELEVANT TOPICS:")
    for tk, tdata in db["core_topics_cross_index"].items():
        field = "compiler_relevance" if "compiler" in key else ("runtime_relevance" if "runtime" in key else None)
        if field and tdata.get(field):
            print(f"  * {tdata['title']}: {tdata[field]}")

def cmd_com(query):
    symbols = load_json(SYMBOLS_FILE)
    q = query.lower()
    print(f"=== SEARCHING COM TYPELIBS FOR: '{query}' ===\\n")

    # Search in msvbvm60 components
    msvbvm_matches = [c for c in symbols["msvbvm60_runtime"]["components"] if q in c["name"].lower() or q in c["guid"].lower()]
    if msvbvm_matches:
        print(f"[msvbvm60.dll Runtime] Found {len(msvbvm_matches)} matches:")
        for c in msvbvm_matches:
            print(f"  - {c['name']:<25} | GUID: {c['guid']}")
        print()

    # Search in VB6.OLB components
    vb_matches = [c for c in symbols["vb6_controls_typelib"]["components"] if q in c["name"].lower() or q in c["guid"].lower()]
    if vb_matches:
        print(f"[VB6.OLB Controls] Found {len(vb_matches)} matches:")
        for c in vb_matches:
            print(f"  - {c['name']:<25} | GUID: {c['guid']}")
        print()

    # Search in VB6 control interface methods
    methods_hits = []
    for iface, methods in symbols["vb6_controls_typelib"]["interfaces_methods"].items():
        for m in methods:
            if q in m["name"].lower():
                methods_hits.append((iface, m["name"], m["args"]))

    if methods_hits:
        print(f"[Control Methods/Properties] Found {len(methods_hits)} occurrences:")
        for iface, mname, margs in methods_hits[:15]:
            print(f"  - Interface {iface:<20}: {mname}({margs})")
        if len(methods_hits) > 15:
            print(f"  ... and {len(methods_hits) - 15} more.")

def cmd_code(query):
    code = load_json(CODE_FILE)
    q = query.lower()
    print(f"=== SEARCHING ROCKFORD LHOTKA CODEBASE FOR: '{query}' ===\\n")

    matches = []
    for cls in code["classes"]:
        class_match = q in cls["class_name"].lower()
        method_match = [m for m in cls.get("methods", []) if q in m.lower()]
        if class_match or method_match:
            matches.append((cls, method_match))

    if matches:
        print(f"Found {len(matches)} matching classes:")
        for cls, mm in matches[:20]:
            print(f"  * Class: {cls['class_name']} ({cls['file']})")
            print(f"    Path:  {cls['relative_path']}")
            if mm:
                print(f"    Matching Methods: {', '.join(mm[:5])}")
            if cls.get("implements"):
                print(f"    Implements:       {', '.join(cls['implements'])}")
            print()
    else:
        print("No matching classes found.")

def main():
    if len(sys.argv) < 2:
        print(__doc__)
        cmd_stats()
        return

    cmd = sys.argv[1].lower()
    arg = sys.argv[2] if len(sys.argv) > 2 else ""

    if cmd == "stats":
        cmd_stats()
    elif cmd == "search":
        if not arg:
            print("Please provide a search keyword.")
            return
        cmd_search(arg)
    elif cmd == "topic":
        if not arg:
            print("Please provide a topic key.")
            return
        cmd_topic(arg)
    elif cmd == "book":
        if not arg:
            print("Please provide a book ID.")
            return
        cmd_book(arg)
    elif cmd == "target":
        if not arg:
            print("Please provide a target (compiler, ide, runtime, api64).")
            return
        cmd_target(arg)
    elif cmd == "com":
        if not arg:
            print("Please provide a COM class, method, or GUID.")
            return
        cmd_com(arg)
    elif cmd == "code":
        if not arg:
            print("Please provide a class name or method name.")
            return
        cmd_code(arg)
    else:
        print(f"Unknown command: {cmd}")
        print(__doc__)

if __name__ == "__main__":
    main()
'''
    out_file = os.path.join(LIB_DIR, "query_library.py")
    with open(out_file, "w", encoding="utf-8") as f:
        f.write(code)
    print(f"Saved {out_file}")

def generate_markdown_index(db, symbols, code):
    print("Generating master VB6_LIBRARY_INDEX.md...")
    md = []
    md.append("# Biblioteca de Ingeniería y Acceso Rápido: VB6 / COM / Win32 x64\n")
    md.append("> **Repositorio Oficial de Referencia Técnica y Base de Conocimiento**  ")
    md.append("> **Ubicación:** `C:\\Programming\\IDE VB6 x64\\LIBRARY VB6`  ")
    md.append("> **Destino:** VB x32/x64 Compiler | VB7-x64 IDE | VB x32/x64 New Runtime | Win32 API x64 Migration  \n")
    md.append("---\n")

    md.append("## 1. Visión General del Ecosistema y Contenido\n")
    md.append("Esta biblioteca recopila los textos canónicos, las bibliotecas de tipos (TypeLibs), los reportes de descompilación COM, los catálogos del runtime (`msvbvm60.dll`) y más de 400 archivos de código fuente de arquitectura empresarial de Visual Basic 6.0.\n\n")

    md.append("```mermaid")
    md.append("flowchart TD")
    md.append("    LIB[\"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\"] --> B1[\"Matthew Curland<br/>(Advanced VB6: VTables, Arrays, Pointers)\"]")
    md.append("    LIB --> B2[\"Bruce McKinney<br/>(Hardcore VB: Strings, Subclassing, API)\"]")
    md.append("    LIB --> B3[\"Francesco Balena<br/>(Programming VB6: Especificación Completa)\"]")
    md.append("    LIB --> B4[\"Dan Appleman<br/>(Win32 API Bible & COM/ActiveX)\"]")
    md.append("    LIB --> B5[\"Don Box<br/>(Essential COM: Teoría Binaria y ABI)\"]")
    md.append("    LIB --> B6[\"Rockford Lhotka<br/>(Business & Distributed Objects Code)\"]")
    md.append("    LIB --> B7[\"TypeLibs & Reports<br/>(msvbvm60.dll & VB6.OLB IDL)\"]")
    md.append("    ")
    md.append("    B1 --> C1[\"VB x32/x64 Compiler<br/>(Parser, AST, VTables, Linking)\"]")
    md.append("    B2 --> C1")
    md.append("    B5 --> C1")
    md.append("    ")
    md.append("    B1 --> C2[\"VB x32/x64 New Runtime<br/>(BSTR, SAFEARRAY, VARIANT, ErrObject)\"]")
    md.append("    B2 --> C2")
    md.append("    B7 --> C2")
    md.append("    ")
    md.append("    B3 --> C3[\"VB7-x64 IDE<br/>(Forms, Controls, IntelliSense, Project)\"]")
    md.append("    B4 --> C3")
    md.append("    B7 --> C3")
    md.append("    ")
    md.append("    B4 --> C4[\"Win32 API x64 Migration<br/>(LongPtr, LongLong, Alignment, Thunks)\"]")
    md.append("    B6 --> C5[\"Enterprise Test Suite<br/>(197 Clases, CSLA Pattern, N-Tier)\"]")
    md.append("```\n\n")

    md.append("### Métricas del Repositorio Indexado:\n")
    md.append(f"- **Libros y Volúmenes de Referencia:** {len(db['books'])}\n")
    total_pages = sum(b.get('pages', 0) for b in db['books'])
    md.append(f"- **Total Páginas Documentadas:** {total_pages:,}\n")
    md.append(f"- **Componentes Runtime (`msvbvm60.dll`):** {symbols['msvbvm60_runtime']['components_count']} módulos/interfaces y {len(symbols['msvbvm60_runtime']['enums'])} enumeraciones clave\n")
    md.append(f"- **Controles e Interfaces GUI (`VB6.OLB`):** {symbols['vb6_controls_typelib']['components_count']} CoClasses e Interfaces nativas\n")
    md.append(f"- **Archivos de Código Fuente Empresarial (Lhotka):** {code['summary']['total_files']} archivos ({code['summary']['class_modules_cls']} clases `.cls`, {code['summary']['forms_frm']} formularios `.frm`, {code['summary']['standard_modules_bas']} módulos `.bas`, {code['summary']['projects_vbp']} proyectos `.vbp`)\n\n")

    md.append("---\n")
    md.append("## 2. Catálogo Maestro de Libros y Documentos\n\n")
    md.append("| ID | Libro / Documento | Autor | Editorial / Año | Páginas | Impacto Principal |\n")
    md.append("|---|---|---|---|:---:|---|\n")
    for b in db["books"]:
        md.append(f"| **`{b['id']}`** | [{b['title']}](file:///{b['file'].replace(' ', '%20')}) | {b['author']} | {b['publisher']} ({b['year']}) | {b['pages']} | {b.get('importance', '')} |\n")
    md.append("\n")

    md.append("### Detalle por Obra y Aporte al Proyecto:\n\n")
    for b in db["books"]:
        md.append(f"### 📘 `{b['id']}`: {b['title']}\n")
        md.append(f"- **Autor:** {b['author']} | **Editorial:** {b['publisher']} ({b['year']}) | **Páginas:** {b['pages']}\n")
        md.append(f"- **Archivo Local:** [`{b['file']}`](file:///C:/Programming/IDE%20VB6%20x64/LIBRARY%20VB6/{b['file'].replace(' ', '%20')})\n")
        md.append(f"- **Resumen:** {b['summary']}\n")
        md.append(f"- **Aporte al Proyecto:**\n")
        for sub, rel in b.get("relevance", {}).items():
            md.append(f"  - **`{sub}`:** {rel}\n")
        md.append(f"- **Capítulos y Contenidos:**\n")
        for ch in b.get("chapters", []):
            top_str = f" (*{', '.join(ch.get('topics', []))}*)" if ch.get('topics') else ""
            md.append(f"  - **Capítulo {ch['number']}:** {ch['title']} (Pág. {ch['page']}){top_str}\n")
        md.append("\n")

    md.append("---\n")
    md.append("## 3. Matriz de Temas Clave y Referencias Cruzadas\n\n")
    for tk, tdata in db["core_topics_cross_index"].items():
        md.append(f"### 🔑 {tdata['title']} (`{tk}`)\n")
        md.append(f"{tdata['description']}\n\n")
        md.append("| Fuente / Libro | Capítulo | Páginas | Enfoque / Contenido |\n")
        md.append("|---|:---:|:---:|---|\n")
        for ref in tdata.get("references", []):
            md.append(f"| **{ref['book'].upper()}** | Cap. {ref['chapter']} | {ref['pages']} | {ref['notes']} |\n")
        md.append(f"\n- **Impacto en Compilador x64:** {tdata.get('compiler_relevance', 'N/A')}\n")
        md.append(f"- **Impacto en Runtime x64:** {tdata.get('runtime_relevance', 'N/A')}\n\n")

    md.append("---\n")
    md.append("## 4. Biblioteca de Tipos (TypeLib) y Símbolos del Runtime\n\n")
    md.append("### Runtime Core (`msvbvm60.dll`)\n")
    md.append(f"- **GUID de TypeLib:** `{symbols['msvbvm60_runtime']['typelib_guid']}` (Versión 6.0 - VBA)\n")
    md.append("- **Módulos y Clases Principales:**\n")
    for c in symbols["msvbvm60_runtime"]["components"]:
        md.append(f"  - `{c['name']:<20}` | CLSID/GUID: `{c['guid']}`\n")
    md.append("\n### Enumeraciones Canónicas de VBA:\n")
    for ename, emembers in symbols["msvbvm60_runtime"]["enums"].items():
        sample_members = list(emembers.keys())[:6]
        md.append(f"- **`{ename}`:** {', '.join(sample_members)}{' ...' if len(emembers) > 6 else ''}\n")
    md.append("\n### Controles Nativos OLE/COM (`VB6.OLB`)\n")
    md.append(f"- **GUID de TypeLib:** `{symbols['vb6_controls_typelib']['typelib_guid']}`\n")
    md.append(f"- **Total de Componentes:** {symbols['vb6_controls_typelib']['components_count']} elementos (PictureBox, TextBox, CommandButton, Form, Menu, etc.) con todas sus interfaces `_I...` y eventos dispinterface `...Events`.\n\n")

    md.append("---\n")
    md.append("## 5. Repositorio de Código Empresarial (Rockford Lhotka)\n\n")
    md.append("Ubicado en `C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\VB6 Business  Distributed Objects`:\n")
    md.append("- **418 Archivos de código completo** divididos en:\n")
    md.append("  1. **Professional VB6 Business Objects 2003:** Capítulos 04 al 15 (Patrón `BrokenRules`, persistencia de estado, `Person`/`PersonManager`, `VideoObjects`, `VideoServer`, y componentes transaccionales MTS/COM+ `VideoObjectsMTS`).\n")
    md.append("  2. **Professional VB6 Distributed Objects 2003:** Capítulos 2 al 8 (Arquitectura distribuida N-Tier, serialización remota RDS, integración Web ASP, y modelo de Cluster Server / Worker Server / Client Workstation).\n\n")

    md.append("---\n")
    md.append("## 6. Herramienta de Acceso Rápido por Línea de Comandos (`query_library.py`)\n\n")
    md.append("Se incluye un script Python interactivo y de consola directamente en la carpeta de la biblioteca para realizar consultas instantáneas:\n\n")
    md.append("```bash\n")
    md.append("# Buscar cualquier palabra clave en libros, capítulos y código\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" search vtable\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" search safearray\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" search LongPtr\n\n")
    md.append("# Ver la ficha técnica y referencias cruzadas de un tema clave\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" topic vtable_and_com_abi\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" topic safearray_memory_descriptors\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" topic bstr_unicode_strings\n\n")
    md.append("# Ver capítulos y detalles de un libro específico\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" book curland\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" book balena\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" book appleman_win32\n\n")
    md.append("# Filtrar todo el contenido relevante para una parte del proyecto\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" target compiler\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" target runtime\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" target ide\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" target api64\n\n")
    md.append("# Buscar clases, métodos o GUIDs COM\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" com TextBox\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" com ErrObject\n\n")
    md.append("# Buscar clases o métodos en el repositorio de código de Lhotka\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" code BrokenRules\n")
    md.append("python \"C:\\Programming\\IDE VB6 x64\\LIBRARY VB6\\query_library.py\" code ApplyEdit\n")
    md.append("```\n")

    out_file = os.path.join(LIB_DIR, "VB6_LIBRARY_INDEX.md")
    with open(out_file, "w", encoding="utf-8") as f:
        f.write("\n".join(md))
    print(f"Saved {out_file}")

def main():
    symbols_data = parse_com_reports()
    code_index = parse_code_repository()
    db = build_master_database(symbols_data, code_index)
    generate_cli_query_tool()
    generate_markdown_index(db, symbols_data, code_index)
    print("\nALL LIBRARY ARTIFACTS GENERATED SUCCESSFULLY!")

if __name__ == "__main__":
    main()
