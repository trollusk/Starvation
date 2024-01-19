
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
        
        // Watched Attributes are permanent entity attributes that are synced between client and server.

        public double energyReserves 
        {
            get => entity.WatchedAttributes.GetDouble("energyReserves", INITIAL_ENERGY_RESERVES);
            set => entity.WatchedAttributes.SetDouble("energyReserves", value);
        }

        public double bodyWeight 
        {
            get => entity.WatchedAttributes.GetDouble("bodyWeight", ModSystemStarvation.HealthyWeight(entity));
            set => entity.WatchedAttributes.SetDouble("bodyWeight", value);
        }

        public double ageInYears 
        {
            get => entity.WatchedAttributes.GetDouble("ageInYears", DEFAULT_ENTITY_AGE);
            set => entity.WatchedAttributes.SetDouble("ageInYears", value);
        }

        public double currentMETs 
        {
            get => entity.WatchedAttributes.GetDouble("currentMETs", 1);
            set => entity.WatchedAttributes.SetDouble("currentMETs", value);
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
                // testing
                // energyReserves = -170000;
            }
        }


        // Called on server only, every 250 milliseconds (see above)
        // Note: deltaTime is in SECONDS i.e. 0.25
        private void ServerTick250(float deltaTime)
        {
            double temp = ModSystemStarvation.HeatIndexTemperature(ModSystemStarvation.GetTemperatureAtEntity(entity), 
                                                                   ModSystemStarvation.GetHumidityAtEntity(entity));
            double kJPerGameDay = ModSystemStarvation.CalculateBMR(bodyWeight, ageInYears, temp) * currentMETs;
            double kJPerGameSecond = kJPerGameDay / 24.0 / 60.0 / 60.0;

            double gameSeconds = DeltaTimeToGameSeconds(deltaTime);

            // Exit if we are in Creative or Spectator game modes
            if (entity is EntityPlayer)
            {
                EntityPlayer plr = entity as EntityPlayer;
                EnumGameMode mode = entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode;

                if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) return;
            }

            // Decrease max health if starving
            EntityBehaviorHealth bh = entity.GetBehavior<EntityBehaviorHealth>();
            double healthPenalty = -1 * MaxHealthPenalty();
            bh.MaxHealthModifiers["starvationMod"] = (float) healthPenalty;

            // Slower health regeneration if starving
            double baseRegenSpeed = entity.Api.World.Config.GetString("playerHealthRegenSpeed", "1").ToFloat();
            bh._playerHealthRegenSpeed = (float) (baseRegenSpeed * HealthRegenPenalty());

            // Slower movement speed if starving
            entity.Stats.Set("walkspeed", "starvationmod", -0.5f, false);

            // We have expended kJPerSecond * gameSeconds (kJ)
            // Decrement our total energy stores by this amount
            energyReserves = energyReserves - (kJPerGameSecond * gameSeconds);

            Console.WriteLine("ServerTick250: BMR = " + (kJPerGameDay/currentMETs) + ", energyReserves = " + energyReserves + 
                                ", kJPerGameSecond = " + kJPerGameSecond + ", deltaTime = " + deltaTime + ", gameSeconds = " + 
                                gameSeconds + ", energy decrement = " + (kJPerGameSecond * gameSeconds));
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
            // It doesn't really, but a very rough approximation is 2 * saturation = kJ

            // Unfortunately "fat" is not a food category in VS
            // TODO some kJ are expended in digestion of food
            // this can be dealt with by deducting a "digestion tax" from the kJ supplied by the food
            //      carbohydrates: 5-10% loss
            //      fat: 0-3% loss
            //      protein: 20-30% loss

            // TODO use "correct" kJ values of foods, instead of saturation
            energyReserves += 2 * saturation;
            // TODO gain a "satiated" buff if the meal was of decent size (esp if protein or fat)
        }


        // Fires when entity dies
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // Set a lower cap for energyReserves, to avoid being stuck in a "death loop"
            energyReserves = Math.Max(STARVE_THRESHOLD_EXTREME, energyReserves);
        }


        // Returns number which is subtracted from max health
        public double MaxHealthPenalty()
        {
            if (energyReserves < STARVE_THRESHOLD_EXTREME)
            {
                return 12;
            }
            else if (energyReserves < STARVE_THRESHOLD_SEVERE)
            {
                return 6;
            }
            else if (energyReserves < STARVE_THRESHOLD_MODERATE)
            {
                return 4.5;
            }
            else if (energyReserves < STARVE_THRESHOLD_MODERATE)
            {
                return 2.5;
            } else {
                return 0;
            }
        }

        // Returns number which health regen speed (default = 1) is multiplied by.
        public double HealthRegenPenalty()
        {
            if (energyReserves < STARVE_THRESHOLD_EXTREME)
            {
                return 0;
            }
            else if (energyReserves < STARVE_THRESHOLD_SEVERE)
            {
                return 0;
            }
            else if (energyReserves < STARVE_THRESHOLD_MODERATE)
            {
                return 0.25;
            }
            else if (energyReserves < STARVE_THRESHOLD_MODERATE)
            {
                return 0.5;
            } else {
                return 1;
            }
        }

        // Returns number of game seconds represented by deltaTime (real world SECONDS)
        private double DeltaTimeToGameSeconds(double deltaTime)
        {
            return deltaTime * entity.World.Calendar.SpeedOfTime * entity.World.Calendar.CalendarSpeedMul;
        }


        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (serverListenerId != 0) 
                entity.World.UnregisterGameTickListener(serverListenerId);
        }


        // Return the short string alias for this mod
        public override string PropertyName()
        {
            return "starve";
        }
    }

}
