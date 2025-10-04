import time
import re
import csv
import os
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException, NoSuchElementException

# Inclusive range 
# if equal only one matchday is scraped
START_GW = 1
END_GW = 38

CHROMEDRIVER_PATH = 'chromedriver_path'
SCHEDULE_URL = 'https://fbref.com/en/comps/9/schedule/Premier-League-Scores-and-Fixtures'

service = Service(executable_path=CHROMEDRIVER_PATH)
driver = webdriver.Chrome(service=service)
wait = WebDriverWait(driver, 20)

def sanitize_filename(s):
    # keep letters, numbers, spaces, underscores, hyphens
    return re.sub(r'[^a-zA-Z0-9_\- ]+', '', s).strip().replace(' ', '_')

def get_schedule_rows():
    """
    On the schedule page return all TRs that represent actual matches
    They should have data-row attr and a match report link cell present (data-stat='match_report')
    """
    wait.until(EC.presence_of_element_located(
        (By.XPATH, '//table[contains(@class, "stats_table sortable")]')
    ))
    # tbody rows with a data-row attribute are the match rows
    rows = driver.find_elements(By.XPATH, '//table[contains(@class,"stats_table")]/tbody/tr[@data-row]')
    return rows

def row_gameweek(tr):
    try:
        th = tr.find_element(By.XPATH, './/th[@data-stat="gameweek"]')
        gw_txt = safe_text(th)
        if gw_txt.isdigit():
            return int(gw_txt)
    except NoSuchElementException:
        pass
    return None

def row_match_report_link(tr):
    """
    The Match Report link lives in td[data-stat='match_report']/a
    """
    try:
        cell = tr.find_element(By.XPATH, './/td[@data-stat="match_report"]')
        link = cell.find_element(By.XPATH, './/a')
        return link
    except NoSuchElementException:
        return None

def open_match_report(link):
    driver.execute_script("arguments[0].click();", link)
    wait.until(EC.presence_of_element_located(
        (By.XPATH, '//div[contains(@id, "switcher_player_stats")]')
    ))

def read_match_header_teams_and_gw(tr_on_schedule):
    """
    We’ll pull GW from the schedule row (cheap and reliable),
    and teams from the match page <h1> (as you do).
    """
    gw = row_gameweek(tr_on_schedule)
    h1 = driver.find_element(By.XPATH, '//h1').text  # eg Crystal Palace vs. Liverpool Match Report
    parts = h1.split(" vs. ")
    home = parts[0].strip()
    away = parts[1].replace(" Match Report", "").strip()
    return gw, home, away

def stat_tabs_map():
    return {
        "Summary": "summary",
        "Passing": "passing",
        "Pass Types": "passing_types",
        "Defensive Actions": "defense",
        "Possession": "possession",
        "Miscellaneous Stats": "misc"
    }

def scrape_match_to_csv(out_csv_path, home_team, away_team):
    writer = None
    f = None
    try:
        # create file/CSV and header
        f = open(out_csv_path, mode='w', newline='', encoding='utf-8')
        writer = csv.writer(f)
        writer.writerow(["Player Name", "Team", "Stat Type", "Stats Data"])

        stats_divs = driver.find_elements(
            By.XPATH,
            '//div[contains(@id, "all_player_stats") or contains(@id, "all_keeper_stats")]'
        )

        # Fbref usually has the blocks: Home outfield, Home GK, Away outfield, Away GK
        # first two blocks -> home, next two blocks -> away
        total_blocks = len(stats_divs)
        home_cut = total_blocks // 2  # first half is home

        mapping = stat_tabs_map()

        for idx in range(total_blocks):
            stats_div = stats_divs[idx]
            div_id = stats_div.get_attribute('id')  
            # identify which team this block belongs to
            team_label = home_team if idx < home_cut else away_team

            # Extract the unique suffix used by the stats tabs 
            m = re.search(r'all_(player|keeper)_stats_(\w+)', div_id)
            if not m:
                # skip blocks without the expected id pattern
                continue
            unique = m.group(2)

            for stat_type, key in mapping.items():
                # Matches: <a data-show=".assoc_stats_{unique}_{key}">
                button_xpath = f'//a[@data-show=".assoc_stats_{unique}_{key}"]'
                try:
                    tab = driver.find_element(By.XPATH, button_xpath)
                    driver.execute_script("arguments[0].click();", tab)

                    # Wait for the stats div with computed id
                    target_div_id = f"div_stats_{unique}_{key}"
                    wait.until(EC.presence_of_element_located((By.ID, target_div_id)))

                    # tbody under the target div
                    tbody = driver.find_element(By.XPATH, f'//div[@id="{target_div_id}"]//tbody')
                    rows = tbody.find_elements(By.TAG_NAME, 'tr')

                    for r in rows:
                        try:
                            name_el = r.find_element(By.XPATH, './/th[@data-stat="player"]/a')
                            player_name = name_el.text.strip()
                        except NoSuchElementException:
                            # sometimes there are separator rows, skip these
                            continue

                        tds = r.find_elements(By.TAG_NAME, 'td')
                        data = [td.text for td in tds]
                        writer.writerow([player_name, team_label, stat_type, data])
                except TimeoutException:
                    continue
                except Exception:
                    continue

    finally:
        if f is not None:
            f.flush()
            f.close()

def return_to_schedule():
    # go back to the schedule page after scraping the match
    driver.back()
    wait.until(EC.presence_of_element_located(
        (By.XPATH, '//table[contains(@class, "stats_table sortable")]')
    ))


try:
    driver.get(SCHEDULE_URL)
    wait.until(EC.presence_of_element_located(
        (By.XPATH, '//table[contains(@class, "stats_table sortable")]')
    ))

    for gw in range(START_GW, END_GW + 1):
        rows = get_schedule_rows()

        # From all rows, pick those matching the current gw
        target_rows = []
        for tr in rows:
            v = row_gameweek(tr)
            if v == gw:
                target_rows.append(tr)

        # possible there are postponed/blank rows, skip those without a match report link.
        for tr in target_rows:
            link = row_match_report_link(tr)
            if link is None:
                continue

            open_match_report(link)

            gw_num, home_team, away_team = read_match_header_teams_and_gw(tr)

            # output file name
            home_s = sanitize_filename(home_team)
            away_s = sanitize_filename(away_team)
            out_name = f"{home_s}_vs_{away_s}_matchday{gw_num}.csv"
            out_path = os.path.join(os.getcwd(), out_name)

            scrape_match_to_csv(out_path, home_team, away_team)

            return_to_schedule()

except TimeoutException:
    print("Loading took too much time (schedule or page element).")
finally:
    driver.quit()
