﻿using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace BetterPawnControl
{
    public class Settings : ModSettings
    {
        public bool automaticPawnsInterrupt = true;

        public override void ExposeData()
        {
            Scribe_Values.Look<bool>(ref automaticPawnsInterrupt, "AutomaticPawnsInterrupt", true, true);
            base.ExposeData();
        }
    }

    public class BetterPawnControl : Mod
    {
        Settings settings;

        public BetterPawnControl(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<Settings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("BPC.AutomaticPawnsInterruptSetting".Translate(), ref settings.automaticPawnsInterrupt);
            listingStandard.End();
        }

        public override string SettingsCategory()
        {
            return "BPC.BetterPawnControl".Translate();
        }
    }
}