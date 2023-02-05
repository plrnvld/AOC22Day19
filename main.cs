using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

class Program
{
    public static void Main(string[] args)
    {
        var score = 1;

        foreach (var blueprint in ReadBlueprints("Input.txt").Skip(2).Take(1)) // ################
        {
            Console.WriteLine($"*** Blueprint ***\n{blueprint}\n");

            var blueprintOptimizer = new BlueprintOptimizer(blueprint);

            var timer = new Stopwatch();
            timer.Start();

            var best = blueprintOptimizer.GetBestStrategy();

            timer.Stop();

            score *= best.Resources.Geodes;

            Console.WriteLine($"\nBest moves have {best.Resources.Geodes} geodes:\n\n{best}\n");
            Console.WriteLine(string.Join("\n", best.Moves));

            Console.WriteLine($"\nCalculation took {timer.Elapsed}");
        }

        Console.WriteLine($"\n\n!!!Final score {score}");
    }

    static IEnumerable<Blueprint> ReadBlueprints(string file) =>
        File.ReadLines(file).Select(Blueprint.From);
}

class BlueprintOptimizer
{
    const int MaxMinutes = 32;
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

            const int div = 100_000_000;
            const double milli = 1_000_000;
            if (step % div == 0)
                Console.WriteLine($"  [{step / milli}M] current stack size: {stack.Count}");

            var strategies = stack.Pop().StepMinute(blueprint, MaxMinutes);

            foreach (var strategy in strategies)
            {
                if (strategy.CanStillBeatRecord(best.Resources.Geodes, MaxMinutes, blueprint))
                {
                    if (strategy.Resources.Minutes == MaxMinutes && strategy.Resources.Geodes > best.Resources.Geodes)
                    {
                        Console.WriteLine($"🚀 Best result now {strategy.Resources.Geodes}");
                        best = strategy;
                    }

                    stack.Push(strategy);
                }
            }
        }

        return best;
    }
}

record struct Strategy(List<(Move move, int minute)> Moves, Resources Resources, Resources PrevResources)
{
    static Robot[] allRobots = new Robot[] { Robot.Geode, Robot.Obsidian, Robot.Clay, Robot.Ore };
    static Robot[] allRobotsReversed = new Robot[] { Robot.Ore, Robot.Clay, Robot.Obsidian, Robot.Geode };
    static Robot[] oreClay = new Robot[] { Robot.Clay, Robot.Ore };
    static Robot[] oreClayObsidian = new Robot[] { Robot.Obsidian, Robot.Clay, Robot.Ore };
    readonly IEnumerable<Strategy> noStrategies = Enumerable.Empty<Strategy>();

    public IEnumerable<Strategy> StepMinute(Blueprint blueprint, int maxMinutes)
    {
        if (Resources.Minutes >= maxMinutes)
            return noStrategies;

        var nextMovesWithoutBuying = this with { Resources = Resources.Next(), PrevResources = Resources };

        return TryBuyingRobots(nextMovesWithoutBuying, blueprint, maxMinutes).Concat(new[] { nextMovesWithoutBuying });
    }

    public bool CanStillBeatRecord(int currRecord, int maxMinutes, Blueprint blueprint)
    {
        var stepsRemaining = maxMinutes - Resources.Minutes;

        switch (stepsRemaining)
        {
            case 1:
                if (Resources.Geodes + Resources.GeodeRobots <= currRecord)
                    return false;
                break;
            case 2:
                if (Resources.Geodes + 2 * Resources.GeodeRobots <= currRecord && Resources.Obsidian < blueprint.GeodeRobotObsidianPrice)
                    return false;
                break;
            case 3:
                if (Resources.Geodes + 3 * Resources.GeodeRobots <= currRecord && Resources.Obsidian + Resources.ObsidianRobots < blueprint.GeodeRobotObsidianPrice)
                    return false;
                break;
            default: 
                break;
        }
        
        // if (stepsRemaining == 10 && Resources.GeodeRobots == 0)
        //       return false;

        if (Resources.Clay > 50)
            return false;

        if (Resources.Obsidian > 100)
            return false;

        if (Resources.Ore > 30) // ############## Was 20
            return false;

        // Example(2) Got the answer after 200M steps with 30,30,15
        // Example(2) Got the answer after 74M steps with 10,30,15, took 8min44
        // Example(2) Got the answer after ? steps with 10,10,15, took ?
        
        return true;
    }

    static IEnumerable<Strategy> TryBuyingRobots(Strategy strategy, Blueprint blueprint, int maxMinutes)
    {
        var robotsForSale = strategy.Resources.Minutes * 2 > maxMinutes 
            ? allRobots 
            : allRobotsReversed;
        var buyableRobots = robotsForSale.Where(robot => strategy.PrevResources.CanBuy(robot, blueprint));

        foreach (var buyableRobot in buyableRobots)
        {
            var newResources = strategy.Resources.Buy(buyableRobot, blueprint);
            var newMoves = new List<(Move move, int minute)>(strategy.Moves.Count + 1);
            newMoves.AddRange(strategy.Moves);
            newMoves.Add((new Move(buyableRobot), newResources.Minutes));
            yield return strategy with
            {
                Moves = newMoves,
                Resources = newResources
            };
        };
    }

    public static Strategy Empty = new(new List<(Move, int)>(), Resources: Resources.StartResources, PrevResources: Resources.EmptyResources);
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

    public static Resources StartResources = new(0, 0, 0, 0, 0, OreRobots: 1, 0, 0, 0);
    public static Resources EmptyResources = new();
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

// Blueprint 1 has 29 geodes (checking finished 1.8B with 50 Cly, 100 Obs, 30 Ore)
// Blueprint 2 has 5 geodes?? (checking 3.1B with 100 Cly, 100 Obs, 30 Ore)
// Blueprint 3 has 12 geodes??? (checking 3.4B with 50 Cly, 100 Obs, 30 Ore)

// 1450 too low
// 1740 too low
// 1885 (29 X 5 X 13) not it, because 1914 too low
// 1914 (29 x 6 x 11) too low
// 2520 (30 x 7 x 12) not right
// 2436 (29 x 7 x 12) not right
// 2030 (29 x 5 x 14) not right
// 2175 (29 x 5 x 15) not right
// 1920 (32 x 5 x 12) not right