using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace BetterPawnControl
{
    public class WorkPriority : IExposable
    {
        private WorkGiverDef workgiver;

        // Scribe
        public WorkPriority()
        {
        }

        public WorkPriority(WorkGiverDef workgiver, int[] priorities)
        {
            this.workgiver = workgiver;
            if (priorities.Length != GenDate.HoursPerDay)
                throw new ArgumentException();
            Priorities = priorities;
        }

        public int[] Priorities { get; private set; }

        public WorkGiverDef Workgiver => workgiver;

        public void ExposeData()
        {
            try
            {
                Scribe_Defs.Look(ref workgiver, "Workgiver");
            }
            catch (Exception e)
            {
                Log.Warning(
                    "[BPC] WorkTab :: failed to load priorities. Did you disable a mod? If so, this message can safely be ignored." +
                    e.Message +
                    "\n\n" +
                    e.StackTrace);
            }

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var _priorities = string.Join("", Priorities.Select(i => i.ToString()).ToArray());
                Scribe_Values.Look(ref _priorities, "Priorities");
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var _priorities = "";
                Scribe_Values.Look(ref _priorities, "Priorities");
                Priorities = _priorities.ToArray().Select(c => int.Parse(c.ToString())).ToArray();
            }
        }

        public WorkPriority Clone()
        {
            var clone = new WorkPriority()
            {
                Priorities = (int[])Priorities.Clone(),
                workgiver = workgiver
            };
            return clone;
        }
    }


    public class WorkLink : Link, IExposable
    {
        //internal int zone = 0;
        internal Pawn colonist = null;
        internal List<WorkPriority> settings =  null;
        //internal int mapId = 0;

        public WorkLink() { }

        public WorkLink(WorkLink link)
        {
            this.zone = link.zone;
            this.colonist = link.colonist;
            this.settings = link.settings.Select(x => x.Clone()).ToList();
            this.mapId = link.mapId;
        }

        public WorkLink(
            int zone, Pawn colonist, List<WorkPriority> settings, int mapId)
        {
            this.zone = zone;
            this.colonist = colonist;
            this.settings = settings;
            this.mapId = mapId;
        }

        public override string ToString()
        {
            return
                "Policy:" + zone +
                "  Pawn: " + colonist +
                //"  WorkSettings: " + settings +
                "  MapID: " + mapId;
        }

        /// <summary>
        /// Data for saving/loading
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look<int>(ref zone, "zone", 0, true);
            Scribe_References.Look<Pawn>(ref colonist, "colonist");
            Scribe_Values.Look<int>(ref mapId, "mapId", 0, true);

            Scribe_Collections.Look(ref settings, "Priorities", LookMode.Deep/*, this*/);
        }
    }
}
