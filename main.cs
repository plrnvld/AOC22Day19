using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    public static void Main(string[] args)
    {
        // SmallExample take(1) with MaxMinutes 9 gives 457M options (and counting...)
        foreach (var blueprint in ReadBlueprints("Example.txt").Take(1)) // ################
        {
            Console.WriteLine($"*** Blueprint ***\n{blueprint}\n");

            var blueprintOptimizer = new BlueprintOptimizer(blueprint);
            var best = blueprintOptimizer.GetBestStrategy();

            Console.WriteLine($"\nBest moves have {best.Resources.Geodes} geodes:\n\n{best}\n");
            // Console.WriteLine($"\nBest moves have {best.Resources.Ore} ore:\n\n{best}\n");
            Console.WriteLine(string.Join("\n", best.Moves));
        }
    }

    static IEnumerable<Blueprint> ReadBlueprints(string file) =>
        File.ReadLines(file).Select(Blueprint.From);
}

class BlueprintOptimizer
{
    const int MaxMinutes = 24;
    Blueprint blueprint;

    public BlueprintOptimizer(Blueprint blueprint) => this.blueprint = blueprint;

    public Strategy GetBestStrategy()
    {
        var startMove = Strategy.Empty;
        var best = startMove;
        var step = 0;

        var stack = new Stack<Strategy>();
        stack.Push(startMove);

        while (stack.Any())
        {
            step += 1;

            const int div = 100_000;
            const double milli = 1_000_000;
            if (step % div == 0)
                Console.WriteLine($"> [{step / milli}M] current # is {stack.Count}");

            var newMovesLists = stack.Pop().StepMinute(blueprint, MaxMinutes).ToList();

            foreach (var movesList in newMovesLists)
            {
                if (movesList.CanStillBeatRecord(best.Resources.Geodes + 1, MaxMinutes, blueprint))
                {
                    if (movesList.Resources.Geodes > best.Resources.Geodes)
                    {
                        Console.WriteLine($"! Best result now {movesList.Resources.Geodes}");
                        best = movesList;
                    }

                    stack.Push(movesList);
                }
            }
        }

        return best;
    }
}

record struct Strategy(List<(Move move, int minute)> Moves, Resources Resources)
{
    static Robot[] allRobots = new Robot[] { Robot.Geode, Robot.Obsidian, Robot.Clay, Robot.Ore };
    static Robot[] oreClay = new Robot[] { Robot.Clay, Robot.Ore };
    static Robot[] oreClayObsidian = new Robot[] { Robot.Obsidian, Robot.Clay, Robot.Ore };

    public IEnumerable<Strategy> StepMinute(Blueprint blueprint, int maxMinutes)
    {
        if (Resources.Minutes >= maxMinutes)
            return Enumerable.Empty<Strategy>();

        var nextMovesWithoutBuying = this with { Resources = Resources.Next() };

        return TryBuyingRobots(nextMovesWithoutBuying, blueprint).Concat(new[] { nextMovesWithoutBuying });
    }

    public bool CanStillBeatRecord(int currRecord, int maxMinutes, Blueprint blueprint)
    {
        if (Resources.Minutes == maxMinutes - 4 && Resources.GeodeRobots == 0)
            return false;

        
        return true;

        /*
        var nextRecord = currRecord + 1;
        var stepsRemaining = maxMinutes - Resources.Minutes;

        var directGeodeOutput = Resources.Geodes + Resources.GeodeRobots * stepsRemaining;
        if (directGeodeOutput >= nextRecord)
            return true;

        if (stepsRemaining >= maxMinutes - 10)
            return true;

        if (Resources.Geodes >= nextRecord)
            return true;

        if (stepsRemaining == 1) // Buying robots won't help you        
            return Resources.Geodes + Resources.GeodeRobots >= nextRecord;

        return (Resources.Geodes - stepsRemaining * Resources.GeodeRobots) +
            (Resources.Obsidian - stepsRemaining * Resources.ObsidianRobots) / blueprint.GeodeRobotObsidianPrice +
            (Resources.Clay - stepsRemaining * Resources.ClayRobots) / (blueprint.GeodeRobotObsidianPrice * blueprint.ObsidianRobotClayPrice)
            >= nextRecord;
        */
    }

    static IEnumerable<Strategy> TryBuyingRobots(Strategy strategy, Blueprint blueprint)
    {
        var buyingStrategies = new List<Strategy>();
        var stack = new Stack<(Move, Strategy)>();
        stack.Push((new Move(), strategy));

        // Console.WriteLine("Loop start");
        
        while (stack.Any())
        {
            var (currMove, currStrategy) = stack.Pop();
            var buyableRobots = allRobots.Where(robot => currStrategy.Resources.CanBuy(robot, blueprint));

            foreach (var buyableRobot in buyableRobots)
            {
                var newMove = currMove.BuyExtra(buyableRobot);
                var newResources = currStrategy.Resources.Buy(buyableRobot, blueprint);
                var newStrategy = currStrategy with
                {
                    Moves = currStrategy.Moves.SkipLast(1).Concat(new[] { (newMove, newResources.Minutes) }).ToList(),
                    Resources = newResources
                };

                buyingStrategies.Add(newStrategy);
                stack.Push((newMove, newStrategy));
            };

            // Console.Write(".");
        }
        
        var result = buyingStrategies.GroupBy(s => s.LastMoveKey()).Select(group => group.First()); // Remove duplicate moves

        result = PickMaximizeStrategies(result);

        var num = result.Count();
        if (num >= 50)
        {
            Console.WriteLine($"\nBuy strategies: {num}");
            Console.WriteLine($"{strategy}\n");
            foreach (var bs in buyingStrategies)
            {
                Console.WriteLine($"> {bs.Moves.Last().Item1}");                
            }
        }
        
        return result;
    }

    static IEnumerable<Strategy> PickMaximizeStrategies(IEnumerable<Strategy> strategies)
    {
        var strategiesList = strategies.ToList();

        bool PartiallyBetterExists(Strategy strat)
        {
            return strategiesList.Any(item => item.Moves.Last().Item1.PartiallyBetterThan(strat.Moves.Last().Item1));
        }

        foreach (var strat in strategiesList)
        {
            if (!PartiallyBetterExists(strat))
                yield return strat;  
        }
    }

    public static Strategy Empty = new Strategy(new List<(Move, int)>(), Resources.StartResources);

    public string LastMoveKey() => Moves.Last().Item1.CreateKey();
}

record struct Move(int BuyOreRobots, int BuyClayRobots, int BuyObsidianRobots, int BuyGeodeRobots)
{
    public static Move Buy(Robot robot) => new Move().BuyExtra(robot);

    public Move BuyExtra(Robot robot) =>
        robot switch
        {
            Robot.Ore => this with { BuyOreRobots = BuyOreRobots + 1 },
            Robot.Clay => this with { BuyClayRobots = BuyClayRobots + 1 },
            Robot.Obsidian => this with { BuyObsidianRobots = BuyObsidianRobots + 1 },
            _ => this with { BuyGeodeRobots = BuyGeodeRobots + 1 },
        };

    public string CreateKey()
    {
        var parts = new[] {
                ("Ore", BuyOreRobots),
                ("Cly", BuyClayRobots),
                ("Obs", BuyObsidianRobots),
                ("Geo", BuyGeodeRobots)
            }
        .Where(buy => buy.Item2 > 0)
        .Select(buy => $"{buy.Item1}({buy.Item2})");

        return string.Join("|", parts);
    }

    public bool PartiallyBetterThan(Move other)
    {
        return BuyOreRobots >= other.BuyOreRobots 
            && BuyClayRobots >= other.BuyClayRobots
            && BuyObsidianRobots >= other.BuyObsidianRobots
            && BuyGeodeRobots >= other.BuyGeodeRobots
            && !other.Equals(this);
    }
}

record struct Resources(int Minutes, int Ore, int Clay, int Obsidian, int Geodes, int OreRobots, int ClayRobots, int ObsidianRobots, int GeodeRobots)
{
    public Resources Next() => new Resources(Minutes + 1, Ore + OreRobots, Clay + ClayRobots, Obsidian + ObsidianRobots, Geodes + GeodeRobots, OreRobots, ClayRobots, ObsidianRobots, GeodeRobots);

    public Resources Buy(Robot robot, Blueprint blueprint) =>
        robot switch
        {
            Robot.Ore => this with
            {
                OreRobots = OreRobots + 1,
                Ore = Ore - blueprint.OreRobotOrePrice
            },
            Robot.Clay => this with
            {
                ClayRobots = ClayRobots + 1,
                Ore = Ore - blueprint.ClayRobotOrePrice
            },
            Robot.Obsidian => this with
            {
                ObsidianRobots = ObsidianRobots + 1,
                Ore = Ore - blueprint.ObsidianRobotOrePrice,
                Clay = Clay - blueprint.ObsidianRobotClayPrice
            },
            _ => this with
            {
                GeodeRobots = GeodeRobots + 1,
                Ore = Ore - blueprint.GeodeRobotOrePrice,
                Obsidian = Obsidian - blueprint.GeodeRobotObsidianPrice
            },
        };

    public bool CanBuy(Robot robot, Blueprint blueprint) =>
        robot switch
        {
            Robot.Ore => Ore >= blueprint.OreRobotOrePrice,
            Robot.Clay => Ore >= blueprint.ClayRobotOrePrice,
            Robot.Obsidian => Ore >= blueprint.ObsidianRobotOrePrice && Clay >= blueprint.ObsidianRobotClayPrice,
            _ => Ore >= blueprint.GeodeRobotOrePrice && Obsidian >= blueprint.GeodeRobotObsidianPrice,
        };

    public static Resources StartResources = new Resources(0, 0, 0, 0, 0, OreRobots: 1, 0, 0, 0);
}

enum Robot
{
    Ore = 0,
    Clay,
    Obsidian,
    Geode
}

record struct Blueprint(int Num, int OreRobotOrePrice, int ClayRobotOrePrice, int ObsidianRobotOrePrice, int ObsidianRobotClayPrice, int GeodeRobotOrePrice, int GeodeRobotObsidianPrice)
{
    public static Blueprint From(string line)
    {
        var words = line.Split();
        int Word(int index) => int.Parse(words[index]);

        return new Blueprint(int.Parse(words[1][..^1]), Word(6), Word(12), Word(18), Word(21), Word(27), Word(30));
    }
}