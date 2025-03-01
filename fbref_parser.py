import time
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException
import re
import csv  # For writing to CSV file

CHROMEDRIVER_PATH = 'your_pathname_here'

service = Service(executable_path=CHROMEDRIVER_PATH)
driver = webdriver.Chrome(service=service)
driver.get('https://fbref.com/en/comps/9/schedule/Premier-League-Scores-and-Fixtures')

# Open a CSV file to write the scraped data
with open('player_stats.csv', mode='w', newline='', encoding='utf-8') as file:
    writer = csv.writer(file)
    
    # Write the header row to the CSV file
    writer.writerow(["Player Name", "Stat Type", "Stats Data"])
    
    try:
        wait = WebDriverWait(driver, 20)

        # Wait for the stats table to load
        wait.until(EC.presence_of_element_located((By.XPATH, '//table[contains(@class, "stats_table sortable")]')))

        # Locate the first row (tr element) using the data-row attribute or class
        first_row = driver.find_element(By.XPATH, '//tr[@data-row="65"]')

        # Locate the link in the row using the specific match report link
        match_report_link = first_row.find_element(By.XPATH, './/td[@data-stat="match_report"]/a')

        # Extract and print the full URL (optional)
        match_report_href = match_report_link.get_attribute('href')
        print(f"Match report link: {match_report_href}")

        # Use JavaScript to click the link (bypasses potential blockers)
        driver.execute_script("arguments[0].click();", match_report_link)

        # Wait for the page to load fully
        wait.until(EC.presence_of_element_located((By.XPATH, '//div[contains(@id, "switcher_player_stats")]')))

        match_title = driver.find_element(By.XPATH, '//h1').text
        print(f"Match Title: {match_title}")

        # Split the string based on " vs. "
        teams_part = match_title.split(" vs. ")

        # Extract home and away team
        home_team = teams_part[0]  # 'Crystal Palace'
        away_team = teams_part[1].split(" Match Report")[0]  # 'Liverpool'

        # Print to verify
        print(f"Home Team: {home_team}")
        print(f"Away Team: {away_team}")

        # Dynamically find all divs that match "all_player_stats" or "all_keeper_stats"
        stats_divs = driver.find_elements(By.XPATH, '//div[contains(@id, "all_player_stats") or contains(@id, "all_keeper_stats")]')

        # List of stat types we want to scrape
        stat_types = ["Summary", "Passing", "Pass Types", "Defensive Actions", "Possession", "Miscellaneous Stats"]

        # Mapping to handle special cases for div IDs
        stat_type_map = {
            "Summary": "summary",
            "Passing": "passing",
            "Pass Types": "passing_types",
            "Defensive Actions": "defense",
            "Possession": "possession",
            "Miscellaneous Stats": "misc"
        }

        i=1
        # Iterate through the player stats divs
        for stats_div in stats_divs:
            div_id = stats_div.get_attribute('id')
            print(f"Found div with ID: {div_id}")
            print(f"I val is {i}")
            if i<3: 
                team = home_team
            else: 
                team = away_team

            # Extract the unique part of the ID (e.g., "b8fd03ef") using regex
            unique_id_match = re.search(r'all_(player|keeper)_stats_(\w+)', div_id)
            if unique_id_match:
                unique_id = unique_id_match.group(2)  # The unique ID part
                print(f"Unique ID: {unique_id}")

                # Loop through each stat type
                for stat_type in stat_types:
                    # Use the stat_type_map to get the correct key for the div ID
                    stat_type_key = stat_type_map.get(stat_type)

                    # Construct the div ID for the current stat type
                    stat_div_id = f"div_stats_{unique_id}_{stat_type_key}"

                    # Construct the correct button to click based on stat type
                    stat_button_xpath = f'//a[@data-show=".assoc_stats_{unique_id}_{stat_type_key}"]'

                    try:
                        # Click the stat type button
                        stat_button = driver.find_element(By.XPATH, stat_button_xpath)
                        driver.execute_script("arguments[0].click();", stat_button)
                        print(f"{stat_type} button clicked for team with ID {unique_id}")

                        # Wait for the stats div to load
                        print(f"Waiting for {stat_type} stats div: {stat_div_id}")
                        wait.until(EC.presence_of_element_located((By.ID, stat_div_id)))

                        # Find the tbody within the stat type section
                        stat_tbody = driver.find_element(By.XPATH, f'//div[@id="{stat_div_id}"]//tbody')

                        # Extract player data from the current stat type section
                        player_rows = stat_tbody.find_elements(By.TAG_NAME, 'tr')
                        for player_row in player_rows:
                            # Extract player name and stats
                            player_name_element = player_row.find_element(By.XPATH, './/th[@data-stat="player"]/a')
                            player_name = player_name_element.text
                            player_stats = player_row.find_elements(By.TAG_NAME, 'td')
                            player_data = [stat.text for stat in player_stats]

                            # Write player name, stat type, and data to the CSV file
                            writer.writerow([player_name, stat_type, team, player_data])
                            #print(f"Player {player_name} - {stat_type} stats: {player_data}")

                    except TimeoutException:
                        print(f"Timeout while waiting for {stat_type} stats div: {stat_div_id}")
                    except Exception as e:
                        print(f"Error while handling {stat_type}: {e}")
                
            else:
                print("Could not extract unique ID from div")
            
            print("End of loop")
            i=i+1

    except TimeoutException:
        print("Loading took too much time!")

    finally:
        # Close the browser
        driver.quit()
