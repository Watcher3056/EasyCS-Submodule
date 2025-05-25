using System;
using UnityEngine;

namespace EasyCS
{
    public class ProcessorDebug
    {

        public static void Log(object source, string message)
        {
            if (!Debug.isDebugBuild)
                return;

            Debug.Log(FormatLog(source, message));
        }
        
        public static void LogWarning(object source, string message)
        {
            if (!Debug.isDebugBuild)
                return;

            Debug.LogWarning(FormatLog(source, message));
        }
        
        public static void LogError(object source, string message)
        {
            if (!Debug.isDebugBuild)
                return;

            Debug.LogError(FormatLog(source, message));
        }

        private static string FormatLog(object source, string message)
        {
            string resultLog = DateTime.Now + " ::: " + source + " ::: " + message;
            return resultLog;
        }
    }
    public static partial class Extensions
    {
        public static void Log(this object source, string message)
        {
            ProcessorDebug.Log(source, message);
        }
        
        public static void LogWarning(this object source, string message)
        {
            ProcessorDebug.LogWarning(source, message);
        }
        
        public static void LogError(this object source, string message)
        {
            ProcessorDebug.LogError(source, message);
        }
    }
}
