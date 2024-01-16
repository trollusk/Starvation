using Vintagestory.API.Client;



namespace Starvation
{
    public class StarvationTextMessage : HudElement
    {
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
            dialogBounds.WithChildren(textBounds, textBounds2, textBounds3);

            //GuiElementStatbar
            //ElementBounds textBoundsLine2 = ElementBounds.Fixed(0, 30, 300, 200);
            Composers["starvemessage"] = capi.Gui
                .CreateCompo("starvemessage", dialogBounds.FlatCopy().FixedGrow(0, 20))
                //.AddDynamicText("energy: 0", CairoFont.WhiteSmallText(), textBounds, "energy")
                //.AddDynamicText("METs: 0", CairoFont.WhiteSmallText(), textBounds, "mets")
                .BeginChildElements(dialogBounds)
                    .AddDynamicText("energy: 0", CairoFont.WhiteSmallText(), textBounds, "energy")
                    .AddDynamicText("METs: 0", CairoFont.WhiteSmallText(), textBounds2, "mets")
                    .AddDynamicText("BMR: 0", CairoFont.WhiteSmallText(), textBounds3, "bmr")
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
