using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

class Program
{
    public static void Main(string[] args)
    {
        var score = 0;

        foreach (var blueprint in ReadBlueprints("Input.txt").Skip(0).Take(30)) // ################
        {
            Console.WriteLine($"*** Blueprint {blueprint.Num} ***\n{blueprint}\n");

            var blueprintOptimizer = new BlueprintOptimizer(blueprint);

            var timer = new Stopwatch();
            timer.Start();

            var best = blueprintOptimizer.GetBestStrategy();

            timer.Stop();

            var n = blueprint.Num;
            var g = best.Resources.Geodes;
            Console.WriteLine($"\n***** ({n} * {g}) = {n * g} ! 🥦");

            score += g * n;

            // Console.WriteLine($"\nBest moves have {best.Resources.Geodes} geodes:\n\n{best}\n");
            // Console.WriteLine(string.Join("\n", best.Moves));

            Console.WriteLine($"\nCalculation took {timer.Elapsed}\n");
        }

        Console.WriteLine($"\n\n!!!Final score {score}");
    }

    static IEnumerable<Blueprint> ReadBlueprints(string file) =>
        File.ReadLines(file).Select(Blueprint.From);
}

class BlueprintOptimizer
{
    const int MaxMinutes = 24;
    const int endGameStart = 5;
    Blueprint blueprint;

    public BlueprintOptimizer(Blueprint blueprint) => this.blueprint = blueprint;

    public Strategy GetBestStrategy()
    {
        var startMove = Strategy.Empty;
        var best = startMove;
        var step = 0L;

        var stack = new Stack<Strategy>();
        stack.Push(startMove);

        while (stack.Any())
        {
            step += 1;

            const int div = 100_000_000;
            const double milli = 1_000_000;
            if (step % div == 0)
                Console.WriteLine($"  [{step / milli}M] current stack size: {stack.Count}");

            var strategies = stack.Pop().StepMinute(blueprint, MaxMinutes, endGameStart);

            foreach (var strategy in strategies)
            {
                if (strategy.CanStillBeatRecord(best.Resources.Geodes, MaxMinutes, blueprint))
                {
                    if (strategy.Resources.Minutes == MaxMinutes && strategy.Resources.Geodes > best.Resources.Geodes)
                    {
                        var res = strategy.Resources;
                        Console.WriteLine($"🚀 Best result now {res.Geodes}");
                        Console.WriteLine($"   (Max ore: {res.MaxOre}, max clay: {res.MaxClay}, max obsidian: {res.MaxObsidian})");
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
    readonly IEnumerable<Strategy> noStrategies = Enumerable.Empty<Strategy>();

    public IEnumerable<Strategy> StepMinute(Blueprint blueprint, int maxMinutes, int endGameStart)
    {
        if (Resources.Minutes == maxMinutes - endGameStart) // Endgame time
            return new[] { GetEndGameStrategy(this, blueprint, maxMinutes, endGameStart) };

        if (Resources.Minutes >= maxMinutes)
            return noStrategies;

        var nextStrategyWithoutBuying = GetNextStrategyWithoutBuying(this);

        var buyStrategies = TryBuyingRobots(nextStrategyWithoutBuying, blueprint, maxMinutes);

        var count = buyStrategies.Count();
        if (count is 4 || count is 1 && buyStrategies.First().Moves.Last().move.Matches(Robot.Geode))
            return buyStrategies;        

        return buyStrategies.Concat(new[] { nextStrategyWithoutBuying });
    }

    static Strategy GetNextStrategyWithoutBuying(Strategy strategy) =>
        strategy with { Resources = strategy.Resources.Next(), PrevResources = strategy.Resources };


    static Strategy GetEndGameStrategy(Strategy strategy, Blueprint blueprint, int maxMinutes, int endGameStart)
    {
        if (strategy.Resources.Minutes + endGameStart != maxMinutes)
            throw new Exception("Weird!");

        var next = strategy;

        for (var i = 0; i < endGameStart - 2; i++)
        {
            var t = endGameStart - 2 - i;
            next = GetNextStrategyWithoutBuying(next);

            var allBuys = TryBuyingRobots(next, blueprint, maxMinutes);

            var gBuys = allBuys.Where(s => s.Moves.Last().move.Matches(Robot.Geode));
            if (gBuys.Any())
            {
                next = gBuys.First();
                continue;
            }

            if (next.Resources.Obsidian + next.Resources.ObsidianRobots < blueprint.GeodeRobotObsidianPrice
                && next.Resources.Obsidian + next.Resources.ObsidianRobots + 1 == blueprint.GeodeRobotObsidianPrice)
            {
                var oBuys = allBuys.Where(s => s.Moves.Last().move.Matches(Robot.Obsidian));
                if (oBuys.Any())
                {
                    next = oBuys.First();
                    continue;
                }
            }

            if (next.Resources.Ore + next.Resources.OreRobots < blueprint.GeodeRobotOrePrice
                && next.Resources.Ore + next.Resources.OreRobots + 1 == blueprint.GeodeRobotObsidianPrice)
            {
                var rBuys = allBuys.Where(s => s.Moves.Last().move.Matches(Robot.Ore));
                if (rBuys.Any())
                {
                    next = rBuys.First();
                    continue;
                }
            }

            if (t == 2 && next.Resources.Obsidian + next.Resources.ObsidianRobots < blueprint.GeodeRobotObsidianPrice
                && next.Resources.Obsidian + next.Resources.ObsidianRobots + 2 == blueprint.GeodeRobotObsidianPrice)
            {
                var oBuys = allBuys.Where(s => s.Moves.Last().move.Matches(Robot.Obsidian));
                if (oBuys.Any())
                {
                    next = oBuys.First();
                    continue;
                }
            }

            if (t == 2 && next.Resources.Ore + next.Resources.OreRobots < blueprint.GeodeRobotOrePrice
                && next.Resources.Ore + next.Resources.OreRobots + 2 == blueprint.GeodeRobotObsidianPrice)
            {
                var rBuys = allBuys.Where(s => s.Moves.Last().move.Matches(Robot.Ore));
                if (rBuys.Any())
                {
                    next = rBuys.First();
                    continue;
                }
            }

            if (t == 3 && next.Resources.Obsidian + next.Resources.ObsidianRobots < blueprint.GeodeRobotObsidianPrice
                && next.Resources.Obsidian + next.Resources.ObsidianRobots + 3 == blueprint.GeodeRobotObsidianPrice)
            {
                var oBuys = allBuys.Where(s => s.Moves.Last().move.Matches(Robot.Obsidian));
                if (oBuys.Any())
                {
                    next = oBuys.First();
                    continue;
                }
            }

            if (t == 3 && next.Resources.Ore + next.Resources.OreRobots < blueprint.GeodeRobotOrePrice
                && next.Resources.Ore + next.Resources.OreRobots + 3 == blueprint.GeodeRobotObsidianPrice)
            {
                var rBuys = allBuys.Where(s => s.Moves.Last().move.Matches(Robot.Ore));
                if (rBuys.Any())
                {
                    next = rBuys.First();
                    continue;
                }
            }
        }

        // MaxMinutes - 2
        next = GetNextStrategyWithoutBuying(next);
        var geodeBuys = TryBuyingRobots(next, blueprint, maxMinutes).Where(s => s.Moves.Last().move.Matches(Robot.Geode));
        next = geodeBuys.Any() ? geodeBuys.First() : next;

        // MaxMinutes - 1
        return GetNextStrategyWithoutBuying(next);
    }

    public bool CanStillBeatRecord(int currRecord, int maxMinutes, Blueprint blueprint)
    {
        var stepsRemaining = maxMinutes - Resources.Minutes;

        // if (stepsRemaining == 8 && Resources.Geodes < 2)
        //    return false;

        if (stepsRemaining == 6) // ############### Is this correct?
        {
            var maxExtraGeodes = Resources.Ore >= blueprint.GeodeRobotOrePrice && Resources.Obsidian >= blueprint.GeodeRobotObsidianPrice ? 15 : 10;

            var maxGeodes = Resources.Geodes + 6 * Resources.GeodeRobots + maxExtraGeodes;
            if (maxGeodes <= currRecord)
                return false;
        }
                
        if (Resources.Ore > 29)
            return false;
        
        if (Resources.Clay > 57)
            return false;        
        
        if (Resources.Obsidian > 23)
            return false;

        // if (Resources.Ore + Resources.Clay + Resources.Obsidian > 100) //############ Changing this to 200
        //    return false;

        return true;
    }

    static IEnumerable<Strategy> TryBuyingRobots(Strategy strategy, Blueprint blueprint, int maxMinutes)
    {
        var buyableRobots = allRobots.Where(robot => strategy.CanBuy(robot, blueprint));

        if (buyableRobots.Contains(Robot.Geode))
            yield return BuyRobot(strategy, Robot.Geode, blueprint);
        else
        {
            foreach (var buyableRobot in buyableRobots)
                yield return BuyRobot(strategy, buyableRobot, blueprint);
        }
    }

    public bool CanBuy(Robot robot, Blueprint blueprint) => PrevResources.CanBuy(robot, blueprint);

    static Strategy BuyRobot(Strategy strategy, Robot robot, Blueprint blueprint)
    {
        var newResources = strategy.Resources.Buy(robot, blueprint);
        var newMoves = new List<(Move move, int minute)>(strategy.Moves.Count + 1);
        newMoves.AddRange(strategy.Moves);
        newMoves.Add((new Move(robot), newResources.Minutes));
        return strategy with
        {
            Moves = newMoves,
            Resources = newResources
        };
    }

    public static Strategy Empty = new(new List<(Move, int)>(), Resources: Resources.StartResources, PrevResources: Resources.EmptyResources);
}

record struct Move(Robot BuyRobot)
{
    public bool Matches(Robot robot) => BuyRobot == robot;
};

record struct Resources(int Minutes, int Ore, int Clay, int Obsidian, int Geodes, int OreRobots, int ClayRobots, int ObsidianRobots, int GeodeRobots, int MaxOre, int MaxClay, int MaxObsidian)
{
    public Resources Next()
    {
        var nextOre = Ore + OreRobots;
        var nextClay = Clay + ClayRobots;
        var nextObsidian = Obsidian + ObsidianRobots;
        return new Resources(Minutes + 1, nextOre, nextClay, nextObsidian, Geodes + GeodeRobots, OreRobots, ClayRobots, ObsidianRobots, GeodeRobots,
            Math.Max(MaxOre, nextOre), Math.Max(MaxClay, nextClay), Math.Max(MaxObsidian, nextObsidian));
    }

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

    public static Resources StartResources = new(0, 0, 0, 0, 0, OreRobots: 1, 0, 0, 0, 0, 0, 0);
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

// Blueprint 1 has 29 geodes (checking finished 1.8B with 50 Cly, 100 Obs, 30 Ore) (but with `if (stepsRemaining == 10` limitation)
// Blueprint 2 has 10 geodes?? (checking 3.1B with 100 Cly, 100 Obs, 30 Ore)
// Blueprint 3 has 16 geodes??? (checking 3.5B with 50 Cly, 100 Obs, 30 Ore)

// 1450 too low
// 1740 too low
// 1885 (29 X 5 X 13) not it, because 1914 too low
// 1914 (29 x 6 x 11) too low
// 2520 (30 x 7 x 12) not right
// 2436 (29 x 7 x 12) not right
// 2030 (29 x 5 x 14) not right
// 2175 (29 x 5 x 15) not right
// 1920 (32 x 5 x 12) not right

// 3480 (32 x 10 x 12) not right
// 4640 (29 x 10 x 16) not right (guessed 22:46)
// 4800 (30 x 10 x 16) not it (23:32)
// 4960 (31 x 10 x 16) not it 23:51
// 5120 (32 x 10 x 16) not it
// 5632 (32 x 11 x 16) not it (guessed 19:14)
// 5104 (29 x 11 x 16) not it (guessed 19:58)


// New calculation (Feb 14, after calculating 7 hours, with Ore + Clay + Obsidian <= 200, and endgame start = 5) now says blueprint 1 has a max result of 29??