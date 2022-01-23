﻿using Celeste.Mod;
using MonoMod.Cil;
using System;
using Monocle;

namespace ExtendedVariants.Variants {
    public class NoFreezeFrames : AbstractExtendedVariant {

        public override Type GetVariantType() {
            return typeof(bool);
        }

        public override object GetDefaultVariantValue() {
            return false;
        }

        public override object GetVariantValue() {
            return Settings.NoFreezeFrames;
        }

        protected override void DoSetVariantValue(object value) {
            Settings.NoFreezeFrames = (bool) value;
        }

        public override void SetLegacyVariantValue(int value) {
            Settings.NoFreezeFrames = (value != 0);
        }

        public override void Load() {
            IL.Monocle.Engine.Update += modEngineUpdate;
            On.Celeste.Celeste.Freeze += onCelesteFreeze;
        }

        public override void Unload() {
            IL.Monocle.Engine.Update -= modEngineUpdate;
            On.Celeste.Celeste.Freeze -= onCelesteFreeze;
        }

        private void onCelesteFreeze(On.Celeste.Celeste.orig_Freeze orig, float time) {
            if (Settings.NoFreezeFrames) {
                return;
            }
            orig(time);
        }

        private void modEngineUpdate(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext(MoveType.Before,
                instr => instr.MatchLdsfld(typeof(Engine), nameof(Engine.FreezeTimer)))
            ) {
                Logger.Log("ExtendedVariantMode/NoFreezeFrames", $"Modding no freeze frames at index {cursor.Index} in CIL code for {cursor.Method.Name}");

                cursor.EmitDelegate<Action>(() => {
                    if (Settings.NoFreezeFrames) {
                        Engine.FreezeTimer = 0f;
                    }
                });
            }
        }
    }
}