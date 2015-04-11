﻿using Patcher;
using TowerFall;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Linq;

namespace Mod {
    [Patch]
    public class MyRoundLogic : RoundLogic {
        protected MyRoundLogic(Session session, bool canHaveMiasma)
            : base(session, canHaveMiasma) {
        }

        public new static RoundLogic GetRoundLogic(Session session) {
            switch (session.MatchSettings.Mode) {
                case RespawnRoundLogic.Mode:
                    return new RespawnRoundLogic(session);
                case MobRoundLogic.Mode:
                    return new MobRoundLogic(session);
                case GemRoundLogic.Mode:
                    return new GemRoundLogic(session);
                default:
                    return RoundLogic.GetRoundLogic(session);
            }
        }
    }

    [Patch]
    public class MyVersusModeButton : VersusModeButton {
        static List<Modes> VersusModes = new List<Modes> {
			Modes.LastManStanding, Modes.HeadHunters, Modes.TeamDeathmatch,
			RespawnRoundLogic.Mode,	MobRoundLogic.Mode, GemRoundLogic.Mode
		};

        public MyVersusModeButton(Vector2 position, Vector2 tweenFrom)
            : base(position, tweenFrom) {
        }

        public new static string GetModeName(Modes mode) {
            switch (mode) {
                case RespawnRoundLogic.Mode:
                    return "RESPAWN";
                case MobRoundLogic.Mode:
                    return "CRAWL";
                case GemRoundLogic.Mode:
                    return "KING OF THE GEM";
                default:
                    return VersusModeButton.GetModeName(mode);
            }
        }

        public new static Subtexture GetModeIcon(Modes mode) {
            switch (mode) {
                case RespawnRoundLogic.Mode:
                    return TFGame.MenuAtlas["gameModes/respawn"];
                case MobRoundLogic.Mode:
                    return TFGame.MenuAtlas["gameModes/crawl"];
                case GemRoundLogic.Mode:
                    return TFGame.MenuAtlas["gameModes/kotgem"];
                default:
                    return VersusModeButton.GetModeIcon(mode);
            }
        }

        // completely re-write to make it enum-independent
        public override void Update() {
            // skip original implementation
            Patcher.Patcher.CallRealBase();

            Modes mode = MainMenu.VersusMatchSettings.Mode;
            if (this.Selected) {
                int idx = VersusModes.IndexOf(mode);
                if (idx < VersusModes.Count - 1 && MenuInput.Right) {
                    MainMenu.VersusMatchSettings.Mode = VersusModes[idx + 1];
                    Sounds.ui_move2.Play(160f, 1f);
                    this.iconWiggler.Start();
                    base.OnConfirm();
                    this.UpdateSides();
                } else if (idx > 0 && MenuInput.Left) {
                    MainMenu.VersusMatchSettings.Mode = VersusModes[idx - 1];
                    Sounds.ui_move2.Play(160f, 1f);
                    this.iconWiggler.Start();
                    base.OnConfirm();
                    this.UpdateSides();
                }
            }
        }

        public override void UpdateSides() {
            base.UpdateSides();
            this.DrawRight = (MainMenu.VersusMatchSettings.Mode < VersusModes[VersusModes.Count - 1]);
        }
    }

    [Patch]
    public class MyMatchSettings : MatchSettings {
        public MyMatchSettings(LevelSystem levelSystem, Modes mode, MatchSettings.MatchLengths matchLength)
            : base(levelSystem, mode, matchLength) {
        }

        public override int GoalScore {
            get {
                int goals;
                switch (this.Mode) {
                    case RespawnRoundLogic.Mode:
                    case MobRoundLogic.Mode:
                        goals = this.PlayerGoals(5, 8, 10);
                        return (int)Math.Ceiling(((float)goals * MatchSettings.GoalMultiplier[(int)this.MatchLength]));
                    case GemRoundLogic.Mode:
                        goals = this.PlayerGoals(5, 4, 3);
                        return (int)Math.Ceiling(((float)goals * MatchSettings.GoalMultiplier[(int)this.MatchLength]));
                    default:
                        return base.GoalScore;
                }
            }
        }
    }

    [Patch]
    public class MyVersusCoinButton : VersusCoinButton {
        public MyVersusCoinButton(Vector2 position, Vector2 tweenFrom)
            : base(position, tweenFrom) {
        }

        public override void Render() {
            var mode = MainMenu.VersusMatchSettings.Mode;
            if (mode == RespawnRoundLogic.Mode || mode == MobRoundLogic.Mode) {
                MainMenu.VersusMatchSettings.Mode = Modes.HeadHunters;
                base.Render();
                MainMenu.VersusMatchSettings.Mode = mode;
            } else if (mode == GemRoundLogic.Mode) {
                MainMenu.VersusMatchSettings.Mode = Modes.LastManStanding;
                base.Render();
                MainMenu.VersusMatchSettings.Mode = mode;
            } else {
                base.Render();
            }
        }
    }

    [Patch]
    public class MyVersusRoundResults : VersusRoundResults {
        private Modes _oldMode;

        public MyVersusRoundResults(Session session)
            : base(session) {
            this._oldMode = session.MatchSettings.Mode;
            if (this._oldMode == RespawnRoundLogic.Mode || this._oldMode == MobRoundLogic.Mode)
                session.MatchSettings.Mode = Modes.HeadHunters;
        }

        public override void TweenOut() {
            this.session.MatchSettings.Mode = this._oldMode;
            base.TweenOut();
        }
    }

    public class RespawnRoundLogic : RoundLogic {
        public const Modes Mode = (Modes)42;

        private KillCountHUD[] killCountHUDs = new KillCountHUD[4];
        private bool wasFinalKill;
        private Counter endDelay;

        public RespawnRoundLogic(Session session)
            : base(session, canHaveMiasma: false) {
            for (int i = 0; i < 4; i++) {
                if (TFGame.Players[i]) {
                    killCountHUDs[i] = new KillCountHUD(i);
                    this.Session.CurrentLevel.Add(killCountHUDs[i]);
                }
            }
            this.endDelay = new Counter();
            this.endDelay.Set(90);
        }

        public override void OnLevelLoadFinish() {
            base.OnLevelLoadFinish();
            base.Session.CurrentLevel.Add<VersusStart>(new VersusStart(base.Session));
            base.Players = base.SpawnPlayersFFA();
        }

        public override bool CheckForAllButOneDead() {
            return false;
        }

        public override void OnUpdate() {
            base.OnUpdate();
            if (base.RoundStarted && base.Session.CurrentLevel.Ending && base.Session.CurrentLevel.CanEnd) {
                if (this.endDelay) {
                    this.endDelay.Update();
                    return;
                }
                base.Session.EndRound();
            }
        }

        protected Player RespawnPlayer(int playerIndex) {
            List<Vector2> spawnPositions = this.Session.CurrentLevel.GetXMLPositions("PlayerSpawn");

            var player = new Player(playerIndex, new Random().Choose(spawnPositions), Allegiance.Neutral, Allegiance.Neutral,
                            this.Session.GetPlayerInventory(playerIndex), this.Session.GetSpawnHatState(playerIndex), frozen: false);
            this.Session.CurrentLevel.Add(player);
            player.Flash(120, null);
            Alarm.Set(player, 60, player.RemoveIndicator, Alarm.AlarmMode.Oneshot);
            return player;
        }

        protected virtual void AfterOnPlayerDeath(Player player) {
            this.RespawnPlayer(player.PlayerIndex);
        }

        public override void OnPlayerDeath(Player player, PlayerCorpse corpse, int playerIndex, DeathCause cause, Vector2 position, int killerIndex) {
            base.OnPlayerDeath(player, corpse, playerIndex, cause, position, killerIndex);

            if (killerIndex == playerIndex || killerIndex == -1) {
                killCountHUDs[playerIndex].Decrease();
                base.AddScore(playerIndex, -1);
            } else if (killerIndex != -1) {
                killCountHUDs[killerIndex].Increase();
                base.AddScore(killerIndex, 1);
            }

            int winner = base.Session.GetWinner();
            if (this.wasFinalKill && winner == -1) {
                this.wasFinalKill = false;
                base.Session.CurrentLevel.Ending = false;
                base.CancelFinalKill();
                this.endDelay.Set(90);
            }
            if (!this.wasFinalKill && winner != -1) {
                base.Session.CurrentLevel.Ending = true;
                this.wasFinalKill = true;
                base.FinalKill(corpse, winner);
            }

            this.AfterOnPlayerDeath(player);
        }
    }

    public class KillCountHUD : Entity {
        int playerIndex;
        List<Sprite<int>> skullIcons = new List<Sprite<int>>();

        public int Count { get { return this.skullIcons.Count; } }

        public KillCountHUD(int playerIndex)
            : base(3) {
            this.playerIndex = playerIndex;
        }

        public void Increase() {
            Sprite<int> sprite = DeathSkull.GetSprite();

            if (this.playerIndex % 2 == 0) {
                sprite.X = 8 + 10 * skullIcons.Count;
            } else {
                sprite.X = 320 - 8 - 10 * skullIcons.Count;
            }

            sprite.Y = this.playerIndex / 2 == 0 ? 20 : 240 - 20;
            //sprite.Play(0, restart: false);
            sprite.Stop();
            this.skullIcons.Add(sprite);
            base.Add(sprite);
        }

        public void Decrease() {
            if (this.skullIcons.Any()) {
                base.Remove(this.skullIcons.Last());
                this.skullIcons.Remove(this.skullIcons.Last());
            }
        }

        public override void Render() {
            foreach (Sprite<int> sprite in this.skullIcons) {
                sprite.DrawOutline(1);
            }
            base.Render();
        }
    }

    [Patch]
    public class MyPlayerGhost : PlayerGhost {
        PlayerCorpse corpse;

        public MyPlayerGhost(PlayerCorpse corpse)
            : base(corpse) {
            this.corpse = corpse;
        }

        public override void Die(int killerIndex, Arrow arrow, Explosion explosion) {
            base.Die(killerIndex, arrow, explosion);
            var mobLogic = this.Level.Session.RoundLogic as MobRoundLogic;
            if (mobLogic != null) {
                mobLogic.OnPlayerDeath(
                    null, this.corpse, this.PlayerIndex, DeathCause.Arrow, // FIXME
                    this.Position, killerIndex
                );
            }
        }
    }

    public class MobRoundLogic : RespawnRoundLogic {
        public new const Modes Mode = (Modes)43;

        PlayerGhost[] activeGhosts = new PlayerGhost[4];

        public MobRoundLogic(Session session)
            : base(session) {
        }

        protected override void AfterOnPlayerDeath(Player player) {
        }

        void RemoveGhostAndRespawn(int playerIndex, Vector2 position = default(Vector2)) {
            if (activeGhosts[playerIndex] != null) {
                var ghost = activeGhosts[playerIndex];
                var player = this.RespawnPlayer(playerIndex);
                // if we've been given a position, make sure the ghost spawns at that position and
                // retains its speed pre-spawn.
                if (position != default(Vector2)) {
                    player.Position.X = position.X;
                    player.Position.Y = position.Y;

                    player.Speed.X = ghost.Speed.X;
                    player.Speed.Y = ghost.Speed.Y;
                }
                activeGhosts[playerIndex].RemoveSelf();
                activeGhosts[playerIndex] = null;
            }
        }

        public override void OnPlayerDeath(Player player, PlayerCorpse corpse, int playerIndex, DeathCause cause, Vector2 position, int killerIndex) {
            base.OnPlayerDeath(player, corpse, playerIndex, cause, position, killerIndex);
            this.Session.CurrentLevel.Add(activeGhosts[playerIndex] = new PlayerGhost(corpse));

            if (killerIndex == playerIndex || killerIndex == -1) {
                if (this.Session.CurrentLevel.LivingPlayers == 0) {
                    var otherPlayers = TFGame.Players.Select((playing, idx) => playing && idx != playerIndex ? (int?)idx : null).Where(idx => idx != null).ToList();
                    var randomPlayer = new Random().Choose(otherPlayers).Value;
                    RemoveGhostAndRespawn(randomPlayer);
                }
            } else {
                RemoveGhostAndRespawn(killerIndex, position);
            }
        }
    }


    public class GemRoundLogic : RoundLogic {
        public const Modes Mode = (Modes)44;

        private Counter endDelay;
        private Counter pointDelay;
        private int gemOwner;
        private bool roundOver = false;

        private GemModePointer pointer;
        private new Miasma miasma;

        private int CROWN_ROUND_LENGTH = 5;

        public GemRoundLogic(Session session)
            : base(session, canHaveMiasma: false) {
        }

        public override void OnRoundStart() {
            base.OnRoundStart();
            base.SpawnTreasureChestsVersus();
        }

        public override void OnLevelLoadFinish() {
            base.OnLevelLoadFinish();
            base.Session.CurrentLevel.Add<VersusStart>(new VersusStart(base.Session));
            base.Players = base.SpawnPlayersFFA();

            roundOver = false;

            List<Vector2> gemPositions = this.Session.CurrentLevel.GetXMLPositions("BigTreasureChest");
            if (gemPositions.Count == 0) gemPositions = this.Session.CurrentLevel.GetXMLPositions("Spawner");
            if (gemPositions.Count == 0) gemPositions = this.Session.CurrentLevel.GetXMLPositions("TreasureChest");
            Vector2 pos = new Random().Choose(gemPositions);
            base.Session.CurrentLevel.Add<GemModeTreasureChest>(new GemModeTreasureChest(pos));

            gemOwner = -1;

            this.endDelay = new Counter();
            this.endDelay.Set(90);
        }

        public override void OnUpdate() {
            base.OnUpdate();
            if (base.RoundStarted && (gemOwner != -1 || roundOver)) {
                if (gemOwner != -1) {
                    if (pointDelay && !CheckForAllButOneDead()) {
                        pointDelay.Update();
                        var str = string.Concat("", Math.Ceiling(pointDelay.Value / 60f));
                        if (pointer.str != str) {
                            pointer.str = str;
                            pointer.textOrigin = (TFGame.Font.MeasureString(str) / 2f).Floor();
                            Sounds.sfx_arrowToggle.Play(160f, 0.6f);
                        }
                        return;
                    }
                    pointer.str = "0";
                    base.Session.CurrentLevel.Ending = true;
                    base.AddScore(gemOwner, 1);
                    if (base.Session.GetWinner() != -1) {
                        this.Session.CurrentLevel.LightingLayer.SetSpotlight(new LevelEntity[] { Session.CurrentLevel.GetPlayer(gemOwner) });
                        FinalKillNoSpotlight();
                    } else Sounds.char_ready[Session.CurrentLevel.GetPlayer(gemOwner).CharacterIndex].Play(160f, 1f);
                    gemOwner = -1;
                    roundOver = true;
                }
                if (endDelay) {
                    if (miasma != null) miasma.Dissipate();
                    endDelay.Update();
                    return;
                }
                base.Session.EndRound();
            }
            if (CheckForAllButOneDead() && !roundOver && miasma == null) {
                miasma = new Miasma();
                this.Session.CurrentLevel.Add<Miasma>(miasma);
            }
        }

        public override void OnPlayerDeath(Player player, PlayerCorpse corpse, int playerIndex, DeathCause cause, Vector2 position, int killerIndex) {
            base.OnPlayerDeath(player, corpse, playerIndex, cause, position, killerIndex);

            if (playerIndex == gemOwner) {
                base.Session.CurrentLevel.Add<GemModePickup>(new GemModePickup(position, position));
                pointer.RemoveSelf();
                gemOwner = -1;
            }

            if (base.Session.CurrentLevel.LivingPlayers == 0) {
                base.Session.CurrentLevel.Ending = true;
                roundOver = true;
            }
        }

        public void OnGemPickup(Player owner) {
            gemOwner = owner.PlayerIndex;
            pointer = new GemModePointer(owner);
            base.Session.CurrentLevel.Add<GemModePointer>(pointer);
            this.pointDelay = new Counter();
            this.pointDelay.Set(60 * CROWN_ROUND_LENGTH);
        }
    }

    public class GemModeTreasureChest : TreasureChest {
        private Counter openCounter;
        private Counter spawnCounter;

        public GemModeTreasureChest(Vector2 position)
            : base(position, TreasureChest.Types.Special, TreasureChest.AppearModes.Normal, Pickups.Mirror) {
            this.openCounter = new Counter();
            this.openCounter.Set(120);
            this.spawnCounter = new Counter();
        }

        public override bool ReadyToAppear() {
            return true;
        }

        public override void Update() {
            base.Update();
            if (this.spawnCounter) {
                this.spawnCounter.Update();
                if (!this.spawnCounter) {
                    this.Level.Remove(this.Level[GameTags.PlayerCollectible]);
                    this.Level.Add<GemModePickup>(new GemModePickup(this.Position, Pickup.GetTargetPositionFromChest(base.Level, this.Position - Vector2.UnitY)));
                }
                return;
            }
            if (this.openCounter) {
                this.openCounter.Update();
                if (!this.openCounter) {
                    OpenChest(-1);
                    base.Flash(30, new Action(this.RemoveSelf));
                    this.spawnCounter.Set(1);
                }
                return;
            }
        }
    }

    public class GemModePickup : Pickup {
        public Sprite<int> sprite;

        private SineWave sine;

        public GemModePickup(Vector2 position, Vector2 target)
            : base(position, target) {
            base.Collider = new Hitbox(16f, 16f, -8f, -8f);
            base.Tag(new GameTags[] { GameTags.PlayerCollectible, GameTags.LightSource });

            this.LightRadius = 50f;
            this.LightColor = Player.LightColors[5].Invert();
            this.LightVisible = true;

            SineWave sineWave = new SineWave(120);
            SineWave sineWave1 = sineWave;
            this.sine = sineWave;
            base.Add(sineWave1);
        }

        public override void Added() {
            base.Added();
            this.sprite = TFGame.SpriteData.GetSpriteInt("Gem5");
            this.sprite.Play(0, false);
            base.Add(this.sprite);
        }

        public override void OnPlayerCollide(Player player) {
            if (base.Level.Session.RoundLogic is GemRoundLogic) {
                ((GemRoundLogic)base.Level.Session.RoundLogic).OnGemPickup(player);
            }
            Sounds.sfx_gemCollect.Play(base.X, 1f);
            base.RemoveSelf();
        }

        public override void TweenUpdate(Tween tween) {
            this.sprite.Rotation = MathHelper.Lerp(0f, 6.28318548f, Ease.CubeOut(tween.Percent));
            this.sprite.Scale = Vector2.One * tween.Eased;
        }

        public override void Update() {
            base.Update();
            this.sprite.Position = new Vector2(3f * this.sine.Value, 3f * this.sine.ValueOverTwo);
        }
    }

    public class GemModePointer : LevelEntity {
        private Color color1;

        private Color color2;

        public Vector2 textOrigin;

        private Color currentColor;

        public Player player;

        public String str = "5";

        public GemModePointer(Player player)
            : base(new Vector2(player.Position.X, player.Position.Y - 24f)) {
            this.color1 = Player.Colors[5];
            this.color2 = Player.LightColors[5];
            base.Depth = -1000;
            base.Tag(GameTags.LightSource);

            this.LightRadius = 50f;
            this.LightColor = Player.LightColors[5].Invert();
            this.LightVisible = true;

            this.currentColor = color1;
            this.textOrigin = (TFGame.Font.MeasureString("5") / 2f).Floor();
            this.player = player;
            Alarm.Set(this, 10, new Action(this.FlipColor), Alarm.AlarmMode.Looping);
        }

        private void FlipColor() {
            if (this.currentColor == this.color1) {
                this.currentColor = this.color2;
                return;
            }
            this.currentColor = this.color1;
        }

        public override void Render() {
            for (int i = -1; i < 2; i++) {
                for (int j = -1; j < 2; j++) {
                    if (i != 0 || j != 0) {
                        Draw.SpriteBatch.DrawString(TFGame.Font, str, this.Position.Floor() + new Vector2((float)i, (float)j), (Color.Black * this.LightAlpha) * 0.5f, 0f, this.textOrigin, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
                    }
                }
            }
            Draw.SpriteBatch.DrawString(TFGame.Font, str, this.Position.Floor(), this.currentColor, 0f, this.textOrigin, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
        }

        public override void Update() {
            base.Update();
            if (!player.Dead) {
                X = player.X;
                if (player.State == Player.PlayerStates.Ducking || player.DodgeSliding) Y = player.Y - 16f;
                else Y = player.Y - 28f;
            } else {
                var entity = Level.GetPlayerOrCorpse(player.PlayerIndex);
                X = entity.X;
                Y = entity.Y - 16f;
            }
        }
    }
}
