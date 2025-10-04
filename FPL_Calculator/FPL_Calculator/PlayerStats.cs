using System;

namespace FPL_Calculator
{
    public class PlayerStats
    {
        public string Name { get; set; }
        public string Club { get; set; }
        public string Position { get; set; }
        public Passing Pass { get; set; }
        public Defending Defence { get; set; }
        public Possession Poss { get; set; }
        public Scoring Score { get; set; }
        public Negatives Negative { get; set; }

        // Constructor 
        public PlayerStats()
        {
            Pass = new Passing();
            Defence = new Defending();
            Poss = new Possession();
            Score = new Scoring();
            Negative = new Negatives();
        }

        // base weights centralized 
        private static class W
        {
            // scoring
            public const double Goal = 5.0;
            public const double Assist = 3.0;
            public const double ShotOnTarget = 1.0;
            public const double NpxG = 0.5;
            public const double SCA = 1.0;
            public const double GCA = 2.0;

            // passing
            public const double KeyPass = 1.0;
            public const double PassAttThird = 0.3;
            public const double PassPenaltyBox = 0.5;
            public const double ThroughBall = 0.5;
            public const double SwitchPass = 0.2;
            public const double CrossPenaltyBox = 0.5;
            public const double PassAccPerPctOver80 = 0.01; // per percentage point > 80%
            public const int PassAccMinCompletedForBonus = 20; 

            // possession
            public const double DribbleCompleted = 0.5;
            public const double ProgressiveCarries = 0.2;
            public const double CarryAttThird = 0.3;
            public const double CarryPenaltyBox = 0.5;
            public const double FoulDrawn = 0.5;
            public const double Recoveries = 0.1;

            // defending
            public const double CleanSheet_DEF = 4.0; // FB/CB
            public const double CleanSheet_MID = 1.0; // mids
            public const double TklWon = 0.5;
            public const double Interception = 0.5;
            public const double Block = 0.5;
            public const double Clearance = 0.3;
            public const double AerialDuel = 0.5;
            public const double PressureToTurnover = 0.2;

            // negatives
            public const double Yellow = 1.0;
            public const double Red = 3.0;
            public const double OwnGoal = 2.0;
            public const double Foul = 0.5;
            public const double Offside = 0.5;
            public const double MistakeToShot = 1.0;
            public const double PenaltyConceded = 2.0;
            public const double Dispossessed = 0.3;
            public const double Miscontrol = 0.2;
        }

        // position normalization and scaling 
        private static string NormalizePosition(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Attacking Midfielder";
            var s = raw.Trim().ToUpperInvariant();

            if (s.Contains("CB") || (s.Contains("DF") && !(s.Contains("LB") || s.Contains("RB") || s.Contains("WB") || s.Contains("FB"))))
                return "Center Back";
            if (s.Contains("LB") || s.Contains("RB") || s.Contains("LWB") || s.Contains("RWB") || s.Contains("WB") || s.Contains("FB"))
                return "Full Back";
            if (s.Contains("DM") || s.Contains("CDM") || s.Contains("DEFENSIVE MID"))
                return "Defensive Midfielder";
            if (s.Contains("CM") || s.Contains("CENTRE MID") || s.Contains("CENTER MID"))
                return "Center Midfielder";
            if (s.Contains("MF")) return "Center Midfielder";
            if (s.Contains("AM") || s.Contains("ATTACKING MID") || s.Contains("CAM"))
                return "Attacking Midfielder";
            if (s.Contains("FW") || s.Contains("ST") || s.Contains("CF") || s.Contains("STRIKER") || s.Contains("FORWARD"))
                return "Striker";
            if (s.Contains("MF")) return "Attacking Midfielder";

            return "Attacking Midfielder";
        }

        private static (double Scoring, double Passing, double Possession, double Defending, double Negatives)
            GetScalingFactors(string position)
        {
            switch (position)
            {
                case "Striker": return (1.0, 0.5, 0.3, 0.1, 1.0);
                case "Attacking Midfielder": return (0.8, 0.7, 0.5, 0.2, 1.0);
                case "Defensive Midfielder": return (0.5, 0.7, 0.6, 0.6, 1.0);
                case "Center Midfielder": return (0.6, 0.7, 0.6, 0.4, 1.0);
                case "Full Back": return (0.3, 0.6, 0.5, 0.8, 1.0);
                case "Center Back": return (0.2, 0.4, 0.3, 1.0, 1.0);
                default: return (0.5, 0.5, 0.5, 0.5, 1.0);
            }
        }

        // implement scoring equation
        public int CalculatePlayerScore()
        {
            // normalize and get scalings
            var posNorm = NormalizePosition(this.Position);
            var s = GetScalingFactors(posNorm);

            double scoring =
                  Score.goals * W.Goal
                + Score.assists * W.Assist
                + Score.shots_on_target * W.ShotOnTarget
                + Score.npxG * W.NpxG
                + Score.shot_creating_action * W.SCA
                + Score.goal_creating_action * W.GCA;

            // passing subtotal 
            double passAccBonus = 0.0;
            if (Pass.pass_completed >= W.PassAccMinCompletedForBonus)
            {
                passAccBonus = Math.Max(0.0, (Pass.pass_accuracy - 80.0) * W.PassAccPerPctOver80);
            }

            double passing =
                  Pass.key_passes * W.KeyPass
                + Pass.pass_atk_third * W.PassAttThird
                + Pass.pass_penalty_box * W.PassPenaltyBox
                + Pass.through_ball * W.ThroughBall
                + Pass.switch_pass * W.SwitchPass
                + Pass.cross_penalty_box * W.CrossPenaltyBox
                + passAccBonus;

            //possession subtotal
            double possession =
                  Poss.dribble_completed * W.DribbleCompleted
                + Poss.progressive_carries * W.ProgressiveCarries
                + Poss.carry_atk_third * W.CarryAttThird
                + Poss.carry_penalty_box * W.CarryPenaltyBox
                + Poss.foul_drawn * W.FoulDrawn
                + Poss.recoveries * W.Recoveries;

            //defending subtotal
            double cleanSheetPts = 0.0;
            if (Defence.clean_sheet > 0)
            {
                if (posNorm == "Center Back" || posNorm == "Full Back") cleanSheetPts = W.CleanSheet_DEF;
                else if (posNorm == "Defensive Midfielder" || posNorm == "Attacking Midfielder") cleanSheetPts = W.CleanSheet_MID;
                // no CS points for Striker
            }

            double defending =
                  cleanSheetPts
                + Defence.tkl_won * W.TklWon
                + Defence.interceptions * W.Interception
                + Defence.blocks * W.Block
                + Defence.clearances * W.Clearance
                + Defence.aerial_duels * W.AerialDuel
                + Defence.pressure_to_turnover * W.PressureToTurnover;

            //negatives subtotal
            double negatives =
                  Negative.yellow_card * W.Yellow
                + Negative.disposessed * W.Dispossessed
                + Negative.red_card * W.Red
                + Negative.own_goal * W.OwnGoal
                + Negative.foul * W.Foul
                + Negative.offside * W.Offside
                + Negative.mistake_to_shot * W.MistakeToShot
                + Negative.penalty_conceded * W.PenaltyConceded
                + Negative.miscontrols * W.Miscontrol;

            // now combine with position scalings
            double total =
                  scoring * s.Scoring
                + passing * s.Passing
                + possession * s.Possession
                + defending * s.Defending
                - negatives * s.Negatives;

            return (int)Math.Round(total);
        }
    }

    public class Passing
    {
        public int prog_pass_distance { get; set; }
        public int pass_atk_third { get; set; }
        public int pass_penalty_box { get; set; }
        public int through_ball { get; set; }
        public int switch_pass { get; set; }
        public int pass_completed { get; set; }
        public double pass_accuracy { get; set; } // percentage (0..100)
        public int pass_offside { get; set; }
        public int key_passes { get; set; }
        public double xA { get; set; }
        public double xAG { get; set; }
        public int prog_pass_total { get; set; }
        public int cross_penalty_box { get; set; }
    }

    public class Defending
    {
        public int clean_sheet { get; set; }
        public int tkl_atk_third { get; set; }
        public int tkl_mid_third { get; set; }
        public int tkl_def_third { get; set; }
        public int interceptions { get; set; }
        public int blocks { get; set; }
        public int aerial_duels { get; set; }
        public int clearances { get; set; }
        public int pressure_to_turnover { get; set; }
        public int tkl_won { get; set; }
        public int tkl { get; set; }
    }

    public class Possession
    {
        public int dribble_attempted { get; set; }
        public int dribble_completed { get; set; }
        public int dribble_tackled { get; set; }
        public int carry_atk_third { get; set; }
        public int progressive_carries { get; set; }
        public int progressive_carry_dist { get; set; }
        public int touches { get; set; }
        public int carry_penalty_box { get; set; }
        public int foul_drawn { get; set; }
        public int recoveries { get; set; }
    }

    public class Scoring
    {
        public int goals { get; set; }
        public double npxG { get; set; }   // CHANGED: double for expected goals
        public int shots { get; set; }
        public int shots_on_target { get; set; }
        public int finishing_accuracy { get; set; }
        public int assists { get; set; }
        public int shot_creating_action { get; set; }
        public int goal_creating_action { get; set; }
    }

    public class Negatives
    {
        public int mistake_to_shot { get; set; }
        public int yellow_card { get; set; }
        public int red_card { get; set; }
        public int own_goal { get; set; }
        public int foul { get; set; }
        public int disposessed { get; set; }  // legacy misspelling in source, kept for compatibility
        public int pass_offside { get; set; }
        public int offside { get; set; }
        public int penalty_conceded { get; set; }
        public int miscontrols { get; set; }
    }
}
