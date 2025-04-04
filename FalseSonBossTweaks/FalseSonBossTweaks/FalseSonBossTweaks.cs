using BepInEx;
using R2API;
using RoR2;
using EntityStates;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using EntityStates.PrimeMeridian;
using UnityEngine.Networking;
using RoR2.Skills;
using RoR2.ContentManagement;
using EntityStates.FalseSonBoss;
using System.Linq;
using BepInEx.Configuration;
//using BepInEx.Configuration;

namespace FalseSonBossTweaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class FalseSonBossTweaks : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Jeffdev";
        public const string PluginName = "FalseSonBossTweaks";
        public const string PluginVersion = "1.0.6";

        public MeridianEventState bossPhase = MeridianEventState.None;
        public static ConfigEntry<int> monsterCredits;
        public static ConfigEntry<float> dashSlamTime;
        public static ConfigEntry<bool> eliteGolems;
        public static ConfigEntry<int> golemAmount;
        public static ConfigEntry<bool> eclipseSevenChanges;
        public static ConfigEntry<bool> slowFalseSonLaser;

        public void Awake()
        {
            Log.Init(Logger);

            monsterCredits = Config.Bind("General", "Meridian Credits", 320, "Change the amount of monster credits Prime Meridian has (450 is vanilla default)");
            dashSlamTime = Config.Bind("General", "Time Between Dash and Slam", 0.4f, "Idle time between the dash and slam attack. (0 will make the boss have no idle time, which is how vanilla works. Keep it between 0-1 second, or else jank will happen.)");
            eliteGolems = Config.Bind("General", "Pre Loop Elite Golems in Fight", false, "Allow golems to be elite in the fight in pre loop (true is vanilla default)");
            golemAmount = Config.Bind("General", "Max Golems in Fight", 4, "Max number of golems allowed to be spawned (5 is vanilla default)");
            eclipseSevenChanges = Config.Bind("General", "Eclipse 7 Laser/Skill Disable Changes", true, "Change the skill cooldowns to be longer for laser and skill disable attacks in Eclipse 7 (false is vanilla default)");
            slowFalseSonLaser = Config.Bind("General", "Slow False Son during Phase 2 Laser", true, "Gives the False Son a slowing debuff during Phase 2 (false is vanilla default)");

            On.RoR2.MeridianEventLightningTrigger.Start += (orig, self) =>
            {
                self.levelstartMonsterCredit = monsterCredits.Value;
                orig(self);
            };

            On.EntityStates.FalseSonBoss.CorruptedPathsDash.GetNextStateAuthority += CorruptedPathsDash_GetNextStateAuthority;
            On.EntityStates.FalseSonBoss.LunarGazeHoldLeap.OnEnter += LunarGazeHoldLeap_OnEnter;
            On.EntityStates.PrimeMeridian.LunarGazeLaserEnd.OnEnter += LunarGazeLaserEnd_OnEnter;

            On.RoR2.MeridianEventTriggerInteraction.Start += MeridianEventTriggerInteraction_Start; ;

            On.EntityStates.MeridianEvent.Phase1.OnEnter += Phase1_OnEnter;
            On.EntityStates.MeridianEvent.Phase2.OnEnter += Phase2_OnEnter;
            On.EntityStates.MeridianEvent.Phase3.OnEnter += Phase3_OnEnter;

            RoR2.Run.onRunStartGlobal += Run_onRunStartGlobal;
            RoR2.Run.onRunDestroyGlobal += Run_onRunDestroyGlobal;

            //On.RoR2.TeleporterInteraction.Start += TeleporterInteraction_Start;

            //// IL Hooking
            //IL.EntityStates.FalseSonBoss.CorruptedPathsDash.FixedUpdate += CorruptedPathsDash_FixedUpdate; ;
        }

        private void MeridianEventTriggerInteraction_Start(On.RoR2.MeridianEventTriggerInteraction.orig_Start orig, MeridianEventTriggerInteraction self)
        {
            orig(self);

            if (self.phase2CombatDirector)
            {
                CombatDirector director = self.phase2CombatDirector.GetComponent<CombatDirector>();
                if (director)
                {
                    director.maxSquadCount = (uint)golemAmount.Value;
                    bool isLooping = Run.instance && Run.instance.loopClearCount > 0;

                    if (!isLooping && eliteGolems.Value == false)
                    {
                        director.eliteBias = 9999;
                    }
                    else
                    {
                        director.eliteBias = 0;
                    }
                    
                }
            }
        }

        private EntityState CorruptedPathsDash_GetNextStateAuthority(On.EntityStates.FalseSonBoss.CorruptedPathsDash.orig_GetNextStateAuthority orig, CorruptedPathsDash self)
        {
            self.skillLocator.primary.DeductStock(2);

            // Schedule transition after 0.4 seconds by default, to give it a bit more time
            if (dashSlamTime.Value != 0)
            {
                return new DelayedState(dashSlamTime.Value, new FissureSlamWindup());
            } else
            {
                return new DelayedState(0.01f, new FissureSlamWindup());
            }
            
        }

        // Custom DelayedState for delaying transitions
        public class DelayedState(float delay, EntityState nextState) : EntityState
        {
            private readonly float delay = delay;
            private readonly EntityState nextState = nextState;

            public override void Update()
            {
                base.Update();

                if (base.fixedAge >= delay)
                {
                    outer.SetNextState(nextState);
                }
            }
        }

        private void Run_onRunStartGlobal(Run obj)
        {
            Log.Debug($"{obj.selectedDifficulty} {DifficultyIndex.Eclipse7}");
            if (obj.selectedDifficulty >= DifficultyIndex.Eclipse7 && eclipseSevenChanges.Value == true)
            {
                SkillDef primeDevestatorSkill = SkillCatalog.GetSkillDef(SkillCatalog.FindSkillIndexByName("PrimeDevastator"));
                SkillDef lunarGazePlusSkill = SkillCatalog.GetSkillDef(SkillCatalog.FindSkillIndexByName("LunarGazePlus"));
                SkillDef lunarGazeSkill = SkillCatalog.allSkillDefs.FirstOrDefault(skill => skill.skillName == "Laser" && skill.baseRechargeInterval == 35f);

                if (primeDevestatorSkill == null || lunarGazePlusSkill == null || lunarGazeSkill == null)
                {
                    Log.Error($"One or more SkillDefs could not be found! Check skill names. {primeDevestatorSkill} | {lunarGazePlusSkill} | {lunarGazeSkill}");
                    return;
                }

                primeDevestatorSkill.baseRechargeInterval *= 1.5f;
                lunarGazePlusSkill.baseRechargeInterval *= 1.5f;
                lunarGazeSkill.baseRechargeInterval *= 1.5f;

                Log.Debug($"Devestator Skill Cooldown: {primeDevestatorSkill.baseRechargeInterval}");
                Log.Debug($"Lunar Gaze Plus Skill Cooldown: {lunarGazePlusSkill.baseRechargeInterval}");
                Log.Debug($"Lunar Gaze Skill Cooldown: {lunarGazeSkill.baseRechargeInterval}");
            }
        }

        private void Run_onRunDestroyGlobal(Run obj)
        {
            if (obj.selectedDifficulty >= DifficultyIndex.Eclipse7 && eclipseSevenChanges.Value == true)
            {
                SkillDef primeDevestatorSkill = SkillCatalog.GetSkillDef(SkillCatalog.FindSkillIndexByName("PrimeDevastator"));
                SkillDef lunarGazePlusSkill = SkillCatalog.GetSkillDef(SkillCatalog.FindSkillIndexByName("LunarGazePlus"));
                SkillDef lunarGazeSkill = SkillCatalog.allSkillDefs.FirstOrDefault(skill => skill.skillName == "Laser" && skill.baseRechargeInterval == 52.5f);

                if (primeDevestatorSkill == null || lunarGazePlusSkill == null || lunarGazeSkill == null)
                {
                    Log.Error($"One or more SkillDefs could not be found! Check skill names. {primeDevestatorSkill} | {lunarGazePlusSkill} | {lunarGazeSkill}");
                    return;
                }

                primeDevestatorSkill.baseRechargeInterval /= 1.5f;
                lunarGazePlusSkill.baseRechargeInterval /= 1.5f;
                lunarGazeSkill.baseRechargeInterval /= 1.5f;

                Log.Debug($"Devestator Skill Cooldown: {primeDevestatorSkill.baseRechargeInterval}");
                Log.Debug($"Lunar Gaze Plus Skill Cooldown: {lunarGazePlusSkill.baseRechargeInterval}");
                Log.Debug($"Lunar Gaze Skill Cooldown: {lunarGazeSkill.baseRechargeInterval}");
            }
        }

        private void Phase1_OnEnter(On.EntityStates.MeridianEvent.Phase1.orig_OnEnter orig, EntityStates.MeridianEvent.Phase1 self)
        {
            orig(self);
            this.bossPhase = MeridianEventState.Phase1;
        }
        private void Phase2_OnEnter(On.EntityStates.MeridianEvent.Phase2.orig_OnEnter orig, EntityStates.MeridianEvent.Phase2 self)
        {
            orig(self);
            this.bossPhase = MeridianEventState.Phase2;
        }
        private void Phase3_OnEnter(On.EntityStates.MeridianEvent.Phase3.orig_OnEnter orig, EntityStates.MeridianEvent.Phase3 self)
        {
            orig(self);
            this.bossPhase = MeridianEventState.Phase3;
        }

        //private static void LunarGazeLaserFire_FireBullet(ILContext il)
        //{
        //    var c = new ILCursor(il);
        //    if (c.TryGotoNext(
        //        MoveType.After, x => x.MatchLdsfld<LunarGazeLaserFire>(nameof(LunarGazeLaserFire.lunarGazeDamageType))))
        //    {
        //        Log.Debug("Time to change Damage Type!");
        //        c.Emit(OpCodes.Pop);
        //        c.Emit(OpCodes.Ldc_I4, (int)DamageType.Generic);
        //    }
        //    else
        //    {
        //        Log.Debug("Oh no, can't find the damage type");
        //    }
        //}

        private void LunarGazeHoldLeap_OnEnter(On.EntityStates.FalseSonBoss.LunarGazeHoldLeap.orig_OnEnter orig, EntityStates.FalseSonBoss.LunarGazeHoldLeap self)
        {
            orig(self);
            Log.Debug("Added Debuff to False Son!");
            if (this.bossPhase == MeridianEventState.Phase2 && slowFalseSonLaser.Value == true)
            {
                self.characterBody.AddBuff(RoR2Content.Buffs.Slow80);
            }
        }
        private void LunarGazeLaserEnd_OnEnter(On.EntityStates.PrimeMeridian.LunarGazeLaserEnd.orig_OnEnter orig, EntityStates.PrimeMeridian.LunarGazeLaserEnd self)
        {
            orig(self);

            // Find the False Son Boss in the scene
            foreach (CharacterBody body in CharacterBody.readOnlyInstancesList)
            {
                if (body && body.name.Contains("FalseSonBoss"))
                {
                    if (body.HasBuff(RoR2Content.Buffs.Slow80))
                    {
                        body.RemoveBuff(RoR2Content.Buffs.Slow80);
                        Debug.Log("Removed Debuff from False Son!");
                    }
                }
            }
        }
    }
}
