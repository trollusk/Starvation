using Vintagestory.API.Client;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Cairo;


namespace Starvation
{
    public class StarvationTextMessage : HudElement
    {
        public override string ToggleKeyCombinationCode => "starvationgui";

        private readonly double[] WARNING_MESSAGE_COLOUR = new double[4] { 1.0, 0.9, 0, 0.9 };
        private readonly double[] DANGER_MESSAGE_COLOUR = new double[4] { 1.0, 0, 0, 0.9 };
        private readonly double[] BLACK_COLOUR = new double[4] { 0.0, 0.0, 0.0, 1.0 };
        private readonly double[] GREEN_COLOUR = new double[4] { 0.0, 1.0, 0.0, 1.0 };
        private readonly double[] BLUE_COLOUR = new double[4] { 0.0, 0.0, 1.0, 1.0 };
        private readonly double[] GRAY_LIGHT_COLOUR = new double[4] { 0.8, 0.8, 0.8, 1.0 };

        public static CairoFont DefaultFont;
        public static CairoFont AlternateFont;
        public static CairoFont GreenFont;
        public static CairoFont WarningFont;
        public static CairoFont DangerFont;


        public static CairoFont HungerLevelToFont(HungerLevel hungerLvl)
        {
            return hungerLvl switch
            {
                HungerLevel.Satiated     => GreenFont,
                HungerLevel.Mild         => GreenFont,           // not starving
                HungerLevel.Moderate     => DefaultFont,           // mild, a few days
                HungerLevel.Severe       => DefaultFont,         // moderate
                HungerLevel.VerySevere   => WarningFont,         // moderate
                HungerLevel.Extreme      => DangerFont,         // severe
                _                        => DefaultFont
            };
        }
        public static CairoFont EnergyLevelToFont(EnergyReserveLevel energyLvl)
        {
            return energyLvl switch
            {
                EnergyReserveLevel.VERY_HIGH => WarningFont,
                EnergyReserveLevel.High => DefaultFont,        
                EnergyReserveLevel.MEDIUM => GreenFont,       
                EnergyReserveLevel.LOW => DefaultFont,         
                EnergyReserveLevel.VERY_LOW => WarningFont,       
                EnergyReserveLevel.MINIMAL => DangerFont,
                EnergyReserveLevel.ZERO => DangerFont,
                _ => DefaultFont
            };
        }


        public StarvationTextMessage(ICoreClientAPI capi) : base(capi)
        {
            DefaultFont = CairoFont.WhiteSmallText();
            AlternateFont = DefaultFont.Clone().WithColor(GRAY_LIGHT_COLOUR);
            GreenFont = DefaultFont.Clone().WithColor(GREEN_COLOUR);
            WarningFont = DefaultFont.Clone().WithColor(WARNING_MESSAGE_COLOUR).WithFontSize((float) GuiStyle.SmallishFontSize);
            DangerFont = WarningFont.Clone().WithColor(DANGER_MESSAGE_COLOUR);

            ComposeGUI();
            TryOpen();
        }


        void ComposeGUI()
        {
            ElementBounds dialogBounds = new ElementBounds() { 
                Alignment = EnumDialogArea.RightBottom,
                fixedWidth = 400,
                fixedHeight = 300,
                fixedX = 0,
                fixedY = 0
            }.WithFixedAlignmentOffset(0, 5);
            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 300, 20);
            ElementBounds textBounds2 = ElementBounds.Fixed(0, 20, 300, 30);
            ElementBounds textBounds3 = ElementBounds.Fixed(0, 50, 300, 20);
            ElementBounds textBounds4 = ElementBounds.Fixed(0, 70, 300, 20);
            ElementBounds textBounds5 = ElementBounds.Fixed(0, 90, 300, 20);
            ElementBounds textBounds6 = ElementBounds.Fixed(0, 110, 300, 30);
            ElementBounds textBounds7 = ElementBounds.Fixed(0, 140, 300, 20);
            dialogBounds.WithChildren(textBounds, textBounds2, textBounds3, textBounds4, textBounds5, textBounds6, textBounds7);

            Composers["starvemessage"] = capi.Gui
                .CreateCompo("starvemessage", dialogBounds.FlatCopy().FixedGrow(0, 20))
                .BeginChildElements(dialogBounds)
                    .AddDynamicText(Lang.Get("starvation:energy") + ": 0", AlternateFont, textBounds, "energy")
                    .AddDynamicText(Lang.Get("starvation:descr-energylvl"), AlternateFont, textBounds2, "energylvl")
                    .AddDynamicText(Lang.Get("starvation:abbrev-metabolic-equivalents") + ": 1", AlternateFont, textBounds3, "mets")
                    .AddDynamicText(Lang.Get("starvation:abbrev-basal-metabolic-rate") + ": 0", AlternateFont, textBounds4, "bmr")
                    .AddDynamicText(Lang.Get("starvation:abbrev-body-mass-index") + ": 0", AlternateFont, textBounds5, "bmi")
                    .AddDynamicText(Lang.Get("starvation:descr-satiated"), AlternateFont, textBounds6, "hunger")
                    .AddDynamicText(Lang.Get("starvation:abbrev-gastro-intestinal-reserves") + ": 0", AlternateFont, textBounds7, "gastrointestinal")
                .EndChildElements()
                .Compose();
        }


        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
        }

        // Can't be focused
        public override bool Focusable => false;
    }
}
