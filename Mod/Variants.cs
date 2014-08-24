﻿using System;
using System.Collections.Generic;
using System.Linq;
using TowerFall;
using Microsoft.Xna.Framework;
using Patcher;
using Monocle;

namespace Mod
{
	[Patch]
	public class MyMatchVariants : MatchVariants
	{
		[Header("MODS")]
		[PerPlayer]
		public Variant NoHeadBounce;
		[PerPlayer]
		public Variant NoLedgeGrab;
		public Variant AwfullySlowArrows;
		public Variant AwfullyFastArrows;
		[PerPlayer]
		public Variant InfiniteArrows;
		[PerPlayer]
		public Variant NoDodgeCooldowns;
		[PerPlayer]
		public Variant GottaGoFast;

		public MyMatchVariants()
		{
			// mutually exclusive variants
			this.CreateLinks(NoHeadBounce, NoTimeLimit);
			this.CreateLinks(NoDodgeCooldowns, ShowDodgeCooldown);
			this.CreateLinks(AwfullyFastArrows, AwfullySlowArrows);
			this.CreateLinks(SpeedBoots, GottaGoFast);
		}
	}

	[Patch]
	public class MyPlayer : Player
	{
		public MyPlayer(int playerIndex, Vector2 position, Allegiance allegiance, Allegiance teamColor, PlayerInventory inventory, Player.HatState hatState, bool frozen = true)
			: base(playerIndex, position, allegiance, teamColor, inventory, hatState, frozen)
		{
		}

		public override bool CanGrabLedge(int a, int b)
		{
			if (((MyMatchVariants)Level.Session.MatchSettings.Variants).NoLedgeGrab[this.PlayerIndex])
				return false;
			return base.CanGrabLedge(a, b);
		}

		public override int GetDodgeExitState()
		{
			if (((MyMatchVariants)Level.Session.MatchSettings.Variants).NoDodgeCooldowns[this.PlayerIndex]) {
				this.DodgeCooldown();
			}
			return base.GetDodgeExitState();
		}

		public override float MaxRunSpeed {
			get {
				float res = base.MaxRunSpeed;
				if (((MyMatchVariants)Level.Session.MatchSettings.Variants).GottaGoFast[this.PlayerIndex]) {
					return res * 1.4f;
				}
				return res;
			}
		}

		public override int NormalUpdate()
		{
			// SpeedBoots add little dust clouds below our feet... we want those too.
			var gottaGoFast = ((MyMatchVariants)Level.Session.MatchSettings.Variants).GottaGoFast[this.PlayerIndex];
			bool hasSpeedBoots = Level.Session.MatchSettings.Variants.SpeedBoots[this.PlayerIndex];
			if (gottaGoFast) {
				typeof(Engine).GetProperty("TimeMult").SetValue(null, Engine.TimeMult * 2f, null);
				Level.Session.MatchSettings.Variants.SpeedBoots[this.PlayerIndex] = true;
			}
			int res = base.NormalUpdate();
			if (gottaGoFast) {
				typeof(Engine).GetProperty("TimeMult").SetValue(null, Engine.TimeMult / 2f, null);
			}
			Level.Session.MatchSettings.Variants.SpeedBoots[this.PlayerIndex] = hasSpeedBoots;
			((MyMatchVariants)Level.Session.MatchSettings.Variants).GottaGoFast[this.PlayerIndex] = gottaGoFast;
			return res;
		}

		public override void ShootArrow()
		{
			if (((MyMatchVariants)Level.Session.MatchSettings.Variants).InfiniteArrows[this.PlayerIndex]) {
				var arrow = this.Arrows.Arrows[0];
				base.ShootArrow();
				this.Arrows.AddArrows(arrow);
			} else {
				base.ShootArrow();
			}
		}

		public override void HurtBouncedOn(int bouncerIndex)
		{
			if (!((MyMatchVariants)Level.Session.MatchSettings.Variants).NoHeadBounce[this.PlayerIndex])
				base.HurtBouncedOn(bouncerIndex);
		}

	}

	[Patch]
	public abstract class MyArrow : Arrow
	{
		const float AwfullySlowArrowMult = 0.2f;
		const float AwfullyFastArrowMult = 3.0f;

		public override void Added()
		{
			base.Added();

			if (((MyMatchVariants)Level.Session.MatchSettings.Variants).AwfullyFastArrows) {
				this.NormalHitbox = new WrapHitbox(6f, 3f, -1f, -1f);
				this.otherArrowHitbox = new WrapHitbox(12f, 4f, -2f, -2f);
			}
		}

		public override void ArrowUpdate()
		{
			if (((MyMatchVariants)Level.Session.MatchSettings.Variants).AwfullySlowArrows) {
				// Engine.TimeMult *= AwfullySlowArrowMult;
				typeof(Engine).GetProperty("TimeMult").SetValue(null, Engine.TimeMult * AwfullySlowArrowMult, null);
				base.ArrowUpdate();
				// Engine.TimeMult /= AwfullySlowArrowMult;
				typeof(Engine).GetProperty("TimeMult").SetValue(null, Engine.TimeMult / AwfullySlowArrowMult, null);
			} else if (((MyMatchVariants)Level.Session.MatchSettings.Variants).AwfullyFastArrows) {
				typeof(Engine).GetProperty("TimeMult").SetValue(null, Engine.TimeMult * AwfullyFastArrowMult, null);
				base.ArrowUpdate();
				typeof(Engine).GetProperty("TimeMult").SetValue(null, Engine.TimeMult / AwfullyFastArrowMult, null);
			} else
				base.ArrowUpdate();
		}
	}
}
