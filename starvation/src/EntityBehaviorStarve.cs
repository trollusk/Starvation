
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
                serverListenerId = entity.World.RegisterGameTickListener(ServerTick250, 250, 2000);
                serverListenerSlowId = entity.World.RegisterGameTickListener(ServerTickSlow, 5000, 2000);
            }
        }


        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedClientPacket(player, packetid, data, ref handled);
            if (packetid == ModSystemStarvation.PACKETID_METS)
            {
                currentMETs = SerializerUtil.Deserialize<double>(data);
            }
            handled = EnumHandling.Handled;
        }


        // Called every 5 seconds
        private void ServerTickSlow(float deltaTime)
        {
            if(GlobalConstants.HungerSpeedModifier<0.0f) gameHungerSpeed = GlobalConstants.HungerSpeedModifier; // update in case it is changed at runtime
            else gameHungerSpeed = 1.0f;
            timeSpeedMult = entity.World.Calendar.SpeedOfTime / 60.0;
            //[BUGFIX] Regarding getRainfallAtEntity error: Quick and dirty fix for humidity would be to not call this function at all but just give it a value here (like 0)
            heatIndexTemp = ModSystemStarvation.HeatIndexTemperature(ModSystemStarvation.GetTemperatureAtEntity(entity), 
                                                                     ModSystemStarvation.GetHumidityAtEntity(entity));
            currentBMR = ModSystemStarvation.CalculateBMR(bodyWeight, ageInYears, heatIndexTemp);
            dayToGameQuarter = DAY_TO_GAMEQUARTERSECOND * gameHungerSpeed; //this may be updated to respect changed gameHungerSpeed on runtime. It is used in the quarter second server callback.
            if (timeSpeedMult <= 5.0) // if not night
            {
                // Ensure sync between energy reserves and hunger bar and prevent going into critical low causing unwanted negative effects
                
                // Apply fat to energy or energy to fat and other metabolism effects
                Metabolism();
                ResetHunger();
                CalculateEnergyModAndPenalty();
            } else
            {
                Console.WriteLine("MOD_STARVATION: It is Night with speed mult: ", timeSpeedMult);
            }

            /* NOT WORKING TODO - punish for using up all energyReserves -> blackout to preserve brain function.
            if (energyReserveLevel == EnergyReserveLevel.ZERO) Blackout(); 
            if (playerBlackedOut && energyReserveLevel > EnergyReserveLevel.VERY_LOW) RecoverBlackout();
            */

            // Decrease max health if starving - disabled because of (potential) mod conflicts
            EntityBehaviorHealth bh = entity.GetBehavior<EntityBehaviorHealth>();
            //double healthPenalty = -1 * MaxHealthPenalty();
            //bh.MaxHealthModifiers["starvationMod"] = (float) healthPenalty;
            // Remove vanilla stat boosts from food groups // why disable nutrition bonus?
            //bh.MaxHealthModifiers["nutrientHealthMod"] = 0;

            // Slower health regeneration if starving (only if client player)
            if (IsSelf)
            {
                double baseRegenSpeed = entity.Api.World.Config.GetString("playerHealthRegenSpeed", "1").ToFloat();
                bh._playerHealthRegenSpeed = (float) (baseRegenSpeed * HealthRegenPenalty());
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

            if (bodyWeight < DEATH_THRESHOLD || bodyWeight > OVERWEIGHT_DEATHCHANCE_THRESHOLD)
            {
                Random random = new Random();
                int rand = random.Next(0, 100);
                if (rand < 1) // One Percent Chance to die (every 5 Seconds)
                {
                    entity.Die(EnumDespawnReason.Death,
                               new DamageSource() { Source = EnumDamageSource.Internal, Type = EnumDamageType.Hunger });
                }
                return;
            }

            // "Intoxicated" effect if severe starvation
            if (bodyWeight < STARVE_THRESHOLD_EXTREME || energyReserveLevel <= EnergyReserveLevel.ZERO)
            {
                entity.WatchedAttributes.SetFloat("intoxication", Math.Max(entity.WatchedAttributes.GetFloat("intoxication"), 1));
                entity.WatchedAttributes.MarkPathDirty("intoxication");
            }

        }


        // Called on server only, every 250 milliseconds (see above)
        // Note: deltaTime is in SECONDS i.e. 0.25
        private void ServerTick250(float deltaTime)
        {
            // Exit if we are in Creative or Spectator game modes
            if (entity is EntityPlayer && IsSelf)
            {
                EntityPlayer plr = entity as EntityPlayer;
                EnumGameMode mode = entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode;

                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) return;
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
                maxGastrointestinalReserves = bhunger.MaxSaturation * MAX_GASTROINTESTINAL_RESERVES; // Calc for new maxsaturation. May be something else than 1500 (like through skills mod)
                if (gastrointestinalReserves > maxGastrointestinalReserves) bhunger.Saturation = bhunger.MaxSaturation;
                else
                {
                    bhunger.Saturation = (float)LinearPosition(0.0, maxGastrointestinalReserves, gastrointestinalReserves) 
                        * (bhunger.MaxSaturation - COMPAT_LOWEST_HUNGERBARVALUE) 
                        + COMPAT_LOWEST_HUNGERBARVALUE;
                }
                if (bhunger.Saturation < COMPAT_LOWEST_HUNGERBARVALUE) bhunger.Saturation = COMPAT_LOWEST_HUNGERBARVALUE;
            }
        }
        /*  Metabolism - expects to be called in the slow ticking (5s) callback. Only in the night it is called in the fast 0,25s callback when time is sped up.
         *  Updates the energyGain depending on energylevel and digestation to be used in the quartersecond callback. Updates AND applies weight gain/loss.
            */
        private void Metabolism(Boolean night=false)
        {
            double energyGainPer5Seconds = 0.0;
            double fatGain = 0;
            double energyToHeat;

            // catch special case
            if ((int) energyReserves > (int) MAX_ENERGY_RESERVES) energyReserves = MAX_ENERGY_RESERVES; //absolute limit

            double metabolismRate = entity.Stats.GetBlended("hungerrate");

            //calculate heat<-energy<->fat
            energyReserveLevel = EnergyToReserveLevel(energyReserves);
            switch (energyReserveLevel){
                case EnergyReserveLevel.VERY_HIGH:
                    double betweenAbsAndSoftLimit = LinearPosition(ENERGY_TO_FAT_AT_MAX_RATE, MAX_ENERGY_RESERVES, energyReserves);
                    energyToHeat = betweenAbsAndSoftLimit * EXCR_ENERGY_TO_HEAT;
                    energyGainPer5Seconds = -(ENERGY_TO_FAT_MAX_RATE + 2 * energyToHeat); //2 * energy to heat simulates 1 * energy to heat + 1 * excreted energy with the same value
                    fatGain = ENERGY_TO_FAT_MAX_RATE;
                    break;
                case EnergyReserveLevel.High:
                    fatGain = LinearPosition(ENERGY_TO_FAT_START, ENERGY_TO_FAT_AT_MAX_RATE, energyReserves) * ENERGY_TO_FAT_MAX_RATE;
                    energyToHeat = fatGain * 0.1; //takes extra energy converting energy to fat
                    energyGainPer5Seconds = -(fatGain + energyToHeat);
                    break;
                case EnergyReserveLevel.MEDIUM:
                    break;
                case EnergyReserveLevel.LOW:
                    energyGainPer5Seconds = metabolismRate * (LinearPosition(FAT_TO_ENERGY_START, PROTEIN_TO_ENERGY_START, energyReserves, true) * PROTEIN_TO_ENERGY_MAX_RATE );
                    fatGain = -energyGainPer5Seconds;
                    break;
                case EnergyReserveLevel.VERY_LOW or EnergyReserveLevel.MINIMAL or EnergyReserveLevel.ZERO:
                    energyGainPer5Seconds = metabolismRate * (LinearPosition(0.0, FAT_TO_ENERGY_START, energyReserves, true) * FAT_TO_ENERGY_MAX_RATE + PROTEIN_TO_ENERGY_MAX_RATE );
                    fatGain = -energyGainPer5Seconds;
                    break;
            };

            //Digestation
            if ((int)gastrointestinalReserves > 0)
            {
                double digestRate = 0.5 * GASTROINTESTINAL_RESERVES_TO_ENERGY_RATE * Math.Min(LinearPosition(0.0, maxGastrointestinalReserves, gastrointestinalReserves), 1.0) 
                    + 0.5 * GASTROINTESTINAL_RESERVES_TO_ENERGY_RATE;

                if( (!night && digestRate > gastrointestinalReserves) || (night && digestRate * timeSpeedMult * FIVE_SECONDS_TO_QUARTERSECOND > gastrointestinalReserves)){
                    digestRate = gastrointestinalReserves;
                }

                if (night) gastrointestinalReserves -= digestRate * timeSpeedMult * FIVE_SECONDS_TO_QUARTERSECOND;
                else gastrointestinalReserves -= digestRate;

                energyGainPer5Seconds += digestRate;
            }
            //Converts fat gain from Energy to Kg and applies it and respects global setting gameHungerSpeed.
            if (night) bodyWeight += gameHungerSpeed * fatGain * KJOULE_TO_KGFAT_CONVERSION_FACTOR * timeSpeedMult * FIVE_SECONDS_TO_QUARTERSECOND;
            else bodyWeight += gameHungerSpeed * fatGain * KJOULE_TO_KGFAT_CONVERSION_FACTOR;
            //[OPTION] energyReserves = energyReserves + gameHungerSpeed * energyGainPer5Seconds; //Alternatively to applying energyGain in the quarterseconds callback it could be done here
            //convert for Quarterseconds and respect global setting gameHungerSpeed.
            energyGainPerQuarterSecond = gameHungerSpeed * energyGainPer5Seconds * FIVE_SECONDS_TO_QUARTERSECOND;  
            //[WIP] call player heat = player heat + energyToHeat * SomeFactor to implement heat gain. //problem is I dont know the correct way to do this and the usage of this feature would be limited anyway
        }
        //Expects that x is between lower and higher. Gives a value between 0 and 1, which tells where the input x is between lower and higher. Reverse for a value that grows to 1 when closer to lower.
        static double LinearPosition(double lower, double higher, double x, bool reverse=false)
        {
            return (reverse? (1.0 - (x - lower) / (higher - lower)) : ((x - lower) / (higher - lower)));
        }
        private void CalculateEnergyModAndPenalty()
        {
            switch (energyReserveLevel)
            {
                case EnergyReserveLevel.VERY_HIGH:
                    energyLevelPenalty = 1.0;
                    metabolismFactor = 1.2;
                    break;
                case EnergyReserveLevel.High:
                    energyLevelPenalty = 1.0;
                    metabolismFactor = 1.1;
                    break;
                case EnergyReserveLevel.LOW:
                    energyLevelPenalty = 1.0;
                    metabolismFactor = 0.9;
                    break;
                case EnergyReserveLevel.VERY_LOW:
                    energyLevelPenalty = 0.8;
                    metabolismFactor = 0.8;
                    break;
                case EnergyReserveLevel.MINIMAL:
                    energyLevelPenalty = 0.3;
                    metabolismFactor = 0.5;
                    break;
                case EnergyReserveLevel.ZERO:
                    energyLevelPenalty = 0.1;
                    metabolismFactor = 0.2;
                    break;
                default:
                    energyLevelPenalty = 1.0;
                    metabolismFactor = 1.0;
                    break;
            }
        }
        // Returns number which is subtracted from max health
        public double MaxHealthPenalty() 
        {
            return bodyWeight switch
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
            if (energyReserves > 0) return 1; //no negative effects when satiated
            return bodyWeight switch
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
                float damageMultipler = bodyWeight switch
                {
                    <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME => 0.5f,          //severe underweight
                    <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE => 0.7f,         // moderate
                    <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE => 0.8f,           // mild underweight
                    < OVERWEIGHT_THRESHOLD_MODERATE and > STARVE_THRESHOLD_MILD => 1.0f,        // healthy weight
                    >= OVERWEIGHT_THRESHOLD_MODERATE and < OVERWEIGHT_THRESHOLD_SEVERE => 0.9f, //overweight - bit less damage
                    >= OVERWEIGHT_THRESHOLD_SEVERE and < OVERWEIGHT_THRESHOLD_EXTREME => 0.7f, //serious overweight - reduced damage
                    >= OVERWEIGHT_THRESHOLD_EXTREME and < OVERWEIGHT_DEATHCHANCE_THRESHOLD => 0.3f, //extreme overweight - greatly reduced damage
                    >= OVERWEIGHT_DEATHCHANCE_THRESHOLD or <= STARVE_THRESHOLD_EXTREME => 0.1f, //death risky weight - nearly no damage
                    _ => 1.0f, //else case for unexpected events
                };
            if (energyReserves <= 0) damageMultipler = damageMultipler * (float) energyLevelPenalty;
            return damageMultipler;
        } 


        // Returns number which is subtracted from movement speed. 1 = normal movement speed.
        public float MoveSpeedPenalty() 
        {
            EntityBehaviorHunger bhunger = entity.GetBehavior<EntityBehaviorHunger>();

            float debuff = bodyWeight switch
            {
                <= STARVE_THRESHOLD_EXTREME and DEATH_THRESHOLD => 0.8f,                    //extreme underweight
                <= STARVE_THRESHOLD_SEVERE and > STARVE_THRESHOLD_EXTREME => 0.4f,          //severe underweight
                <= STARVE_THRESHOLD_MODERATE and > STARVE_THRESHOLD_SEVERE => 0.1f,         // strong underweight
                <= STARVE_THRESHOLD_MILD and > STARVE_THRESHOLD_MODERATE => 0.0f,           // mild underweight
                < OVERWEIGHT_THRESHOLD_MILD and > STARVE_THRESHOLD_MILD => 0.0f,        // healthy weight
                >= OVERWEIGHT_THRESHOLD_MILD and < OVERWEIGHT_THRESHOLD_MODERATE => 0.2f, //slight overweight - bit slower
                >= OVERWEIGHT_THRESHOLD_MODERATE and < OVERWEIGHT_THRESHOLD_SEVERE => 0.4f, //overweight - slower
                >= OVERWEIGHT_THRESHOLD_SEVERE and < OVERWEIGHT_THRESHOLD_EXTREME => 0.6f, //serious overweight - reduced movement
                >= OVERWEIGHT_THRESHOLD_EXTREME and < OVERWEIGHT_DEATHCHANCE_THRESHOLD => 0.8f, //extreme overweight - very slow movement 
                >= OVERWEIGHT_DEATHCHANCE_THRESHOLD => 0.9f, //death risky weight - nearly no ability to move
                _ => 0.0f, //else case for unexpected events
            };
            debuff = Math.Max(debuff, (float)(1.0-energyLevelPenalty)); //takes only the worse debuff, so there is no accumulation in debuff. This is a design decision for hitting never too hard but may also be changed. Generally energy low debuf will dominate skinny debuff and overweight debuff dominates energy debufs which feels kind of right.
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
            return "starvation";
        }
    }

}
