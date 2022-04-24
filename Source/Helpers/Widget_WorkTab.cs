using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterPawnControl
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }

    [StaticConstructorOnStartup]
    public static class Widget_WorkTab
    {
        private const string WORKTAB = "Work Tab";
        private const string WORKTAB_UPDATE = "Work Tab 1.3 Update";
        private const string PAWN_EXTENSIONS = "WorkTab.Pawn_Extensions";
        private const string METHOD_GET_PRIORITIES = "GetPriorities";
        private const string METHOD_SET_PRIORITY = "SetPriority";

        // public + private + static + instance;
        private const BindingFlags BINDINGDLAGS_ALL = (BindingFlags)60; 

        private static bool _anyError = false;
        private static bool _initialized = false;
        private static bool _available = false;

        private static readonly Dictionary<WorkTypeDef, List<WorkGiverDef>> _workgiversByType =
            new Dictionary<WorkTypeDef, List<WorkGiverDef>>();
        private static Func<Pawn, WorkTypeDef, int[]> _GetPrioritiesWorkType;
        private static Func<Pawn, WorkGiverDef, int[]> _GetPrioritiesWorkGiver;
        private static Action<Pawn, WorkTypeDef, int, int, bool> _SetPriorityWorkType;
        private static Action<Pawn, WorkGiverDef, int, int, bool> _SetPriorityWorkGiver;

        public static bool WorkTabAvailable
        {
            get
            {
                if (!_initialized)
                    Initialize();
                return (_available && !_anyError);
            }
        }

        private static void Initialize()
        {
            _available = LoadedModManager.RunningMods.Any(mod => mod.Name == WORKTAB) ||
                         LoadedModManager.RunningMods.Any(mod => mod.Name == WORKTAB_UPDATE);

            _initialized = true;
            if (_available)
            {
                try
                {
                    //get the assembly
                    var asm = LoadedModManager
                                        .RunningMods.FirstOrDefault(mod => mod.Name == WORKTAB)?
                                        .assemblies.loadedAssemblies.Last();
                    if (asm == null)
                    {
                        asm = LoadedModManager
                            .RunningMods.FirstOrDefault(mod => mod.Name == WORKTAB_UPDATE)?
                            .assemblies.loadedAssemblies.Last();
                    }

                    if (asm == null)
                    {
                        throw new Exception(
                            "[BPC] Work Tab assembly not found.");
                    }

                    var Pawn_Extensions = asm.GetType(PAWN_EXTENSIONS);
                    if (Pawn_Extensions == null)
                    {
                        throw new Exception(
                            "[BPC] Work Tab type not found: " +
                            Pawn_Extensions);
                    }

                    var getPrioritiesWorkType = Pawn_Extensions.GetMethod(
                        METHOD_GET_PRIORITIES, new[] { typeof(Pawn), typeof(WorkTypeDef) });
                    if (getPrioritiesWorkType == null)
                    {
                        throw new Exception(
                            "[BPC] Work Tab method not found: " +
                            METHOD_GET_PRIORITIES);
                    }
                    _GetPrioritiesWorkType = (Func<Pawn, WorkTypeDef, int[]>)Delegate.CreateDelegate(typeof(Func<Pawn, WorkTypeDef, int[]>), getPrioritiesWorkType);

                    var getPrioritiesWorkGiver = Pawn_Extensions.GetMethod(
                        METHOD_GET_PRIORITIES, new[] { typeof(Pawn), typeof(WorkGiverDef) });
                    if (getPrioritiesWorkGiver == null)
                    {
                        throw new Exception(
                            "[BPC] Work Tab method not found: " +
                            METHOD_GET_PRIORITIES);
                    }
                    _GetPrioritiesWorkGiver = (Func<Pawn, WorkGiverDef, int[]>)Delegate.CreateDelegate(typeof(Func<Pawn, WorkGiverDef, int[]>), getPrioritiesWorkGiver);
                    
                    var setPriorityWorkType = Pawn_Extensions.GetMethod(
                        METHOD_SET_PRIORITY, new[] { typeof(Pawn), typeof(WorkTypeDef), typeof(int), typeof(int), typeof(bool) });
                    if (setPriorityWorkType == null)
                    {
                        throw new Exception(
                            "[BPC] Work Tab method not found: " +
                            METHOD_SET_PRIORITY);
                    }
                    _SetPriorityWorkType = (Action<Pawn, WorkTypeDef, int, int, bool>)Delegate.CreateDelegate(typeof(Action<Pawn, WorkTypeDef, int, int, bool>), setPriorityWorkType);

                    var setPriorityWorkGiver = Pawn_Extensions.GetMethod(
                        METHOD_SET_PRIORITY, new[] { typeof(Pawn), typeof(WorkGiverDef), typeof(int), typeof(int), typeof(bool) });
                    if (setPriorityWorkGiver == null)
                    {
                        throw new Exception(
                            "[BPC] Work Tab method not found: " +
                            METHOD_SET_PRIORITY);
                    }
                    _SetPriorityWorkGiver = (Action<Pawn, WorkGiverDef, int, int, bool>)Delegate.CreateDelegate(typeof(Action<Pawn, WorkGiverDef, int, int, bool>), setPriorityWorkGiver);

                    Log.Message("[BPC] Work Tab functionality integrated");
                }
                catch
                {
                    _anyError = true;
                    Log.Error("[BPC] Error in Work Tab integration - functionality disabled");
                    throw;
                }
            }
        }

        public static int[] GetPriorities(Pawn pawn, WorkTypeDef worktype)
        {
            return (int[])_GetPrioritiesWorkType(pawn, worktype).Clone();
        }

        public static int[] GetPriorities(Pawn pawn, WorkGiverDef workgiver)
        {
            return (int[])_GetPrioritiesWorkGiver(pawn, workgiver).Clone();
        }

        public static void SetPriority(Pawn pawn, WorkTypeDef worktype, int priority, int hour, bool recache = true)
        {
            _SetPriorityWorkType(pawn, worktype, priority, hour, recache);
        }

        public static void SetPriority(Pawn pawn, WorkGiverDef workgiver, int priority, int hour, bool recache = true)
        {
            _SetPriorityWorkGiver(pawn, workgiver, priority, hour, recache);
        }

        public static List<WorkGiverDef> WorkGivers(this WorkTypeDef worktype)
        {
            List<WorkGiverDef> result;
            if (!_workgiversByType.TryGetValue(worktype, out result))
            {
                result = DefDatabase<WorkGiverDef>
                    .AllDefsListForReading.Where(wg => wg.workType == worktype).ToList();
                _workgiversByType[worktype] = result;
            }

            return result;
        }
    }
}