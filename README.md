Valheim mod that moves deer and hare prefabs from default procreation/tameable and animalAI into custom components that allow taming, the mod is modular, if the user modifies/adds in animal config another prefab instance, for example

{
            "$enemy_neck", new AnimalConfig
            {
                AnimalName = "Neck",
                TamingTime = 600f,
                FedDuration = 600f,
                PregnancyDuration = 600f,
                MaxCreatures = 6,
                PartnerCheckRange = 2f,
                PregnancyChance = 1f,
                SpawnOffset = 2f,
                RequiredLovePoints = 5,
                MinOffspringLevel = 1,
                FoodItems = new List<string> { "Blueberries", "Raspberry", "Cloudberry" }
            }
        },
}


And then rebuilds the project, the neck will behave in the same way as the deer/hare.
