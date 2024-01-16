lose energy from basal metabolic rate + activity
- average BMR is 6500 kJ/day (0.075 per second) - higher in cold environment (less so if fat)
- minimum energy requirement is 7500 kJ/day (BMR + minimal sedentary activity)

there is an energy cost to digesting food, which varies based on type of nutrient:
- protein 20-30%
- carbohydrate 5-10%
- fat 0-3%

METs are a measure of the energy expended by a particular activity.
They are expressed as multiples of BMR
1 MET = BMR = completely inactive
2 METS = malking slowly
5 METS = walking briskly
11.5 METs = running
https://www.healthline.com/health/what-are-mets

1. energy is first lost from GLYCOGENOLYSIS
   - average glycogen stores 8000 kJ ie about a day's worth of energy
   - about 1/3 in liver, 2/3 in skeletal muscle
2. once glycogen is gone, FAT (triglyceride) starts being broken down into FFAs + GLYCEROL
   - glycerol is converted to glucose in liver (GLUCONEOGENESIS)
   - FFAs are used directly for energy in peripheral tissues (everywhere but brain, FFAs cannot cross BBB)
   - average fat reserves 400,000 kJ
   - during this stage, brain is reliant on glucose from glycerol (and any food)
3. after about 3 days, FFAs start being converted into KETONES (KETOSIS)
   - ketones can cross BBB, and brain can use them in place of glucose
4. once fat reserves are getting low, MUSCLE starts being broken down
   - glucose is formed from amino acids (alanine) via GLUCONEOGENESIS

food intake adds to energy
if starved, recovery should be slow
should not use 100% of fat before starting to break down protein - obese people do start to break down protein while some fat reserves remain

"buckets" - glycogen, fat, protein, carbohydrate

game effects of starvation
- reduced maximum health (show "cap" on health bar)
- reduced maximum stamina
- reduced regeneration rate of health and stamina
- slow movement
- "satiated" buff
- eating should lessen SOME of the debuffs temporarily

each tick
- look at what we are doing, and subtract kJ per tick from our energy stores (from short term first)
- check if energy < X - has debuff

debuff "health cap": reduce max health, and display cap on healthbar

upon eating
- add energy content to short term energy reserve
- add temporary "sated" buff


EntityBehaviorHunger.hungerCounter

hungerTree = entity.WatchedAttributes.GetTreeAttribute("hunger")

hungerTree
    saturation 1500
    maxsaturation 1500
    saturationlossdelayfruit/veg/grain/protein/dairy 0

listenerId = entity.World.RegisterGameTickListener(SlowTick, 6000)
UpdateNutrientHealthBoost()

ConsumeSaturation(float amount) : ReduceSaturation(amount / 10f)

EQuations to predict BMR based on age, body mass, sex, and environmental temperature

BMR = 13.6m - 4.8a + 147s - 4.3h + 857
(result is in kcal, multiply by 4.184 to get kJ)
m = mass in kg
a = age in years
s = 1 if male, 0 if female
h = monthly high heat index temperature in C
(dry bulb temperature, or higher if high humidity)


Cold weather

Shivering can increase BMR by up to 5 fold
Adaptation to cold environment: BMR is increased by 15-30%

real seconds x SpeedOfTime x CalendarSpeedMul = game seconds passed 

deltaTimeToGameSeconds(dt)
return dt / 1000 * World.Calendar.SpeedOfTime * World.Calendar.CalendarSpeedMul 


Weight loss during starvation
first day: 1 kg
first 5 days: 0.5 kg/day
by day 21, falls to 0.3 kg/day and remains stable after that

BMI 12 is generally unrecoverable

60 real seconds = 1 game hour (60x time)
1 real second = 1 game minute
1 real second = 1/1440 of a game day
BMR = 6800
so should be ticking by at 4.7 per second
