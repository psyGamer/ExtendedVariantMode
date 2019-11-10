﻿using System;
using System.Collections.Generic;
using Monocle;
using FMOD.Studio;
using System.Collections;
using Celeste.Mod;
using ExtendedVariants.UI;
using Celeste;
using ExtendedVariants.Variants;

namespace ExtendedVariants.Module {
    public class ExtendedVariantsModule : EverestModule {

        public static ExtendedVariantsModule Instance;

        private bool stuffIsHooked = false;
        private bool triggerIsHooked = false;
        private bool variantsWereForceEnabled = false;
        private Postcard forceEnabledVariantsPostcard;

        public override Type SettingsType => typeof(ExtendedVariantsSettings);
        public override Type SessionType => typeof(ExtendedVariantsSession);

        public static ExtendedVariantsSettings Settings => (ExtendedVariantsSettings)Instance._Settings;
        public static ExtendedVariantsSession Session => (ExtendedVariantsSession)Instance._Session;

        public VariantRandomizer Randomizer;

        public enum Variant {
            Gravity, FallSpeed, JumpHeight, WallBouncingSpeed, DisableWallJumping, JumpCount, RefillJumpsOnDashRefill, DashSpeed, DashLength,
            HyperdashSpeed, DashCount, HeldDash, DontRefillDashOnGround, SpeedX, Friction, AirFriction, BadelineChasersEverywhere, ChaserCount,
            AffectExistingChasers, BadelineLag, OshiroEverywhere, DisableOshiroSlowdown, WindEverywhere, SnowballsEverywhere, SnowballDelay, AddSeekers,
            DisableSeekerSlowdown, TheoCrystalsEverywhere, Stamina, UpsideDown, DisableNeutralJumping, RegularHiccups, HiccupStrength, RoomLighting,
            RoomBloom, ForceDuckOnGround, InvertDashes, AllStrawberriesAreGoldens, GameSpeed
        }

        public Dictionary<Variant, AbstractExtendedVariant> VariantHandlers = new Dictionary<Variant, AbstractExtendedVariant>();

        public ExtendedVariantTriggerManager TriggerManager;

        // ================ Module loading ================

        public ExtendedVariantsModule() {
            Instance = this;
            Randomizer = new VariantRandomizer();
            TriggerManager = new ExtendedVariantTriggerManager();

            DashCount dashCount;
            VariantHandlers[Variant.Gravity] = new Gravity();
            VariantHandlers[Variant.FallSpeed] = new FallSpeed();
            VariantHandlers[Variant.JumpHeight] = new JumpHeight();
            VariantHandlers[Variant.SpeedX] = new SpeedX();
            VariantHandlers[Variant.Stamina] = new Stamina();
            VariantHandlers[Variant.DashSpeed] = new DashSpeed();
            VariantHandlers[Variant.DashCount] = (dashCount = new DashCount());
            VariantHandlers[Variant.HeldDash] = new HeldDash();
            VariantHandlers[Variant.Friction] = new Friction();
            VariantHandlers[Variant.AirFriction] = new AirFriction();
            VariantHandlers[Variant.DisableWallJumping] = new DisableWallJumping();
            VariantHandlers[Variant.JumpCount] = new JumpCount(dashCount);
            VariantHandlers[Variant.UpsideDown] = new UpsideDown();
            VariantHandlers[Variant.HyperdashSpeed] = new HyperdashSpeed();
            VariantHandlers[Variant.WallBouncingSpeed] = new WallbouncingSpeed();
            VariantHandlers[Variant.DashLength] = new DashLength();
            VariantHandlers[Variant.ForceDuckOnGround] = new ForceDuckOnGround();
            VariantHandlers[Variant.InvertDashes] = new InvertDashes();
            VariantHandlers[Variant.DisableNeutralJumping] = new DisableNeutralJumping();
            VariantHandlers[Variant.BadelineChasersEverywhere] = new BadelineChasersEverywhere();
            // ChaserCount is not a variant
            // AffectExistingChasers is not a variant
            VariantHandlers[Variant.RegularHiccups] = new RegularHiccups();
            // HiccupStrength is not a variant
            // RefillJumpsOnDashRefill is not a variant
            VariantHandlers[Variant.RoomLighting] = new RoomLighting();
            VariantHandlers[Variant.RoomBloom] = new RoomBloom();
            VariantHandlers[Variant.OshiroEverywhere] = new OshiroEverywhere();
            // DisableOshiroSlowdown is not a variant
            VariantHandlers[Variant.WindEverywhere] = new WindEverywhere();
            VariantHandlers[Variant.SnowballsEverywhere] = new SnowballsEverywhere();
            // SnowballDelay is not a variant
            VariantHandlers[Variant.AddSeekers] = new AddSeekers();
            // DisableSeekerSlowdown is not a variant
            VariantHandlers[Variant.TheoCrystalsEverywhere] = new TheoCrystalsEverywhere();
            // BadelineLag is not a variant
            VariantHandlers[Variant.AllStrawberriesAreGoldens] = new AllStrawberriesAreGoldens();
            VariantHandlers[Variant.DontRefillDashOnGround] = new DontRefillDashOnGround();
            VariantHandlers[Variant.GameSpeed] = new GameSpeed();
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
            base.CreateModMenuSection(menu, inGame, snapshot);

            new ModOptionsEntries().CreateAllOptions(menu, inGame, triggerIsHooked);
        }

        // ================ Variants hooking / unhooking ================

        public override void Load() {
            Logger.Log("ExtendedVariantsModule", "Initializing Extended Variant Mode");

            On.Celeste.LevelEnter.Go += checkForceEnableVariants;
            On.Celeste.LevelExit.ctor += checkForTriggerUnhooking;

            if (Settings.MasterSwitch) {
                // variants are enabled: we want to hook them on startup.
                HookStuff();
            }
        }

        public override void Unload() {
            Logger.Log("ExtendedVariantsModule", "Unloading Extended Variant Mode");

            On.Celeste.LevelEnter.Go -= checkForceEnableVariants;
            On.Celeste.LevelExit.ctor -= checkForTriggerUnhooking;

            if (stuffIsHooked) {
                UnhookStuff();
            }
        }

        public void HookStuff() {
            if (stuffIsHooked) return;

            Logger.Log("ExtendedVariantsModule", $"Loading variant common methods...");
            On.Celeste.AreaComplete.VersionNumberAndVariants += modVersionNumberAndVariants;
            Everest.Events.Level.OnExit += onLevelExit;
            On.Celeste.BadelineBoost.BoostRoutine += modBadelineBoostRoutine;
            On.Celeste.CS00_Ending.OnBegin += onPrologueEndingCutsceneBegin;

            Logger.Log("ExtendedVariantsModule", $"Loading variant randomizer...");
            Randomizer.Load();

            foreach(Variant variant in VariantHandlers.Keys) {
                Logger.Log("ExtendedVariantsModule", $"Loading variant {variant}...");
                VariantHandlers[variant].Load();
            }

            Logger.Log("ExtendedVariantsModule", "Done hooking stuff.");

            stuffIsHooked = true;
        }

        public void UnhookStuff() {
            if (!stuffIsHooked) return;

            Logger.Log("ExtendedVariantsModule", $"Unloading variant common methods...");
            On.Celeste.AreaComplete.VersionNumberAndVariants -= modVersionNumberAndVariants;
            Everest.Events.Level.OnExit -= onLevelExit;
            On.Celeste.BadelineBoost.BoostRoutine -= modBadelineBoostRoutine;
            On.Celeste.CS00_Ending.OnBegin -= onPrologueEndingCutsceneBegin;

            // unset flags
            onLevelExit();

            Logger.Log("ExtendedVariantsModule", $"Unloading variant randomizer...");
            Randomizer.Unload();
            
            foreach(Variant variant in VariantHandlers.Keys) {
                Logger.Log("ExtendedVariantsModule", $"Unloading variant {variant}...");
                VariantHandlers[variant].Unload();
            }

            Logger.Log("ExtendedVariantsModule", "Done unhooking stuff.");

            stuffIsHooked = false;
        }

        private void hookTrigger() {
            if (triggerIsHooked) return;

            Logger.Log("ExtendedVariantsModule", $"Loading variant trigger manager...");
            TriggerManager.Load();

            On.Celeste.LevelEnter.Routine += addForceEnabledVariantsPostcard;
            On.Celeste.LevelEnter.BeforeRender += addForceEnabledVariantsPostcardRendering;

            Logger.Log("ExtendedVariantsModule", $"Done loading variant trigger manager.");

            triggerIsHooked = true;
        }

        private void unhookTrigger() {
            if (!triggerIsHooked) return;

            Logger.Log("ExtendedVariantsModule", $"Unloading variant trigger manager...");
            TriggerManager.Unload();

            On.Celeste.LevelEnter.Routine -= addForceEnabledVariantsPostcard;
            On.Celeste.LevelEnter.BeforeRender -= addForceEnabledVariantsPostcardRendering;

            Logger.Log("ExtendedVariantsModule", $"Done unloading variant trigger manager.");

            triggerIsHooked = false;
        }
        
        private void checkForceEnableVariants(On.Celeste.LevelEnter.orig_Go orig, Session session, bool fromSaveData) {
            if(session.MapData.Levels.Exists(levelData => levelData.Triggers.Exists(entityData => entityData.Name == "ExtendedVariantTrigger"))) {
                // the level we're entering has an Extended Variant Trigger: load the trigger on-demand.
                hookTrigger();

                // if variants are disabled, we want to enable them as well, with default values
                // (so that we don't get variants that were enabled long ago).
                if(!stuffIsHooked) {
                    variantsWereForceEnabled = true;
                    Settings.MasterSwitch = true;
                    ResetToDefaultSettings();
                    HookStuff();
                    SaveSettings();
                }
            }

            orig(session, fromSaveData);
        }

        private IEnumerator addForceEnabledVariantsPostcard(On.Celeste.LevelEnter.orig_Routine orig, LevelEnter self) {
            if(variantsWereForceEnabled) {
                variantsWereForceEnabled = false;

                // let's show a postcard to let the player know Extended Variants have been enabled.
                self.Add(forceEnabledVariantsPostcard = new Postcard(Dialog.Get("POSTCARD_EXTENDEDVARIANTS_FORCEENABLED"), "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out"));
                yield return forceEnabledVariantsPostcard.DisplayRoutine();
                forceEnabledVariantsPostcard = null;
            }

            // just go on with vanilla behavior (other postcards, B-side intro, etc)
            yield return orig(self);
        }

        private void addForceEnabledVariantsPostcardRendering(On.Celeste.LevelEnter.orig_BeforeRender orig, LevelEnter self) {
            orig(self);

            if (forceEnabledVariantsPostcard != null) forceEnabledVariantsPostcard.BeforeRender();
        }

        private void checkForTriggerUnhooking(On.Celeste.LevelExit.orig_ctor orig, LevelExit self, LevelExit.Mode mode, Session session, HiresSnow snow) {
            orig(self, mode, session, snow);

            if (triggerIsHooked) {
                // we want to get rid of the trigger now.
                unhookTrigger();
            }
        }

        public void ResetToDefaultSettings() {
            if(Settings.RoomLighting != -1 && Engine.Scene.GetType() == typeof(Level)) {
                // currently in level, change lighting right away
                Level lvl = (Engine.Scene as Level);
                lvl.Lighting.Alpha = lvl.BaseLightingAlpha + lvl.Session.LightingAlphaAdd;
            }
            if(Settings.RoomBloom != -1 && Engine.Scene.GetType() == typeof(Level)) {
                // currently in level, change bloom right away
                Level lvl = (Engine.Scene as Level);
                lvl.Bloom.Base = AreaData.Get(lvl).BloomBase + lvl.Session.BloomBaseAdd;
            }
            
            // reset all proper variants to their default values
            foreach(AbstractExtendedVariant variant in VariantHandlers.Values) {
                variant.SetValue(variant.GetDefaultValue());
            }

            // reset variant customization options as well
            Settings.ChaserCount = 1;
            Settings.AffectExistingChasers = false;
            Settings.HiccupStrength = 10;
            Settings.RefillJumpsOnDashRefill = false;
            Settings.SnowballDelay = 8;
            Settings.BadelineLag = 0;
            Settings.ChangeVariantsRandomly = false;
            Settings.DisableOshiroSlowdown = false;
            Settings.DisableSeekerSlowdown = false;
        }
        
        // ================ Stamp on Chapter Complete screen ================

        /// <summary>
        /// Wraps the VersionNumberAndVariants in the base game in order to add the Variant Mode logo if Extended Variants are enabled.
        /// </summary>
        private void modVersionNumberAndVariants(On.Celeste.AreaComplete.orig_VersionNumberAndVariants orig, string version, float ease, float alpha) {
            if(Settings.MasterSwitch) {
                // The "if" conditioning the display of the Variant Mode logo is in an "orig_" method, we can't access it with IL.Celeste.
                // The best we can do is turn on Variant Mode, run the method then restore its original value.
                bool oldVariantModeValue = SaveData.Instance.VariantMode;
                SaveData.Instance.VariantMode = true;

                orig.Invoke(version, ease, alpha);

                SaveData.Instance.VariantMode = oldVariantModeValue;
            } else {
                // Extended Variants are disabled so just keep the original behaviour
                orig.Invoke(version, ease, alpha);
            }
        }

        // ================ Common methods for multiple variants ================

        private static bool badelineBoosting = false;
        private static bool prologueEndingCutscene = false;

        public static bool ShouldIgnoreCustomDelaySettings() {
            if (Engine.Scene.GetType() == typeof(Level)) {
                Player player = (Engine.Scene as Level).Tracker.GetEntity<Player>();
                // those states are "Intro Walk", "Intro Jump" and "Intro Wake Up". Getting killed during such an intro is annoying but can also **crash the game**
                if (player != null && (player.StateMachine.State == 12 || player.StateMachine.State == 13 || player.StateMachine.State == 15)) {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Generates a new EntityData instance, linked to the level given, an ID which will be the same if and only if generated
        /// in the same room with the same entityNumber, and an empty map of attributes.
        /// </summary>
        /// <param name="level">The level the entity belongs to</param>
        /// <param name="entityNumber">An entity number, between 0 and 19 inclusive</param>
        /// <returns>A fresh EntityData linked to the level, and with an ID</returns>
        public static EntityData GenerateBasicEntityData(Level level, int entityNumber) {
            EntityData entityData = new EntityData();

            // we hash the current level name, so we will get a hopefully-unique "room hash" for each room in the level
            // the resulting hash should be between 0 and 49_999_999 inclusive
            int roomHash = Math.Abs(level.Session.Level.GetHashCode()) % 50_000_000;

            // generate an ID, minimum 1_000_000_000 (to minimize chances of conflicting with existing entities)
            // and maximum 1_999_999_999 inclusive (1_000_000_000 + 49_999_999 * 20 + 19) => max value for int32 is 2_147_483_647
            // => if the same entity (same entityNumber) is generated in the same room, it will have the same ID, like any other entity would
            entityData.ID = 1_000_000_000 + roomHash * 20 + entityNumber;

            entityData.Level = level.Session.LevelData;
            entityData.Values = new Dictionary<string, object>();

            return entityData;
        }

        public static bool ShouldEntitiesAutoDestroy(Player player) {
            return (player != null && (player.StateMachine.State == 10 || player.StateMachine.State == 11) && !badelineBoosting)
                || prologueEndingCutscene // this kills Oshiro, that prevents the Prologue ending cutscene from even triggering.
                || !Instance.stuffIsHooked; // this makes all the mess instant vanish when Extended Variants are disabled entirely.
        }

        private IEnumerator modBadelineBoostRoutine(On.Celeste.BadelineBoost.orig_BoostRoutine orig, BadelineBoost self, Player player) {
            badelineBoosting = true;
            IEnumerator coroutine = orig(self, player);
            while (coroutine.MoveNext()) {
                yield return coroutine.Current;
            }
            badelineBoosting = false;
        }
        
        private void onLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            onLevelExit();
        }

        private void onLevelExit() {
            badelineBoosting = false;
            prologueEndingCutscene = false;
        }

        private void onPrologueEndingCutsceneBegin(On.Celeste.CS00_Ending.orig_OnBegin orig, CS00_Ending self, Level level) {
            orig(self, level);

            prologueEndingCutscene = true;
        }
    }
}