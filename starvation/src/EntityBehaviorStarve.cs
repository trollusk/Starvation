
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.Collections;
using Vintagestory.ServerMods.NoObf;
using Vintagestory.API.Config;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Vintagestory.API.Server;



namespace Starvation
{
    public class EntityBehaviorStarve : EntityBehavior
    {
        private const double DEFAULT_ENTITY_AGE = 25;
        private const double INITIAL_ENERGY_RESERVES = 0;
        private const double STARVE_THRESHOLD_MILD = -7500;
        private const double STARVE_THRESHOLD_MODERATE = -45000;
        private const double STARVE_THRESHOLD_SEVERE = -165000;
        private const double STARVE_THRESHOLD_EXTREME = -450000;
        private const double DEATH_THRESHOLD = -510000;
        long serverListenerId;
        long serverListenerSlowId;

        double heatIndexTemp = 15;      // heat index temperature at entity's current location

        // Watched Attributes are permanent entity attributes that are synced between client and server,
        // and persist across save/load.

        public double energyReserves 
        {
            get { return entity.WatchedAttributes.GetDouble("energyReserves", INITIAL_ENERGY_RESERVES); }
            set { 
                entity.WatchedAttributes.SetDouble("energyReserves", value); 
                entity.WatchedAttributes.MarkPathDirty("energyReserves");
            }
        }

        public double bodyWeight 
        {
            get { return entity.WatchedAttributes.GetDouble("bodyWeight", ModSystemStarvation.HealthyWeight(entity)); }
            set { 
                entity.WatchedAttributes.SetDouble("bodyWeight", value); 
                entity.WatchedAttributes.MarkPathDirty("bodyWeight");
            }
        }

        public double ageInYears 
        {
            get { return entity.WatchedAttributes.GetDouble("ageInYears", DEFAULT_ENTITY_AGE); }
            set { 
                entity.WatchedAttributes.SetDouble("ageInYears", value); 
                entity.WatchedAttributes.MarkPathDirty("ageInYears");
            }
        }

        public double currentMETs 
        {
            get { return entity.WatchedAttributes.GetDouble("currentMETs", 1); }
            set { 
                entity.WatchedAttributes.SetDouble("currentMETs", value); 
                entity.WatchedAttributes.MarkPathDirty("currentMETs");
            }
        }


        // This IS needed, even though it's empty
        public EntityBehaviorStarve(Entity entity) : base(entity)
        {
            //
        }


        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            // Initialise this behaviour instance's internal variables
            base.Initialize(properties, attributes);

            if (entity.World.Side == EnumAppSide.Server)
            {
                serverListenerId = entity.World.RegisterGameTickListener(ServerTick250, 250);
                serverListenerSlowId = entity.World.RegisterGameTickListener(ServerTickSlow, 5000);
                // testing
                // energyReserves = -170000;
            }
        }


        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedClientPacket(player, packetid, data, ref handled);
            if (packetid == ModSystemStarvation.PACKETID_METS)
            {
                currentMETs = SerializerUtil.Deserialize<double>(data);
                Console.WriteLine("Server received packet: mets=" + currentMETs);
            }
            handled = EnumHandling.Handled;
        }


        // Called every 5 seconds
        private void ServerTickSlow(float deltaTime)
        {
            float damageMul = DamageMultipler();

            heatIndexTemp = ModSystemStarvation.HeatIndexTemperature(ModSystemStarvation.GetTemperatureAtEntity(entity), 
                                                                     ModSystemStarvation.GetHumidityAtEntity(entity));
            
            // Set vanilla saturation to an OK value, to "deactivate" vanilla hunger system
            EntityBehaviorHunger bhunger = entity.GetBehavior<EntityBehaviorHunger>();
            // if (bhunger != null)
            // {
            //     bhunger.Saturation = bhunger.MaxSaturation * 0.75f;
            // }
            
            // Modify body weight to be in line with energy reserves
            bodyWeight = ModSystemStarvation.EnergyReservesToBMI(energyReserves) * Math.Pow(entity.Properties.EyeHeight, 2);

            // Decrease max health if starving
            EntityBehaviorHealth bh = entity.GetBehavior<EntityBehaviorHealth>();
            double healthPenalty = -1 * MaxHealthPenalty();
            bh.MaxHealthModifiers["starvationMod"] = (float) healthPenalty;
            // Remove vanilla stat boosts from food groups
            bh.MaxHealthModifiers["nutrientHealthMod"] = 0;

            // Slower health regeneration if starving (only if client player)
            if (IsSelf)
            {
                double baseRegenSpeed = entity.Api.World.Config.GetString("playerHealthRegenSpeed", "1").ToFloat();
                bh._playerHealthRegenSpeed = (float) (baseRegenSpeed * HealthRegenPenalty());
            }

            // Slower movement speed if starving
            entity.Stats.Set("walkspeed", "starvationmod", -1 * MoveSpeedPenalty(), false);

            // Weaker damage and slower mining
            entity.Stats.Set("meleeWeaponsDamage", "starvationmod", damageMul, false);
            entity.Stats.Set("rangedWeaponsDamage", "starvationmod", damageMul, false);
            entity.Stats.Set("mechanicalsDamage", "starvationmod", damageMul, false);
            entity.Stats.Set("bowDrawingStrength", "starvationmod", damageMul, false);
            entity.Stats.Set("miningSpeedMul", "starvationmod", damageMul, false);

            if (energyReserves < DEATH_THRESHOLD)
            {
                entity.Die(EnumDespawnReason.Death, 
                           new DamageSource() { Source = EnumDamageSource.Internal, Type = EnumDamageType.Hunger });
                return;
            }

            // "Intoxicated" effect if severe starvation
            if (energyReserves < STARVE_THRESHOLD_EXTREME)
            {
                entity.WatchedAttributes.SetFloat("intoxication", Math.Max(entity.WatchedAttributes.GetFloat("intoxication"), 1));
                entity.WatchedAttributes.MarkPathDirty("intoxication");
            }

            // List<string> keyList = new List<string>((entity as EntityPlayer).TpAnimManager.ActiveAnimationsByAnimCode.Keys);
            // Console.WriteLine("Animations: <" + string.Join( ", ", keyList) + ">");
            // if ((entity as EntityPlayer).AnimManager.Animator != null)
            // {
            //     foreach (var anim in (entity as EntityPlayer).AnimManager.Animator.RunningAnimations)
            //     {
            //         // RunningAnimations is an array of ALL the entity's animations
            //         if (!anim.Active) continue;
            //         Console.WriteLine("AN:" + anim.Animation.Code);
            //     }
            // }
            // if ((entity as EntityPlayer).TpAnimManager.Animator != null)
            // {
            //     foreach (var anim in (entity as EntityPlayer).TpAnimManager.Animator.RunningAnimations)
            //     {
            //         // RunningAnimations is an array of ALL the entity's animations
            //         if (!anim.Active) continue;
            //         Console.WriteLine("TP:" + anim.Animation.Code);
            //     }
            // }
            // if ((entity as EntityPlayer).OtherAnimManager.Animator != null)
            // {
            //     foreach (var anim in (entity as EntityPlayer).OtherAnimManager.Animator.RunningAnimations)
            //     {
            //         // RunningAnimations is an array of ALL the entity's animations
            //         if (!anim.Active) continue;
            //         Console.WriteLine("OT:" + anim.Animation.Code);
            //     }
            // }
        }


        // Called on server only, every 250 milliseconds (see above)
        // Note: deltaTime is in SECONDS i.e. 0.25
        private void ServerTick250(float deltaTime)
        {
            double kJPerGameDay = ModSystemStarvation.CalculateBMR(bodyWeight, ageInYears, heatIndexTemp) * currentMETs;
            double kJPerGameSecond = kJPerGameDay / 24.0 / 60.0 / 60.0;
            double gameSeconds = DeltaTimeToGameSeconds(deltaTime);

            // Exit if we are in Creative or Spectator game modes
            if (entity is EntityPlayer && IsSelf)
            {
                EntityPlayer plr = entity as EntityPlayer;
                EnumGameMode mode = entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode;

                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) return;
            }

            // We have expended kJPerSecond * gameSeconds (kJ)
            // Decrement our total energy stores by this amount
            energyReserves = energyReserves - (kJPerGameSecond * gameSeconds);

            // Console.WriteLine("ServerTick250: speedoftime=" + entity.World.Calendar.SpeedOfTime + ", speedmul=" + entity.World.Calendar.CalendarSpeedMul + ", currentMETs = " + currentMETs + ", BMR = " + (kJPerGameDay/currentMETs) + ", energyReserves = " + energyReserves + 
            //                     ", kJPerGameSecond = " + kJPerGameSecond + ", deltaTime = " + deltaTime + ", gameSeconds = " + 
            //                     gameSeconds + ", energy decrement = " + (kJPerGameSecond * gameSeconds));
        }


        // Fires when entity receives saturation (nutrition)
        //  saturation = amount of "saturation" received
        //  foodCat = category of food
        //  saturationLossDelay = delay before saturation begins to decrement
        //  nutritionGainMultiplier = ?
        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, 
                                                       float saturationLossDelay = 10f, float nutritionGainMultiplier = 1f)
        {
            // vanilla game saturation/satiety is meant to "correlate" with calories/kilojoules
            // It doesn't really, but a very rough approximation is saturation = 2 * kJ

            // Unfortunately "fat" is not a food category in VS, so we use "Dairy" instead.

            energyReserves += ModSystemStarvation.CaloriesToKilojoules(0.5 * saturation);
        }


        // Fires when entity dies
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // Set a lower cap for energyReserves, to avoid being stuck in a "death loop"
            energyReserves = Math.Max(STARVE_THRESHOLD_EXTREME, energyReserves);
        }

        // Return true if this entity is the player controlled by the client.
        bool IsSelf => entity?.WatchedAttributes.GetString("playerUID") == ModSystemStarvation.clientAPI?.Settings.String["playeruid"];

        // Returns number which is subtracted from max health
        public double MaxHealthPenalty() 
        {
            return energyReserves switch
            {
                > STARVE_THRESHOLD_MILD                                     => 0,           // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE    => 2,           // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE  => 4,         // moderate
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME   => 7,         // severe
                _                                                           => 12,         // extreme
            };
        } 


        // Returns number which health regen speed (default = 1) is multiplied by.
        public double HealthRegenPenalty() 
        {
            return energyReserves switch
            {
                > STARVE_THRESHOLD_MILD                                     => 1,           // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE    => 1,           // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE  => 0.5,         // moderate
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME   => 0,         // severe
                _                                                           => 0,         // extreme
            };
        } 


        // Returns number which damage and mining speed (default = 1) is multiplied by.
        public float DamageMultipler() 
        {
            return energyReserves switch
            {
                > STARVE_THRESHOLD_MILD                                     => 1,           // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE    => 1,           // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE  => 0.7f,         // moderate
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME   => 0.5f,         // severe
                _                                                           => 0.4f,         // extreme
            };
        } 


        // Returns number which is subtracted from movement speed. 1 = normal movement speed.
        public float MoveSpeedPenalty() 
        {
            EntityBehaviorHunger bhunger = entity.GetBehavior<EntityBehaviorHunger>();

            float debuff = energyReserves switch
            {
                > STARVE_THRESHOLD_MILD                                     => 0,           // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE    => 0,           // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE  => 0.2f,         // moderate
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME   => 0.4f,         // severe
                _                                                           => 0.6f,         // extreme
            };

            if (bhunger.SaturationLossDelayDairy > 0 || bhunger.SaturationLossDelayFruit > 0 || bhunger.SaturationLossDelayGrain > 0 
                || bhunger.SaturationLossDelayProtein > 0 || bhunger.SaturationLossDelayVegetable > 0)
            {
                // Reduce the movement speed debuff temporarily while satiated
                debuff *= 0.5f;
            }
            return debuff;
        } 


        public static string HungerText(double energy)
        {
            return energy switch
            {
                > 0                                                         => "Satiated",
                > STARVE_THRESHOLD_MILD                                     => "Hungry",           // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE    => "Desperate For Food",           // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE  => "Starving!",         // moderate
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME   => "Severe starvation!",         // severe
                _                                                           => "EXTREME STARVATION!",         // extreme
            };
        }


        // Returns number of game seconds represented by deltaTime (real world SECONDS)
        private double DeltaTimeToGameSeconds(double deltaTime)
        {
            return deltaTime * entity.World.Calendar.SpeedOfTime * entity.World.Calendar.CalendarSpeedMul;
        }


        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (serverListenerId != 0) {
                entity.World.UnregisterGameTickListener(serverListenerId);
                entity.World.UnregisterGameTickListener(serverListenerSlowId);
            }
                
        }


        // Return the short string alias for this mod
        public override string PropertyName()
        {
            return "starve";
        }
    }

}
