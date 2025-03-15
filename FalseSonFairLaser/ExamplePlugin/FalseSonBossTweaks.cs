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
using BepInEx.Configuration;

namespace FalseSonBossTweaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class FalseSonBossTweaks : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Jeffdev";
        public const string PluginName = "FalseSonFairLaser";
        public const string PluginVersion = "1.0.0";

        public MeridianEventState bossPhase = MeridianEventState.None;
        internal static ConfigEntry<bool> stage4GreenOrb { get; private set; }

        public void Awake()
        {
            Log.Init(Logger);

            stage4GreenOrb = Config.Bind<bool>("Main", "Spawn a Green Orb on Stage 4", false, "Spawns a guaranteed green orb on stage 4, to give you an extra stage to fight the False Son with.");

            On.RoR2.MeridianEventLightningTrigger.Start += (orig, self) =>
            {
                self.levelstartMonsterCredit = 320;
                orig(self);
            };

            On.EntityStates.FalseSonBoss.CorruptedPathsDash.OnEnter += CorruptedPathsDash_OnEnter;
            On.EntityStates.FalseSonBoss.CorruptedPathsDash.OnExit += CorruptedPathsDash_OnExit;
            On.EntityStates.FalseSonBoss.LunarGazeHoldLeap.OnEnter += LunarGazeHoldLeap_OnEnter;
            On.EntityStates.PrimeMeridian.LunarGazeLaserEnd.OnEnter += LunarGazeLaserEnd_OnEnter;

            On.EntityStates.MeridianEvent.Phase1.OnEnter += Phase1_OnEnter;
            On.EntityStates.MeridianEvent.Phase2.OnEnter += Phase2_OnEnter;
            On.EntityStates.MeridianEvent.Phase3.OnEnter += Phase3_OnEnter;

            RoR2.Run.onRunStartGlobal += Run_onRunStartGlobal;
            RoR2.Run.onRunDestroyGlobal += Run_onRunDestroyGlobal;

            On.RoR2.SceneDirector.PopulateScene += SceneDirector_PopulateScene;

            //On.RoR2.TeleporterInteraction.Start += TeleporterInteraction_Start;

            //// IL Hooking
            //IL.EntityStates.PrimeMeridian.LunarGazeLaserFire.FireBullet += LunarGazeLaserFire_FireBullet;
        }

        //private void TeleporterInteraction_Start(On.RoR2.TeleporterInteraction.orig_Start orig, TeleporterInteraction self)
        //{
        //    orig(self);
        //    if (Run.instance.stageClearCount != 3)
        //    {
        //        return;
        //    }
        //    TeleporterInteraction.instance.shouldAttemptToSpawnShopPortal = true;
        //    PortalStatueBehavior[] array = Object.FindObjectsOfType<PortalStatueBehavior>();
        //    PurchaseInteraction val2 = default(PurchaseInteraction);
        //    foreach (PortalStatueBehavior val in array)
        //    {
        //        if ((int)val.portalType == 0 && ((Component)val).TryGetComponent<PurchaseInteraction>(ref val2))
        //        {
        //            val2.Networkavailable = false;
        //        }
        //    }
        //}

        private void SceneDirector_PopulateScene(On.RoR2.SceneDirector.orig_PopulateScene orig, SceneDirector self)
        {
            orig(self);
        }

        private void CorruptedPathsDash_OnEnter(On.EntityStates.FalseSonBoss.CorruptedPathsDash.orig_OnEnter orig, EntityStates.FalseSonBoss.CorruptedPathsDash self)
        {
            if (Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse4)
            {
                self.characterBody.AddBuff(RoR2Content.Buffs.Slow60);
            }

            orig(self);
        }

        private void CorruptedPathsDash_OnExit(On.EntityStates.FalseSonBoss.CorruptedPathsDash.orig_OnExit orig, EntityStates.FalseSonBoss.CorruptedPathsDash self)
        {
            if (self.characterBody.HasBuff(RoR2Content.Buffs.Slow60)) 
            {
                self.characterBody.RemoveBuff(RoR2Content.Buffs.Slow60);
            }

            orig(self);
        }

        private void Run_onRunStartGlobal(Run obj)
        {
            if (Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse7)
            {
                SkillDef primeDevestatorSkill = SkillCatalog.GetSkillDef(324);
                SkillDef laserSkill = SkillCatalog.GetSkillDef(320);
                SkillDef lunarGazePlusSkill = SkillCatalog.GetSkillDef(321);

                primeDevestatorSkill.baseRechargeInterval *= 2;
                laserSkill.baseRechargeInterval *= 2;
                lunarGazePlusSkill.baseRechargeInterval *= 2;

                Log.Debug($"Devestator Skill Cooldown: {primeDevestatorSkill.baseRechargeInterval}");
                Log.Debug($"Laser Skill Cooldown: {laserSkill.baseRechargeInterval}");
                Log.Debug($"Lunar Gaze Plus Skill Cooldown: {lunarGazePlusSkill.baseRechargeInterval}");
            }
        }

        private void Run_onRunDestroyGlobal(Run obj)
        {
            if (Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse7)
            {
                SkillDef primeDevestatorSkill = SkillCatalog.GetSkillDef(324);
                SkillDef laserSkill = SkillCatalog.GetSkillDef(320);
                SkillDef lunarGazePlusSkill = SkillCatalog.GetSkillDef(321);

                primeDevestatorSkill.baseRechargeInterval *= 0.5f;
                laserSkill.baseRechargeInterval *= 0.5f;
                lunarGazePlusSkill.baseRechargeInterval *= 0.5f;

                Log.Debug($"Devestator Skill Cooldown: {primeDevestatorSkill.baseRechargeInterval}");
                Log.Debug($"Laser Skill Cooldown: {laserSkill.baseRechargeInterval}");
                Log.Debug($"Lunar Gaze Plus Skill Cooldown: {lunarGazePlusSkill.baseRechargeInterval}");
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
            if (this.bossPhase == MeridianEventState.Phase2)
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
