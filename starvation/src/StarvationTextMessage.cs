using Vintagestory.API.Client;



namespace Starvation
{
    public class StarvationTextMessage : HudElement
    {
        public override string ToggleKeyCombinationCode => "starvationgui";


        public StarvationTextMessage(ICoreClientAPI capi) : base(capi)
        {
            ComposeGUI();
            TryOpen();
        }


        void ComposeGUI()
        {
            ElementBounds dialogBounds = new ElementBounds() { 
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
            ElementBounds textBounds5 = ElementBounds.Fixed(0, 100, 200, 20);
            dialogBounds.WithChildren(textBounds, textBounds2, textBounds3, textBounds4, textBounds5);

            Composers["starvemessage"] = capi.Gui
                .CreateCompo("starvemessage", dialogBounds.FlatCopy().FixedGrow(0, 20))
                .BeginChildElements(dialogBounds)
                    .AddDynamicText("energy: 0", CairoFont.WhiteSmallText(), textBounds, "energy")
                    .AddDynamicText("METs: 0", CairoFont.WhiteSmallText(), textBounds2, "mets")
                    .AddDynamicText("BMR: 0", CairoFont.WhiteSmallText(), textBounds3, "bmr")
                    .AddDynamicText("BMI: 0", CairoFont.WhiteSmallText(), textBounds4, "bmi")
                    .AddDynamicText("Satiated", CairoFont.WhiteSmallText(), textBounds5, "hunger")
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
