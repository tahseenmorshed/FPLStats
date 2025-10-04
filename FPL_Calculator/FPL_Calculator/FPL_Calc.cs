using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using FPL_Calculator;

class FPL_Calc
{
    public static void Main(string[] args)
    {
        string baseDir = AppContext.BaseDirectory;
        string projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        string filepath = Path.Combine(projectDir, "player_stats.txt");

        if (!File.Exists(filepath))
        {
            Console.WriteLine("File not found: " + filepath);
            return;
        }

        string[] lines = File.ReadAllLines(filepath);
        List<PlayerStats> newList = ParseFile(lines);

        // Output: {projectDir}/Scores/player_scores.txt
        string scoresDir = Path.Combine(projectDir, "Scores");
        if (!Directory.Exists(scoresDir))
        {
            Directory.CreateDirectory(scoresDir);
        }
        string outPath = Path.Combine(scoresDir, "player_scores.txt");

        StreamWriter sw = new StreamWriter(outPath, false);
        sw.WriteLine("FANTASY SCORING REPORT");
        sw.WriteLine("Source: " + filepath);
        sw.WriteLine("========================================================================");

        for (int i = 0; i < newList.Count; i++)
        {
            PlayerStats p = newList[i];
            int totalScore = p.CalculatePlayerScore();

            sw.WriteLine("Name: " + p.Name);
            sw.WriteLine("Position: " + (string.IsNullOrEmpty(p.Position) ? "-" : p.Position));
            sw.WriteLine("Club: " + (string.IsNullOrEmpty(p.Club) ? "-" : p.Club));
            sw.WriteLine("  TOTAL: " + totalScore.ToString());
            sw.WriteLine("------------------------------------------------------------------------");
        }

        sw.Flush();
        sw.Close();

        Console.WriteLine("Wrote report: " + outPath);
    }

    static List<PlayerStats> ParseFile(string[] list)
    {
        List<PlayerStats> players = new List<PlayerStats>();
        List<string> PlayerList = new List<string>();

        for (int i = 0; i < list.Length; i++)
        {
            string line = list[i];


            string[] parts = line.Split(new[] { ",\"['" }, StringSplitOptions.None);

            if (parts.Length > 1)
            {
                //Extract the stats
                parts[1] = parts[1].Substring(0, parts[1].Length - 3);

                string[] part1 = parts[0].Split(',');
                string player_name = part1[0];
                string stat_type = part1[1];
                PlayerStats player; 

                //Search if player already exists
                if (PlayerList.Contains(player_name))
                {
                    player = GetPlayerByName(player_name, players);
                }
                else
                {
                    player = new PlayerStats();
                    player.Name = player_name;

                    Passing passing = new Passing();
                    Defending defending = new Defending();
                    Possession possession = new Possession();
                    Scoring scoring = new Scoring();
                    Negatives negatives = new Negatives();
                    player.Pass = passing;
                    player.Defence = defending;
                    player.Score = scoring;
                    player.Poss = possession;
                    player.Negative = negatives;

                    players.Add(player);
                    PlayerList.Add(player_name);
                }

                //Handle the missing data
                string[] stat_parts = parts[1].Split(new[] { "', '" }, StringSplitOptions.None);
                for (int k=0; k<stat_parts.Length; k++)
                {
                    if (stat_parts[k].Equals(""))
                    {
                        stat_parts[k] = "0"; 
                    }
                }

                if (stat_type.Equals("Miscellaneous Stats"))
                {
                    ParseMisc(player, stat_parts);
                }

                if (stat_type.Equals("Possession"))
                {
                    ParsePossession(player, stat_parts);
                }

                if (stat_type.Equals("Defensive Actions"))
                {
                    ParseDefence(player, stat_parts);
                }

                if (stat_type.Equals("Pass Types"))
                {
                    
                }

                if (stat_type.Equals("Passing"))
                {
                    ParsePass(player, stat_parts);
                }

                if (stat_type.Equals("Summary"))
                {
                    ParseSummary(player, stat_parts);
                }
            }
        }

        return players;
    }

    static void ParsePass(PlayerStats player, string[] stats)
    {
        player.Pass.prog_pass_distance = Int32.Parse(stats[9]);
        player.Pass.pass_atk_third = Int32.Parse(stats[23]);
        player.Pass.pass_completed = Int32.Parse(stats[5]);
        player.Pass.pass_accuracy = Double.Parse(stats[7]);
        player.Pass.xA = Double.Parse(stats[21]);
        player.Pass.key_passes = Int32.Parse(stats[22]);
        player.Pass.pass_penalty_box = Int32.Parse(stats[24]);
        player.Pass.cross_penalty_box = Int32.Parse(stats[25]);

    }


    static void ParseDefence(PlayerStats player, string[] stats)
    {
        player.Defence.tkl = Int32.Parse(stats[5]);
        player.Defence.tkl_won = Int32.Parse(stats[6]);
        player.Defence.tkl_def_third = Int32.Parse(stats[7]);
        player.Defence.tkl_mid_third = Int32.Parse(stats[8]);
        player.Defence.tkl_atk_third = Int32.Parse(stats[9]);
        player.Defence.blocks = Int32.Parse(stats[14]);
        player.Defence.interceptions = Int32.Parse(stats[17]);
        player.Defence.clearances = Int32.Parse(stats[19]);
        player.Negative.mistake_to_shot = Int32.Parse(stats[20]);
    }

    static void ParseMisc(PlayerStats player, string[] stats)
    {
        player.Position = stats[2];
        player.Negative.yellow_card = Int32.Parse(stats[5]);
        player.Negative.red_card = Int32.Parse(stats[6]);
        player.Negative.foul = Int32.Parse(stats[8]);
        player.Poss.foul_drawn = Int32.Parse(stats[9]);
        player.Negative.offside = Int32.Parse(stats[10]);
        player.Negative.penalty_conceded = Int32.Parse(stats[15]);
        player.Negative.own_goal = Int32.Parse(stats[16]);
        player.Poss.recoveries = Int32.Parse(stats[17]);

    }

    static void ParsePossession(PlayerStats player, string[] stats)
    {
        player.Poss.touches = Int32.Parse(stats[5]);
        player.Poss.dribble_attempted = Int32.Parse(stats[12]);
        player.Poss.dribble_completed = Int32.Parse(stats[13]);
        player.Poss.dribble_tackled = Int32.Parse(stats[15]);
        player.Poss.progressive_carry_dist = Int32.Parse(stats[19]);
        player.Poss.progressive_carries = Int32.Parse(stats[20]);
        player.Poss.carry_atk_third = Int32.Parse(stats[21]);
        player.Poss.carry_penalty_box = Int32.Parse(stats[22]);
        player.Negative.miscontrols = Int32.Parse(stats[23]);
        player.Negative.disposessed = Int32.Parse(stats[24]);

    }

    static void ParseSummary(PlayerStats player, string[] stats)
    {
        player.Score.goals = Int32.Parse(stats[5]);
        player.Score.assists = Int32.Parse(stats[6]);
        player.Score.shots = Int32.Parse(stats[9]);
        player.Score.shots_on_target = Int32.Parse(stats[10]);
        player.Score.npxG = Double.Parse(stats[18]);
        player.Pass.xAG = Double.Parse(stats[19]);
        player.Score.shot_creating_action = Int32.Parse(stats[20]);
        player.Score.goal_creating_action = Int32.Parse(stats[21]);
    }

    static PlayerStats GetPlayerByName(string player_name, List<PlayerStats> players)
    {

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Name.Equals(player_name))
            {
                return players[i];
            }
        }
        return null;
    }
}
