using System;
using System.Collections.Generic;

namespace VB7x64.ProjectModel.Execution;

public enum ExecutionState
{
    Stopped,
    Running,
    Paused
}

public enum StepMode
{
    None,
    StepInto,
    StepOver
}

public enum OnErrorHandling
{
    Standard,
    ResumeNext,
    GoToLabel
}

public class VbErrObject
{
    public int Number { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public void Clear()
    {
        Number = 0;
        Description = string.Empty;
        Source = string.Empty;
    }

    public void Raise(int number, string? source = null, string? description = null)
    {
        Number = number;
        Source = source ?? "VB7-x64 Live Runtime";
        Description = description ?? $"Error en tiempo de ejecución {number}";
    }
}

public interface ILiveControl
{
    string Name { get; }
    string TypeName { get; }
    VbValue GetProperty(string propertyName);
    void SetProperty(string propertyName, VbValue value);
    VbValue InvokeMethod(string methodName, VbValue[] args);
}

public interface ILiveControlArray : ILiveControl
{
    int Count { get; }
    int LBound { get; }
    int UBound { get; }
    ILiveControl? GetItem(int index);
    IReadOnlyCollection<ILiveControl> Items { get; }
}

public class LiveControlArray : ILiveControlArray
{
    private readonly string _name;
    private readonly SortedDictionary<int, ILiveControl> _items = new();

    public string Name => _name;
    public string TypeName => "ControlArray";
    public int Count => _items.Count;
    public int LBound => _items.Count > 0 ? _items.Keys.First() : 0;
    public int UBound => _items.Count > 0 ? _items.Keys.Last() : 0;
    public IReadOnlyCollection<ILiveControl> Items => _items.Values;

    public LiveControlArray(string name)
    {
        _name = name;
    }

    public void Add(int index, ILiveControl control)
    {
        _items[index] = control;
    }

    public ILiveControl? GetItem(int index)
    {
        return _items.TryGetValue(index, out var ctrl) ? ctrl : null;
    }

    public VbValue GetProperty(string propertyName)
    {
        switch (propertyName.ToUpperInvariant())
        {
            case "COUNT":
                return VbValue.FromLong(Count);
            case "LBOUND":
                return VbValue.FromLong(LBound);
            case "UBOUND":
                return VbValue.FromLong(UBound);
            case "NAME":
                return VbValue.FromString(_name);
            default:
                return VbValue.Empty;
        }
    }

    public void SetProperty(string propertyName, VbValue value)
    {
    }

    public VbValue InvokeMethod(string methodName, VbValue[] args)
    {
        if ((string.IsNullOrEmpty(methodName) || methodName.Equals("Item", StringComparison.OrdinalIgnoreCase)) && args.Length > 0)
        {
            int idx = (int)args[0].AsLong();
            var item = GetItem(idx);
            return item != null ? VbValue.FromObject(item) : VbValue.Empty;
        }
        return VbValue.Empty;
    }
}

public interface ILiveForm : ILiveControl
{
    IReadOnlyDictionary<string, ILiveControl> Controls { get; }
    ILiveControl? FindControl(string name);
}

public class StackFrame
{
    public string ProcedureName { get; }
    public string? ModuleOrFormName { get; }
    public Dictionary<string, VbValue> LocalVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public OnErrorHandling ErrorMode { get; set; } = OnErrorHandling.Standard;
    public string? ErrorHandlerLabel { get; set; }
    public int CurrentLineNumber { get; set; }

    public StackFrame(string procedureName, string? moduleOrFormName = null)
    {
        ProcedureName = procedureName;
        ModuleOrFormName = moduleOrFormName;
    }
}

/// <summary>
/// Contexto completo de ejecución en tiempo real (Live Execution Context).
/// Mantiene la pila de llamadas, variables locales y globales, referencias a formularios vivos,
/// eventos de depuración y estado de la máquina de ejecución.
/// </summary>
public class VbExecutionContext
{
    public ExecutionState State { get; set; } = ExecutionState.Running;
    public StepMode CurrentStepMode { get; set; } = StepMode.None;
    public OnErrorHandling GlobalErrorMode { get; set; } = OnErrorHandling.Standard;

    public Stack<StackFrame> CallStack { get; } = new();
    public Dictionary<string, VbValue> ModuleVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, VbValue> StaticVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ILiveForm> ActiveForms { get; } = new(StringComparer.OrdinalIgnoreCase);

    public VbErrObject Err { get; } = new();

    public HashSet<(string FilePath, int LineNumber)> Breakpoints { get; } = [];

    // Interacción con UI y consola
    public event Action<string>? OnDebugPrint;
    public event Action<ExecutionState>? OnStateChanged;
    public event Action<string, int>? OnBreakpointHit;

    public Func<string, int, string, int>? MsgBoxHandler { get; set; }
    public Func<string, string, string, string>? InputBoxHandler { get; set; }
    public Action? DoEventsHandler { get; set; }

    public void DoEvents()
    {
        DoEventsHandler?.Invoke();
    }

    public static string GetStaticVarKey(string? procedureName, string varName)
        => $"{procedureName ?? ""}.{varName}";

    public StackFrame? CurrentFrame => CallStack.Count > 0 ? CallStack.Peek() : null;

    public void PushFrame(string procedureName, string? moduleOrFormName = null)
    {
        CallStack.Push(new StackFrame(procedureName, moduleOrFormName));
    }

    public void PopFrame()
    {
        if (CallStack.Count > 0)
        {
            CallStack.Pop();
        }
    }

    public VbValue GetVariable(string name)
    {
        if (name.Equals("Err", StringComparison.OrdinalIgnoreCase))
        {
            return VbValue.FromObject(Err);
        }

        // 1. Variable local en el marco actual
        if (CurrentFrame != null && CurrentFrame.LocalVariables.TryGetValue(name, out var localVal))
        {
            return localVal;
        }

        // 2. Variable estática del procedimiento actual
        if (CurrentFrame != null && StaticVariables.TryGetValue(GetStaticVarKey(CurrentFrame.ProcedureName, name), out var staticVal))
        {
            return staticVal;
        }

        // 3. Control en el formulario activo del marco actual
        if (CurrentFrame?.ModuleOrFormName != null &&
            ActiveForms.TryGetValue(CurrentFrame.ModuleOrFormName, out var currentForm))
        {
            var ctrl = currentForm.FindControl(name);
            if (ctrl != null) return VbValue.FromObject(ctrl);
        }

        // 4. Formulario activo con ese nombre
        if (ActiveForms.TryGetValue(name, out var form))
        {
            return VbValue.FromObject(form);
        }

        // 5. Variable a nivel de módulo o global
        if (ModuleVariables.TryGetValue(name, out var modVal))
        {
            return modVal;
        }

        // 6. Constantes intrínsecas de VB6 (vbInformation, vbOKOnly, vbCrLf, etc.)
        if (VbConstants.TryGetConstant(name, out var constVal))
        {
            return constVal;
        }

        return VbValue.Empty;
    }

    public void SetVariable(string name, VbValue value)
    {
        // Si ya existe como local en el marco actual, actualiza
        if (CurrentFrame != null && CurrentFrame.LocalVariables.ContainsKey(name))
        {
            CurrentFrame.LocalVariables[name] = value;
            return;
        }

        // Si ya existe como variable estática del procedimiento actual
        if (CurrentFrame != null)
        {
            string staticKey = GetStaticVarKey(CurrentFrame.ProcedureName, name);
            if (StaticVariables.ContainsKey(staticKey))
            {
                StaticVariables[staticKey] = value;
                return;
            }
        }

        // Si ya existe como variable de módulo
        if (ModuleVariables.ContainsKey(name))
        {
            ModuleVariables[name] = value;
            return;
        }

        // Por defecto en VB6, las variables nuevas se crean como locales
        if (CurrentFrame != null)
        {
            CurrentFrame.LocalVariables[name] = value;
        }
        else
        {
            ModuleVariables[name] = value;
        }
    }

    public void WriteDebugPrint(string text)
    {
        OnDebugPrint?.Invoke(text);
    }

    public void TriggerBreakpoint(string filePath, int lineNumber)
    {
        State = ExecutionState.Paused;
        OnStateChanged?.Invoke(State);
        OnBreakpointHit?.Invoke(filePath, lineNumber);
    }

    public void Clear()
    {
        CallStack.Clear();
        ModuleVariables.Clear();
        StaticVariables.Clear();
        ActiveForms.Clear();
        Err.Clear();
        State = ExecutionState.Stopped;
        CurrentStepMode = StepMode.None;
    }
}
