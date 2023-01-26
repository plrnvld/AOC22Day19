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
            Console.WriteLine($"Best moves have {best.Resources.Geodes} geodes: {best}\n");
            Console.WriteLine(moveListText);
        }
    }

    static IEnumerable<Blueprint> ReadBlueprints(string file) =>
        File.ReadLines(file).Select(Blueprint.From);
}

class BlueprintOptimizer
{
    const int MaxMinutes = 6;
    Blueprint blueprint;

    public BlueprintOptimizer(Blueprint blueprint) => this.blueprint = blueprint;

    public MoveList GetBestMoveList()
    {
        var startMove = MoveList.Empty;
        var bestMoves = startMove;
        var currMinute = 0;
        var step = 0;

        var queue = new Stack<MoveList>();
        queue.Push(startMove);

        while (queue.Any())
        {
            const int div = 1_000_000;
            const int milli = 1_000_000;
            if (step % div == 0)
                Console.WriteLine($"> [{step/milli}M] current # is {queue.Count}");
            step += 1;
            
            var curr = queue.Pop();

            if (curr.Resources.Minutes > currMinute)
            {
                currMinute = curr.Resources.Minutes;
                // Console.WriteLine($"Starting with minute {currMinute}");
            }

            var newMovesLists = curr.StepMinute(blueprint).ToList();

            foreach (var movesList in newMovesLists.Where(ml => ml.Resources.Minutes <= MaxMinutes))
            {
                if (movesList.Resources.Ore > bestMoves.Resources.Ore)
                {
                    Console.WriteLine($"! Best result now {movesList.Resources.Ore}");
                    bestMoves = movesList;
                }
            }

            // Only enqueue when there are minutes left
            foreach (var movesList in newMovesLists.Where(ml => ml.Resources.Minutes <= MaxMinutes))
            {
                // var lastMoveText = movesList.Moves.Any() ?  movesList.Moves.Last().ToString() : "<no moves>";
                // Console.WriteLine($"    enqueueing with last move {lastMoveText}");
                queue.Push(movesList);
            }
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

    public IEnumerable<MoveList> StepMinute(Blueprint blueprint)
    {
        var resources = Resources;
        var possibleRobots = PossibleRobots().ToList();
        var nextRobots = possibleRobots.Where(robot => resources.CanBuy(robot, blueprint)).ToList();
        var nextMoves = this with { Resources = Resources.Next() };

        // if (possibleRobots.Count > 1)
        //     Console.WriteLine($"> Possible robots: {string.Join(",", possibleRobots)}");

        if (!nextRobots.Any())
        {
            // Console.WriteLine("> No robots to buy");
            return new[] { nextMoves };
        }
        else
        {
            // ################ What about buying multiple robots at once?
            
            var addRobotsToMoves = nextRobots.Select(newRobot =>
                {
                    var newMove = (new Move(newRobot), resources.Minutes);
                    var newMovesList = nextMoves with 
                    { 
                        Moves = nextMoves.Moves.Concat(new[] { newMove }).ToList(),
                        Resources = resources.Buy(newRobot, blueprint) 
                    };
                    // Console.WriteLine($"> Buying {newRobot}");
                    return newMovesList;
                });

            var waitWithoutBuying = nextMoves;
            return addRobotsToMoves.Concat(new [] { waitWithoutBuying });;
        }    
    }

    public IEnumerable<Robot> PossibleRobots()
    {
        
        
        IEnumerable<Robot> Trace(Robot[] robots)
        {
            // Console.WriteLine($"Trace possible: {string.Join("|", robots)}");
            return robots;
        }
        
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