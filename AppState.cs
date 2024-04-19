namespace ProgrammableApp;

using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;


using EFrtScript;


[AttributeUsage(AttributeTargets.Property)]
public class AppStatePropertyAttribute: Attribute
{
}


// https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/how-to-publish-events-that-conform-to-net-framework-guidelines
public class AppStateEventArgs : EventArgs
{
    public AppStateEventArgs(string variableName)
    {
        AppState.CheckVariableName(variableName);

        VariableName = variableName;
    }

    public string VariableName { get; set; }
    public IValue? OldValue { get; set; }
    public IValue? NewValue { get; set; }
}


//public delegate void AppStateEventHandler(object? sender, AppStateEventArgs args);


internal class AppState
{
    #region properties

    [AppStateProperty]
    public string AppName { get; set; } = "App";
    
    [AppStateProperty]
    public string AppVersion { get; set; } = "1.0.0";

    [AppStateProperty]
    public bool DebugEnabled { get; set; }


    [AppStateProperty]
    public int IntValue { get; set; } = 1;

    [AppStateProperty]
    public float FloatValue { get; set; } = 2.1f;

    [AppStateProperty]
    public double DoubleValue { get; set; } = 3.4;

    [AppStateProperty]
    public decimal DecimalValue { get; set; } = 4.5M;


    public IReadOnlyDictionary<string, IValue> Variables { get; } = new Dictionary<string, IValue>();


    IReadOnlyDictionary<string, PropertyInfo>? _stateProperties = null;

    [JsonIgnore]
    public IReadOnlyDictionary<string, PropertyInfo> StateProperties
    {
        get
        {
            if (_stateProperties == null)
            {
                var stateProperties = new Dictionary<string, PropertyInfo>();

                foreach (var propertyInfo in GetType().GetProperties())
                {
                    if (propertyInfo.CanRead == false || propertyInfo.CanWrite == false)
                    {
                        // Skip properties without a getter or setter.
                        continue;
                    }

                    var attributes = propertyInfo.GetCustomAttributes(typeof(AppStatePropertyAttribute), true);
                    if (attributes.Length == 0)
                    {
                        // Skip properties without the AppStateProperty attribute.
                        continue;
                    }

                    stateProperties.Add(propertyInfo.Name.ToLowerInvariant(), propertyInfo);
                }

                _stateProperties = stateProperties;
            }

            return _stateProperties;
        }
    }
    

    #endregion
    

    #region variables

    /// <summary>
    /// Checks a variable name if it is a valid one.
    /// </summary>
    /// <param name="variableName">A variable name.</param>
    /// <exception cref="ArgumentException">If the given variable name is not valid.</exception>
    public static void CheckVariableName(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException($"A variable name expected.", nameof(variableName));
        }
    }

    /// <summary>
    /// Returns a variable name normalised for storage.
    /// </summary>
    /// <param name="variableName">A variable name.</param>
    /// <returns>Normalised variable name.</returns>
    public static string GetNormalizedVariableName(string variableName)
    {
        CheckVariableName(variableName);

        return variableName.ToLowerInvariant();
    }

    /// <summary>
    /// Returns true, if a variable with the given name is defined.
    /// </summary>
    /// <param name="variableName">A normalised variable name.</param>
    /// <returns>True, if a variable is defined.</returns>
    public bool HasVariable(string variableName)
    {
        CheckVariableName(variableName);

        return Variables.ContainsKey(variableName);
    }

    /// <summary>
    /// Gets a value of a variable. If no such variable is defined, the default value is returned.
    /// </summary>
    /// <param name="variableName">A normalised variable name.</param>
    /// <param name="defaultValue">A value, that will be returned, if the variable is not defined.</param>
    /// <returns>A variable value or the default value.</returns>
    public IValue? Get(string variableName, IValue? defaultValue = default)
    {
        CheckVariableName(variableName);
        
        return Variables.ContainsKey(variableName)
            ? Variables[variableName]
            : defaultValue;
    }

    /// <summary>
    /// Sets a value to a variable. If no such variable exists, a new variable is created. If the value is null,
    /// the variable is deleted.
    /// </summary>
    /// <param name="variableName">A normalised variable name.</param>
    /// <param name="value">A value.</param>
    public void Set(string variableName, IValue? value)
    {
        CheckVariableName(variableName);

        IValue? previousValue = null;
        var variables = (Dictionary<string, IValue>)Variables;
        if (variables.ContainsKey(variableName))
        {
            previousValue = variables[variableName];

            variables.Remove(variableName);
        }

        // Variable removed or nothing happened for nonexisting variable set to null.
        if (value == null)
        {
            if (previousValue != null)
            {
                OnRaiseVariableRemovedEvent(new AppStateEventArgs(variableName)
                {
                    OldValue = previousValue
                });
            }

            return;
        }
            
        variables.Add(variableName, value);

        // Variable changed or added.
        if (previousValue == null)
        {
            OnRaiseVariableAddedEvent(new AppStateEventArgs(variableName)
            {
                NewValue = value
            });
        }
        else
        {
            OnRaiseVariableValueUpdatedEvent(new AppStateEventArgs(variableName)
            {
                OldValue = previousValue,
                NewValue = value
            });
        }
    }

    #endregion


    #region events

    /// <summary>
    /// An event raised when a new variable was added.
    /// The value of the nev variable is in the NewValue property of the event args returned.
    /// </summary>
    public event EventHandler<AppStateEventArgs>? VariableAdded;
    
    /// <summary>
    /// An event raised when a variable value was changed.
    /// The old value is returned in the OldValue properts and the new value is returned in the NewValue property of the event args returned.
    /// </summary>
    public event EventHandler<AppStateEventArgs>? VariableValueUpdated;
    
    /// <summary>
    /// An event raised when a variable value was set to null, which removes that variable.
    /// The value of the removed variable is returned in the OldValue of the event args returned.
    /// </summary>
    public event EventHandler<AppStateEventArgs>? VariableRemoved;


    protected virtual void OnRaiseVariableAddedEvent(AppStateEventArgs e)
    {
        // Make a temporary copy of the event to avoid possibility of
        // a race condition if the last subscriber unsubscribes
        // immediately after the null check and before the event is raised.
        var raiseEvent = VariableAdded;

        // Event will be null if there are no subscribers
        if (raiseEvent != null)
        {
            // Call to raise the event.
            raiseEvent(this, e);
        }
    }


    protected virtual void OnRaiseVariableValueUpdatedEvent(AppStateEventArgs e)
    {
        var raiseEvent = VariableValueUpdated;
        if (raiseEvent != null)
        {
            raiseEvent(this, e);
        }
    }


    protected virtual void OnRaiseVariableRemovedEvent(AppStateEventArgs e)
    {
        var raiseEvent = VariableRemoved;
        if (raiseEvent != null)
        {
            raiseEvent(this, e);
        }
    }

    #endregion


    #region serialize

    /// <summary>
    /// Converts this instance to JSON.
    /// </summary>
    /// <returns>JSON representation of this instance.</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(
            this,
            JsonSerializerOptions);
    }


    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new IValueJsonConverter()
        }
    };


    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to?pivots=dotnet-8-0
    public class IValueJsonConverter : JsonConverter<IValue>
    {
        public override IValue Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
            {
                throw new NotSupportedException();
            }

        public override void Write(
            Utf8JsonWriter writer,
            IValue value,
            JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.String);
            }
    }

    #endregion
}

    