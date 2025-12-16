# Phase Planning: Game Logic Implementation

This document covers four interconnected game logic phases essential for gameplay functionality.

---

## PHASE04: Scripting Engine Implementation

**Phase Identifier**: PHASE04_SCRIPTING_ENGINE  
**Status**: Planning  
**Priority**: Critical  
**Estimated Duration**: 3-4 weeks

### Overview

Implement a custom scripting engine for mission logic, cutscenes, and game events. The original game uses a custom binary scripting format (`.scb` files) with conditions, actions, and sequential execution.

**Current Status**: 0% complete (not started)  
**Target Status**: 100% complete

### Detailed Tasks

#### Task 1: Script File Parser (PLAN-016)
**Phase**: Phase 1 (Week 1)  
**Complexity**: High  
**Effort**: 3-4 days  
**Dependencies**: None  

**Description**:
Parse binary `.scb` (Script Binary) files containing mission scripts.

**Current State**:
- No parser implementation
- Reference: `references/generals_code/` contains script loading code

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Scripting/ScriptFile.cs

public sealed class ScriptFile
{
    public List<Script> Scripts { get; private set; }
    public List<ScriptGroup> ScriptGroups { get; private set; }
    
    public static ScriptFile Load(Stream stream)
    {
        var reader = new BinaryReader(stream);
        var scriptFile = new ScriptFile();
        
        // Parse binary format
        int scriptCount = reader.ReadInt32();
        scriptFile.Scripts = new List<Script>(scriptCount);
        
        for (int i = 0; i < scriptCount; i++)
        {
            scriptFile.Scripts.Add(Script.Parse(reader));
        }
        
        int groupCount = reader.ReadInt32();
        scriptFile.ScriptGroups = new List<ScriptGroup>(groupCount);
        
        for (int i = 0; i < groupCount; i++)
        {
            scriptFile.ScriptGroups.Add(ScriptGroup.Parse(reader));
        }
        
        return scriptFile;
    }
}

public sealed class Script
{
    public string Name { get; set; }
    public bool OneShot { get; set; }
    public List<Condition> Conditions { get; private set; }
    public List<ScriptAction> Actions { get; private set; }
    public DifficultyPreferences DifficultyPreferences { get; set; }
    
    public static Script Parse(BinaryReader reader)
    {
        var script = new Script();
        script.Name = reader.ReadCString();
        script.OneShot = reader.ReadBoolean();
        
        int conditionCount = reader.ReadInt32();
        script.Conditions = new List<Condition>(conditionCount);
        for (int i = 0; i < conditionCount; i++)
        {
            script.Conditions.Add(Condition.Parse(reader));
        }
        
        int actionCount = reader.ReadInt32();
        script.Actions = new List<ScriptAction>(actionCount);
        for (int i = 0; i < actionCount; i++)
        {
            script.Actions.Add(ScriptAction.Parse(reader));
        }
        
        return script;
    }
}

public sealed class Condition
{
    public string Name { get; set; }
    public List<ConditionParameter> Parameters { get; private set; }
    
    public static Condition Parse(BinaryReader reader)
    {
        var condition = new Condition();
        condition.Name = reader.ReadCString();
        
        int paramCount = reader.ReadInt32();
        condition.Parameters = new List<ConditionParameter>(paramCount);
        for (int i = 0; i < paramCount; i++)
        {
            condition.Parameters.Add(ConditionParameter.Parse(reader));
        }
        
        return condition;
    }
}

public sealed class ScriptAction
{
    public string Name { get; set; }
    public List<ActionParameter> Parameters { get; private set; }
    
    public static ScriptAction Parse(BinaryReader reader)
    {
        var action = new ScriptAction();
        action.Name = reader.ReadCString();
        
        int paramCount = reader.ReadInt32();
        action.Parameters = new List<ActionParameter>(paramCount);
        for (int i = 0; i < paramCount; i++)
        {
            action.Parameters.Add(ActionParameter.Parse(reader));
        }
        
        return action;
    }
}
```

**Acceptance Criteria**:
- [ ] Parse all script files without errors
- [ ] Conditions extracted correctly
- [ ] Actions extracted correctly
- [ ] Script groups working
- [ ] Performance acceptable with 1000+ scripts

**Testing**:
```csharp
[Test]
public void TestScriptParsing()
{
    using (var stream = File.OpenRead("Data/Scripts/TestScript.scb"))
    {
        var scriptFile = ScriptFile.Load(stream);
        
        Assert.Greater(scriptFile.Scripts.Count, 0);
        Assert.Greater(scriptFile.ScriptGroups.Count, 0);
    }
}
```

#### Task 2: Condition Evaluator (PLAN-017)
**Phase**: Phase 1 (Week 1-2)  
**Complexity**: High  
**Effort**: 2-3 days  
**Dependencies**: PLAN-016  

**Description**:
Evaluate script conditions (e.g., "player has 50 units", "timer > 30 seconds").

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Scripting/ConditionEvaluator.cs

public sealed class ConditionEvaluator
{
    private readonly GameLogic _gameLogic;
    private readonly Dictionary<string, Func<Condition, bool>> _evaluators;
    
    public ConditionEvaluator(GameLogic gameLogic)
    {
        _gameLogic = gameLogic;
        _evaluators = new Dictionary<string, Func<Condition, bool>>()
        {
            ["CounterEquals"] = EvaluateCounterEquals,
            ["CounterGreaterThan"] = EvaluateCounterGreaterThan,
            ["PlayerUnitCount"] = EvaluatePlayerUnitCount,
            ["PlayerHasMoney"] = EvaluatePlayerHasMoney,
            ["BuildingDestroyed"] = EvaluateBuildingDestroyed,
            ["UnitEnters"] = EvaluateUnitEnters,
            ["TimerExpired"] = EvaluateTimerExpired,
        };
    }
    
    public bool Evaluate(Condition condition)
    {
        if (_evaluators.TryGetValue(condition.Name, out var evaluator))
        {
            return evaluator(condition);
        }
        
        throw new NotImplementedException($"Condition {condition.Name} not implemented");
    }
    
    private bool EvaluateCounterEquals(Condition condition)
    {
        var counterIndex = condition.GetParameter<int>("CounterIndex");
        var value = condition.GetParameter<int>("Value");
        
        return _gameLogic.Counters[counterIndex] == value;
    }
    
    private bool EvaluatePlayerUnitCount(Condition condition)
    {
        var player = condition.GetParameter<Player>("Player");
        var minCount = condition.GetParameter<int>("MinCount");
        
        return player.Objects.OfType<Unit>().Count() >= minCount;
    }
    
    private bool EvaluatePlayerHasMoney(Condition condition)
    {
        var player = condition.GetParameter<Player>("Player");
        var amount = condition.GetParameter<int>("Amount");
        
        return player.Money >= amount;
    }
    
    private bool EvaluateBuildingDestroyed(Condition condition)
    {
        var building = condition.GetParameter<Building>("Building");
        
        return building.IsDestroyed;
    }
    
    private bool EvaluateUnitEnters(Condition condition)
    {
        var unitType = condition.GetParameter<string>("UnitType");
        var area = condition.GetParameter<TriggerArea>("Area");
        
        return _gameLogic.Objects
            .OfType<Unit>()
            .Where(u => u.Template.Name == unitType)
            .Any(u => area.Contains(u.Position));
    }
    
    private bool EvaluateTimerExpired(Condition condition)
    {
        var timerId = condition.GetParameter<int>("TimerId");
        var elapsedMs = condition.GetParameter<int>("ElapsedMs");
        
        var timer = _gameLogic.GetTimer(timerId);
        return timer != null && timer.ElapsedMilliseconds >= elapsedMs;
    }
}
```

**Acceptance Criteria**:
- [ ] All common conditions implemented
- [ ] Parameter types correctly handled
- [ ] Extensible for new condition types
- [ ] Performance fast enough for real-time evaluation

#### Task 3: Action Executor (PLAN-018)
**Phase**: Phase 2 (Week 2-3)  
**Complexity**: High  
**Effort**: 3-4 days  
**Dependencies**: PLAN-016  

**Description**:
Execute script actions (e.g., "create unit", "play sound", "display message").

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Scripting/ActionExecutor.cs

public sealed class ActionExecutor
{
    private readonly GameLogic _gameLogic;
    private readonly Dictionary<string, Action<ScriptAction>> _executors;
    
    public ActionExecutor(GameLogic gameLogic)
    {
        _gameLogic = gameLogic;
        _executors = new Dictionary<string, Action<ScriptAction>>()
        {
            ["CreateUnit"] = ExecuteCreateUnit,
            ["DestroyObject"] = ExecuteDestroyObject,
            ["PlaySound"] = ExecutePlaySound,
            ["DisplayMessage"] = ExecuteDisplayMessage,
            ["IncrementCounter"] = ExecuteIncrementCounter,
            ["StartTimer"] = ExecuteStartTimer,
            ["PlayVideo"] = ExecutePlayVideo,
            ["TriggerDialog"] = ExecuteTriggerDialog,
        };
    }
    
    public void Execute(ScriptAction action)
    {
        if (_executors.TryGetValue(action.Name, out var executor))
        {
            executor(action);
            return;
        }
        
        throw new NotImplementedException($"Action {action.Name} not implemented");
    }
    
    private void ExecuteCreateUnit(ScriptAction action)
    {
        var player = action.GetParameter<Player>("Player");
        var unitType = action.GetParameter<string>("UnitType");
        var position = action.GetParameter<Coord3D>("Position");
        
        var template = _gameLogic.ContentManager.Load<ObjectTemplate>(unitType);
        var unit = new Unit(template, player, position);
        
        _gameLogic.AddObject(unit);
    }
    
    private void ExecuteDestroyObject(ScriptAction action)
    {
        var objectId = action.GetParameter<uint>("ObjectId");
        var obj = _gameLogic.GetObjectById(objectId);
        
        if (obj != null)
        {
            _gameLogic.RemoveObject(obj);
        }
    }
    
    private void ExecutePlaySound(ScriptAction action)
    {
        var soundName = action.GetParameter<string>("SoundName");
        
        var audio = _gameLogic.AudioSystem;
        audio.PlaySound(soundName);
    }
    
    private void ExecuteDisplayMessage(ScriptAction action)
    {
        var text = action.GetParameter<string>("Text");
        var duration = action.GetParameter<float>("Duration");
        
        // Display message in UI
        _gameLogic.Scene2D.DisplayMessage(text, TimeSpan.FromSeconds(duration));
    }
    
    private void ExecuteIncrementCounter(ScriptAction action)
    {
        var counterIndex = action.GetParameter<int>("CounterIndex");
        var amount = action.GetParameter<int>("Amount");
        
        _gameLogic.Counters[counterIndex] += amount;
    }
    
    private void ExecuteStartTimer(ScriptAction action)
    {
        var timerId = action.GetParameter<int>("TimerId");
        var durationMs = action.GetParameter<int>("DurationMs");
        
        _gameLogic.StartTimer(timerId, durationMs);
    }
    
    private void ExecutePlayVideo(ScriptAction action)
    {
        var videoName = action.GetParameter<string>("VideoName");
        
        _gameLogic.PlayVideo(videoName);
    }
    
    private void ExecuteTriggerDialog(ScriptAction action)
    {
        var dialogName = action.GetParameter<string>("DialogName");
        
        _gameLogic.TriggerDialog(dialogName);
    }
}
```

**Acceptance Criteria**:
- [ ] All common actions implemented
- [ ] Actions execute without errors
- [ ] Extensible for new action types
- [ ] Queuing for delayed execution

#### Task 4: Script Engine Manager (PLAN-019)
**Phase**: Phase 2-3 (Week 3)  
**Complexity**: Medium  
**Effort**: 2-3 days  
**Dependencies**: PLAN-017, PLAN-018  

**Description**:
Manage script execution, state, and lifecycle.

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Scripting/ScriptEngine.cs

public sealed class ScriptEngine : DisposableBase
{
    private readonly GameLogic _gameLogic;
    private readonly ScriptFile _scriptFile;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly ActionExecutor _actionExecutor;
    
    private readonly Dictionary<string, ScriptState> _scriptStates;
    private readonly List<SequentialScript> _sequentialScripts;
    private int[] _counters;
    private int[] _flags;
    
    public ScriptEngine(GameLogic gameLogic, ScriptFile scriptFile)
    {
        _gameLogic = gameLogic;
        _scriptFile = scriptFile;
        _conditionEvaluator = new ConditionEvaluator(gameLogic);
        _actionExecutor = new ActionExecutor(gameLogic);
        
        _scriptStates = new Dictionary<string, ScriptState>();
        _sequentialScripts = new List<SequentialScript>();
        _counters = new int[256];
        _flags = new int[256];
        
        InitializeScripts();
    }
    
    private void InitializeScripts()
    {
        foreach (var script in _scriptFile.Scripts)
        {
            _scriptStates[script.Name] = new ScriptState(script);
        }
    }
    
    public void Update(in TimeInterval gameTime)
    {
        // Evaluate and execute scripts
        foreach (var scriptState in _scriptStates.Values)
        {
            if (scriptState.IsExecuted && scriptState.Script.OneShot)
                continue;
            
            // Evaluate all conditions
            bool allConditionsMet = scriptState.Script.Conditions
                .All(c => _conditionEvaluator.Evaluate(c));
            
            if (allConditionsMet)
            {
                // Execute all actions
                foreach (var action in scriptState.Script.Actions)
                {
                    _actionExecutor.Execute(action);
                }
                
                scriptState.IsExecuted = true;
            }
        }
        
        // Update sequential scripts
        for (int i = _sequentialScripts.Count - 1; i >= 0; i--)
        {
            var seqScript = _sequentialScripts[i];
            if (seqScript.Update(gameTime))
            {
                _sequentialScripts.RemoveAt(i);
            }
        }
    }
    
    public void RunScript(string scriptName)
    {
        if (_scriptStates.TryGetValue(scriptName, out var state))
        {
            state.IsExecuted = false; // Reset for re-execution
        }
    }
    
    public int AllocateCounter()
    {
        for (int i = 0; i < _counters.Length; i++)
        {
            if (_counters[i] == 0) // Assuming 0 means unused
                return i;
        }
        
        throw new InvalidOperationException("No available counters");
    }
    
    public override void Dispose()
    {
        base.Dispose();
    }
}

public sealed class ScriptState
{
    public Script Script { get; }
    public bool IsExecuted { get; set; }
    
    public ScriptState(Script script)
    {
        Script = script;
        IsExecuted = false;
    }
}

public sealed class SequentialScript
{
    public List<ScriptAction> Actions { get; private set; }
    public int CurrentActionIndex { get; private set; }
    public TimeSpan FrameWaitTime { get; set; }
    public int LoopCount { get; set; }
    
    public bool Update(in TimeInterval gameTime)
    {
        if (CurrentActionIndex >= Actions.Count)
        {
            if (LoopCount > 0)
            {
                LoopCount--;
                CurrentActionIndex = 0;
                return false;
            }
            
            return true; // Finished
        }
        
        CurrentActionIndex++;
        return false;
    }
}
```

**Acceptance Criteria**:
- [ ] Scripts execute when conditions met
- [ ] Sequential script queuing working
- [ ] Counter/flag management working
- [ ] One-shot scripts don't repeat
- [ ] Performance acceptable

---

## PHASE05: APT Virtual Machine & ActionScript

**Phase Identifier**: PHASE05_APT_VIRTUAL_MACHINE  
**Status**: Planning  
**Priority**: High  
**Estimated Duration**: 4-5 weeks

### Overview

Implement the APT (Adobe Flash) virtual machine and ActionScript interpreter for GUI animations and interactions. APT files are binary SWF-like files with ActionScript bytecode.

**Current Status**: 40% complete (parsing done, VM incomplete)  
**Target Status**: 100% complete

### Detailed Tasks

#### Task 1: ActionScript Type System (PLAN-020)
**Phase**: Phase 1 (Week 1)  
**Complexity**: High  
**Effort**: 2-3 days  
**Dependencies**: None  

**Description**:
Implement ActionScript value types, objects, and class system.

**Implementation**:

```csharp
// File: src/OpenSage.Game/Gui/Apt/ActionScript/Value.cs

public abstract class Value
{
    public static readonly Value Undefined = new UndefinedValue();
    public static readonly Value Null = new NullValue();
    
    public abstract T As<T>();
    public abstract string ToDebugString();
    
    public static Value FromObject(object obj)
    {
        return obj switch
        {
            int i => new IntValue(i),
            float f => new FloatValue(f),
            bool b => new BooleanValue(b),
            string s => new StringValue(s),
            ObjectValue ov => ov,
            _ => Undefined
        };
    }
}

public sealed class IntValue : Value
{
    public int Value { get; }
    
    public IntValue(int value) => Value = value;
    
    public override T As<T>()
    {
        if (typeof(T) == typeof(int))
            return (T)(object)Value;
        if (typeof(T) == typeof(float))
            return (T)(object)(float)Value;
        if (typeof(T) == typeof(string))
            return (T)(object)Value.ToString();
        if (typeof(T) == typeof(bool))
            return (T)(object)(Value != 0);
        
        return default;
    }
    
    public override string ToDebugString() => Value.ToString();
}

public sealed class StringValue : Value
{
    public string Value { get; }
    
    public StringValue(string value) => Value = value ?? string.Empty;
    
    public override T As<T>()
    {
        if (typeof(T) == typeof(string))
            return (T)(object)Value;
        if (typeof(T) == typeof(int))
            return (T)(object)(int.TryParse(Value, out var i) ? i : 0);
        if (typeof(T) == typeof(bool))
            return (T)(object)(!string.IsNullOrEmpty(Value));
        
        return default;
    }
    
    public override string ToDebugString() => $"\"{Value}\"";
}

public sealed class BooleanValue : Value
{
    public bool Value { get; }
    
    public BooleanValue(bool value) => Value = value;
    
    public override T As<T>()
    {
        if (typeof(T) == typeof(bool))
            return (T)(object)Value;
        if (typeof(T) == typeof(int))
            return (T)(object)(Value ? 1 : 0);
        if (typeof(T) == typeof(string))
            return (T)(object)(Value ? "true" : "false");
        
        return default;
    }
    
    public override string ToDebugString() => Value ? "true" : "false";
}

public sealed class ObjectValue : Value
{
    public Dictionary<string, Value> Properties { get; }
    
    public ObjectValue()
    {
        Properties = new Dictionary<string, Value>();
    }
    
    public override T As<T>()
    {
        return default;
    }
    
    public override string ToDebugString() => "[object Object]";
}

public sealed class ArrayValue : Value
{
    public List<Value> Elements { get; }
    
    public ArrayValue()
    {
        Elements = new List<Value>();
    }
    
    public override T As<T>()
    {
        return default;
    }
    
    public override string ToDebugString() => $"[{string.Join(", ", Elements.Select(e => e.ToDebugString()))}]";
}

public sealed class FunctionValue : Value
{
    public delegate Value FunctionDelegate(params Value[] args);
    
    public FunctionDelegate Function { get; }
    
    public FunctionValue(FunctionDelegate function)
    {
        Function = function;
    }
    
    public override T As<T>()
    {
        return default;
    }
    
    public override string ToDebugString() => "[Function]";
}
```

**Acceptance Criteria**:
- [ ] All basic types implemented
- [ ] Type conversion working
- [ ] Objects and arrays working
- [ ] Functions callable

#### Task 2: ActionScript VM Bytecode Interpreter (PLAN-021)
**Phase**: Phase 2 (Week 2-3)  
**Complexity**: Very High  
**Effort**: 4-5 days  
**Dependencies**: PLAN-020  

**Description**:
Implement ActionScript bytecode interpretation and execution.

**Implementation**:

```csharp
// File: src/OpenSage.Game/Gui/Apt/ActionScript/VM/VM.cs

public sealed class VM
{
    private Stack<Value> _stack;
    private Dictionary<string, Value> _globalVariables;
    private List<Frame> _callStack;
    
    public VM()
    {
        _stack = new Stack<Value>();
        _globalVariables = new Dictionary<string, Value>();
        _callStack = new List<Frame>();
    }
    
    public void ExecuteMethod(byte[] bytecode, Dictionary<string, Value> localVariables)
    {
        var frame = new Frame(bytecode, localVariables);
        _callStack.Add(frame);
        
        try
        {
            while (frame.PC < bytecode.Length)
            {
                var opcode = (Opcode)bytecode[frame.PC];
                frame.PC++;
                
                ExecuteOpcode(opcode, bytecode, frame);
            }
        }
        finally
        {
            _callStack.RemoveAt(_callStack.Count - 1);
        }
    }
    
    private void ExecuteOpcode(Opcode opcode, byte[] bytecode, Frame frame)
    {
        switch (opcode)
        {
            case Opcode.PushConstant:
                {
                    var constIndex = ReadInt32(bytecode, ref frame.PC);
                    _stack.Push(frame.Constants[constIndex]);
                }
                break;
                
            case Opcode.PushLocal:
                {
                    var varIndex = ReadInt32(bytecode, ref frame.PC);
                    var varName = frame.LocalNames[varIndex];
                    _stack.Push(frame.LocalVariables[varName]);
                }
                break;
                
            case Opcode.SetLocal:
                {
                    var varIndex = ReadInt32(bytecode, ref frame.PC);
                    var varName = frame.LocalNames[varIndex];
                    frame.LocalVariables[varName] = _stack.Pop();
                }
                break;
                
            case Opcode.Add:
                {
                    var b = _stack.Pop().As<float>();
                    var a = _stack.Pop().As<float>();
                    _stack.Push(new FloatValue(a + b));
                }
                break;
                
            case Opcode.Subtract:
                {
                    var b = _stack.Pop().As<float>();
                    var a = _stack.Pop().As<float>();
                    _stack.Push(new FloatValue(a - b));
                }
                break;
                
            case Opcode.Multiply:
                {
                    var b = _stack.Pop().As<float>();
                    var a = _stack.Pop().As<float>();
                    _stack.Push(new FloatValue(a * b));
                }
                break;
                
            case Opcode.Divide:
                {
                    var b = _stack.Pop().As<float>();
                    var a = _stack.Pop().As<float>();
                    _stack.Push(new FloatValue(a / b));
                }
                break;
                
            case Opcode.Equal:
                {
                    var b = _stack.Pop();
                    var a = _stack.Pop();
                    _stack.Push(new BooleanValue(ValuesEqual(a, b)));
                }
                break;
                
            case Opcode.JumpIfFalse:
                {
                    var target = ReadInt32(bytecode, ref frame.PC);
                    var condition = _stack.Pop().As<bool>();
                    if (!condition)
                        frame.PC = target;
                }
                break;
                
            case Opcode.Call:
                {
                    var numArgs = ReadInt32(bytecode, ref frame.PC);
                    var args = new Value[numArgs];
                    for (int i = numArgs - 1; i >= 0; i--)
                        args[i] = _stack.Pop();
                    
                    var function = _stack.Pop().As<FunctionValue>();
                    var result = function.Function.Invoke(args);
                    _stack.Push(result);
                }
                break;
                
            default:
                throw new NotImplementedException($"Opcode {opcode} not implemented");
        }
    }
    
    private bool ValuesEqual(Value a, Value b)
    {
        if (a is IntValue ia && b is IntValue ib)
            return ia.Value == ib.Value;
        if (a is StringValue sa && b is StringValue sb)
            return sa.Value == sb.Value;
        if (a is BooleanValue ba && b is BooleanValue bb)
            return ba.Value == bb.Value;
        
        return false;
    }
    
    private int ReadInt32(byte[] data, ref int pc)
    {
        var value = BitConverter.ToInt32(data, pc);
        pc += 4;
        return value;
    }
}

public enum Opcode : byte
{
    PushConstant = 0x01,
    PushLocal = 0x02,
    SetLocal = 0x03,
    Add = 0x04,
    Subtract = 0x05,
    Multiply = 0x06,
    Divide = 0x07,
    Equal = 0x08,
    JumpIfFalse = 0x09,
    Call = 0x0A,
}

public sealed class Frame
{
    public byte[] Bytecode { get; }
    public int PC { get; set; }
    public Dictionary<string, Value> LocalVariables { get; }
    public List<Value> Constants { get; }
    public List<string> LocalNames { get; }
    
    public Frame(byte[] bytecode, Dictionary<string, Value> locals)
    {
        Bytecode = bytecode;
        PC = 0;
        LocalVariables = locals;
        Constants = new List<Value>();
        LocalNames = new List<string>();
    }
}
```

**Acceptance Criteria**:
- [ ] Basic opcodes executing correctly
- [ ] Stack operations working
- [ ] Variable scoping correct
- [ ] Function calls working
- [ ] Performance acceptable

#### Task 3: APT Callback System (PLAN-022)
**Phase**: Phase 3 (Week 4)  
**Complexity**: Medium  
**Effort**: 2-3 days  
**Dependencies**: PLAN-021  

**Description**:
Implement callbacks from ActionScript to C# game code.

**Implementation**:

```csharp
// File: src/OpenSage.Game/Gui/Apt/ActionScript/AptCallbacks.cs

[AttributeUsage(AttributeTargets.Method)]
public sealed class AptCallbackAttribute : Attribute
{
    public string Name { get; }
    
    public AptCallbackAttribute(string name)
    {
        Name = name;
    }
}

public sealed class AptCallbackResolver
{
    private Dictionary<string, MethodInfo> _callbacks;
    
    public AptCallbackResolver(Type[] callbackTypes)
    {
        _callbacks = new Dictionary<string, MethodInfo>();
        
        foreach (var type in callbackTypes)
        {
            foreach (var method in type.GetMethods())
            {
                var attr = method.GetCustomAttribute<AptCallbackAttribute>();
                if (attr != null)
                {
                    _callbacks[attr.Name] = method;
                }
            }
        }
    }
    
    public Value InvokeCallback(string name, params Value[] args)
    {
        if (_callbacks.TryGetValue(name, out var method))
        {
            try
            {
                var result = method.Invoke(null, args.Cast<object>().ToArray());
                return Value.FromObject(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error invoking callback {name}: {ex}");
                return Value.Undefined;
            }
        }
        
        throw new InvalidOperationException($"Callback {name} not found");
    }
}

// Example usage:
public static class AptGameCallbacks
{
    [AptCallback("playSound")]
    public static void PlaySound(string soundName)
    {
        // Play sound
    }
    
    [AptCallback("getPlayerGold")]
    public static int GetPlayerGold()
    {
        // Return player gold amount
        return 0;
    }
    
    [AptCallback("displayMessage")]
    public static void DisplayMessage(string message)
    {
        // Display message in UI
    }
}
```

**Acceptance Criteria**:
- [ ] Callbacks resolving correctly
- [ ] Parameter passing working
- [ ] Return values handled
- [ ] Error handling graceful

#### Task 4: APT Event System (PLAN-023)
**Phase**: Phase 3-4 (Week 4-5)  
**Complexity**: Medium  
**Effort**: 2-3 days  
**Dependencies**: PLAN-022  

**Description**:
Implement event handling (mouse clicks, frame events, etc.).

**Implementation**:

```csharp
// File: src/OpenSage.Game/Gui/Apt/ActionScript/AptEventSystem.cs

public sealed class AptEventSystem
{
    private Dictionary<string, List<Action<AptEvent>>> _eventHandlers;
    
    public AptEventSystem()
    {
        _eventHandlers = new Dictionary<string, List<Action<AptEvent>>>();
    }
    
    public void RegisterHandler(string eventName, Action<AptEvent> handler)
    {
        if (!_eventHandlers.ContainsKey(eventName))
            _eventHandlers[eventName] = new List<Action<AptEvent>>();
        
        _eventHandlers[eventName].Add(handler);
    }
    
    public void DispatchEvent(AptEvent @event)
    {
        if (_eventHandlers.TryGetValue(@event.Name, out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler(@event);
            }
        }
    }
}

public sealed class AptEvent
{
    public string Name { get; set; }
    public Dictionary<string, Value> Properties { get; set; }
    
    public AptEvent(string name)
    {
        Name = name;
        Properties = new Dictionary<string, Value>();
    }
}
```

**Acceptance Criteria**:
- [ ] Events dispatching correctly
- [ ] Handlers firing appropriately
- [ ] Event properties accessible

---

## PHASE06: Physics Engine

**Phase Identifier**: PHASE06_PHYSICS_ENGINE  
**Status**: Planning  
**Priority**: High  
**Estimated Duration**: 2-3 weeks

### Overview

Implement rigid body physics with collisions, gravity, and friction.

**Current Status**: 20% complete (basic structure exists)  
**Target Status**: 100% complete

### Detailed Tasks

#### Task 1: Rigid Body Physics (PLAN-024)
**Phase**: Phase 1 (Week 1)  
**Complexity**: High  
**Effort**: 3-4 days  
**Dependencies**: None  

#### Task 2: Collision Detection (PLAN-025)
**Phase**: Phase 1-2 (Week 1-2)  
**Complexity**: High  
**Effort**: 3-4 days  
**Dependencies**: PLAN-024  

#### Task 3: Gravity & Friction (PLAN-026)
**Phase**: Phase 2 (Week 2)  
**Complexity**: Medium  
**Effort**: 2-3 days  
**Dependencies**: PLAN-024  

---

## PHASE07: Weapons, Locomotors & AI

**Phase Identifier**: PHASE07_COMBAT_LOCOMOTION_AI  
**Status**: Planning  
**Priority**: Critical  
**Estimated Duration**: 5-6 weeks

### Overview

Implement weapons system, unit locomotion, and AI behaviors for combat.

**Current Status**: 15% complete (basic structures only)  
**Target Status**: 100% complete

### Detailed Tasks

#### Task 1: Weapons System (PLAN-027)
**Phase**: Phase 1-2 (Weeks 1-2)  
**Complexity**: Very High  
**Effort**: 5-6 days  
**Dependencies**: None  

#### Task 2: Locomotor System (PLAN-028)
**Phase**: Phase 2 (Week 2-3)  
**Complexity**: High  
**Effort**: 3-4 days  
**Dependencies**: PLAN-026 (Physics)  

#### Task 3: AI Pathfinding (PLAN-029)
**Phase**: Phase 3 (Week 3-4)  
**Complexity**: High  
**Effort**: 4-5 days  
**Dependencies**: None  

#### Task 4: AI Base Building (PLAN-030)
**Phase**: Phase 4 (Week 4-5)  
**Complexity**: Very High  
**Effort**: 4-5 days  
**Dependencies**: PLAN-029  

#### Task 5: AI Unit Control (PLAN-031)
**Phase**: Phase 4-5 (Week 5-6)  
**Complexity**: Very High  
**Effort**: 5-6 days  
**Dependencies**: PLAN-029, PLAN-027  

---

## Integration Points

All game logic components integrate through `GameLogic`:

```csharp
public sealed class GameLogic : DisposableBase
{
    public ScriptEngine ScriptEngine { get; set; }
    public AptEventSystem AptEvents { get; set; }
    public PhysicsEngine PhysicsEngine { get; set; }
    public WeaponSystem WeaponSystem { get; set; }
    public AISystem AISystem { get; set; }
    public PathfindingSystem PathfindingSystem { get; set; }
    
    public void Update(in TimeInterval gameTime)
    {
        ScriptEngine?.Update(gameTime);
        PhysicsEngine?.Update(gameTime);
        AISystem?.Update(gameTime);
        WeaponSystem?.Update(gameTime);
    }
}
```

---

## Success Metrics

- [ ] All scripting engine features working
- [ ] APT/ActionScript rendering and interaction smooth
- [ ] Physics accurate and performant
- [ ] Weapons dealing damage correctly
- [ ] Units moving with proper locomotion
- [ ] AI making intelligent decisions
- [ ] All systems integrate seamlessly
- [ ] Unit test coverage > 75%
- [ ] Performance: 60 FPS with 100+ units
