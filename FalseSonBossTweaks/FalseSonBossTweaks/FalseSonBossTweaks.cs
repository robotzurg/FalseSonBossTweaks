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
using HG.GeneralSerializer;
using R2API.Utils;
//using BepInEx.Configuration;

namespace FalseSonBossTweaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class FalseSonBossTweaks : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Jeffdev";
        public const string PluginName = "FalseSonBossTweaks";
        public const string PluginVersion = "1.1.0";

        public MeridianEventState bossPhase = MeridianEventState.None;
        public static ConfigEntry<int> monsterCredits;
        public static ConfigEntry<bool> lessLightning;
        public static ConfigEntry<float> dashSlamTime;
        public static ConfigEntry<bool> eliteGolems;
        public static ConfigEntry<int> golemAmount;
        public static ConfigEntry<bool> eclipseSevenChanges;
        public static ConfigEntry<bool> slowFalseSonLaser2;
        public static ConfigEntry<bool> slowFalseSonLaser3;

        public void Awake()
        {
            Log.Init(Logger);

            monsterCredits = Config.Bind("General", "Meridian Credits", 320, "Change the amount of monster credits Prime Meridian has (450 is vanilla default)");
            lessLightning = Config.Bind("General", "Reduce Lightning Strikes", true, "Reduces amount of lightning in the Prime Meridian leadup (false is vanilla default)");
            dashSlamTime = Config.Bind("General", "Time Between Dash and Slam", 0.4f, "Idle time between the dash and slam attack. (0 will make the boss have no idle time, which is how vanilla works. Keep it between 0-1 second, or else jank will happen.)");
            eliteGolems = Config.Bind("General", "Pre Loop Elite Golems in Fight", false, "Allow golems to be elite in the fight in pre loop (true is vanilla default)");
            golemAmount = Config.Bind("General", "Max Golems in Fight", 4, "Max number of golems allowed to be spawned (5 is vanilla default)");
            eclipseSevenChanges = Config.Bind("General", "Eclipse 7 Laser/Skill Disable Changes", true, "Change the skill cooldowns to be longer for laser and skill disable attacks in Eclipse 7 (false is vanilla default)");
            slowFalseSonLaser2 = Config.Bind("General", "Slow False Son during Phase 2 Laser", true, "Gives the False Son a slowing debuff during Phase 2 laser (false is vanilla default)");
            slowFalseSonLaser3 = Config.Bind("General", "Slow False Son during Phase 3 Laser", false, "Gives the False Son a slowing debuff during Phase 3 laser (false is vanilla default)");

            On.RoR2.MeridianEventLightningTrigger.Start += (orig, self) =>
            {
                self.levelstartMonsterCredit = monsterCredits.Value;
                orig(self);
            };

            if (lessLightning.Value)
            {
                LightningStrikePattern pattern = Addressables.LoadAssetAsync<LightningStrikePattern>("RoR2/DLC2/meridian/DisableSkillsLightning/Default Lightning Pattern.asset").WaitForCompletion();
                if (pattern)
                {
                    pattern.timeBetweenIndividualStrikes = 0.6f;
                }
                else
                {
                    Log.Info("Nothin found for pattern 1");
                }

                LightningStrikePattern pattern2 = Addressables.LoadAssetAsync<LightningStrikePattern>("RoR2/DLC2/meridian/DisableSkillsLightning/Default Lightning Pattern_v2.asset").WaitForCompletion();
                if (pattern2)
                {
                    pattern2.timeBetweenIndividualStrikes = 0.6f;
                }
                else
                {
                    Log.Info("Nothin found for pattern 2");
                }
            }


            EntityStateConfiguration meridianEventPhase2 = Addressables.LoadAssetAsync<EntityStateConfiguration>
                ("RoR2/DLC2/meridian/RoR2.MeridianEventPhase2.asset")
                .WaitForCompletion();

            if (!meridianEventPhase2.TryModifyFieldValue(
                nameof(EntityStates.MeridianEvent.Phase2.endStateDelay),
                3))
            {
                Log.Error("Could not patch meridianEventPhase2.endStateDelay");
            }

            if (!meridianEventPhase2.TryModifyFieldValue(
                nameof(EntityStates.MeridianEvent.Phase2.endStateDelayTimer),
                3))
            {
                Log.Error("Could not patch meridianEventPhase2.endStateDelayTimer");
            }

            On.EntityStates.FalseSonBoss.CorruptedPathsDash.GetNextStateAuthority += CorruptedPathsDash_GetNextStateAuthority;
            On.EntityStates.FalseSonBoss.LunarGazeHoldLeap.OnEnter += LunarGazeHoldLeap_OnEnter;
            On.EntityStates.PrimeMeridian.LunarGazeLaserEnd.OnEnter += LunarGazeLaserEnd_OnEnter;

            On.RoR2.MeridianEventTriggerInteraction.Start += MeridianEventTriggerInteraction_Start;

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
            Log.Debug(self.endStateDelay);
        }
        private void Phase2_OnEnter(On.EntityStates.MeridianEvent.Phase2.orig_OnEnter orig, EntityStates.MeridianEvent.Phase2 self)
        {
            orig(self);
            this.bossPhase = MeridianEventState.Phase2;
            self.durationBeforeEnablingCombatEncounter = 0f;
            self.durationBeforeRingsSpawn = 0.5f;
            Log.Debug(self.endStateDelay);
        }
        private void Phase3_OnEnter(On.EntityStates.MeridianEvent.Phase3.orig_OnEnter orig, EntityStates.MeridianEvent.Phase3 self)
        {
            orig(self);
            this.bossPhase = MeridianEventState.Phase3;
            self.durationBeforeEnablingCombatEncounter = 0f;
            self.durationBeforeRingsSpawn = 0.5f;
            Log.Debug(self.endStateDelay);
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
            if (this.bossPhase == MeridianEventState.Phase2 && slowFalseSonLaser2.Value == true)
            {
                self.characterBody.AddBuff(RoR2Content.Buffs.Slow80);
            }
            else if (this.bossPhase == MeridianEventState.Phase3 && slowFalseSonLaser3.Value == true)
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

    public static class EntityStateConfigurationExtensions
    {
        public static bool TryModifyFieldValue<T>(this EntityStateConfiguration entityStateConfiguration, string fieldName, T value)
        {
            ref var serializedField = ref entityStateConfiguration.serializedFieldsCollection.GetOrCreateField(fieldName);
            if (serializedField.fieldValue.objectValue && typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
            {
                serializedField.fieldValue.objectValue = value as UnityEngine.Object;
                return true;
            }
            else if (serializedField.fieldValue.stringValue != null && StringSerializer.CanSerializeType(typeof(T)))
            {
                serializedField.fieldValue.stringValue = StringSerializer.Serialize(typeof(T), value);
                return true;
            }
            Debug.LogError("Failed to modify field " + fieldName);
            return false;
        }

        public static bool TryGetFieldValue<T>(this EntityStateConfiguration entityStateConfiguration, string fieldName, out T value) where T : UnityEngine.Object
        {
            ref var serializedField = ref entityStateConfiguration.serializedFieldsCollection.GetOrCreateField(fieldName);
            if (serializedField.fieldValue.objectValue && typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)))
            {
                value = (T)serializedField.fieldValue.objectValue;
                return true;
            }
            if (!string.IsNullOrEmpty(serializedField.fieldValue.stringValue))
                Debug.LogError($"Failed to return {fieldName} as an Object, try getting the string value instead.");
            else
                Debug.LogError("Field is null " + fieldName);
            value = default;
            return false;
        }
        public static bool TryGetFieldValueString<T>(this EntityStateConfiguration entityStateConfiguration, string fieldName, out T value) where T : IEquatable<T>
        {
            ref var serializedField = ref entityStateConfiguration.serializedFieldsCollection.GetOrCreateField(fieldName);
            if (serializedField.fieldValue.stringValue != null && StringSerializer.CanSerializeType(typeof(T)))
            {
                value = (T)StringSerializer.Deserialize(typeof(T), serializedField.fieldValue.stringValue);
                return true;
            }

            if (serializedField.fieldValue.objectValue)
                Debug.LogError($"Failed to return {fieldName} as a string, try getting the Object value instead.");
            else
                Debug.LogError("Field is null " + fieldName);

            value = default;
            return false;
        }
    }
}
