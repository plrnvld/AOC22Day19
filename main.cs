using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    public static void Main(string[] args)
    {
        foreach (var bp in ReadBlueprints("Example.txt"))
        {
            Console.WriteLine(bp);
        }
    }

    static IEnumerable<Blueprint> ReadBlueprints(string file) =>
        File.ReadLines(file).Select(Blueprint.From);

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

record struct MoveList(IList<Move> Moves)
{
    static Robot[] allRobots = new Robot[] { Robot.Ore, Robot.Clay, Robot.Obsidian, Robot.Geode };
    
    public IEnumerable<Robot> AllowedToBuy()
    {
        var hasClay = Moves.Any(m => m.Buy == Robot.Clay);
        var hasObsidian = Moves.Any(m => m.Buy == Robot.Obsidian);
        
        if (!hasClay)
            return allRobots[0..2];            

        if (hasClay && !hasObsidian)
            return allRobots[0..3];

        return allRobots[0..4];        
    }
}

record struct Move(Robot Buy, int Num);

enum Robot
{
    Ore,
    Clay,
    Obsidian,
    Geode    
}