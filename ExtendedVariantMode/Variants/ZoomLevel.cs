﻿using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Cil;
using System;

namespace ExtendedVariants.Variants {
    public class ZoomLevel : AbstractExtendedVariant {
        private Vector2 previousDiff;
        private float transitionPercent = 1f;

        public override int GetDefaultValue() {
            return 10;
        }

        public override int GetValue() {
            return Settings.ZoomLevel;
        }

        public override void SetValue(int value) {
            Settings.ZoomLevel = value;
        }

        public override void Load() {
            On.Celeste.Player.ctor += onPlayerConstructor;
            IL.Celeste.Level.Render += modLevelRender;
        }

        public override void Unload() {
            On.Celeste.Player.ctor -= onPlayerConstructor;
            IL.Celeste.Level.Render -= modLevelRender;
        }

        private void onPlayerConstructor(On.Celeste.Player.orig_ctor orig, Player self, Vector2 position, PlayerSpriteMode spriteMode) {
            orig(self, position, spriteMode);

            // make the player spy on transitions
            self.Add(new TransitionListener {
                OnOutBegin = () => transitionPercent = 0f,
                OnOut = percent => transitionPercent = percent
            });
        }

        private void modLevelRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<Level>("Zoom"))) {
                Logger.Log("ExtendedVariantMode/ZoomLevel", $"Modding zoom at {cursor.Index} in IL code for Level.Render");
                cursor.EmitDelegate<Func<float, float>>(modZoom);
            }
        }

        private float modZoom(float zoom) {
            return zoom * Settings.ZoomLevel / 10f;
        }

        public Vector2 getScreenPosition(Vector2 originalPosition) {
            if (Settings.ZoomLevel == 10) {
                // nothing to do, spare us some processing.
                return originalPosition;
            }

            // compute the size difference between regular screen and zoomed in screen
            Vector2 screenSize = new Vector2(320f, 180f) * Settings.ZoomLevel / 10f;
            Vector2 diff = screenSize - new Vector2(320f, 180f);

            Player player = Engine.Scene.Tracker.GetEntity<Player>();
            if (Settings.ZoomLevel > 10 && player != null) {
                // if the player is on the left of the screen, we shouldn't move the screen (left is aligned with left side of the screen).
                // if they are on the right, we should move it left by the difference (right is aligned with right side of the screen).
                // in between, just lerp
                diff *= new Vector2(
                    Calc.ClampedMap(player.CenterX, (Engine.Scene as Level).Bounds.Left, (Engine.Scene as Level).Bounds.Right),
                    Calc.ClampedMap(player.CenterY, (Engine.Scene as Level).Bounds.Top, (Engine.Scene as Level).Bounds.Bottom));
            } else {
                // no player, or < 1x zoom: center the screen.
                diff *= 0.5f;
            }

            if (player == null || player.Dead) {
                // no player: no target, don't move
                diff = previousDiff;
            } else if (transitionPercent == 1) {
                // save the position in case we're transitioning later
                previousDiff = diff;
            } else {
                // lerp in the same way transitions do, synchronized with the transition: this allows for a seemless realignment.
                diff = Vector2.Lerp(previousDiff, diff, Ease.CubeOut(transitionPercent));
            }

            return originalPosition - diff;
        }
    }
}
