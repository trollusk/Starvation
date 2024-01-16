using System;
using System.Text;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using static Vintagestory.API.Client.GuiDialog;
using Vintagestory.API.Common.Entities;



namespace Starvation
{
    // "Controller" class that handles initialising the mod itself
    public class ModSystemStarvation  : ModSystem
    {
        public const double HEALTHY_BMI = 22;

        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        GuiDialog dialog;


       // Dictionary mapping animation names to METs
        // TODO add all "-fp" versions, and add METs for each activity
        Dictionary<string, double> METsByActivity = new Dictionary<string, double>
        {
            { "walk", 4 },
            { "idle", 1.3 },
            { "helditemready", 1 },
            { "sitflooridle.", 1 },
            { "sitflooredge.", 1 },
            { "sprint", 10 },
            { "sprint-fp", 10 },
            { "sneakwalk", 1 },
            { "sneakidle", 1 },
            { "glide", 1 },
            { "swim", 1 },
            { "swimidle", 1 },
            { "jump", 1 },
            { "climbup", 1 },
            { "climbidle", 1 },
            { "sleep", 1 },
            { "coldidle", 1 },
            { "protecteyes", 1 },
            { "coldidleheld", 1 },
            { "holdunderarm", 1 },
            { "holdinglanternlefthand", 1 },
            { "holdbothhands", 1 },
            { "holdbothhandslarge", 1 },
            { "hurt", 1 },
            { "bowaim", 1 },
            { "bowaimcrude", 1 },
            { "bowaimlong", 1 },
            { "bowaimrecurve", 1 },
            { "bowhit", 1 },
            { "throwaim", 1 },
            { "throw", 1 },
            { "slingaimgreek", 1 },
            { "slingthrowgreek", 1 },
            { "slingaimbalearic", 1 },
            { "slingthrowbalearic", 1 },
            { "hit", 1 },
            { "smithing", 1 },
            { "smithingwide", 1 },
            { "knap", 1 },
            { "breaktool", 1 },
            { "breakhand", 1 },
            { "falx", 1 },
            { "swordhit", 1 },
            { "axechop", 1 },
            { "axeheld", 1 },
            { "axeready", 1 },
            { "hoe", 1 },
            { "water", 1 },
            { "shoveldig", 1 },
            { "shovelready", 1 },
            { "shovelidle", 1 },
            { "spearhit", 1 },
            { "spearready", 1 },
            { "spearidle", 1 },
            { "scythe", 1 },
            { "scytheIdle", 1 },
            { "scytheReady", 1 },
            { "hammerandchisel", 1 },
            { "shears", 1 },
            { "placeblock", 1 },
            { "interactstatic", 1 },
            { "twohandplaceblock", 1 },
            { "eat", 1 },
            { "wave", 1 },
            { "nod", 1 },
            { "bow", 1 },
            { "facepalm", 1 },
            { "cry", 1 },
            { "shrug", 1 },
            { "cheer", 1 },
            { "laugh", 1 },
            { "rage", 1 },
            { "panning", 1 },
            { "pour", 1 },
            { "petlarge", 1 },
            { "petsmall", 1 },
            { "crudeOarIdle", 1 },
            { "crudeOarStandingReady", 1 },
            { "crudeOarHit", 1 },
            { "crudeOarForward", 1 },
            { "crudeOarBackward", 1 },
            { "crudeOarReady", 1 },
            { "yawn", 1 },
            { "stretch", 1 },
            { "cough", 1 },
            { "headscratch", 1 },
            { "raiseshield-left", 1 },
            { "raiseshield-right", 1 },
            { "knifecut", 1 },
            { "knifestab", 1 },
            { "startfire", 1 },
            { "shieldBlock", 1 },
            { "chiselready", 1 },
            { "chiselhit", 1 }
        };


        public override void Start(ICoreAPI api)
        {
            // Called on both server and client, before any content is actually loaded.
            //
            // Common uses:
            // - Register the Classes of new behaviours, blocks, items, entities, etc
            // - Register listeners

            api.RegisterEntityBehaviorClass("starve", typeof(EntityBehaviorStarve));
        }


        // If you want to add or adjust attributes or properties of other game objects, do so in this method.
        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                // TODO add kJ and protein/fat/carb classification to all foods
                foreach (IPlayer iplayer in api.World.AllPlayers)
                {
                    if (iplayer.Entity.GetBehavior("starve") == null)
                    {
                        iplayer.Entity.AddBehavior(new EntityBehaviorStarve(iplayer.Entity));
                    }
                }
            }
        }


        // Called from the client, when the game world is fully loaded and ready to start.
        public override void StartClientSide(ICoreClientAPI clientAPI)
        {
            dialog = new StarvationTextMessage(clientAPI);
            capi = clientAPI;
            dialog.TryOpen();
            clientAPI.Event.RegisterGameTickListener(On1sClientTick, 500);
        }


        // Same thing, but called from the server. 
        // Initialisation of assets independent of the game world should not be done here, but in AssetsFinalize
        public override void StartServerSide(ICoreServerAPI serverAPI)
        {
            sapi = serverAPI;
            // serverAPI.Event.RegisterGameTickListener(On1sTick_Server, 1000);
        }


        // Called every 1000 milliseconds
        private void On1sClientTick(float deltaTime)
        {
            EntityPlayer clientPlayer = capi.World.Player.Entity;
            double mets = CalculateCurrentMETs(clientPlayer);

            clientPlayer.WatchedAttributes.SetDouble("currentMETs", mets);

            dialog.TryOpen();
            if (dialog.IsOpened())
            {
                updateStarvationMessage();
            }
            Console.WriteLine("Client tick 1s: METS = " + mets);
        }


        void updateStarvationMessage()
        {
            EntityPlayer clientPlayer = capi.World.Player.Entity;

            double METs = clientPlayer.WatchedAttributes.GetDouble("currentMETs", 1);
            double energy = clientPlayer.WatchedAttributes.GetDouble("energyReserves", 999);
            double age = clientPlayer.WatchedAttributes.GetDouble("ageInYears", 25);
            double weight = clientPlayer.WatchedAttributes.GetDouble("bodyWeight", HealthyWeight(clientPlayer));
            double temp = GetTemperatureAtEntity(clientPlayer);
            Console.WriteLine("calculating BMR based on age " + age + ", weight " + weight + ", temp " + temp);
            dialog.Composers["starvemessage"].GetDynamicText("energy").SetNewTextAsync("energy: " + Math.Round(energy, 5).ToString());
            dialog.Composers["starvemessage"].GetDynamicText("mets").SetNewTextAsync("METs: " + METs);
            dialog.Composers["starvemessage"].GetDynamicText("bmr").SetNewTextAsync("BMR: " + CalculateBMR(weight, age, temp));
        }


        static public double GetTemperatureAtEntity(Entity entity)
        {
            return entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, entity.World.Calendar.TotalDays).Temperature;
        }


        // Return a "healthy" weight for the (human) entity, in kg, using eyeHeight as its height. 
        static public double HealthyWeight(Entity entity)
        {
            return HEALTHY_BMI * Math.Pow(entity.Properties.EyeHeight, 2);
        }

        // Returns (human) entity's basal metabolic rate in kilojoules/day
        // This is the "baseline" energy expended if engaged in no activity other than breathing.
        // Depends on age, body weight, sex (ignored), and heat index (basically = ambient temperature, but increased a bit in humid environments)
        // Accounts for increased BMR seen with adaptation to cold environment.
        // Does not account for shivering (occurs when core body temp unacceptably low)
        static public double CalculateBMR(double weightkg, double age, double tempC)
        {
            // BMR in kcal = (13.6 * MASS) - (4.8 * AGE) + (147 * (1 if male, 0 if female)) - (4.3 * TEMP) + 857
            // kcal * 4.189 = kJ
            // temp is supposed to be high heat index temperature
            // assuming mass 64 kg, age 25, temp 15, BMR = approx 6800 kJ
            // double temp = entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, entity.World.Calendar.TotalDays).Temperature;
            // double humidity = 50;
            return (13.6 * weightkg - (4.8 * age) + 73.5 - (4.3 * tempC) + 857) * 4.189;
        }


        // Return the METs of the client player entity's current activity, as judged by active animations.
        // We do this client side because the server version of the player entity doesn't have any of the active animations
        // that we're interested in
        public double CalculateCurrentMETs(EntityPlayer entity)
        {
            //EntityPlayer clientPlayer = capi.World.Player.Entity;
            double maxMETs = 1;
            double value = 1;
            // list of all active animations
            List<string> keyList = new List<string>(entity.AnimManager.ActiveAnimationsByAnimCode.Keys);

            Console.Write("Anims: ");
            foreach (string animName in keyList)
            {
                Console.Write(animName + ", ");
                METsByActivity.TryGetValue(animName, out value);
                maxMETs = Math.Max(maxMETs, value);
            }
            Console.WriteLine();
            return maxMETs;
        }

    }
}
