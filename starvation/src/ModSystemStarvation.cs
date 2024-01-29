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
using Vintagestory.Server;
using System.Data.Common;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.WorldEdit;
using Vintagestory.Client.NoObf;



namespace Starvation
{
    public enum HungerLevel
    {
        Satiated,
        Mild,
        Moderate,
        Severe,
        VerySevere,
        Extreme
    };


    // "Controller" class that handles initialising the mod itself
    public class ModSystemStarvation  : ModSystem
    {
        public const double HEALTHY_BMI = 22;
        public const int PACKETID_METS = 19877583;

        Dictionary<HungerLevel, string> HungerLevelToText = new Dictionary<HungerLevel, string>
        { 
            { HungerLevel.Satiated, "Satiated" },
            { HungerLevel.Mild, "Hungry" },
            { HungerLevel.Moderate, "Very Hungry" },
            { HungerLevel.Severe, "Desperate For Food!" },
            { HungerLevel.VerySevere, "Starving!" },
            { HungerLevel.Extreme, "STARVING TO DEATH!" },
        };

        public static ICoreClientAPI clientAPI;
        public static ICoreServerAPI serverAPI;

        GuiDialog dialog;

       // Dictionary mapping animation names to METs
        // TODO add all "-fp" versions
        Dictionary<string, double> METsByActivity = new Dictionary<string, double>
        {
            { "walk", 4 },
            { "idle", 1.3 },
            { "helditemready", 1.5 },
            { "sitflooridle.", 1.3 },
            { "sitflooredge.", 1.3 },
            { "sprint", 12 },
            { "sprint-fp", 12 },
            { "sneakwalk", 2.5 },
            { "sneakidle", 1.3 },
            { "glide", 3.5 },
            { "swim", 5.3 },
            { "swimidle", 3.5 },
            { "jump", 8 },
            { "climbup", 8 },
            { "climbidle", 5 },
            { "sleep", 0.95 },
            { "coldidle", 4 },
            { "protecteyes", 1.5 },
            { "coldidleheld", 5 },
            { "holdunderarm", 1.5 },
            { "holdinglanternlefthand", 1.5 },
            { "holdbothhands", 1.5 },
            { "holdbothhandslarge", 2 },
            { "hurt", 1 },
            { "bowaim", 2.5 },
            { "bowaimcrude", 2.5 },
            { "bowaimlong", 2.5 },
            { "bowaimrecurve", 2.5 },
            { "bowhit", 1 },
            { "throwaim", 2 },
            { "throw", 4 },
            { "slingaimgreek", 2 },
            { "slingthrowgreek", 2 },
            { "slingaimbalearic", 2 },
            { "slingthrowbalearic", 2 },
            { "hit", 2 },
            { "smithing", 4 },
            { "smithingwide", 4 },
            { "knap", 3 },
            { "breaktool", 1.3 },
            { "breakhand", 1.3 },
            { "falx", 5 },
            { "swordhit", 2 },
            { "axechop", 5 },
            { "axeheld", 1.3 },
            { "axeready", 1.3 },
            { "hoe", 3.5 },
            { "water", 1.5 },
            { "shoveldig", 5 },
            { "shovelready", 1.3 },
            { "shovelidle", 1.3 },
            { "spearhit", 1.5 },
            { "spearready", 2.3 },
            { "spearidle", 2.3 },
            { "scythe", 2 },
            { "scytheIdle", 1.3 },
            { "scytheReady", 1.3 },
            { "hammerandchisel", 3 },
            { "shears", 4 },
            { "placeblock", 3 },
            { "interactstatic", 1.3 },
            { "twohandplaceblock", 4 },
            { "eat", 2 },
            { "wave", 1.5 },
            { "nod", 1.5 },
            { "bow", 1.5 },
            { "facepalm", 1.5 },
            { "cry", 1.5 },
            { "shrug", 1.5 },
            { "cheer", 1.5 },
            { "laugh", 1 },
            { "rage", 1.5 },
            { "panning", 2.8 },
            { "pour", 1.5 },
            { "petlarge", 1.5 },
            { "petsmall", 1.5 },
            { "crudeOarIdle", 2.3 },
            { "crudeOarStandingReady", 2.3 },
            { "crudeOarHit", 2 },
            { "crudeOarForward", 5.8 },
            { "crudeOarBackward", 5.8 },
            { "crudeOarReady", 2.3 },
            { "yawn", 2.3 },
            { "stretch", 2.3 },
            { "cough", 2.3 },
            { "headscratch", 1.5 },
            { "raiseshield-left", 2 },
            { "raiseshield-right", 2 },
            { "knifecut", 5 },
            { "knifestab", 5 },
            { "startfire", 3 },
            { "shieldBlock", 10 },
            { "chiselready", 1.5 },
            { "chiselhit", 3 }
        };


        public override void Start(ICoreAPI api)
        {
            // Called on server, before any content is actually loaded.

            api.RegisterEntityBehaviorClass("starve", typeof(EntityBehaviorStarve));

            if (! ClientSettings.Inst.HasSetting("starveShowCalories"))
            {
                ClientSettings.Inst.Bool["starveShowCalories"] = false;
            }
        }


        // If you want to add or adjust attributes or properties of other game objects, do so in this method.
        public override void AssetsFinalize(ICoreAPI api)
        {
           GlobalConstants.HungerSpeedModifier = 0;

            if (api.Side == EnumAppSide.Server)
            {
                foreach (IPlayer iplayer in api.World.AllPlayers)
                {
                    if (iplayer.Entity.GetBehavior("starve") == null)
                    {
                        iplayer.Entity.AddBehavior(new EntityBehaviorStarve(iplayer.Entity));
                    }
                }
            }
        }


        public override void StartServerSide(ICoreServerAPI sapi)
        {
            // Called on server, before any content is actually loaded.

            serverAPI = sapi;
        }


        // Called from the client, when the game world is fully loaded and ready to start.
        public override void StartClientSide(ICoreClientAPI capi)
        {
            clientAPI = capi;
            dialog = new StarvationTextMessage(clientAPI);
            // dialog.TryOpen();

            clientAPI.Input.RegisterHotKey("starvationgui", "Toggle the Starvation debug messages.", GlKeys.U, HotkeyType.GUIOrOtherControls);
            clientAPI.Input.SetHotKeyHandler("starvationgui", ToggleGui);

            clientAPI.Event.RegisterGameTickListener(ClientTick500, 500);
        }


        private bool ToggleGui(KeyCombination comb)
        {
            if (dialog.IsOpened()) dialog.TryClose();
            else dialog.TryOpen();

            return true;
        }


        // Called within the CLIENT, every 500 milliseconds.
        // The role of this function is to calculate the player's current expended METs.
        // This has to be done in the client because the server seems not to have access to 
        // the list of active animations.
        // Note:deltaTime is in SECONDS (i.e. 0.5)
        private void ClientTick500(float deltaTime)
        {
            EntityPlayer clientPlayer = clientAPI.World.Player.Entity;
            double mets = CalculateCurrentMETs(clientPlayer);

            clientPlayer.WatchedAttributes.SetDouble("currentMETs", mets);
            clientPlayer.WatchedAttributes.MarkPathDirty("currentMETs");

            clientAPI.Network.SendEntityPacket(clientPlayer.EntityId, PACKETID_METS, SerializerUtil.Serialize(mets));

            if (dialog.IsOpened())
            {
                updateStarvationMessage();
            }
        }


        void updateStarvationMessage()
        {
            EntityPlayer clientPlayer = clientAPI.World.Player.Entity;
            
            double METs = clientPlayer.WatchedAttributes.GetDouble("currentMETs", 1);
            double energy = clientPlayer.WatchedAttributes.GetDouble("energyReserves", 999);
            double age = clientPlayer.WatchedAttributes.GetDouble("ageInYears", 25);
            double weight = clientPlayer.WatchedAttributes.GetDouble("bodyWeight", HealthyWeight(clientPlayer));
            double bmi = weight / Math.Pow(clientPlayer.Properties.EyeHeight, 2);
            HungerLevel hungerLevel = EntityBehaviorStarve.EnergyToHungerLevel(energy);
            string hungerTxt = HungerLevelToText.Get(hungerLevel, "");

            double temp = GetTemperatureAtEntity(clientPlayer);
            
            bool showCalories = ClientSettings.Inst.GetBoolSetting("starveShowCalories");

            // Console.WriteLine("calculating BMR based on age " + age + ", weight " + weight + ", temp " + temp);
            if (showCalories)
            {
                dialog.Composers["starvemessage"].GetDynamicText("energy").SetNewTextAsync("energy: " + Math.Round(energy/4.189) + " cal");
            } else {
                dialog.Composers["starvemessage"].GetDynamicText("energy").SetNewTextAsync("energy: " + Math.Round(energy) + " kJ");
            }
            dialog.Composers["starvemessage"].GetDynamicText("mets").SetNewTextAsync("METs: " + METs);
            // TODO store this value (BMR)
            dialog.Composers["starvemessage"].GetDynamicText("bmr").SetNewTextAsync("BMR: " + Math.Round(CalculateBMR(weight, age, temp)));
            dialog.Composers["starvemessage"].GetDynamicText("bmi").SetNewTextAsync("BMI: " + Math.Round(bmi, 1));
            dialog.Composers["starvemessage"].GetDynamicText("hunger").Font = StarvationTextMessage.HungerLevelToFont(hungerLevel);
            dialog.Composers["starvemessage"].GetDynamicText("hunger").SetNewTextAsync(hungerTxt);
        }


        static public double GetTemperatureAtEntity(Entity entity)
        {
            return entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, entity.World.Calendar.TotalDays).Temperature;
        }


        // Number from 0-1
        static public double GetRainfallAtEntity(Entity entity)
        {
            return entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDateValues, entity.World.Calendar.TotalDays).Rainfall;
        }


        static public double GetHumidityAtEntity(Entity entity)
        {
            // Humidity correlates to rainfall pretty closely
            return Math.Clamp(GetRainfallAtEntity(entity) * 100, 10, 90);
        }


        // Return a "healthy" weight for the (human) entity, in kg, using eyeHeight as its height. 
        static public double HealthyWeight(Entity entity)
        {
            return HEALTHY_BMI * Math.Pow(entity.Properties.EyeHeight, 2);
        }


        // Returns estimated heat index temperature in degrees celsius.
        //      ambientTemperature is the dry-bulb temperature in C
        //      relativeHumidity is a percentage 0-100
        // Equation from Steadman, R. G. (July 1979). "The Assessment of Sultriness" (!!) (via Wikipedia)
        public static double HeatIndexTemperature(double ambientTemperature, double relativeHumidity)
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


        static public double CaloriesToKilojoules(double cal)
        {
            return cal * 4.189;
        }


        static public double EnergyReservesToBMI(double energy)
        {
            return 5.5948e-12 * Math.Pow(energy, 2) + 0.0000226104 * energy + 21.9336;
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

            // Console.Write("Anims: ");
            foreach (string animName in keyList)
            {
                // Console.Write(animName + ", ");
                METsByActivity.TryGetValue(animName, out value);
                maxMETs = Math.Max(maxMETs, value);
            }
            // Console.WriteLine();
            return maxMETs;
        }

    }
}
