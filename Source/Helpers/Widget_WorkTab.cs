using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterPawnControl
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class HotSwappableAttribute : Attribute
    {
    }

    public class PrioritiesReflected
    {
        // FastAccess
        delegate bool FnTryGetValue(Pawn pawn, out object value);

        private AccessTools.FieldRef<object, object> PriorityManager_priorities;
        private Func<Pawn, bool> PriorityManager_priorities_Remove;
        private FnTryGetValue PriorityManager_priorities_TryGetValue;
        private Action<Pawn, object> PriorityManager_priorities_Add;
        private Func<Pawn, object> PawnPriorityTracker_Ctor;
        private Func<object, WorkGiverDef, int[]> PriorityTracker_GetPriorities;
        private Action<object, WorkGiverDef, int, int, bool> PriorityTracker_SetPriority;
        public Func<WorkTypeDef, List<WorkGiverDef>> WorkType_Extensions_WorkGivers;

        // Create delegate from Action<Pawn, PawnPriorityTracker> => Action<Pawn, object>
        private static Action<T, object> DictionaryAdd_Wrapper<T, U>(object firstArg, MethodInfo method)
        {
            var f = (Action<T, U>)Delegate.CreateDelegate(typeof(Action<T, U>), firstArg, method);
            return (k, v) => f(k, (U)v);
        }

        // Func<object, WorkGiverDef, int[]> -> Func<PriorityTracker, WorkGiverDef, int[]>
        private static Func<object, WorkGiverDef, int[]> PriorityTrackerGetPriorities_Wrapper<T>(MethodInfo method)
        {
            var f = (Func<T, WorkGiverDef, int[]>)Delegate.CreateDelegate(typeof(Func<T, WorkGiverDef, int[]>), method);
            return (t, g) => f((T)t, g);
        }

        private static Action<object, WorkGiverDef, int, int, bool> PriorityTrackerSetPriority_Wrapper<T>(MethodInfo method)
        {
            var f = (Action<T, WorkGiverDef, int, int, bool>)Delegate.CreateDelegate(typeof(Action<T, WorkGiverDef, int, int, bool>), method);
            return (tracker, giver, priority, hour, recache) => f((T)tracker, giver, priority, hour, recache);
        }

        private ReturnType CallWrapper<ReturnType>(string methodName, Type[] genericArgs, params object[] args)
        {
            var wrapper = typeof(PrioritiesReflected).GetMethod(methodName, AccessTools.all);
            var generic = wrapper.MakeGenericMethod(genericArgs);
            return (ReturnType)generic.Invoke(null, args);
        }

        private static Func<T, object> DelegateFromCtor1Arg<T>(Type type)
        {
            var args = new Type[] { typeof(T) };
            var ctor = type.GetConstructor(args);
            if (ctor == null)
                throw new MissingMethodException("There is no constructor without defined parameters for this object");
            DynamicMethod dynamic = new DynamicMethod(string.Empty, type, args, type);
            ILGenerator il = dynamic.GetILGenerator();
            il.DeclareLocal(type);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);
            return (Func<T, object>)dynamic.CreateDelegate(typeof(Func<T, object>));
        }

        private Type TypeOrThrow(string type)
        {
            var result = AccessTools.TypeByName(type);
            return result ?? throw new NullReferenceException($"[BPC] Type not found: {type}");
        }

        private MethodInfo MethodOrThrow(Type type, string methodName, Type[] argTypes = null)
        {
            var result = AccessTools.Method(type, methodName, argTypes);
            return result ?? throw new NullReferenceException($"[BPC] Method not found: {type.Name}:{methodName}");
        }

        private FieldInfo FieldOrThrow(Type type, string fieldName)
        {
            var result = AccessTools.Field(type, fieldName);
            return result ?? throw new NullReferenceException($"[BPC] Field not found: {type.Name}:{fieldName}");
        }

        public bool Init()
        {
            try
            {
                /*** Types ***/
                var TPriorityTracker = TypeOrThrow("WorkTab.PriorityTracker");
                var TPriorityManager = TypeOrThrow("WorkTab.PriorityManager");
                var TPawnPriorityTracker = TypeOrThrow("WorkTab.PawnPriorityTracker");
                var TWorkType_Extensions = TypeOrThrow("WorkTab.WorkType_Extensions");

                /*** Fields ***/
                var Fpriorities = FieldOrThrow(TPriorityManager, "priorities");
                PriorityManager_priorities = AccessTools.FieldRefAccess<object>(TPriorityManager, "priorities");

                /*** Methods ***/
                // WorkTab.PriorityManager: Dictionary<Pawn, PawnPriorityTracker> priorities
                var TDictionary_Pawn_PawnPriorityTracker = typeof(Dictionary<,>).MakeGenericType(typeof(Pawn), TPawnPriorityTracker);

                // priorities.Remove
                var MRemove = MethodOrThrow(TDictionary_Pawn_PawnPriorityTracker, "Remove", new[] { typeof(Pawn) });
                PriorityManager_priorities_Remove = (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), PriorityManager_priorities(), MRemove);

                // priorities.TryGetValue
                var MTryGetValue = MethodOrThrow(TDictionary_Pawn_PawnPriorityTracker, "TryGetValue");
                PriorityManager_priorities_TryGetValue = (FnTryGetValue)Delegate.CreateDelegate(typeof(FnTryGetValue), PriorityManager_priorities(), MTryGetValue);

                // priorities.Add
                //   Exception: System.ArgumentException: method arguments are incompatible
                //     valid signature: Action<Pawn, PawnPriorityTracker>
                //     FMAdd = (Action<Pawn, object>)Delegate.CreateDelegate(typeof(Action<Pawn, object>), FFpriorities(), MAdd);
                var MAdd = MethodOrThrow(TDictionary_Pawn_PawnPriorityTracker, "Add");
                PriorityManager_priorities_Add = CallWrapper<Action<Pawn, object>>(nameof(DictionaryAdd_Wrapper), new[] { typeof(Pawn), TPawnPriorityTracker }, PriorityManager_priorities(), MAdd);

                var getPriorities = MethodOrThrow(TPriorityTracker, "GetPriorities", new Type[] { typeof(WorkGiverDef) });
                PriorityTracker_GetPriorities = CallWrapper<Func<object, WorkGiverDef, int[]>>(nameof(PriorityTrackerGetPriorities_Wrapper), new[] { TPriorityTracker }, getPriorities);

                var setPriority = MethodOrThrow(TPriorityTracker, "SetPriority", new Type[] { typeof(WorkGiverDef), typeof(int), typeof(int), typeof(bool) });
                PriorityTracker_SetPriority = CallWrapper<Action<object, WorkGiverDef, int, int, bool>>(nameof(PriorityTrackerSetPriority_Wrapper), new[] { TPriorityTracker }, setPriority);

                var workGivers = MethodOrThrow(TWorkType_Extensions, "WorkGivers");
                WorkType_Extensions_WorkGivers = (Func<WorkTypeDef, List<WorkGiverDef>>)Delegate.CreateDelegate(typeof(Func<WorkTypeDef, List<WorkGiverDef>>), workGivers);

                PawnPriorityTracker_Ctor = DelegateFromCtor1Arg<Pawn>(TPawnPriorityTracker);
            }
            catch (Exception e)
            {
                Log.Error($"{e}");
                return false;
            }

            return true;
        }

        public object GetPriorityTrackerNotFavourite(Pawn pawn)
        {
            //var favourite = FavouriteManager.Get[pawn];
            //if (favourite != null ) return favourite;

            //if (PriorityManager.priorities.TryGetValue(pawn, out var tracker))
            // FMRemove(pawn); //DEBUG
            if (PriorityManager_priorities_TryGetValue(pawn, out object tracker))
                return tracker;

            // tracker = new PawnPriorityTracker(pawn);
            tracker = PawnPriorityTracker_Ctor(pawn);

            // PriorityManager.priorities.Add(pawn, tracker);
            PriorityManager_priorities_Add(pawn, tracker);
            return tracker;
        }

        public int[] GetPriorities(Pawn pawn, WorkGiverDef workgiver)
        {
            return PriorityTracker_GetPriorities(GetPriorityTrackerNotFavourite(pawn), workgiver);
        }

        public void SetPriority(Pawn pawn, WorkGiverDef workgiver, int priority, int hour, bool recache = true)
        {
            PriorityTracker_SetPriority(GetPriorityTrackerNotFavourite(pawn), workgiver, priority, hour, recache);
        }

        public void SetPriority(Pawn pawn, WorkGiverDef workgiver, int[] priorities, bool recache = true)
        {
            var tracker = GetPriorityTrackerNotFavourite(pawn);
            for (int i = 0; i < 24; i++)
                PriorityTracker_SetPriority(tracker, workgiver, priorities[i], i, recache);
        }
    }


    [StaticConstructorOnStartup]
    public static class Widget_WorkTab
    {
        private const string WORKTAB = "Work Tab";
        private const string WORKTAB_UPDATE = "Work Tab 1.3 Update";

        private static bool _anyError = false;
        private static bool _initialized = false;
        private static bool _available = false;

        private static PrioritiesReflected _prioritiesReflected;

        public static void Reset()
        {
            // re-initalize all wrappers, after create new game or load save
            // because after ExposeData PriorityManager.priorities has new instance!!!
            _initialized = false;
        }

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
                _prioritiesReflected = new PrioritiesReflected();
                _anyError = !_prioritiesReflected.Init();
                if (_anyError)
                    Log.Error("[BPC] Error in Work Tab integration - functionality disabled");
            }
        }

        public static int[] GetPriorities(Pawn pawn, WorkGiverDef workgiver)
        {
            return (int[])_prioritiesReflected.GetPriorities(pawn, workgiver).Clone();
        }

        public static void SetPriority(Pawn pawn, WorkGiverDef workgiver, int priority, int hour, bool recache = true)
        {
            _prioritiesReflected.SetPriority(pawn, workgiver, priority, hour, recache);
        }

        public static void SetPriority(Pawn pawn, WorkGiverDef workgiver, int[] priorities, bool recache = true)
        {
            _prioritiesReflected.SetPriority(pawn, workgiver, priorities, recache);
        }

        public static List<WorkGiverDef> WorkGivers(this WorkTypeDef worktype)
        {
            return _prioritiesReflected.WorkType_Extensions_WorkGivers(worktype);
        }
    }
}