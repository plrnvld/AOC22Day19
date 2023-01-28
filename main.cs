using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    public static void Main(string[] args)
    {
        foreach (var blueprint in ReadBlueprints("SmallExample.txt").Take(1)) // ################
        {
            Console.WriteLine($"*** Blueprint ***\n{blueprint}\n");

            var blueprintOptimizer = new BlueprintOptimizer(blueprint);
            var best = blueprintOptimizer.GetBestMoveList();

            var moveListText = string.Join(", ", best.Moves);
            Console.WriteLine($"\nBest moves have {best.Resources.Geodes} geodes: {best}\n");
            Console.WriteLine(moveListText);
        }
    }

    static IEnumerable<Blueprint> ReadBlueprints(string file) =>
        File.ReadLines(file).Select(Blueprint.From);
}

class BlueprintOptimizer
{
    const int MaxMinutes = 12;
    Blueprint blueprint;

    public BlueprintOptimizer(Blueprint blueprint) => this.blueprint = blueprint;

    public MoveList GetBestMoveList()
    {
        var startMove = MoveList.Empty;
        var bestMoves = startMove;
        var step = 0;

        var stack = new Stack<MoveList>();
        stack.Push(startMove);

        while (stack.Any())
        {
            step += 1;

            const int div = 1_000_000;
            const int milli = 1_000_000;
            if (step % div == 0)
                Console.WriteLine($"> [{step / milli}M] current # is {stack.Count}");

            var newMovesLists = stack.Pop().StepMinute(blueprint, MaxMinutes).ToList();

            foreach (var movesList in newMovesLists)
            {
                if (movesList.Resources.Geodes > bestMoves.Resources.Geodes)
                {
                    Console.WriteLine($"! Best result now {movesList.Resources.Geodes}");
                    bestMoves = movesList;
                }
            }

            foreach (var movesList in newMovesLists)
                stack.Push(movesList);
        }

        return bestMoves;
    }
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

record struct MoveList(List<(Move move, int minute)> Moves, Resources Resources)
{
    static Robot[] allRobots = new Robot[] { Robot.Ore, Robot.Clay, Robot.Obsidian, Robot.Geode };

    static Robot[] oreClay = new Robot[] { Robot.Ore, Robot.Clay };

    static Robot[] oreClayObsidian = new Robot[] { Robot.Ore, Robot.Clay, Robot.Obsidian };

    public IEnumerable<MoveList> StepMinute(Blueprint blueprint, int maxMinutes)
    {
        if (Resources.Minutes >= maxMinutes)
            return Enumerable.Empty<MoveList>();

        var resources = Resources;
        var possibleRobots = PossibleRobots().ToList();
        var nextMoves = this with { Resources = Resources.Next() };

        var buyableRobots = possibleRobots.Where(robot => resources.CanBuy(robot, blueprint));

        // ########################## What about buying multiple robots??????????
        return TryBuyingRobots(nextMoves, possibleRobots, blueprint);
    }

    static IEnumerable<MoveList> TryBuyingRobots(MoveList moveList, List<Robot> possibleRobots, Blueprint blueprint)
    {
        var buyableRobots = possibleRobots.Where(robot => moveList.Resources.CanBuy(robot, blueprint));
        var addRobotsToMoves = buyableRobots.Select(newRobot =>
        {
            // What about buying multiple robots
            var buyRobotMove = (new Move(newRobot), moveList.Resources.Minutes);
            return moveList with
            {
                Moves = moveList.Moves.Concat(new[] { buyRobotMove }).ToList(),
                Resources = moveList.Resources.Buy(newRobot, blueprint)
            };
        });

        return addRobotsToMoves.Concat(new[] { moveList });
    }

    public IEnumerable<Robot> PossibleRobots()
    {
        var hasClay = Moves.Any(m => m.move.Buy == Robot.Clay);
        var hasObsidian = Moves.Any(m => m.move.Buy == Robot.Obsidian);

        if (!hasClay)
            return oreClay;

        if (hasClay && !hasObsidian)
            return oreClayObsidian;

        return allRobots;
    }

    public static MoveList Empty = new MoveList(new List<(Move, int)>(), Resources.StartResources);
}

record struct Move(Robot Buy);

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

    public static Resources StartResources = new Resources(0, 0, 0, 0, 0, 1, 0, 0, 0);
}

enum Robot
{
    Ore = 0,
    Clay,
    Obsidian,
    Geode
}