using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

class Program
{
    public static void Main(string[] args)
    {
        var quality = 0;

        foreach (var blueprint in ReadBlueprints("Input.txt")) // ################
        {
            Console.WriteLine($"*** Blueprint ***\n{blueprint}\n");

            var blueprintOptimizer = new BlueprintOptimizer(blueprint);

            var timer = new Stopwatch();
            timer.Start();

            var best = blueprintOptimizer.GetBestStrategy();

            timer.Stop();

            quality += best.Resources.Geodes * blueprint.Num;

            Console.WriteLine($"\nBest moves have {best.Resources.Geodes} geodes:\n\n{best}\n");
            Console.WriteLine(string.Join("\n", best.Moves));

            Console.WriteLine($"\nCalculation took {timer.Elapsed}");
        }


        Console.WriteLine($"\n\n!!!Final quality {quality}");
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

            const int div = 20_000_000;
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
    static Robot[] oreClay = new Robot[] { Robot.Clay, Robot.Ore };
    static Robot[] oreClayObsidian = new Robot[] { Robot.Obsidian, Robot.Clay, Robot.Ore };
    readonly IEnumerable<Strategy> noStrategies = Enumerable.Empty<Strategy>();

    public IEnumerable<Strategy> StepMinute(Blueprint blueprint, int maxMinutes)
    {
        if (Resources.Minutes >= maxMinutes)
            return noStrategies;

        var nextMovesWithoutBuying = this with { Resources = Resources.Next(), PrevResources = Resources };

        return TryBuyingRobots(nextMovesWithoutBuying, blueprint).Concat(new[] { nextMovesWithoutBuying });
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
        
        // if (stepsRemaining == 4 && Resources.GeodeRobots == 0)
        //       return false;

        if (Resources.Clay > blueprint.ObsidianRobotClayPrice + Resources.ClayRobots + 20)
            return false;

        if (Resources.Obsidian > blueprint.GeodeRobotObsidianPrice + Resources.ObsidianRobots + 20)
            return false;

        if (Resources.Ore > 20) // ############## Was 20
            return false;

        // Example(2) Got the answer after 200M steps with 30,30,15
        // Example(2) Got the answer after 74M steps with 10,30,15, took 8min44
        // Example(2) Got the answer after ? steps with 10,10,15, took ?
        
        return true;
    }

    static IEnumerable<Strategy> TryBuyingRobots(Strategy strategy, Blueprint blueprint)
    {
        var buyableRobots = allRobots.Where(robot => strategy.PrevResources.CanBuy(robot, blueprint));

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


// 1522 too low