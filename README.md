This repo is a small end-to-end pipeline to scrape Premier League match data and score player performances. 

It has 2 parts: 
1. Python (Selenium) scraper: Opens "https://fbref.com/en/comps/9/schedule/Premier-League-Scores-and-Fixtures" opens each Match Report, clicks through the player stat tabs, and dumps rows to CSV per match.
2. C# scorer – parses files with raw player stats, applies the scoring model (with position scaling), and writes a neat report of totals.

**Scoring Model**

Players are scored by category: Scoring, Passing, Possession, Defending, Negatives.
Each category sums base weights for the actions we track (e.g., goals = 5, assists = 3, clean sheet for CB/FB = 4, etc).
The category totals are then scaled by position so a center back isn’t judged like a striker.
Final = (Scoring × s₁) + (Passing × s₂) + (Possession × s₃) + (Defending × s₄) − (Negatives × s₅).
Positions supported: Striker, Attacking Midfielder, Center Midfielder, Defensive Midfielder, Full Back, Center Back.
Clean sheets only give points to defenders (and a small amount to midfielders), not strikers.
All weights and scalings live in playerstats.cs so there’s a single source of truth.

**Data flow**

Scraper → per-match CSVs
For each matchday, the scraper creates a file named: "{HOME}_vs_{AWAY}_matchday{GW}.csv".
Each row: Player Name, Team, Stat Type, Stats Data
Stat Type is one of: Summary, Passing, Pass Types, Defensive Actions, Possession, Miscellaneous Stats.
Stats Data is the list of the raw cell texts for that table row.
C# app reads player_stats.txt (from the project folder)
It parses into PlayerStats objects and calls CalculatePlayerScore() for each.

Output report
A single "{HOME}_vs_{AWAY}_matchday{GW}player_scores.txt" lands in ./Scores/ under the project directory - this represents all players scores from the given match.
Right now the report prints Name / Position / Club / TOTAL per player.

Sample raw player stats and sample player scores can be found in the repository for Newcastle vs Arsenal (28/09/2025)
https://fbref.com/en/matches/e851cb5c/Newcastle-United-Arsenal-September-28-2025-Premier-League

**Setup**

1) C# project
From the FPL_Calculator folder:
dotnet restore
dotnet build

2) Python scraper
Install dependencies:
python3 -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install selenium


Download a chromedriver that matches your Chrome version and update:
CHROMEDRIVER_PATH = '/Users/you/Downloads/chromedriver/chromedriver'

**How to run**

A) Scrape matches (per-match CSVs)
Open fbref_scraper.py and set:
START_GW = 1
END_GW = 38
Then:
python scraper/fbref_scraper.py

Files will be generated like: 
Crystal_Palace_vs_Liverpool_matchday1.csv
Tottenham_Hotspur_vs_Chelsea_matchday2.csv
...

Each file contains rows for both teams and all of the stat tabs you asked the script to click.
If you only want one gameweek, set START_GW = END_GW.

B) Score players
Put your player_stats.txt in the project folder (same level as FPL_Calc.csproj).
Then run:
dotnet run --project FPL_Calculator

Then the player score file will be generated. Example snippet:

<img width="597" height="346" alt="Screenshot 2025-10-04 at 6 25 56 pm" src="https://github.com/user-attachments/assets/0ced4723-b852-435f-92f0-9861213ba657" />

