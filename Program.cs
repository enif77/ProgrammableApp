namespace ProgrammableApp;

using System.IO;

using EFrtScript;
using EFrtScript.IO;
using EFrtScript.Extensions;

internal static class Program
{
    private static void Main(string[] args)
    {
        var interpreter = new Interpreter(new ConsoleOutputWriter());

        new EFrtScript.Libs.Core.Library().Initialize(interpreter);
        new EFrtScript.Libs.CoreExt.Library().Initialize(interpreter);
        new EFrtScript.Libs.Exception.Library().Initialize(interpreter);
        
        Console.WriteLine("ProgrammableApp is starting...");

        SetupAppState(interpreter);
        AddCustomWords(interpreter);

        // The app script...
        interpreter.Interpret(File.ReadAllText("Resources/app.efrts"));


        // foreach (var p in _appState.StateProperties.Values)
        // {
        //     Console.WriteLine("Name: {0}", p.Name);
        // }

        while (true)
        {
            Console.Write(": ");

            var input = Console.ReadLine();            
            if (input == "bye")
            {
                break;
            }
            else
            {
                Console.WriteLine(">> {0}", input);
            }
        }

        Console.WriteLine("DONE");
    }   


    private static void SetupAppState(IInterpreter interpreter)
    {
        _appState.AppName = "App";
        _appState.AppVersion = "0";

        _appState.VariableAdded += HandleAppStateVariableAdded;
        _appState.VariableValueUpdated += HandleAppStateVariableValueUpdated;
        _appState.VariableRemoved += HandleAppStateVariableRemoved;
    }


    #region event handlers

    private static void HandleAppStateVariableAdded(object? sender, AppStateEventArgs e)
    {
        Console.WriteLine($"The {e.VariableName} variable added with value: '{e.NewValue!.String}'.");
    }


    private static void HandleAppStateVariableValueUpdated(object? sender, AppStateEventArgs e)
    {
        Console.WriteLine($"The {e.VariableName} variable value: '{e.OldValue!.String}' updated to: '{e.NewValue!.String}'.");
    }


    private static void HandleAppStateVariableRemoved(object? sender, AppStateEventArgs e)
    {
        Console.WriteLine($"The {e.VariableName} variable removed. Its value was: '{e.OldValue!.String}'.");
    }

    #endregion


    // private const string AppNamePropertyName = "appname";
    // private const string AppVersionPropertyName = "appversion";
    // private const string DebugPropertyName = "debug";
    

    private static void AddCustomWords(IInterpreter interpreter)
    {
        // HELLO ( -- )
        interpreter.AddPrimitiveWord("HELLO", (IInterpreter i) => 
        {
            Console.WriteLine("Hello from programmable app!");

            return 1;
        });


        // DEBUG (value -- )
        interpreter.AddPrimitiveWord("DEBUG", (IInterpreter i) => 
        {
            interpreter.StackExpect(1);

            var value = interpreter.StackPop().String;
            if (_appState.DebugEnabled)
            {
                Console.WriteLine("Debug: {0}", value);
            }

            return 1;
        });


        // GET-APP-STATE-JSON ( -- )
        interpreter.AddPrimitiveWord("GET-APP-STATE-JSON", (IInterpreter i) => 
        {
            interpreter.StackFree(1);

            //interpreter.StackPush(new StringValue(_appState.ToJson()));
            interpreter.StackPush(_appState.ToJson());

            return 1;
        });

        // GET (var-name -- )
        interpreter.AddPrimitiveWord("GET", (IInterpreter i) => 
        {
            interpreter.StackExpect(1);

            // We store all variables in lowercase, so a user does not have to remember the correct variable name case.
            var normalizedVariableName = AppState.GetNormalizedVariableName(interpreter.StackPop().String);

            // A direct property access example.
            // switch (normalizedVariableName)
            // {
            //     case AppNamePropertyName:
            //         interpreter.StackPush(_appState.AppName);
            //         break;

            //     case AppVersionPropertyName:
            //         interpreter.StackPush(_appState.AppVersion);
            //         break;

            //     case DebugPropertyName:
            //         interpreter.StackPush(_appState.DebugEnabled ? "true" : "false");
            //         break;

            //     default:
            //         if (_appState.HasVariable(normalizedVariableName) == false)
            //         {
            //             throw new InvalidOperationException($"Variable '{normalizedVariableName}' does not exists.");
            //         }

            //         interpreter.StackPush(_appState.Get(normalizedVariableName)!);
            //         break;
            // }

            if (_appState.StateProperties.TryGetValue(normalizedVariableName, out var property))
            {
                // bool, string, int, float, double, decimal
                switch (property.PropertyType.Name)
                {
                    case "String":
                        interpreter.StackPush((string)property.GetValue(_appState)!);
                        break;

                    case "Boolean":
                        interpreter.StackPush((bool)property.GetValue(_appState)!);
                        break;

                    case "Int32":
                        interpreter.StackPush((int)property.GetValue(_appState)!);
                        break;

                    case "Single":
                        interpreter.StackPush((float)property.GetValue(_appState)!);
                        break;

                    case "Double":
                        interpreter.StackPush((double)property.GetValue(_appState)!);
                        break;

                    case "Decimal":
                        interpreter.StackPush((double)(decimal)property.GetValue(_appState)!);
                        break;

                    default:
                        throw new InvalidOperationException($"Property '{property.Name}' has unsupported type '{property.PropertyType.Name}'.");
                }
            }
            else
            {
                if (_appState.HasVariable(normalizedVariableName) == false)
                {
                    throw new InvalidOperationException($"Variable '{normalizedVariableName}' does not exists.");
                }

                interpreter.StackPush(_appState.Get(normalizedVariableName)!);
            }

            return 1;
        });

        // SET (value var-name -- )
        interpreter.AddPrimitiveWord("SET", (IInterpreter i) => 
        {
            interpreter.StackExpect(2);

            var normalizedVariableName = AppState.GetNormalizedVariableName(interpreter.StackPop().String);

            // // A direct property access example.
            // switch (normalizedVariableName)
            // {
            //     case AppNamePropertyName:
            //         _appState.AppName = interpreter.StackPop().String;
            //         break;

            //     case AppVersionPropertyName:
            //         _appState.AppVersion = interpreter.StackPop().String;
            //         break;

            //     case DebugPropertyName:
            //         _appState.DebugEnabled = interpreter.StackPop().Boolean;
            //         break;

            //     default:
            //         _appState.Set(normalizedVariableName, interpreter.StackPop());
            //         break;
            // }

            if (_appState.StateProperties.TryGetValue(normalizedVariableName, out var property))
            {
                // bool, string, int, float, double, decimal
                switch (property.PropertyType.Name)
                {
                    case "String":
                        property.SetValue(_appState, interpreter.StackPop().String);
                        break;

                    case "Boolean":
                        property.SetValue(_appState, interpreter.StackPop().Boolean);
                        break;

                    case "Int32":
                        property.SetValue(_appState, interpreter.StackPop().Integer);
                        break;

                    case "Single":
                        property.SetValue(_appState, (float)interpreter.StackPop().Float);
                        break;

                    case "Double":
                        property.SetValue(_appState, interpreter.StackPop().Float);
                        break;

                    case "Decimal":
                        property.SetValue(_appState, (decimal)interpreter.StackPop().Float);
                        break;

                    default:
                        throw new InvalidOperationException($"Property '{property.Name}' has unsupported type '{property.PropertyType.Name}'.");
                }
            }
            else 
            {
                _appState.Set(normalizedVariableName, interpreter.StackPop());
            }

            return 1;
        });


        // REMOVE-VARIABLE (var-name -- )
        interpreter.AddPrimitiveWord("REMOVE-VARIABLE", (IInterpreter i) => 
        {
            interpreter.StackExpect(1);

            _appState.Set(AppState.GetNormalizedVariableName(interpreter.StackPop().String), null);

            return 1;
        });


        // INCLUDE-SCRIPT (script-path -- )
        interpreter.AddPrimitiveWord("INCLUDE-SCRIPT", (IInterpreter i) => 
        {
            interpreter.StackExpect(1);

            var scriptPath = interpreter.StackPop().String;
            if (File.Exists(scriptPath) == false)
            {
                throw new Exception($"Script '{scriptPath}' does not exist.");
            }

            interpreter.Interpret(File.ReadAllText(scriptPath));

            return 1;
        });
    } 
    

    private static AppState _appState = new AppState();
}
