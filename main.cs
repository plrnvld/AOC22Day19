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

                        if (best.Resources.Geodes is 11)
                        {
                            var movesTexts = best.Moves.Select(m => $"  [{m.Item2}] {m.Item1}");
                            Console.WriteLine(string.Join("\n", movesTexts));
                        }
                    }

                    stack.Push(movesList);
                }
            }
        }

        return best;
    }
}

record struct Strategy(List<(Move move, int minute)> Moves, Resources Resources, Resources PrevResources)
{
    static Robot[] allRobots = new Robot[] { Robot.Geode, Robot.Obsidian, Robot.Clay, Robot.Ore };
    static Robot[] oreClay = new Robot[] { Robot.Clay, Robot.Ore };
    static Robot[] oreClayObsidian = new Robot[] { Robot.Obsidian, Robot.Clay, Robot.Ore };

    public IEnumerable<Strategy> StepMinute(Blueprint blueprint, int maxMinutes)
    {
        if (Resources.Minutes >= maxMinutes)
            return Enumerable.Empty<Strategy>();

        var nextMovesWithoutBuying = this with { Resources = Resources.Next(), PrevResources = Resources };

        return TryBuyingRobots(nextMovesWithoutBuying, blueprint).Concat(new[] { nextMovesWithoutBuying });
    }

    public bool CanStillBeatRecord(int currRecord, int maxMinutes, Blueprint blueprint)
    {
        var stepsRemaining = maxMinutes - Resources.Minutes;

        if (stepsRemaining == 1 && Resources.Geodes + Resources.GeodeRobots <= currRecord)
            return false;

        if (stepsRemaining == 2 && Resources.Geodes + 2 * Resources.GeodeRobots <= currRecord && Resources.Obsidian < blueprint.GeodeRobotObsidianPrice)
            return false;

        if (stepsRemaining == 3 && Resources.Geodes + 3 * Resources.GeodeRobots <= currRecord && Resources.Obsidian + Resources.ObsidianRobots < blueprint.GeodeRobotObsidianPrice)
            return false;

        if (stepsRemaining == 4 && Resources.GeodeRobots == 0)
             return false;

        if (Resources.Clay > blueprint.ObsidianRobotClayPrice + Resources.ClayRobots + 20)
            return false;

        if (Resources.Obsidian > blueprint.GeodeRobotObsidianPrice + Resources.ObsidianRobots + 20)
            return false;

        var maxOrePrice = new[] { blueprint.GeodeRobotOrePrice, blueprint.ObsidianRobotOrePrice, blueprint.ClayRobotOrePrice }.Max();
        if (Resources.Ore > maxOrePrice * 2)
            return false;

        return true;
    }

    static IEnumerable<Strategy> TryBuyingRobots(Strategy strategy, Blueprint blueprint)
    {
        var buyingStrategies = new List<Strategy>();

        var buyableRobots = allRobots.Where(robot => strategy.PrevResources.CanBuy(robot, blueprint));

        foreach (var buyableRobot in buyableRobots)
        {
            var newMove = new Move(buyableRobot);
            var newResources = strategy.Resources.Buy(buyableRobot, blueprint);
            var newStrategy = strategy with
            {
                Moves = strategy.Moves.Concat(new[] { (newMove, newResources.Minutes) }).ToList(),
                Resources = newResources,
                PrevResources = strategy.PrevResources
            };

            buyingStrategies.Add(newStrategy);
        };

        return buyingStrategies;
    }

    public static Strategy Empty = new Strategy(new List<(Move, int)>(), Resources: Resources.StartResources, PrevResources: Resources.EmptyResources);
}

record struct Move(Robot BuyRobot);

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
    public static Resources EmptyResources = new ();
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