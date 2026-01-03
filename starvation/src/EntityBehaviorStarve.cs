using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using System.Collections;
using Vintagestory.ServerMods.NoObf;
using Vintagestory.API.Config;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Vintagestory.API.Server;
using System.Runtime.InteropServices;



namespace Starvation
{
    public class EntityBehaviorStarve : EntityBehavior
    {
        private const double DEFAULT_ENTITY_AGE = 25;
        private const double DEFAULT_BODY_WEIGHT = 65;
        private const double INITIAL_ENERGY_RESERVES = 6000;
        private const double MAX_ENERGY_RESERVES = 12000.0; // Energy level beyond this point is just excreted. Functions as a hard cap but should only under extreme conditions be needed (for example eating highest density food as much as possible over long time).
        private const double ENERGY_TO_FAT_AT_MAX_RATE = 10000.0; // Energy level above this point triggers max rate energy turned into fat and excreted or burned to body heat. Functions as a soft cap.
        private const double ENERGY_TO_FAT_START = 7000.0; // Energy level above this point is with increasing energy Storage increasingly fast turned into fat until max rate then constant at max rate
        private const double PROTEIN_TO_ENERGY_START = 4000.0; // Energy level which triggers start of fat to energy conversion increasing with Energy level decreasing until no (Glyco-)Energy Reserve is left.
        private const double FAT_TO_ENERGY_START = 2000.0; // Energy level which triggers start of fat to energy conversion increasing with Energy level decreasing until no (Glyco-)Energy Reserve is left.
        private const double ENERGY_DANGEROUS_LOW = 500.0; // Energy level which triggers life saving mechanisms sending signals of stop to energy consumers but the brain.
        private const double MAX_GASTROINTESTINAL_RESERVES = 6.6; // 1500 * 6.6 ~= 10000 (kj) translates max hungerbar to gastrointestinal reserves. This supports change of max hungerbar value from 1500 to something else.
        private const double INITIAL_GASTROINTESTINAL_RESERVES = 5000.0; // Amount of kilojoules worth of food in the gastrointernal system which fills Energy Reserve at given rate at the start.
        private const double GASTROINTESTINAL_RESERVES_TO_ENERGY_RATE = 80.0; // how fast kilojoules from food in gastrointernals are turned into energy reserves (per 5 seconds).
        private const double METABOLISM_ENERGYTOFAT_SPEED_FACTOR = 1.0; // how fast energy is converted to weight vice versa
        private const double METABOLSIM_INCREASE_ENERGY_TO_HEAT_FACTOR = 1.1; // how much faster energy is consumed when metabolism enters fat buildup.
        private const double METABOLSIM_SLOWDOWN_FASTENING_FACTOR = 0.8; // how much slower energy is consumed when no energy reserves are left (and first slight negative effects occur).
        private const double ENERGY_TO_FAT_MAX_RATE = 50.85; // assumes to be applied every 5 seconds assumes 1 kg = 29288 kJ per day can be turned to fat, day has 48 minutes => 29288 * 5 / 48*60 = 50,85
        private const double PROTEIN_TO_ENERGY_MAX_RATE = 5.21; // assumes to be applied every 5 seconds assumes 12000 kJ worth fat be turned to energy per day, protein (first stage) is 0.25 of that day has 48 minutes => 12000 * 5 * 0.25 / 48*60 = 5,21
        private const double FAT_TO_ENERGY_MAX_RATE = 15.63; // // assumes to be applied every 5 seconds assumes 12000 kJ worth fat be turned to energy per day, fat and protein (second stage) is 0.75 of that day has 48 minutes => 12000 * 5 * 0.75 / 48*60 = 15,63
        private const double KJOULE_TO_KGFAT_CONVERSION_FACTOR = 1.0 / 29288.0;
        private const double KGFAT_TO_KJOULE_CONVERSION_FACTOR = 29288.0;
        private const double EXCR_ENERGY_TO_HEAT = 50.0; // this is supposed to soft cap energyGain and should be higher than GASTROINTESTINAL_RESERVES_TO_ENERGY_RATE - ENERGY_TO_FAT_MAX_RATE
        //Starvation depends on fat and muscle reserves (weight) and energyReserves instead of negative energy reserves
        private const double OVERWEIGHT_DEATHCHANCE_THRESHOLD = 150;
        private const double OVERWEIGHT_THRESHOLD_EXTREME = 110;
        private const double OVERWEIGHT_THRESHOLD_SEVERE = 100;
        private const double OVERWEIGHT_THRESHOLD_MODERATE = 90;
        private const double OVERWEIGHT_THRESHOLD_MILD = 80;
        private const double STARVE_THRESHOLD_MILD = 60;
        private const double STARVE_THRESHOLD_MODERATE = 55;
        private const double STARVE_THRESHOLD_SEVERE = 45;
        private const double STARVE_THRESHOLD_EXTREME = 35;
        private const double DEATH_THRESHOLD = 30;
        private const float COMPAT_LOWEST_HUNGERBARVALUE = 500f;
        private const double DAY_TO_GAMEQUARTERSECOND = 8.68e-5; //1 / (4.0 * 60.0 * 48.0);
        private const double FIVE_SECONDS_TO_QUARTERSECOND = 0.05; // 0.25 / 5 -> Multiply work done in 5s to get work that must be done in a quartersecond to equal that.

        long serverListenerId;
        long serverListenerSlowId;

        double timeSpeedMult = 1.0;
        double currentBMR = 6000;
        float gameHungerSpeed = 1.0f;        // default = 1
        double dayToGameQuarter = DAY_TO_GAMEQUARTERSECOND;
        double energyGainPerQuarterSecond = 0.0; //gain from digestation or metabolism to be applied at the fast ticking callback
        double heatIndexTemp = 15;      // heat index temperature at entity's current location
        double metabolismFactor = 1.0; // used to change energy usage depending on energy reserves
        double energyLevelPenalty = 1.0;
        double maxGastrointestinalReserves = MAX_GASTROINTESTINAL_RESERVES * 1500.0;
        Boolean playerBlackedOut = false;

        EnergyReserveLevel energyReserveLevel = EnergyReserveLevel.MEDIUM;

        // Watched Attributes are permanent entity attributes that are synced between client and server,
        // and persist across save/load.

        public double energyReserves
        {
            get => entity.WatchedAttributes.GetDouble("energyReserves", INITIAL_ENERGY_RESERVES);
            set
            {
                entity.WatchedAttributes.SetDouble("energyReserves", value);
                entity.WatchedAttributes.MarkPathDirty("energyReserves");
            }
        }

        public double bodyWeight
        {
            get =>
                entity.WatchedAttributes.GetDouble(
                    "bodyWeight",
                    ModSystemStarvation.HealthyWeight(entity)
                );
            set
            {
                entity.WatchedAttributes.SetDouble("bodyWeight", value);
                entity.WatchedAttributes.MarkPathDirty("bodyWeight");
            }
        }

        public double ageInYears
        {
            get => entity.WatchedAttributes.GetDouble("ageInYears", DEFAULT_ENTITY_AGE);
            set
            {
                entity.WatchedAttributes.SetDouble("ageInYears", value);
                entity.WatchedAttributes.MarkPathDirty("ageInYears");
            }
        }

        public double currentMETs
        {
            get => entity.WatchedAttributes.GetDouble("currentMETs", 1);
            set
            {
                entity.WatchedAttributes.SetDouble("currentMETs", value);
                entity.WatchedAttributes.MarkPathDirty("currentMETs");
            }
        }
        public double gastrointestinalReserves
        {
            get { return entity.WatchedAttributes.GetDouble("gastrointestinalReserves", INITIAL_GASTROINTESTINAL_RESERVES); }
            set
            {
                entity.WatchedAttributes.SetDouble("gastrointestinalReserves", value);
                entity.WatchedAttributes.MarkPathDirty("gastrointestinalReserves");
            }
        }


        // This IS needed, even though it's empty
        public EntityBehaviorStarve(Entity entity)
            : base(entity)
        {
            //
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            // Initialise this behaviour instance's internal variables
            base.Initialize(properties, attributes);

            if (entity.World.Side == EnumAppSide.Server)
            {
                serverListenerId = entity.World.RegisterGameTickListener(_ServerTick250, 250, 2000);
                serverListenerSlowId = entity.World.RegisterGameTickListener(
                    _ServerTickSlow,
                    5000,
                    2000
                );
            }
        }

        public override void OnReceivedClientPacket(
            IServerPlayer player,
            int packetid,
            byte[] data,
            ref EnumHandling handled
        )
        {
            base.OnReceivedClientPacket(player, packetid, data, ref handled);
            if (packetid == ModSystemStarvation.PACKETID_METS)
            {
                currentMETs = SerializerUtil.Deserialize<double>(data);
            }
            handled = EnumHandling.Handled;
        }

        // Called every 5 seconds
        private void _ServerTickSlow(float _)
        {
            float damageMul = DamageMultipler();

            heatIndexTemp = ModSystemStarvation.HeatIndexTemperature(ModSystemStarvation.GetTemperatureAtEntity(entity),
                                                                     ModSystemStarvation.GetHumidityAtEntity(entity));

            // Set vanilla saturation to an OK value, to "deactivate" vanilla hunger system
            ResetHunger();

            // Modify body weight to be in line with energy reserves
            bodyWeight = ModSystemStarvation.EnergyReservesToBMI(energyReserves) * Math.Pow(entity.Properties.EyeHeight, 2);

            // Decrease max health if starving - disabled because of (potential) mod conflicts
            EntityBehaviorHealth bh = entity.GetBehavior<EntityBehaviorHealth>();
            //double healthPenalty = -1 * MaxHealthPenalty();
            //bh.MaxHealthModifiers["starvationMod"] = (float) healthPenalty;
            // Remove vanilla stat boosts from food groups // why disable nutrition bonus?
            //bh.MaxHealthModifiers["nutrientHealthMod"] = 0;

            // Slower health regeneration if starving (only if client player)
            if (IsSelf)
            {
                double baseRegenSpeed = entity
                    .Api.World.Config.GetString("playerHealthRegenSpeed", "1")
                    .ToFloat();
                entity.WatchedAttributes.SetFloat(
                    "regenSpeed",
                    (float)(baseRegenSpeed * HealthRegenPenalty())
                );
                double baseRegenSpeed = entity.Api.World.Config.GetString("playerHealthRegenSpeed", "1").ToFloat();
                entity.WatchedAttributes.SetFloat("regenSpeed", (float)(baseRegenSpeed * HealthRegenPenalty()));
            }

            // Slower movement speed if starving
            entity.Stats.Set("walkspeed", "starvationmod", -1 * MoveSpeedPenalty(), false);

            float damageMul = DamageMultipler();
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
            if (bodyWeight < STARVE_THRESHOLD_EXTREME || energyReserveLevel <= EnergyReserveLevel.ZERO)
            {
                entity.WatchedAttributes.SetFloat(
                    "intoxication",
                    Math.Max(entity.WatchedAttributes.GetFloat("intoxication"), 1)
                );
                entity.WatchedAttributes.MarkPathDirty("intoxication");
            }
        }

        // Called on server only, every 250 milliseconds (see above)
        // Note: deltaTime is in SECONDS i.e. 0.25
        private void _ServerTick250(float deltaTime)
        {
            double kJPerGameDay =
                ModSystemStarvation.CalculateBMR(bodyWeight, ageInYears, heatIndexTemp)
                * currentMETs;
            double kJPerGameSecond = kJPerGameDay / 24.0 / 60.0 / 60.0;
            double gameSeconds = _DeltaTimeToGameSeconds(deltaTime);
            float gameHungerSpeed = GlobalConstants.HungerSpeedModifier; // default = 1

            // Exit if we are in Creative or Spectator game modes
            if (entity is EntityPlayer && IsSelf)
            {
                EntityPlayer plr = entity as EntityPlayer;
                EnumGameMode mode = entity
                    .World.PlayerByUid(plr.PlayerUID)
                    .WorldData.CurrentGameMode;
                if (mode is EnumGameMode.Creative or EnumGameMode.Spectator)
                    return;
            }


            // Multiply base rate BMR with dynamic factor MET and break it down to a GameDays Quarter Second. BMR is calculated in client side half second callback and BMR in server side 5 second callback.
            // GameDayToQuarter consists of a breakdown constant (GAMEDAY_TOQUARTERSECOND) which assumes vanilla time for a day and the world hunger speed setting (GlobalConstants.HungerSpeedModifier)
            double kJPerDay = currentBMR * currentMETs;
            double kJPerGameQuarterSecond = kJPerDay * dayToGameQuarter; //gameDayToQuarter is calculated in the slow ticking callback. Could be changed to GlobalConstants.HungerSpeedModifier

            // We have expended kJPerGameQuarterSecond * metabolismFactor) + energyGain
            // Decrement our total energy stores by this amount
            if (timeSpeedMult > 5.0) // expects night speed mult beeing larger 5.0
            {
                Metabolism(true);
                ResetHunger();
                CalculateEnergyModAndPenalty();

                energyReserves = energyReserves - (kJPerGameQuarterSecond * metabolismFactor - energyGainPerQuarterSecond) * timeSpeedMult; // reserves - steady usage + energy gain from digestation or metabolism (which is calculated on 5s base while this is applied 20 times as often->so /20.)
            } else //depend on the calculation in the slow ticking callback for better performance
            {
                energyReserves = energyReserves - (kJPerGameQuarterSecond * metabolismFactor - energyGainPerQuarterSecond); // reserves - steady usage + energy gain from digestation or metabolism (which is calculated on 5s base while this is applied 20 times as often->so /20.)
            }


            if (energyReserves < 0.0) energyReserves = 0.0; //prevent underflow

        }

        // Fires when entity receives saturation (nutrition)
        //  saturation = amount of "saturation" received
        //  foodCat = category of food
        //  saturationLossDelay = delay before saturation begins to decrement
        //  nutritionGainMultiplier = ?
        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown,
                                                       float saturationLossDelay = 10f, float nutritionGainMultiplier = 1f)
        {
            if (gastrointestinalReserves > maxGastrointestinalReserves)
            {
                return; // can't take anymore body will puke it out. Thats what you get from excessive eating, jerk.
            }
            // vanilla game saturation/satiety is meant to "correlate" with calories/kilojoules
            // It doesn't really, but a very rough approximation is saturation = 2 * kJ
            gastrointestinalReserves += ModSystemStarvation.CaloriesToKilojoules(0.5 * saturation);
            ResetHunger();
        }

        // Fires when entity dies
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // Set a lower cap for energyReserves, to avoid being stuck in a "death loop"
            bodyWeight = Math.Max(STARVE_THRESHOLD_SEVERE, bodyWeight);
            energyReserves = INITIAL_ENERGY_RESERVES;
            gastrointestinalReserves = INITIAL_GASTROINTESTINAL_RESERVES;
            ResetHunger();
        }

        // Return true if this entity is the player controlled by the client.
        bool IsSelf => entity?.WatchedAttributes.GetString("playerUID") == ModSystemStarvation.clientAPI?.Settings.String["playeruid"];

        /* Sadly these dont work(?) as intended and need to be properly done
        private void Blackout()
        {
            if (!playerBlackedOut)
            {
                entity.AnimManager.ActiveAnimationsByAnimCode.Clear();
                entity.AnimManager.StartAnimation("die");
                playerBlackedOut = true;
            }
        }
        private void RecoverBlackout()
        {
            if (playerBlackedOut)
            {
                entity.AnimManager.StopAnimation("die");
                playerBlackedOut = false;
            }
        }*/

        // Lower limit for satieaty to prevent damage by hunger from base game
        // The value must be less than MaxSaturation, to allow meals to be consumed.
        private void ResetHunger()
        {
            EntityBehaviorHunger bhunger = entity.GetBehavior<EntityBehaviorHunger>();
            if (bhunger != null)
            {
                bhunger.Saturation = 100;
            }
        }


        // Returns number which is subtracted from max health
        public double MaxHealthPenalty()
        {
            return bodyWeight switch
            {
                > STARVE_THRESHOLD_MILD => 0, // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE => 2, // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE => 4, // moderate
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME => 7, // severe
                _ => 12, // extreme
            };
        }

        // Returns number which health regen speed (default = 1) is multiplied by.
        public double HealthRegenPenalty()
        {
            if (energyReserves > 0) return 1; //no negative effects when satiated
            return bodyWeight switch
            {
                > STARVE_THRESHOLD_MILD => 1, // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE => 1, // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE => 0.5, // moderate
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME => 0, // severe
                _ => 0, // extreme
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

            float debuff = bodyWeight switch
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


        public static HungerLevel WeightToHungerLevel(double weight)
        {
            return weight switch
            {
                > STARVE_THRESHOLD_MILD                                                                     => HungerLevel.Satiated,           // not starving
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE                                    => HungerLevel.Mild,           // mild, a few days
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE                                  => HungerLevel.Moderate,         // moderate
                <= STARVE_THRESHOLD_SEVERE and > 0.5*(STARVE_THRESHOLD_SEVERE+STARVE_THRESHOLD_EXTREME)     => HungerLevel.Severe,         // severe
                <= 0.5 * (STARVE_THRESHOLD_SEVERE + STARVE_THRESHOLD_EXTREME) and > STARVE_THRESHOLD_EXTREME => HungerLevel.VerySevere,
                <= STARVE_THRESHOLD_EXTREME                                                                 => HungerLevel.Extreme,         // extreme
                _ => HungerLevel.Extreme
            };
        }

        public static EnergyReserveLevel EnergyToReserveLevel(double energyReserves)
        {
            return energyReserves switch
            {
                > ENERGY_TO_FAT_AT_MAX_RATE                             =>  EnergyReserveLevel.VERY_HIGH,
                > ENERGY_TO_FAT_START and <= ENERGY_TO_FAT_AT_MAX_RATE  =>  EnergyReserveLevel.High,
                >= PROTEIN_TO_ENERGY_START and <= ENERGY_TO_FAT_START   =>  EnergyReserveLevel.MEDIUM,
                < PROTEIN_TO_ENERGY_START and >= FAT_TO_ENERGY_START    =>  EnergyReserveLevel.LOW,
                < FAT_TO_ENERGY_START and >= ENERGY_DANGEROUS_LOW       =>  EnergyReserveLevel.VERY_LOW,
                < ENERGY_DANGEROUS_LOW and >= 1.0                       =>  EnergyReserveLevel.MINIMAL,
                < 1.0                                                   =>  EnergyReserveLevel.ZERO,
                _                                                       =>  EnergyReserveLevel.MEDIUM
            };
        }

        // Returns number of game seconds represented by deltaTime (real world SECONDS)
        private double _DeltaTimeToGameSeconds(double deltaTime)
        {
            return deltaTime
                * entity.World.Calendar.SpeedOfTime
                * entity.World.Calendar.CalendarSpeedMul;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (serverListenerId != 0)
            {
                entity.World.UnregisterGameTickListener(serverListenerId);
                entity.World.UnregisterGameTickListener(serverListenerSlowId);
            }
        }

        // Return the short string alias for this mod
        public override string PropertyName()
        {
            return "starvation";
        }
    }
}
