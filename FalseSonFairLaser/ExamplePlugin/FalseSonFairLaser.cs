using BepInEx;
using R2API;
using RoR2;
using EntityStates;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;

namespace FalseSonFairLaser
{
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class FalseSonFairLaser : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Jeffdev";
        public const string PluginName = "FalseSonFairLaser";
        public const string PluginVersion = "1.0.0";

        public static BuffDef WaitForLaser = ScriptableObject.CreateInstance<BuffDef>();

        public void Awake()
        {
            Log.Init(Logger);

            WaitForLaser.name = "dtWaitForLaser";
            WaitForLaser.buffColor = Color.black;
            WaitForLaser.canStack = false;
            WaitForLaser.isDebuff = true;
            WaitForLaser.isCooldown = false;
            WaitForLaser.isHidden = true;
            WaitForLaser.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/Common/bdSlow50.asset").WaitForCompletion().iconSprite;
            ContentAddition.AddBuffDef(WaitForLaser);

            On.EntityStates.FalseSonBoss.LunarGazeHoldLeap.OnEnter += LunarGazeHoldLeap_OnEnter;
            On.EntityStates.PrimeMeridian.LunarGazeLaserEnd.OnEnter += LunarGazeLaserEnd_OnEnter;
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        private void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            if (self.HasBuff(WaitForLaser))
            {
                self.moveSpeed = 0;
                self.armor = 2000;
            }
            orig(self);
            if (self.HasBuff(WaitForLaser))
            {
                self.moveSpeed = 0;
                self.armor = 2000;
            }
        }

        private static void LunarGazeHoldLeap_OnEnter(On.EntityStates.FalseSonBoss.LunarGazeHoldLeap.orig_OnEnter orig, EntityStates.FalseSonBoss.LunarGazeHoldLeap self)
        {
            orig(self);
            Log.Debug("Added Debuff to False Son!");
            self.characterBody.AddBuff(WaitForLaser);
        }
        private static void LunarGazeLaserEnd_OnEnter(On.EntityStates.PrimeMeridian.LunarGazeLaserEnd.orig_OnEnter orig, EntityStates.PrimeMeridian.LunarGazeLaserEnd self)
        {
            orig(self);

            // Find the False Son Boss in the scene
            foreach (CharacterBody body in CharacterBody.readOnlyInstancesList)
            {
                if (body && body.name.Contains("FalseSonBoss"))
                {
                    if (body.HasBuff(WaitForLaser))
                    {
                        body.RemoveBuff(WaitForLaser);
                        Debug.Log("Removed Debuff from False Son!");
                    }
                }
            }
        }
    }
}
