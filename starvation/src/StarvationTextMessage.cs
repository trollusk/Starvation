//using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Starvation
{
    public class StarvationTextMessage : HudElement
    {
        public override string ToggleKeyCombinationCode => "starvationgui";

        private readonly double[] WARNING_MESSAGE_COLOUR = new double[4] { 1.0, 0.9, 0, 0.9 };
        private readonly double[] DANGER_MESSAGE_COLOUR = new double[4] { 1.0, 0, 0, 0.9 };

        private static CairoFont DefaultFont;
        private static CairoFont WarningFont;
        private static CairoFont DangerFont;

        public static CairoFont HungerLevelToFont(HungerLevel hungerLvl)
        {
            return hungerLvl switch
            {
                HungerLevel.Satiated => DefaultFont,
                HungerLevel.Mild => DefaultFont, // not starving
                HungerLevel.Moderate => DefaultFont, // mild, a few days
                HungerLevel.Severe => DefaultFont, // moderate
                HungerLevel.VerySevere => WarningFont, // moderate
                HungerLevel.Extreme => DangerFont, // severe
                _ => DefaultFont
            };
        }

        public StarvationTextMessage(ICoreClientAPI capi)
            : base(capi)
        {
            DefaultFont = CairoFont.WhiteSmallText();
            WarningFont = DefaultFont
                .Clone()
                .WithColor(WARNING_MESSAGE_COLOUR)
                .WithFontSize((float) GuiStyle.SmallishFontSize);
            DangerFont = WarningFont.Clone().WithColor(DANGER_MESSAGE_COLOUR);

            _ComposeGUI();
            TryOpen();
        }

        private void _ComposeGUI()
        {
            ElementBounds dialogBounds = new ElementBounds()
            {
                Alignment = EnumDialogArea.LeftFixed,
                fixedWidth = 300,
                fixedHeight = 250,
                fixedX = 0,
                fixedY = 10
            }.WithFixedAlignmentOffset(0, 5);
            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 200, 20);
            ElementBounds textBounds2 = ElementBounds.Fixed(0, 25, 200, 20);
            ElementBounds textBounds3 = ElementBounds.Fixed(0, 50, 200, 20);
            ElementBounds textBounds4 = ElementBounds.Fixed(0, 75, 200, 20);
            ElementBounds textBounds5 = ElementBounds.Fixed(0, 100, 200, 50);
            dialogBounds.WithChildren(
                textBounds,
                textBounds2,
                textBounds3,
                textBounds4,
                textBounds5
            );

            Composers["starvemessage"] = capi
                .Gui.CreateCompo("starvemessage", dialogBounds.FlatCopy().FixedGrow(0, 20))
                .BeginChildElements(dialogBounds)
                .AddDynamicText(
                    Lang.Get("starvation:energy") + ": 0",
                    DefaultFont,
                    textBounds,
                    "energy"
                )
                .AddDynamicText(
                    Lang.Get("starvation:abbrev-metabolic-equivalents") + ": 1",
                    DefaultFont,
                    textBounds2,
                    "mets"
                )
                .AddDynamicText(
                    Lang.Get("starvation:abbrev-basal-metabolic-rate") + ": 0",
                    DefaultFont,
                    textBounds3,
                    "bmr"
                )
                .AddDynamicText(
                    Lang.Get("starvation:abbrev-body-mass-index") + ": 0",
                    DefaultFont,
                    textBounds4,
                    "bmi"
                )
                .AddDynamicText(
                    Lang.Get("starvation:descr-satiated"),
                    DefaultFont,
                    textBounds5,
                    "hunger"
                )
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
