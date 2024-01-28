# Realistic Starvation

A mod for the sandbox survival game Vintage Story.

This mod provides a replacement for Vintage Story's hunger system. The aim is to be realistic - you will take up to 60-70 game days to starve to death, but before that you will suffer various debuffs as your nutritional status worsens.

## Motivation and Features

Vintage Story aims for depth and realism in many of its game mechanics. However where hunger is concerned, its mechanic is taken straight from Minecraft. Minecraft's model of hunger is "video-gamey" and wildly unrealistic:

* The player can go from satiated to starving to death within a few in-game hours
* Conversely, the player can go from starving to completely well within a few seconds, by scarfing down several food items
* Until the player dies, there are no adverse consequences to hunger other than reduced health regeneration
* There is no consistent relationship between the food type and size, and the amount of nutrition provided

In this mod:

* It takes, on average, more than six in-game weeks to starve to death.
* As the player's nutritional deficit worsens, they suffer from increasingly severe debuffs to maximum health, health regeneration, movement speed, weapon damage, and mining speed.
* These debuffs can be temporarily alleviated by eating a meal. However, to return to full health, the player must fully correct their energy deficit.
* All food items give a realistic amount of energy. 

## Physiology

Humans use energy in the following ways:

1. **Basal Metabolic Rate (BMR)** - the energy required to maintain physiological systems and stay alive, even when completely inactive. Usually around 6500 kJ/day.
2. **Physical activity** - self-explanatory. The energy consumed during different types of physical activity is measured in Metabolic Equivalents (METs), where 1 MET = BMR, 2 METs = 2 x BMR, and so on. An extra 1000 to 1500 kJ/day is required on average by sedentary adults. 
3. **Digestion** - A proportion of the energy contained in any meal is used up in the act of digesting it. Protein takes the most energy to digest (about 25%), fat takes the least.
4. **Thermoregulation** - extra energy that is expended to maintain a constant body temperature in very hot or cold environments. 

All of these are simulated by this mod:

1. BMR is calculated based on body weight, age (assumed to be 25 years old), and ambient temperature. As body weight falls with starvation, BMR falls also (slightly).
2. METs are calculated moment to moment based on player model animations.
3. The energy cost of digestion has been subtracted from the total energy provided by each food item in the game. 
4. Thermoregulation is accounted for via the ambient temperature component of BMR. When players are very cold their models play "shivering" animations (Vanilla feature), which use up a lot of energy (5 METs).

Most foods seem to have a saturation value of around twice their calories, therefore we convert using saturation * 0.5 = calories. There are lots of exceptions to this rule, which have been corrected in the mod. For example, in Vanilla VS, the item "fat" gives 200 saturation, or 100 calories. This is equivalent to 11 grams of lard (about 2.5 teaspoons). This mod makes fat give the amount of calories present in 100g of lard (about half a cup).

\A small HUD in the top left corner shows the player's energy balance (in kilojoules), current METs, BMI, and description of hunger level. This can be toggled with a key (default "U").

The player's body weight falls as their energy reserves diminish, representing loss of fat reserves, and eventually loss of muscle. Once BMI gets below 13, the player is close to death from starvation.

The Vanilla VS "food group" system is disabled. The only effect of this system in Vanilla is to boost maximum health by a couple of points for each maxed-out food group. With this mod, it is not possible to increase your maximum health beyond its normal limit by eating.

The item "fat" has been reassigned from the "Protein" category (!) to "Dairy", and the mod generally treats all "Dairy" items as fat. 


## Hints

* As in Vanilla, combining food items into meals is more effective than eating them separately.
* Alcohol can contain large amounts of energy per litre. For example, a litre of spirits contains 2,100 calories (8,800 kJ)!
* Cottage cheese, jam and honey are other energy-dense liquids.
* Other notably energy-dense foods: perfectly baked loaves of bread, fat, peanuts, breadfruit, cassava.
