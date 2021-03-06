﻿using System;
using System.Threading.Tasks;
using BedrockServerConfigurator.Library.Commands;
using BedrockServerConfigurator.Library.Entities;

namespace BedrockServerConfigurator.Library.Minigame.Microgames
{
    public class SpawnRandomMobsMicrogame : Microgame
    {
        public int MinMobs { get; }
        public int MaxMobs { get; }

        readonly string[] hostileMobs =
        {
            "creeper",
            "skeleton",
            "zombie",
            "witch",
            "blaze"
        };

        public SpawnRandomMobsMicrogame(TimeSpan minDelay, TimeSpan maxDelay, ServerPlayer player, ServerApi api, int minMobs, int maxMobs) :
            base(minDelay, maxDelay, player, api)
        {
            MinMobs = minMobs;
            MaxMobs = maxMobs;
        }

        public override Func<Task> GetGame()
        {
            var amount = Utilities.RandomGenerator.Next(MinMobs, MaxMobs + 1);
            var mob = hostileMobs.RandomElement();

            MicrogameCreated(new MicrogameEventArgs(this, $"Mobs: {mob}, Amount: {amount}"));

            string[] messages =
            {
                $"Hey {Player.Name}, I hope you like {mob}'s. And I hope you like {amount} of them.",
                $"{Player.Name}, go ahead and hug all those {amount} {mob}'s.",
                $"Dear {Player.Name}, enjoy some company with {amount} of your new {mob} friends.",
                $"Knock knock {Player.Name}. Who's there you're asking? Oh just {amount} {mob}'s."
            };

            var randomMessage = messages.RandomElement();

            async Task game()
            {
                await Api.Say(randomMessage);
                await Api.SpawnMobsOnEntity(Player.Name, mob, amount);
            }

            return game;
        }
    }
}
