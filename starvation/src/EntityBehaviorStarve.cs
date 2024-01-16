
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.GameContent;



namespace Starvation
{
    public class EntityBehaviorStarve : EntityBehavior
    {
        private const double DEFAULT_ENTITY_AGE = 25;
        private const double INITIAL_ENERGY_RESERVES = 0;

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
                serverListenerId = entity.World.RegisterGameTickListener(ServerSlowTick, 250);
            }
        }


        // Called on server only, every 250 milliseconds (see above)
        // Note: deltaTime is in milliseconds
        private void ServerSlowTick(float deltaTime)
        {
            double temp = ModSystemStarvation.GetTemperatureAtEntity(entity);
            double kJPerDay = ModSystemStarvation.CalculateBMR(bodyWeight, ageInYears, temp) * currentMETs;
            double kJPerSecond = kJPerDay / 24 / 60 / 60;

            double gameSeconds = DeltaTimeToGameSeconds(deltaTime);

            // We have expended kJPerSecond * gameSeconds (kJ)
            // Decrement our total energy stores by this amount
            energyReserves = energyReserves - (kJPerSecond * gameSeconds);

            // TODO need to add/remove consequences of various stages of starvation:
            // lowered max HP
            // lowered max stamina
            // slowed regeneration
            // slow movement
            // altered weight (decrement bodyWeight)
            Console.WriteLine("ServerSlowTick: BMR = " + (kJPerDay/currentMETs) + ", energyReserves = " + energyReserves + ", kJPerSecond = " + kJPerSecond, ", deltaTime = " + deltaTime + ", gameSeconds = " + gameSeconds);
        }


        // Fires when entity receives saturation (nutrition)
        //  saturation = amount of "saturation" received
        //  foodCat = category of food
        //  saturationLossDelay = delay before saturation begins to decrement
        //  nutritionGainMultiplier = ?
        public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10f, float nutritionGainMultiplier = 1f)
        {
            // saturation/satiety is meant to correlate with calories/kilojoules
            // It doesn't really, but a very rough approximation is 2 * saturation = kJ

            // Unfortunately "fat" is not a food category in VS
            // TODO some kJ are expended in digestion of food
            // this can be dealt with by deducting a "digestion tax" from the kJ supplied by the food
            //      carbohydrates: 5-10% loss
            //      fat: 0-3% loss
            //      protein: 20-30% loss

            // TODO use "correct" kJ values of foods, instead of saturation
            // TODO do we want to gain energy from a meal over time rather than all at once?
            energyReserves += 2 * saturation;
            // TODO gain a "satiated" buff if the meal was of decent size (esp if protein or fat)
        }


        // Fires when entity dies
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // TODO reset energyReserves?
        }


        // Return the METs of the entity's current activity, as judged by active animations.
        // TODO the problem is that the server version of the player entity doesn't seem to have any of the active animations
        // that we're interested in
        // We should see if they appear when we are in 3rd person view
        // public double CalculateCurrentMETs()
        // {
        //     double maxMETs = 1;
        //     double value = 1;
        //     // list of all active animations
        //     List<string> keyList = new List<string>(entity.AnimManager.ActiveAnimationsByAnimCode.Keys);

        //     Console.Write("Anims: ");
        //     foreach (string animName in keyList)
        //     {
        //         Console.Write(animName + ", ");
        //         METsByActivity.TryGetValue(animName, out value);
        //         maxMETs = Math.Max(maxMETs, value);
        //     }
        //     Console.WriteLine();
        //     return maxMETs;
        // }


        // // Returns (human) entity's basal metabolic rate in kilojoules/day
        // // This is the "baseline" energy expended if engaged in no activity other than breathing.
        // // Depends on age, body weight, sex (ignored), and heat index (basically = ambient temperature, but increased a bit in humid environments)
        // // Accounts for increased BMR seen with adaptation to cold environment.
        // // Does not account for shivering (occurs when core body temp unacceptably low)
        // private double BMR()
        // {
        //     // BMR in kcal = (13.6 * MASS) - (4.8 * AGE) + (147 * (1 if male, 0 if female)) - (4.3 * TEMP) + 857
        //     // kcal * 4.189 = kJ
        //     // temp is supposed to be high heat index temperature
        //     // assuming mass 64 kg, age 25, temp 15, BMR = approx 6800 kJ
        //     double temp = entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, entity.World.Calendar.TotalDays).Temperature;
        //     double humidity = 50;
        //     return (13.6 * HealthyWeight() - (4.8 * entityAge) + 73.5 - (4.3 * HeatIndexTemperature(temp, humidity)) + 857) * 4.189;
        // }


        // Returns estimated heat index temperature in degrees celsius.
        //      ambientTemperature is the dry-bulb temperature in C
        //      relativeHumidity is a percentage 0-100
        // Equation from Steadman, R. G. (July 1979). "The Assessment of Sultriness" (!!) (via Wikipedia)
        private double HeatIndexTemperature(double ambientTemperature, double relativeHumidity)
        {
            const double c1 = -8.78469476;
            const double c2 = 1.61139411;
            const double c3 = 2.33854884;
            const double c4 = -0.14611605;
            const double c5 = -0.012308094;
            const double c6 = -0.01642483;
            const double c7 = 0.002211732;
            const double c8 = 0.00072546;
            const double c9 = -0.000003582;
            return c1 + c2 * ambientTemperature 
                + c3 * relativeHumidity 
                + c4 * ambientTemperature * relativeHumidity 
                + c5 * Math.Pow(ambientTemperature, 2) 
                + c6 * Math.Pow(relativeHumidity, 2) 
                + c7 * Math.Pow(ambientTemperature, 2) * relativeHumidity 
                + c8 * ambientTemperature * Math.Pow(relativeHumidity, 2) 
                + c9 * Math.Pow(ambientTemperature, 2) * Math.Pow(relativeHumidity, 2) ;
        }


        // Returns number of game seconds represented by deltaTime (real world milliseconds)
        private double DeltaTimeToGameSeconds(double deltaTime)
        {
            return deltaTime / 1000 * entity.World.Calendar.SpeedOfTime * entity.World.Calendar.CalendarSpeedMul;
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
