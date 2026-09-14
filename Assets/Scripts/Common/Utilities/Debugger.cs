using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class Debugger
{
    private static bool enableDebugLogs = true;
    public static bool EnableDebugLogs
    {
        get { return enableDebugLogs; }
        set { enableDebugLogs = value; }
    }
    public const string separator = " || ";

    public enum MessageType
    {
        Log,
        Warn,
        Error
    }

    public static void PrintMsg(string msg, MessageType msgType = MessageType.Log)
    {
        if (!enableDebugLogs) return;
        Print(msg, msgType);
    }

    public static void PrintVarMsg<T>(T variable, MessageType msgType = MessageType.Log, string variableName = null)
    {
        if (!enableDebugLogs) return;
        if (typeof(T) == typeof(string))
        {
            Print(variable.ToString(), msgType);
        }
        else
        {
            Print(variableName + separator + variable, msgType);
        }
    }

    public static void PrintMethodCall<T>(string methodName, T methodCall, string message = "", MessageType msgType = MessageType.Log)
    {
        if (!enableDebugLogs) return;
        Print(methodCall.GetType().Name + separator + methodName + separator + message, msgType);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GetCurrentMethodName()
    {
        var st = new System.Diagnostics.StackTrace(new System.Diagnostics.StackFrame(1));
        return st.GetFrame(0).GetMethod().Name;
    }

    public static void PrintIEnumMsg<T>(IEnumerable<T> iEnum, MessageType msgType = MessageType.Log, string variableName = null)
    {
        if (!enableDebugLogs) return;
        var res = "";
        foreach (var item in iEnum)
        {
            res = res + (item.ToString() + ", ");
        }
        Print(variableName + separator + res, msgType);
    }

    public static void PrintNotImplemented<T>(T methodCall, string methodName)
    {
        if (!enableDebugLogs) return;
        Print(methodCall.GetType().Name + separator + methodName + " not implemented", MessageType.Warn);
    }

    private static void Print(string msg, MessageType msgType)
    {
        switch (msgType)
        {
            case MessageType.Log:
                Debug.Log(msg);
                break;
            case MessageType.Warn:
                Debug.LogWarning(msg);
                break;
            case MessageType.Error:
                Debug.LogError(msg);
                break;
        }
    }
}
