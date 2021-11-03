﻿using Celeste;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace ExtendedVariants.Variants {
    public class DashTrailAllTheTime : AbstractExtendedVariant {
        private float dashTrailTimer = 0f;

        public override int GetDefaultValue() {
            return 0;
        }

        public override int GetValue() {
            return Settings.DashTrailAllTheTime ? 1 : 0;
        }

        public override void SetValue(int value) {
            Settings.DashTrailAllTheTime = (value != 0);
        }

        public override void Load() {
            On.Celeste.Player.CreateTrail += onCreateTrail;
            On.Celeste.Player.Update += onPlayerUpdate;
        }

        public override void Unload() {
            On.Celeste.Player.CreateTrail -= onCreateTrail;
            On.Celeste.Player.Update -= onPlayerUpdate;
        }

        private void onCreateTrail(On.Celeste.Player.orig_CreateTrail orig, Player self) {
            orig(self);

            // we don't want to add trails on top of the trails the game already makes.
            dashTrailTimer = 0.1f;
        }

        private void onPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self) {
            orig(self);

            if (Settings.DashTrailAllTheTime) {
                dashTrailTimer -= Engine.DeltaTime;
                if (dashTrailTimer <= 0f) {
                    createTrail(self);
                    dashTrailTimer = 0.1f;
                }
            }
        }

        // near vanilla copypaste to escape having to make reflection calls
        private static void createTrail(Player player) {
            Vector2 scale = new Vector2(Math.Abs(player.Sprite.Scale.X) * (float) player.Facing, player.Sprite.Scale.Y);
            if (player.StateMachine.State != 14) {
                TrailManager.Add(player, scale, player.Hair.Color);
            }
        }
    }
}
