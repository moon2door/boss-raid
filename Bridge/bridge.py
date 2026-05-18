#!/usr/bin/env python3
import base64
import hashlib
import json
import os
import random
import re
import socket
import struct
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse


HOST = "127.0.0.1"
PORT = 8765


def resolve_project_root():
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent

    script_path = Path(__file__).resolve()
    if script_path.parent.name.lower() == "bridge":
        return script_path.parents[1]
    return script_path.parent


PROJECT_ROOT = resolve_project_root()
SETTING_PATH = PROJECT_ROOT / "Setting.Json"
API_PATH = PROJECT_ROOT / "API.Json"


def normalize_mp_command(message):
    text = str(message or "").replace("\r", " ").replace("\n", " ").strip()
    text = re.sub(r"\s+", " ", text)
    if len(text) > 220:
        text = text[:220].rstrip()

    if text[:3].lower() != "!mp":
        return ""
    if len(text) > 3 and not text[3].isspace():
        return ""
    return text


class BossRaidBridge:
    def __init__(self, load_settings=True):
        self.lock = threading.RLock()
        self.clients = set()
        self.irc_client = None
        self.screen_listeners = []
        self.tosu_auto_finish_armed = False
        self.tosu_auto_finish_result_since = None
        self.tosu_auto_finish_key = None
        self.state = self._state_from_setting() if load_settings else self._default_state()

    def _default_state(self):
        modes = ["Aim", "Speed", "Tech", "Stamina"]
        maps = []
        selected_ids = []
        for mode in modes:
            for index in range(1, 7):
                map_id = f"{mode.lower()}-{index}"
                maps.append(
                    {
                        "id": map_id,
                        "mode": mode,
                        "title": f"{mode} Raid #{index}",
                        "artist": "Map Pool",
                        "mapper": "Staff",
                        "difficultyName": f"Set {index}",
                        "link": "",
                        "beatmapId": 0,
                        "isBurger": False,
                        "played": False,
                    }
                )
                selected_ids.append(map_id)

        return {
            "version": 1,
            "animationNonce": 0,
            "eventTitle": "BOSS RAID",
            "screen": "standby",
            "currentTeamId": "team-1",
            "difficulty": "normal",
            "bossHp": 1400000,
            "totalScore": 0,
            "prizePool": 0,
            "burgerCount": 0,
            "burgerMissCount": 0,
            "clearCount": 0,
            "failCount": 0,
            "roundIndex": 0,
            "selectedMode": "",
            "selectedMapId": "",
            "lastResult": "none",
            "resultMessage": "",
            "connectionLabel": "BRIDGE ONLINE",
            "chatStatus": "IRC DISABLED",
            "scoreSourceStatus": "TOURNEY IPC DISABLED",
            "obsStatus": "OBS DISABLED",
            "chatMessages": [],
            "teams": [
                {"id": "team-1", "name": "Team A", "score": 0, "color": {"r": 0.95, "g": 0.25, "b": 0.20, "a": 1.0}, "players": []},
                {"id": "team-2", "name": "Team B", "score": 0, "color": {"r": 0.20, "g": 0.55, "b": 0.95, "a": 1.0}, "players": []},
                {"id": "team-3", "name": "Team C", "score": 0, "color": {"r": 0.25, "g": 0.85, "b": 0.45, "a": 1.0}, "players": []},
                {"id": "team-4", "name": "Team D", "score": 0, "color": {"r": 0.95, "g": 0.75, "b": 0.20, "a": 1.0}, "players": []},
            ],
            "mapPool": maps,
            "selectedRoundMapIds": selected_ids,
            "difficulties": [
                {"id": "easy", "label": "Easy", "bossHp": 1000000, "prize": 3000},
                {"id": "normal", "label": "Normal", "bossHp": 1400000, "prize": 5000},
                {"id": "hard", "label": "Hard", "bossHp": 2000000, "prize": 15000},
            ],
        }

    def _state_from_setting(self):
        state = self._default_state()
        if not SETTING_PATH.exists():
            self._log_setting_summary(state, "missing Setting.Json, using defaults")
            return state

        try:
            with SETTING_PATH.open("r", encoding="utf-8-sig") as setting_file:
                setting = json.load(setting_file)
        except Exception as exc:
            print(f"[SETTING] Failed to read {SETTING_PATH}: {exc}", flush=True)
            self._log_setting_summary(state, "invalid Setting.Json, using defaults")
            return state

        if not isinstance(setting, dict):
            print(f"[SETTING] Failed to read {SETTING_PATH}: root must be a JSON object", flush=True)
            self._log_setting_summary(state, "invalid Setting.Json, using defaults")
            return state

        state["eventTitle"] = str(setting.get("eventTitle", state["eventTitle"]))[:80]
        state["currentTeamId"] = str(setting.get("currentTeamId", state["currentTeamId"]))
        state["difficulty"] = str(setting.get("difficulty", state["difficulty"]))

        difficulties = setting.get("difficulties")
        if isinstance(difficulties, list) and difficulties:
            state["difficulties"] = self._sanitize_difficulties(difficulties)

        teams = setting.get("teams")
        if isinstance(teams, list) and teams:
            state["teams"] = self._sanitize_teams(teams)

        maps = setting.get("maps", setting.get("mapPool"))
        if isinstance(maps, list) and maps:
            state["mapPool"] = self._sanitize_maps(maps)
            state["selectedRoundMapIds"] = [raid_map["id"] for raid_map in state["mapPool"]]

        if self._team_in_state(state, state["currentTeamId"]) is None and state["teams"]:
            state["currentTeamId"] = state["teams"][0]["id"]

        self._normalize_state(state)
        self._log_setting_summary(state, "loaded Setting.Json")
        return state

    def _log_setting_summary(self, state, status):
        modes = {}
        for raid_map in state.get("mapPool", []):
            mode = raid_map.get("mode", "Unknown")
            modes[mode] = modes.get(mode, 0) + 1

        print("", flush=True)
        print("[SETTING] " + status, flush=True)
        print(f"[SETTING] Root : {PROJECT_ROOT}", flush=True)
        print(f"[SETTING] File : {SETTING_PATH}", flush=True)
        print(f"[SETTING] Event: {state.get('eventTitle', '')}", flush=True)
        print(f"[SETTING] Teams: {len(state.get('teams', []))}", flush=True)
        for team in state.get("teams", []):
            players = team.get("players", [])
            player_label = ", ".join(self._player_name(player) for player in players) if players else "-"
            print(f"[SETTING]  - {team.get('id')}: {team.get('name')} / players: {player_label}", flush=True)
        print(f"[SETTING] Maps : {len(state.get('mapPool', []))}", flush=True)
        print("[SETTING] Modes: " + (", ".join(f"{mode}({count})" for mode, count in modes.items()) or "-"), flush=True)
        print("", flush=True)

    def _sanitize_difficulties(self, difficulties):
        sanitized = []
        for difficulty in difficulties:
            if not isinstance(difficulty, dict):
                continue
            difficulty_id = str(difficulty.get("id", "")).strip()
            if not difficulty_id:
                continue
            sanitized.append(
                {
                    "id": difficulty_id,
                    "label": str(difficulty.get("label", difficulty_id))[:32],
                    "bossHp": self._to_int(difficulty.get("bossHp", 0), 0),
                    "prize": self._to_int(difficulty.get("prize", 0), 0),
                }
            )
        return sanitized or self._default_state()["difficulties"]

    def _sanitize_teams(self, teams):
        sanitized = []
        for index, team in enumerate(teams):
            if not isinstance(team, dict):
                continue
            team_id = str(team.get("id", f"team-{index + 1}")).strip() or f"team-{index + 1}"
            sanitized_team = {
                "id": team_id,
                "name": str(team.get("name", f"Team {index + 1}"))[:32],
                "score": self._to_int(team.get("score", 0), 0),
                "color": self._sanitize_color(team.get("color")),
                "players": [],
            }
            players = team.get("players")
            if isinstance(players, list):
                sanitized_team["players"] = self._sanitize_players(players)
            sanitized.append(sanitized_team)
        return sanitized or self._default_state()["teams"]

    def _sanitize_players(self, players):
        sanitized = []
        if not isinstance(players, list):
            return sanitized

        for index, player in enumerate(players[:16]):
            if isinstance(player, dict):
                name = str(player.get("name", player.get("playerName", f"P{index + 1}"))).strip() or f"P{index + 1}"
                combo = player.get("combo", 0)
                if isinstance(combo, dict):
                    max_combo = self._to_int(combo.get("max", combo.get("current", 0)), 0)
                    combo = combo.get("current", 0)
                else:
                    max_combo = self._to_int(player.get("maxCombo", 0), 0)

                sanitized.append(
                    {
                        "name": name[:32],
                        "score": self._to_int(player.get("score", 0), 0),
                        "accuracy": self._to_float(player.get("accuracy", 0.0), 0.0),
                        "combo": self._to_int(combo, 0),
                        "maxCombo": max_combo,
                        "misses": self._to_int(player.get("misses", player.get("miss", 0)), 0),
                        "team": str(player.get("team", ""))[:12],
                        "ipcId": self._to_int(player.get("ipcId", -1), -1),
                    }
                )
            else:
                name = str(player).strip()
                if name:
                    sanitized.append(
                        {
                            "name": name[:32],
                            "score": 0,
                            "accuracy": 0.0,
                            "combo": 0,
                            "maxCombo": 0,
                            "misses": 0,
                            "team": "",
                            "ipcId": -1,
                        }
                    )

        return sanitized

    def _sanitize_maps(self, maps):
        sanitized = []
        used_ids = set()
        for index, raid_map in enumerate(maps):
            if not isinstance(raid_map, dict):
                continue
            mode = str(raid_map.get("mode", "Mode")).strip() or "Mode"
            base_id = str(raid_map.get("id", f"{mode.lower()}-{index + 1}")).strip() or f"map-{index + 1}"
            map_id = base_id
            suffix = 2
            while map_id in used_ids:
                map_id = f"{base_id}-{suffix}"
                suffix += 1
            used_ids.add(map_id)
            sanitized.append(
                {
                    "id": map_id,
                    "mode": mode[:32],
                    "title": str(raid_map.get("title", f"Map #{index + 1}"))[:80],
                    "artist": str(raid_map.get("artist", ""))[:80],
                    "mapper": str(raid_map.get("mapper", ""))[:80],
                    "difficultyName": str(raid_map.get("difficultyName", ""))[:80],
                    "link": str(raid_map.get("link", raid_map.get("url", "")))[:220],
                    "beatmapId": self._extract_beatmap_id(raid_map),
                    "isBurger": bool(raid_map.get("isBurger", False)),
                    "played": bool(raid_map.get("played", False)),
                }
            )
        return sanitized[:24] or self._default_state()["mapPool"]

    def _extract_beatmap_id(self, raid_map):
        direct = self._to_int(raid_map.get("beatmapId", raid_map.get("beatmap_id", 0)), 0)
        if direct > 0:
            return direct

        link = str(raid_map.get("link", raid_map.get("url", "")))
        match = re.search(r"#(?:osu|taiko|fruits|mania)/(\d+)", link)
        if match:
            return self._to_int(match.group(1), 0)

        match = re.search(r"/b/(\d+)", link)
        return self._to_int(match.group(1), 0) if match else 0

    def _sanitize_color(self, color):
        if isinstance(color, str) and color.startswith("#") and len(color) in (7, 9):
            try:
                red = int(color[1:3], 16) / 255
                green = int(color[3:5], 16) / 255
                blue = int(color[5:7], 16) / 255
                alpha = int(color[7:9], 16) / 255 if len(color) == 9 else 1.0
                return {"r": red, "g": green, "b": blue, "a": alpha}
            except ValueError:
                pass

        if isinstance(color, dict):
            return {
                "r": self._to_float(color.get("r", 1.0), 1.0),
                "g": self._to_float(color.get("g", 1.0), 1.0),
                "b": self._to_float(color.get("b", 1.0), 1.0),
                "a": self._to_float(color.get("a", 1.0), 1.0),
            }

        palette = [
            {"r": 0.95, "g": 0.25, "b": 0.20, "a": 1.0},
            {"r": 0.20, "g": 0.55, "b": 0.95, "a": 1.0},
            {"r": 0.25, "g": 0.85, "b": 0.45, "a": 1.0},
            {"r": 0.95, "g": 0.75, "b": 0.20, "a": 1.0},
        ]
        return random.choice(palette)

    def _normalize_state(self, state):
        difficulty = None
        for item in state.get("difficulties", []):
            if item.get("id") == state.get("difficulty"):
                difficulty = item
                break
        if difficulty is not None:
            state["bossHp"] = int(difficulty["bossHp"])

        total = 0
        for team in state.get("teams", []):
            team["score"] = self._to_int(team.get("score", 0), 0)
            team["players"] = self._sanitize_players(team.get("players", []))
            total += max(0, team["score"])
        state["totalScore"] = total

        if not state.get("selectedRoundMapIds"):
            state["selectedRoundMapIds"] = [m["id"] for m in state.get("mapPool", [])]

        if "chatStatus" not in state:
            state["chatStatus"] = "IRC DISABLED"
        if "scoreSourceStatus" not in state:
            state["scoreSourceStatus"] = "TOURNEY IPC DISABLED"
        if "obsStatus" not in state:
            state["obsStatus"] = "OBS DISABLED"
        if not isinstance(state.get("chatMessages"), list):
            state["chatMessages"] = []
        del state["chatMessages"][:-30]

    @staticmethod
    def _team_in_state(state, team_id):
        for team in state.get("teams", []):
            if team.get("id") == team_id:
                return team
        return None

    def snapshot(self):
        with self.lock:
            self._normalize_locked()
            return json.loads(json.dumps(self.state, ensure_ascii=False))

    def snapshot_json(self):
        with self.lock:
            self._normalize_locked()
            return json.dumps(self.state, ensure_ascii=False, separators=(",", ":"))

    def add_client(self, client):
        with self.lock:
            self.clients.add(client)

    def remove_client(self, client):
        with self.lock:
            self.clients.discard(client)

    def broadcast(self):
        message = self.snapshot_json().encode("utf-8")
        dead = []
        with self.lock:
            clients = list(self.clients)

        for client in clients:
            try:
                send_ws_text(client, message)
            except OSError:
                dead.append(client)

        for client in dead:
            self.remove_client(client)
            try:
                client.close()
            except OSError:
                pass

    def add_screen_listener(self, listener):
        with self.lock:
            if listener not in self.screen_listeners:
                self.screen_listeners.append(listener)

    def remove_screen_listener(self, listener):
        with self.lock:
            if listener in self.screen_listeners:
                self.screen_listeners.remove(listener)

    def _notify_screen_changed(self, screen):
        with self.lock:
            listeners = list(self.screen_listeners)
        for listener in listeners:
            try:
                listener(screen)
            except Exception as exc:
                print(f"[BRIDGE] Screen listener failed: {exc}", flush=True)

    def set_chat_status(self, status):
        with self.lock:
            self.state["chatStatus"] = str(status)[:80]
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
        self.broadcast()

    def set_score_source_status(self, status):
        status = str(status or "")[:80]
        with self.lock:
            if self.state.get("scoreSourceStatus") == status:
                return
            self.state["scoreSourceStatus"] = status
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
        self.broadcast()

    def set_obs_status(self, status):
        status = str(status or "")[:80]
        with self.lock:
            if self.state.get("obsStatus") == status:
                return
            self.state["obsStatus"] = status
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
        self.broadcast()

    def apply_external_scores(self, scores, source_label="Tourney IPC", team_ids=None, allowed_screens=None):
        if not isinstance(scores, list):
            return False

        source_label = str(source_label or "Score Source")[:32]
        allowed = {str(screen) for screen in allowed_screens or [] if str(screen)}
        with self.lock:
            if allowed and self.state.get("screen") not in allowed:
                self.state["scoreSourceStatus"] = f"{source_label} READY ({len(scores)} scores)"[:80]
                return False

            teams = self.state.get("teams", [])
            changed = False

            if team_ids:
                for index, raw_score in enumerate(scores):
                    if index >= len(team_ids):
                        break
                    team = self._team(str(team_ids[index]))
                    if team is None:
                        continue
                    next_score = self._to_int(raw_score, 0)
                    if team.get("score") != next_score:
                        team["score"] = next_score
                        changed = True
            else:
                for index, raw_score in enumerate(scores):
                    if index >= len(teams):
                        break
                    next_score = self._to_int(raw_score, 0)
                    if teams[index].get("score") != next_score:
                        teams[index]["score"] = next_score
                        changed = True

            next_status = f"{source_label} LIVE ({len(scores)} scores)"[:80]
            if self.state.get("scoreSourceStatus") != next_status:
                self.state["scoreSourceStatus"] = next_status
                changed = True

            if changed:
                self._normalize_locked()
                self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1

        if changed:
            self.broadcast()
        return changed

    def apply_tosu_tourney_state(self, tourney, team_ids=None, allowed_screens=None):
        if not isinstance(tourney, dict):
            return False

        clients = tourney.get("clients", [])
        if not isinstance(clients, list):
            clients = []

        allowed = {str(screen) for screen in allowed_screens or [] if str(screen)}
        with self.lock:
            if allowed and self.state.get("screen") not in allowed:
                status = f"TOSU READY ({len(clients)} clients)"[:80]
                changed = self.state.get("scoreSourceStatus") != status
                if changed:
                    self.state["scoreSourceStatus"] = status
                    self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
            else:
                changed = self._apply_tosu_tourney_locked(tourney, clients, team_ids or [])

        if changed:
            self.broadcast()
        return changed

    def maybe_auto_finish_from_tosu(self, data, delay_seconds=5.0, enabled=True):
        if not enabled or not isinstance(data, dict):
            self._reset_tosu_auto_finish()
            return False

        tourney = data.get("tourney")
        if not isinstance(tourney, dict):
            self._reset_tosu_auto_finish()
            return False

        clients = tourney.get("clients", [])
        if not isinstance(clients, list) or not clients:
            self._reset_tosu_auto_finish()
            return False

        now = time.monotonic()
        delay_seconds = max(0.0, self._to_float(delay_seconds, 5.0))
        with self.lock:
            if self.state.get("screen") != "inGame":
                self._reset_tosu_auto_finish_locked()
                return False

            finish_key = f"{self.state.get('roundIndex', 0)}:{self.state.get('selectedMapId', '')}"
            if self.tosu_auto_finish_key != finish_key:
                self.tosu_auto_finish_key = finish_key
                self.tosu_auto_finish_armed = False
                self.tosu_auto_finish_result_since = None

            if self._tosu_has_active_score_locked(tourney, clients) or self._tosu_is_play_screen(data, tourney):
                self.tosu_auto_finish_armed = True

            if not self.tosu_auto_finish_armed:
                return False

            if not self._tosu_is_result_screen(data, tourney):
                self.tosu_auto_finish_result_since = None
                return False

            if self.tosu_auto_finish_result_since is None:
                self.tosu_auto_finish_result_since = now
                return False

            if now - self.tosu_auto_finish_result_since < delay_seconds:
                return False

            print("[TOSU] Auto finishing map after result screen delay.", flush=True)
            self._finish_map_locked()
            self._normalize_locked()
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
            self._reset_tosu_auto_finish_locked()
            next_screen = self.state.get("screen", "")

        self.broadcast()
        self._notify_screen_changed(next_screen)
        return True

    def _reset_tosu_auto_finish(self):
        with self.lock:
            self._reset_tosu_auto_finish_locked()

    def _reset_tosu_auto_finish_locked(self):
        self.tosu_auto_finish_armed = False
        self.tosu_auto_finish_result_since = None
        self.tosu_auto_finish_key = None

    def _tosu_has_active_score_locked(self, tourney, clients):
        total_score = tourney.get("totalScore", {})
        if isinstance(total_score, dict):
            if self._to_int(total_score.get("left", 0), 0) > 0 or self._to_int(total_score.get("right", 0), 0) > 0:
                return True

        for client in clients:
            if not isinstance(client, dict):
                continue
            play = client.get("play", {})
            if isinstance(play, dict) and self._to_int(play.get("score", 0), 0) > 0:
                return True

        return self._to_int(self.state.get("totalScore", 0), 0) > 0

    @staticmethod
    def _tosu_is_result_screen(data, tourney):
        states = []
        state = data.get("state")
        if isinstance(state, dict):
            states.append(state)

        states.append({"number": tourney.get("ipcState"), "name": ""})
        result_names = {"resultscreen", "rankingvs", "rankingteam", "rankingtagcoop"}
        result_numbers = {7, 14, 17, 18}
        for state_item in states:
            name = str(state_item.get("name", "")).replace("_", "").replace("-", "").lower()
            try:
                number = int(state_item.get("number", -1))
            except (TypeError, ValueError):
                number = -1
            if name in result_names or number in result_numbers:
                return True

        return False

    @staticmethod
    def _tosu_is_play_screen(data, tourney):
        states = []
        state = data.get("state")
        if isinstance(state, dict):
            states.append(state)

        states.append({"number": tourney.get("ipcState"), "name": ""})
        for state_item in states:
            name = str(state_item.get("name", "")).replace("_", "").replace("-", "").lower()
            try:
                number = int(state_item.get("number", -1))
            except (TypeError, ValueError):
                number = -1
            if name == "play" or number == 2:
                return True

        return False

    def _apply_tosu_tourney_locked(self, tourney, clients, team_ids):
        teams = self.state.get("teams", [])
        side_to_team = {}
        configured_ids = [str(team_id).strip() for team_id in team_ids or [] if str(team_id).strip()]
        if configured_ids:
            if len(configured_ids) > 0:
                side_to_team["left"] = self._team(configured_ids[0])
            if len(configured_ids) > 1:
                side_to_team["right"] = self._team(configured_ids[1])

        if side_to_team.get("left") is None and len(teams) > 0:
            side_to_team["left"] = teams[0]
        if side_to_team.get("right") is None and len(teams) > 1:
            side_to_team["right"] = teams[1]

        grouped_players = {"left": [], "right": []}
        for client in sorted(clients, key=lambda item: self._to_int(item.get("ipcId", 9999) if isinstance(item, dict) else 9999, 9999)):
            if not isinstance(client, dict):
                continue
            side = str(client.get("team", "")).lower()
            if side not in grouped_players:
                continue
            grouped_players[side].append(self._tosu_client_to_player(client))

        total_score = tourney.get("totalScore", {})
        if not isinstance(total_score, dict):
            total_score = {}

        changed = False
        for side in ("left", "right"):
            team = side_to_team.get(side)
            if team is None:
                continue

            players = grouped_players.get(side, [])
            fallback_total = sum(max(0, self._to_int(player.get("score", 0), 0)) for player in players)
            next_score = self._to_int(total_score.get(side, fallback_total), fallback_total)
            if team.get("score") != next_score:
                team["score"] = next_score
                changed = True

            if players and team.get("players") != players:
                team["players"] = players
                changed = True

        status = f"TOSU LIVE ({len(clients)} clients)"[:80]
        if self.state.get("scoreSourceStatus") != status:
            self.state["scoreSourceStatus"] = status
            changed = True

        if changed:
            self._normalize_locked()
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
        return changed

    def _tosu_client_to_player(self, client):
        user = client.get("user", {}) if isinstance(client.get("user"), dict) else {}
        play = client.get("play", {}) if isinstance(client.get("play"), dict) else {}
        combo = play.get("combo", {}) if isinstance(play.get("combo"), dict) else {}
        hits = play.get("hits", {}) if isinstance(play.get("hits"), dict) else {}
        name = str(user.get("name") or play.get("playerName") or f"P{self._to_int(client.get('ipcId', 0), 0) + 1}").strip()
        return {
            "name": name[:32],
            "score": self._to_int(play.get("score", 0), 0),
            "accuracy": self._to_float(play.get("accuracy", 0.0), 0.0),
            "combo": self._to_int(combo.get("current", 0), 0),
            "maxCombo": self._to_int(combo.get("max", 0), 0),
            "misses": self._to_int(hits.get("0", 0), 0),
            "team": str(client.get("team", ""))[:12],
            "ipcId": self._to_int(client.get("ipcId", -1), -1),
        }

    def add_chat_message(self, sender, message, kind="chat"):
        sender = str(sender or "system")[:32]
        message = str(message or "").replace("\r", " ").replace("\n", " ").strip()
        if not message:
            return

        with self.lock:
            messages = self.state.setdefault("chatMessages", [])
            messages.append(
                {
                    "time": time.strftime("%H:%M:%S"),
                    "sender": sender,
                    "message": message[:220],
                    "kind": str(kind or "chat")[:20],
                }
            )
            del messages[:-30]
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
        self.broadcast()

    def auto_start_map_if_ready(self):
        with self.lock:
            if self.state.get("screen") != "mapReady":
                return False

            self.state["screen"] = "inGame"
            self.state["lastResult"] = "none"
            self.state["resultMessage"] = ""
            self._normalize_locked()
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1

        self.broadcast()
        self._notify_screen_changed("inGame")
        return True

    def set_irc_client(self, irc_client):
        with self.lock:
            self.irc_client = irc_client

    def request_mp_setup(self, setup):
        irc_client = None
        with self.lock:
            irc_client = self.irc_client

        if irc_client is None:
            self.add_chat_message("Bridge", "MP command bot is unavailable. Check API.Json and IRC connection.", "system")
            print("[MP BOT] Cannot send setup: IRC client is not available.", flush=True)
            return

        irc_client.queue_mp_setup(setup)

    def request_mp_command(self, message):
        irc_client = None
        with self.lock:
            irc_client = self.irc_client

        if irc_client is None:
            self.add_chat_message("Bridge", "Manual MP command skipped: IRC client is unavailable. Check API.Json.", "system")
            print("[MP BOT] Cannot send manual command: IRC client is not available.", flush=True)
            return False

        return irc_client.send_manual_mp_command(message)

    def apply_command(self, command):
        if not isinstance(command, dict):
            raise ValueError("command must be a json object")

        command_type = command.get("type", "")
        mp_setup_request = None
        mp_command_request = None
        previous_screen = None
        next_screen = None
        with self.lock:
            previous_screen = self.state.get("screen", "")
            if command_type == "reset_event":
                self.state = self._state_from_setting()
            elif command_type == "reload_settings":
                self.state = self._state_from_setting()
            elif command_type == "set_screen":
                self.state["screen"] = str(command.get("screen", "standby"))
            elif command_type == "enter_difficulty_select":
                self.state["screen"] = "difficultySelect"
                self.state["selectedMode"] = ""
                self.state["selectedMapId"] = ""
                self.state["lastResult"] = "none"
                self.state["resultMessage"] = ""
            elif command_type == "enter_mode_roulette":
                self.state["screen"] = "rouletteMode"
                self.state["selectedMode"] = ""
                self.state["selectedMapId"] = ""
                self.state["lastResult"] = "none"
                self.state["resultMessage"] = ""
            elif command_type == "complete_mode_roulette":
                mode = str(command.get("mode", ""))
                if any(raid_map.get("mode") == mode for raid_map in self.state["mapPool"]):
                    self.state["selectedMode"] = mode
                self.state["selectedMapId"] = ""
                self.state["screen"] = "rouletteMap"
            elif command_type == "complete_map_roulette":
                raid_map = self._map(str(command.get("mapId", "")))
                if raid_map is not None:
                    self.state["selectedMapId"] = raid_map["id"]
                    self.state["selectedMode"] = raid_map["mode"]
                    mp_setup_request = self._build_mp_setup_locked()
                self.state["screen"] = "mapReady"
            elif command_type == "set_current_team":
                team_id = str(command.get("teamId", ""))
                if self._team(team_id) is not None:
                    self.state["currentTeamId"] = team_id
            elif command_type == "set_team_name":
                team = self._team(str(command.get("teamId", "")))
                if team is not None:
                    team["name"] = str(command.get("name", team["name"]))[:32]
            elif command_type == "set_difficulty":
                difficulty_id = str(command.get("difficulty", self.state["difficulty"]))
                if self._difficulty(difficulty_id) is not None:
                    self.state["difficulty"] = difficulty_id
            elif command_type == "set_team_score":
                team = self._team(str(command.get("teamId", "")))
                if team is not None:
                    team["score"] = self._to_int(command.get("score", 0), 0)
            elif command_type == "set_scores":
                scores = command.get("scores", {})
                if isinstance(scores, dict):
                    for team in self.state["teams"]:
                        if team["id"] in scores:
                            team["score"] = self._to_int(scores[team["id"]], 0)
            elif command_type == "assign_burgers":
                self._assign_burgers_locked()
                self.state["screen"] = "burgerReveal"
            elif command_type == "set_burger_maps":
                self._set_burger_maps_locked(command.get("mapIds", []))
                self.state["selectedMapId"] = ""
                self.state["screen"] = "burgerReveal"
            elif command_type == "pick_round_maps":
                self._pick_round_maps_locked()
                self.state["screen"] = "burgerReveal"
            elif command_type == "spin_mode":
                self._spin_mode_locked()
                self.state["screen"] = "rouletteMode"
            elif command_type == "spin_map":
                self._spin_map_locked()
                self.state["screen"] = "rouletteMap"
            elif command_type == "ready_map":
                self.state["screen"] = "mapReady"
            elif command_type == "confirm_difficulty":
                difficulty_id = str(command.get("difficulty", self.state["difficulty"]))
                if self._difficulty(difficulty_id) is not None:
                    self.state["difficulty"] = difficulty_id
                self.state["screen"] = "rouletteMode"
                self.state["selectedMode"] = ""
                self.state["selectedMapId"] = ""
                self.state["lastResult"] = "none"
                self.state["resultMessage"] = ""
            elif command_type == "start_map":
                self.state["screen"] = "inGame"
                self.state["lastResult"] = "none"
                self.state["resultMessage"] = ""
            elif command_type == "finish_map":
                self._finish_map_locked()
            elif command_type == "next_round":
                self._next_round_locked()
            elif command_type == "load_maps":
                maps = command.get("maps")
                if isinstance(maps, list) and maps:
                    self._load_maps_locked(maps)
            elif command_type == "send_mp_setup":
                mp_setup_request = self._build_mp_setup_locked()
            elif command_type == "send_mp_command":
                mp_command_request = normalize_mp_command(command.get("message", ""))
                if not mp_command_request:
                    raise ValueError("message must be a !mp command")
            elif command_type == "clear_chat":
                self.state["chatMessages"] = []
            elif command_type == "add_chat_message":
                message = str(command.get("message", "")).replace("\r", " ").replace("\n", " ").strip()
                if not message:
                    return self.snapshot()
                messages = self.state.setdefault("chatMessages", [])
                messages.append(
                    {
                        "time": time.strftime("%H:%M:%S"),
                        "sender": str(command.get("sender", "Bridge"))[:32],
                        "message": message[:220],
                        "kind": str(command.get("kind", "system"))[:20],
                    }
                )
                del messages[:-30]
            else:
                raise ValueError(f"unknown command type: {command_type}")

            self._normalize_locked()
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1
            next_screen = self.state.get("screen", "")

        self.broadcast()
        if next_screen != previous_screen:
            self._notify_screen_changed(next_screen)
        if mp_setup_request is not None:
            self.request_mp_setup(mp_setup_request)
        if mp_command_request is not None:
            self.request_mp_command(mp_command_request)
        return self.snapshot()

    def _normalize_locked(self):
        self._normalize_state(self.state)

    def _build_mp_setup_locked(self):
        selected_map = self._selected_map()
        if selected_map is None:
            return None

        beatmap_id = self._to_int(selected_map.get("beatmapId", 0), 0)
        return {
            "mapId": selected_map.get("id", ""),
            "beatmapId": beatmap_id,
            "mode": selected_map.get("mode", self.state.get("selectedMode", "")),
            "title": selected_map.get("title", ""),
        }

    def _assign_burgers_locked(self):
        maps = self.state["mapPool"]
        for raid_map in maps:
            raid_map["isBurger"] = False

        for raid_map in random.sample(maps, min(8, len(maps))):
            raid_map["isBurger"] = True

    def _set_burger_maps_locked(self, map_ids):
        if not isinstance(map_ids, list):
            map_ids = []

        selected_ids = {str(map_id) for map_id in map_ids}
        for raid_map in self.state["mapPool"]:
            raid_map["isBurger"] = raid_map.get("id") in selected_ids

    def _pick_round_maps_locked(self):
        maps = self.state["mapPool"]
        self.state["selectedRoundMapIds"] = [m["id"] for m in random.sample(maps, min(8, len(maps)))]
        for raid_map in maps:
            raid_map["played"] = False

    def _spin_mode_locked(self):
        candidates = self._eligible_maps_locked(require_mode=False)
        modes = sorted({raid_map["mode"] for raid_map in candidates})
        if modes:
            self.state["selectedMode"] = random.choice(modes)
            self.state["selectedMapId"] = ""
            self.state["resultMessage"] = ""
        else:
            self.state["selectedMode"] = ""
            self.state["selectedMapId"] = ""
            self.state["resultMessage"] = "No unplayed maps remain."

    def _spin_map_locked(self):
        candidates = self._eligible_maps_locked(require_mode=True)
        if not candidates:
            candidates = self._eligible_maps_locked(require_mode=False)
        if candidates:
            raid_map = random.choice(candidates)
            self.state["selectedMode"] = raid_map["mode"]
            self.state["selectedMapId"] = raid_map["id"]
            self.state["resultMessage"] = ""
        else:
            self.state["selectedMapId"] = ""
            self.state["resultMessage"] = "No unplayed maps remain."

    def _eligible_maps_locked(self, require_mode):
        round_ids = set(self.state.get("selectedRoundMapIds", []))
        selected_mode = self.state.get("selectedMode", "")
        candidates = []
        for raid_map in self.state["mapPool"]:
            if raid_map.get("played"):
                continue
            if round_ids and raid_map["id"] not in round_ids:
                continue
            if require_mode and raid_map.get("mode") != selected_mode:
                continue
            candidates.append(raid_map)
        return candidates

    def _finish_map_locked(self):
        self._normalize_locked()
        selected_map = self._selected_map()
        difficulty = self._difficulty(self.state["difficulty"])
        clear = self.state["totalScore"] >= self.state["bossHp"]
        self.state["lastResult"] = "clear" if clear else "fail"
        self.state["screen"] = "result"

        if selected_map is not None:
            selected_map["played"] = True

        if clear:
            self.state["clearCount"] = int(self.state.get("clearCount", 0)) + 1
            if difficulty is not None:
                self.state["prizePool"] = int(self.state.get("prizePool", 0)) + int(difficulty["prize"])

            if selected_map and selected_map.get("isBurger") and self.state["difficulty"] == "hard":
                self.state["burgerCount"] = int(self.state.get("burgerCount", 0)) + 1
                self.state["resultMessage"] = "Boss defeated. Viewer burger added."
            elif selected_map and selected_map.get("isBurger"):
                self.state["burgerMissCount"] = int(self.state.get("burgerMissCount", 0)) + 1
                self.state["resultMessage"] = "Boss defeated, but burger condition missed."
            else:
                self.state["resultMessage"] = "Boss defeated. Prize pool increased."
        else:
            self.state["failCount"] = int(self.state.get("failCount", 0)) + 1
            if selected_map and selected_map.get("isBurger"):
                self.state["burgerMissCount"] = int(self.state.get("burgerMissCount", 0)) + 1
            self.state["resultMessage"] = "Boss survived. Failure count increased."

    def _next_round_locked(self):
        self.state["roundIndex"] = min(7, int(self.state.get("roundIndex", 0)) + 1)
        self.state["screen"] = "difficultySelect"
        self.state["lastResult"] = "none"
        self.state["resultMessage"] = ""
        self.state["selectedMode"] = ""
        self.state["selectedMapId"] = ""
        for team in self.state["teams"]:
            team["score"] = 0
            for player in team.get("players", []):
                if isinstance(player, dict):
                    player["score"] = 0
                    player["accuracy"] = 0.0
                    player["combo"] = 0
                    player["maxCombo"] = 0
                    player["misses"] = 0

    def _load_maps_locked(self, maps):
        next_maps = self._sanitize_maps(maps)
        if next_maps:
            self.state["mapPool"] = next_maps
            self.state["selectedRoundMapIds"] = [m["id"] for m in self.state["mapPool"]]
            self.state["selectedMode"] = ""
            self.state["selectedMapId"] = ""

    def _team(self, team_id):
        for team in self.state.get("teams", []):
            if team.get("id") == team_id:
                return team
        return None

    def _difficulty(self, difficulty_id):
        for difficulty in self.state.get("difficulties", []):
            if difficulty.get("id") == difficulty_id:
                return difficulty
        return None

    def _map(self, map_id):
        for raid_map in self.state.get("mapPool", []):
            if raid_map.get("id") == map_id:
                return raid_map
        return None

    def _selected_map(self):
        selected_id = self.state.get("selectedMapId", "")
        return self._map(selected_id)

    @staticmethod
    def _to_int(value, fallback):
        try:
            return int(value)
        except (TypeError, ValueError):
            return fallback

    @staticmethod
    def _to_float(value, fallback):
        try:
            return float(value)
        except (TypeError, ValueError):
            return fallback

    @staticmethod
    def _player_name(player):
        if isinstance(player, dict):
            return str(player.get("name", "")).strip() or "-"
        return str(player).strip() or "-"


def load_bridge_config():
    if not API_PATH.exists():
        print(f"[API] Missing {API_PATH}. IRC chat, tourney IPC, and OBS automation are disabled.", flush=True)
        return None

    try:
        with API_PATH.open("r", encoding="utf-8-sig") as api_file:
            config = json.load(api_file)
    except Exception as exc:
        print(f"[API] Failed to read {API_PATH}: {exc}", flush=True)
        return None

    if not isinstance(config, dict):
        print(f"[API] Failed to read {API_PATH}: root must be a JSON object", flush=True)
        return None

    return config


def load_api_config(config=None):
    config = config if isinstance(config, dict) else load_bridge_config()
    if not isinstance(config, dict):
        return None

    enabled = bool(config.get("enabled", False))
    username = str(config.get("ircUsername", "")).strip()
    password = str(config.get("ircPassword", "")).strip()
    channel = str(config.get("mpChannel", "")).strip()
    server = str(config.get("ircServer", "irc.ppy.sh")).strip() or "irc.ppy.sh"
    port = BossRaidBridge._to_int(config.get("ircPort", 6667), 6667)
    debug_raw_irc = bool(config.get("debugRawIrc", False))
    command_bot_enabled = bool(config.get("commandBotEnabled", True))
    timer_seconds = max(5, BossRaidBridge._to_int(config.get("mpTimerSeconds", 90), 90))
    start_delay_seconds = max(1, BossRaidBridge._to_int(config.get("mpStartDelaySeconds", 5), 5))
    auto_in_game_after_start = bool(config.get("mpAutoInGameAfterStart", True))
    auto_in_game_extra_delay_seconds = max(0.0, BossRaidBridge._to_float(config.get("mpAutoInGameExtraDelaySeconds", 0.5), 0.5))
    ready_reminder_message = str(config.get("readyReminderMessage", "모두 준비를 눌러주세요.")).strip() or "모두 준비를 눌러주세요."

    if not enabled:
        print("[API] IRC chat disabled. Set enabled=true in API.Json to connect.", flush=True)
        return None

    if not username or not password or not channel:
        print("[API] IRC chat disabled. Fill ircUsername, ircPassword, and mpChannel in API.Json.", flush=True)
        return None

    if not channel.startswith("#"):
        channel = "#" + channel

    print(f"[API] IRC chat enabled. Server={server}:{port}, user={username.replace(' ', '_')}, channel={channel}, rawLog={debug_raw_irc}", flush=True)
    print(f"[API] MP command bot: {'enabled' if command_bot_enabled else 'disabled'}, timer={timer_seconds}s, start={start_delay_seconds}s, autoInGame={auto_in_game_after_start}", flush=True)
    return {
        "server": server,
        "port": port,
        "username": username.replace(" ", "_"),
        "password": password,
        "channel": channel,
        "debugRawIrc": debug_raw_irc,
        "commandBotEnabled": command_bot_enabled,
        "mpTimerSeconds": timer_seconds,
        "mpStartDelaySeconds": start_delay_seconds,
        "mpAutoInGameAfterStart": auto_in_game_after_start,
        "mpAutoInGameExtraDelaySeconds": auto_in_game_extra_delay_seconds,
        "readyReminderMessage": ready_reminder_message[:120],
    }


def load_tourney_ipc_config(config=None):
    config = config if isinstance(config, dict) else load_bridge_config()
    if not isinstance(config, dict):
        return None

    enabled = bool(config.get("tourneyIpcEnabled", False))
    if not enabled:
        print("[SCORE IPC] Tourney IPC disabled. Set tourneyIpcEnabled=true in API.Json to read osu! tournament scores.", flush=True)
        return None

    poll_ms = max(50, BossRaidBridge._to_int(config.get("tourneyIpcPollMs", 250), 250))
    team_ids = config.get("tourneyIpcTeamIds", [])
    if isinstance(team_ids, list):
        team_ids = [str(team_id).strip() for team_id in team_ids if str(team_id).strip()]
    else:
        team_ids = []

    update_screens = config.get("tourneyIpcUpdateScreens", ["inGame"])
    if isinstance(update_screens, list):
        update_screens = [str(screen).strip() for screen in update_screens if str(screen).strip()]
    else:
        update_screens = ["inGame"]

    return {
        "path": str(config.get("tourneyIpcPath", "")).strip(),
        "scoreFile": str(config.get("tourneyIpcScoreFile", "ipc-scores.txt")).strip() or "ipc-scores.txt",
        "pollSeconds": poll_ms / 1000,
        "teamIds": team_ids,
        "updateScreens": update_screens,
    }


def load_tosu_config(config=None):
    config = config if isinstance(config, dict) else load_bridge_config()
    if not isinstance(config, dict):
        return None

    enabled = bool(config.get("tosuEnabled", False))
    if not enabled:
        print("[TOSU] tosu disabled. Set tosuEnabled=true in API.Json to read live tournament player scores.", flush=True)
        return None

    team_ids = config.get("tosuTeamIds", [])
    if isinstance(team_ids, list):
        team_ids = [str(team_id).strip() for team_id in team_ids if str(team_id).strip()]
    else:
        team_ids = []

    update_screens = config.get("tosuUpdateScreens", config.get("tourneyIpcUpdateScreens", ["inGame"]))
    if isinstance(update_screens, list):
        update_screens = [str(screen).strip() for screen in update_screens if str(screen).strip()]
    else:
        update_screens = ["inGame"]

    reconnect_seconds = max(1, BossRaidBridge._to_int(config.get("tosuReconnectSeconds", 5), 5))
    auto_finish_enabled = bool(config.get("tosuAutoFinishEnabled", True))
    auto_finish_delay_seconds = max(0.0, BossRaidBridge._to_float(config.get("tosuAutoFinishDelaySeconds", 5.0), 5.0))
    return {
        "url": str(config.get("tosuWebSocketUrl", "ws://127.0.0.1:24050/websocket/v2")).strip() or "ws://127.0.0.1:24050/websocket/v2",
        "teamIds": team_ids,
        "updateScreens": update_screens,
        "reconnectSeconds": reconnect_seconds,
        "autoFinishEnabled": auto_finish_enabled,
        "autoFinishDelaySeconds": auto_finish_delay_seconds,
    }


def load_obs_websocket_config(config=None):
    config = config if isinstance(config, dict) else load_bridge_config()
    if not isinstance(config, dict):
        return None

    enabled = bool(config.get("obsWebSocketEnabled", False))
    if not enabled:
        print("[OBS] OBS WebSocket automation disabled. Set obsWebSocketEnabled=true in API.Json to control spectator sources.", flush=True)
        return None

    sources = config.get("obsSpectatorSources", [])
    if isinstance(sources, str):
        sources = [sources]
    if isinstance(sources, list):
        sources = [str(source).strip() for source in sources if str(source).strip()]
    else:
        sources = []

    screen_sources = {}
    raw_screen_sources = config.get("obsSpectatorScreenSources", {})
    if isinstance(raw_screen_sources, dict):
        for screen, screen_source_list in raw_screen_sources.items():
            screen_name = str(screen).strip()
            if not screen_name:
                continue
            if isinstance(screen_source_list, str):
                screen_source_list = [screen_source_list]
            if isinstance(screen_source_list, list):
                screen_sources[screen_name] = [str(source).strip() for source in screen_source_list if str(source).strip()]

    if not sources and not any(screen_sources.values()):
        print("[OBS] OBS automation disabled. Fill obsSpectatorSources or obsSpectatorScreenSources in API.Json.", flush=True)
        return None

    visible_screens = config.get("obsSpectatorVisibleScreens", ["mapReady", "inGame"])
    if isinstance(visible_screens, list):
        visible_screens = [str(screen).strip() for screen in visible_screens if str(screen).strip()]
    else:
        visible_screens = ["mapReady", "inGame"]

    poll_ms = max(100, BossRaidBridge._to_int(config.get("obsWebSocketPollMs", 250), 250))
    reconnect_seconds = max(1, BossRaidBridge._to_int(config.get("obsWebSocketReconnectSeconds", 5), 5))

    return {
        "url": str(config.get("obsWebSocketUrl", "ws://127.0.0.1:4455")).strip() or "ws://127.0.0.1:4455",
        "password": str(config.get("obsWebSocketPassword", "")),
        "sceneName": str(config.get("obsSceneName", "")).strip(),
        "sources": sources,
        "screenSources": screen_sources,
        "visibleScreens": visible_screens,
        "pollSeconds": poll_ms / 1000,
        "reconnectSeconds": reconnect_seconds,
    }


class TourneyIpcScoreClient:
    def __init__(self, bridge, config):
        self.bridge = bridge
        self.config = config
        self.stop_event = threading.Event()
        self.thread = None
        self.last_status = ""
        self.last_path = None

    def start(self):
        if self.thread is not None:
            return
        self.thread = threading.Thread(target=self._run, name="TourneyIpcScoreClient", daemon=True)
        self.thread.start()

    def stop(self):
        self.stop_event.set()
        if self.thread is not None:
            self.thread.join(timeout=2.0)

    def _run(self):
        poll_seconds = float(self.config.get("pollSeconds", 0.25))
        while not self.stop_event.is_set():
            score_path = self._resolve_score_file()
            if score_path is None:
                self._set_status("TOURNEY IPC WAITING")
                self.stop_event.wait(1.0)
                continue

            if score_path != self.last_path:
                self.last_path = score_path
                print(f"[SCORE IPC] Reading osu! tournament scores from {score_path}", flush=True)
                self._set_status("TOURNEY IPC WATCHING")

            try:
                scores = self._read_scores(score_path)
            except Exception as exc:
                print(f"[SCORE IPC] Failed to read {score_path}: {exc}", flush=True)
                self._set_status(f"TOURNEY IPC ERROR: {type(exc).__name__}")
                self.stop_event.wait(1.0)
                continue

            if scores:
                self.bridge.apply_external_scores(
                    scores,
                    source_label="Tourney IPC",
                    team_ids=self.config.get("teamIds", []),
                    allowed_screens=self.config.get("updateScreens", []),
                )
            else:
                self._set_status("TOURNEY IPC EMPTY")

            self.stop_event.wait(poll_seconds)

    def _set_status(self, status):
        status = str(status or "")[:80]
        if status == self.last_status:
            return
        self.last_status = status
        print(f"[SCORE IPC] {status}", flush=True)
        self.bridge.set_score_source_status(status)

    def _resolve_score_file(self):
        configured = self.config.get("path", "")
        env_path = os.environ.get("BOSS_RAID_TOURNEY_IPC_PATH", "")
        score_file = self.config.get("scoreFile", "ipc-scores.txt")

        for raw_path in [env_path, configured]:
            candidate = self._score_file_from_path(raw_path, score_file)
            if candidate is not None:
                return candidate

        for directory in self._default_candidate_directories():
            candidate = Path(directory) / score_file
            if candidate.exists():
                return candidate
        return None

    @staticmethod
    def _score_file_from_path(raw_path, score_file):
        if not raw_path:
            return None

        path = Path(os.path.expandvars(os.path.expanduser(str(raw_path))))
        if path.is_file():
            return path
        if path.is_dir():
            candidate = path / score_file
            return candidate if candidate.exists() else None

        if path.name.lower() == score_file.lower() or path.suffix:
            return path if path.exists() else None

        candidate = path / score_file
        return candidate if candidate.exists() else None

    @staticmethod
    def _default_candidate_directories():
        candidates = [PROJECT_ROOT, PROJECT_ROOT / "Bridge"]
        local_app_data = os.environ.get("LOCALAPPDATA", "")
        app_data = os.environ.get("APPDATA", "")
        user_profile = os.environ.get("USERPROFILE", "")
        if local_app_data:
            candidates.append(Path(local_app_data) / "osu!")
            candidates.append(Path(local_app_data) / "osulazer")
        if app_data:
            candidates.append(Path(app_data) / "osu")
        if user_profile:
            candidates.append(Path(user_profile) / ".osu")
        return candidates

    @staticmethod
    def _read_scores(score_path):
        with Path(score_path).open("r", encoding="utf-8-sig", errors="ignore") as score_file:
            return TourneyIpcScoreClient._parse_score_lines(score_file.read())

    @staticmethod
    def _parse_score_lines(text):
        scores = []
        for line in str(text or "").splitlines():
            matches = re.findall(r"-?\d[\d,]*", line)
            if not matches:
                continue
            raw_value = matches[-1]
            cleaned = re.sub(r"[^\d-]", "", raw_value)
            if not cleaned or cleaned == "-":
                continue
            try:
                scores.append(max(0, int(cleaned)))
            except ValueError:
                continue
        return scores


class TosuTourneyScoreClient:
    def __init__(self, bridge, config):
        self.bridge = bridge
        self.config = config
        self.stop_event = threading.Event()
        self.thread = None
        self.sock = None
        self.last_status = ""

    def start(self):
        if self.thread is not None:
            return
        self.thread = threading.Thread(target=self._run, name="TosuTourneyScoreClient", daemon=True)
        self.thread.start()

    def stop(self):
        self.stop_event.set()
        if self.sock is not None:
            try:
                self.sock.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            try:
                self.sock.close()
            except OSError:
                pass
        if self.thread is not None:
            self.thread.join(timeout=2.0)

    def _run(self):
        reconnect_seconds = float(self.config.get("reconnectSeconds", 5))
        while not self.stop_event.is_set():
            try:
                self._connect()
                self._set_status("TOSU CONNECTED")
                self._read_loop()
            except Exception as exc:
                if self.stop_event.is_set():
                    break
                self._close_socket()
                status = f"TOSU RETRYING: {type(exc).__name__}"
                print(f"[TOSU] {status}: {exc}", flush=True)
                self._set_status(status)
                self.stop_event.wait(reconnect_seconds)

    def _connect(self):
        self._close_socket()
        parsed = urlparse(self.config.get("url", "ws://127.0.0.1:24050/websocket/v2"))
        if parsed.scheme not in ("ws", ""):
            raise ValueError("only ws:// tosu WebSocket URLs are supported")

        host = parsed.hostname or "127.0.0.1"
        port = parsed.port or 24050
        path = parsed.path or "/websocket/v2"
        if parsed.query:
            path += "?" + parsed.query

        print(f"[TOSU] Connecting to ws://{host}:{port}{path}", flush=True)
        sock_obj = socket.create_connection((host, port), timeout=5)
        sock_obj.settimeout(10)
        key = base64.b64encode(os.urandom(16)).decode("ascii")
        request = (
            f"GET {path} HTTP/1.1\r\n"
            f"Host: {host}:{port}\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            "Sec-WebSocket-Version: 13\r\n"
            "\r\n"
        )
        sock_obj.sendall(request.encode("ascii"))
        response = ObsWebSocketVisibilityClient._read_http_response(sock_obj)
        if " 101 " not in response.split("\r\n", 1)[0]:
            raise ConnectionError(response.split("\r\n", 1)[0])
        self.sock = sock_obj

    def _read_loop(self):
        while not self.stop_event.is_set():
            opcode, payload = read_ws_frame(self.sock)
            if opcode is None:
                raise ConnectionError("tosu socket closed")
            if opcode == 8:
                raise ConnectionError("tosu closed websocket")
            if opcode == 9:
                send_ws_client_frame(self.sock, 10, payload)
                continue
            if opcode not in (1, 2) or not payload:
                continue

            data = json.loads(payload.decode("utf-8"))
            tourney = data.get("tourney") if isinstance(data, dict) else None
            if not isinstance(tourney, dict):
                self._set_status("TOSU NO TOURNEY")
                continue

            clients = tourney.get("clients", [])
            if not isinstance(clients, list) or not clients:
                self._set_status("TOSU TOURNEY EMPTY")
                continue

            self.bridge.apply_tosu_tourney_state(
                tourney,
                team_ids=self.config.get("teamIds", []),
                allowed_screens=self.config.get("updateScreens", []),
            )
            self.bridge.maybe_auto_finish_from_tosu(
                data,
                delay_seconds=self.config.get("autoFinishDelaySeconds", 5.0),
                enabled=self.config.get("autoFinishEnabled", True),
            )

    def _set_status(self, status):
        status = str(status or "")[:80]
        if status == self.last_status:
            return
        self.last_status = status
        print(f"[TOSU] {status}", flush=True)
        self.bridge.set_score_source_status(status)

    def _close_socket(self):
        if self.sock is None:
            return
        try:
            self.sock.close()
        except OSError:
            pass
        self.sock = None


class ObsWebSocketVisibilityClient:
    def __init__(self, bridge, config):
        self.bridge = bridge
        self.config = config
        self.stop_event = threading.Event()
        self.thread = None
        self.sock = None
        self.request_index = 0
        self.scene_item_cache = {}
        self.last_status = ""
        self.last_screen = None
        self.screen_event = threading.Event()

    def start(self):
        if self.thread is not None:
            return
        self.bridge.add_screen_listener(self.on_screen_changed)
        self.thread = threading.Thread(target=self._run, name="ObsWebSocketVisibilityClient", daemon=True)
        self.thread.start()

    def stop(self):
        self.stop_event.set()
        self.screen_event.set()
        self.bridge.remove_screen_listener(self.on_screen_changed)
        if self.sock is not None:
            try:
                self.sock.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            try:
                self.sock.close()
            except OSError:
                pass
        if self.thread is not None:
            self.thread.join(timeout=2.0)

    def on_screen_changed(self, screen):
        self.screen_event.set()

    def _run(self):
        reconnect_seconds = float(self.config.get("reconnectSeconds", 5))
        while not self.stop_event.is_set():
            try:
                self._connect()
                self._set_status("OBS CONNECTED")
                self._sync_loop()
            except Exception as exc:
                if self.stop_event.is_set():
                    break
                self._close_socket()
                self.scene_item_cache.clear()
                self.last_screen = None
                status = f"OBS RETRYING: {type(exc).__name__}"
                print(f"[OBS] {status}: {exc}", flush=True)
                self._set_status(status)
                self.stop_event.wait(reconnect_seconds)

    def _sync_loop(self):
        poll_seconds = float(self.config.get("pollSeconds", 0.25))
        while not self.stop_event.is_set():
            screen = self.bridge.snapshot().get("screen", "")
            if screen != self.last_screen:
                self._apply_screen(screen)
                self.last_screen = screen
            self.screen_event.wait(poll_seconds)
            self.screen_event.clear()

    def _connect(self):
        self._close_socket()
        parsed = urlparse(self.config.get("url", "ws://127.0.0.1:4455"))
        if parsed.scheme not in ("ws", ""):
            raise ValueError("only ws:// OBS WebSocket URLs are supported")

        host = parsed.hostname or "127.0.0.1"
        port = parsed.port or 4455
        path = parsed.path or "/"
        if parsed.query:
            path += "?" + parsed.query

        print(f"[OBS] Connecting to {host}:{port}", flush=True)
        sock_obj = socket.create_connection((host, port), timeout=5)
        sock_obj.settimeout(5)
        key = base64.b64encode(os.urandom(16)).decode("ascii")
        request = (
            f"GET {path} HTTP/1.1\r\n"
            f"Host: {host}:{port}\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            "Sec-WebSocket-Version: 13\r\n"
            "Sec-WebSocket-Protocol: obswebsocket.json\r\n"
            "\r\n"
        )
        sock_obj.sendall(request.encode("ascii"))
        response = self._read_http_response(sock_obj)
        if " 101 " not in response.split("\r\n", 1)[0]:
            raise ConnectionError(response.split("\r\n", 1)[0])

        self.sock = sock_obj
        hello = self._recv_obs_message()
        if hello.get("op") != 0:
            raise ConnectionError("OBS did not send Hello")

        hello_data = hello.get("d", {})
        rpc_version = min(1, BossRaidBridge._to_int(hello_data.get("rpcVersion", 1), 1))
        identify_data = {
            "rpcVersion": rpc_version,
            "eventSubscriptions": 0,
        }
        authentication = hello_data.get("authentication")
        if isinstance(authentication, dict):
            identify_data["authentication"] = self._build_authentication(
                self.config.get("password", ""),
                authentication.get("salt", ""),
                authentication.get("challenge", ""),
            )

        self._send_obs_message({"op": 1, "d": identify_data})
        identified = self._recv_obs_message()
        if identified.get("op") != 2:
            raise ConnectionError("OBS did not accept Identify")

    def _apply_screen(self, screen):
        scene_name = self._resolve_scene_name()
        desired_sources = set(self._sources_for_screen(screen))
        all_sources = self._all_sources()
        errors = []
        for source_name in all_sources:
            try:
                self._set_source_enabled(scene_name, source_name, source_name in desired_sources)
            except Exception as exc:
                errors.append(f"{source_name}: {exc}")

        action = "ON" if desired_sources else "OFF"
        if errors:
            for error in errors:
                print(f"[OBS] Source toggle failed: {error}", flush=True)
            self._set_status(f"OBS PARTIAL {screen} {action}")
            return

        self._set_status(f"OBS {screen} {action}")

    def _sources_for_screen(self, screen):
        screen_sources = self.config.get("screenSources", {})
        if isinstance(screen_sources, dict) and screen in screen_sources:
            return screen_sources.get(screen, [])

        if screen in set(self.config.get("visibleScreens", [])):
            return self.config.get("sources", [])
        return []

    def _all_sources(self):
        ordered_sources = []
        seen = set()

        def add(source_name):
            if not source_name or source_name in seen:
                return
            seen.add(source_name)
            ordered_sources.append(source_name)

        for source_name in self.config.get("sources", []):
            add(source_name)
        screen_sources = self.config.get("screenSources", {})
        if isinstance(screen_sources, dict):
            for source_list in screen_sources.values():
                for source_name in source_list:
                    add(source_name)
        return ordered_sources

    def _set_source_enabled(self, scene_name, source_name, enabled):
        scene_item_id = self._get_scene_item_id(scene_name, source_name)
        self._request(
            "SetSceneItemEnabled",
            {
                "sceneName": scene_name,
                "sceneItemId": scene_item_id,
                "sceneItemEnabled": bool(enabled),
            },
        )

    def _resolve_scene_name(self):
        configured = self.config.get("sceneName", "")
        if configured:
            return configured

        response = self._request("GetCurrentProgramScene")
        scene_name = response.get("sceneName") or response.get("currentProgramSceneName")
        if not scene_name:
            raise ValueError("OBS current scene is unavailable")
        return scene_name

    def _get_scene_item_id(self, scene_name, source_name):
        cache_key = (scene_name, source_name)
        if cache_key in self.scene_item_cache:
            return self.scene_item_cache[cache_key]

        response = self._request(
            "GetSceneItemId",
            {
                "sceneName": scene_name,
                "sourceName": source_name,
                "searchOffset": -1,
            },
        )
        scene_item_id = response.get("sceneItemId")
        if scene_item_id is None:
            raise ValueError("sceneItemId missing")
        self.scene_item_cache[cache_key] = scene_item_id
        return scene_item_id

    def _request(self, request_type, request_data=None):
        if self.sock is None:
            raise ConnectionError("OBS socket is not connected")

        self.request_index += 1
        request_id = f"bossraid-{int(time.time() * 1000)}-{self.request_index}"
        payload = {
            "op": 6,
            "d": {
                "requestType": request_type,
                "requestId": request_id,
            },
        }
        if request_data is not None:
            payload["d"]["requestData"] = request_data

        self._send_obs_message(payload)
        while not self.stop_event.is_set():
            message = self._recv_obs_message()
            if message.get("op") != 7:
                continue
            data = message.get("d", {})
            if data.get("requestId") != request_id:
                continue
            status = data.get("requestStatus", {})
            if not status.get("result"):
                comment = status.get("comment") or status.get("code") or "request failed"
                raise RuntimeError(f"{request_type}: {comment}")
            return data.get("responseData", {}) or {}

        raise ConnectionError("OBS client stopped")

    def _send_obs_message(self, message):
        if self.sock is None:
            raise ConnectionError("OBS socket is not connected")
        send_ws_client_text(self.sock, json.dumps(message, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))

    def _recv_obs_message(self):
        if self.sock is None:
            raise ConnectionError("OBS socket is not connected")
        while not self.stop_event.is_set():
            opcode, payload = read_ws_frame(self.sock)
            if opcode is None:
                raise ConnectionError("OBS socket closed")
            if opcode == 8:
                raise ConnectionError("OBS closed websocket")
            if opcode == 9:
                send_ws_client_frame(self.sock, 10, payload)
                continue
            if opcode == 1 and payload:
                return json.loads(payload.decode("utf-8"))
        raise ConnectionError("OBS client stopped")

    def _close_socket(self):
        if self.sock is None:
            return
        try:
            self.sock.close()
        except OSError:
            pass
        self.sock = None

    def _set_status(self, status):
        status = str(status or "")[:80]
        if status == self.last_status:
            return
        self.last_status = status
        print(f"[OBS] {status}", flush=True)
        self.bridge.set_obs_status(status)

    @staticmethod
    def _read_http_response(sock_obj):
        chunks = []
        data = b""
        while b"\r\n\r\n" not in data:
            chunk = sock_obj.recv(4096)
            if not chunk:
                raise ConnectionError("OBS HTTP handshake closed")
            chunks.append(chunk)
            data = b"".join(chunks)
            if len(data) > 16384:
                raise ConnectionError("OBS HTTP handshake response too large")
        return data.decode("iso-8859-1", errors="replace")

    @staticmethod
    def _build_authentication(password, salt, challenge):
        secret = base64.b64encode(hashlib.sha256((str(password) + str(salt)).encode("utf-8")).digest()).decode("ascii")
        return base64.b64encode(hashlib.sha256((secret + str(challenge)).encode("utf-8")).digest()).decode("ascii")


class BanchoIrcChatClient:
    def __init__(self, bridge, config):
        self.bridge = bridge
        self.config = config
        self.stop_event = threading.Event()
        self.thread = None
        self.sock = None
        self.connected = False
        self.send_lock = threading.Lock()
        self.bot_lock = threading.RLock()
        self.bot_generation = 0
        self.bot_waiting_for_ready = False
        self.bot_timer_deadline = 0
        self.bot_timer_prompted = False
        self.bot_start_sent = False
        self.bot_last_setup_key = ""

    def start(self):
        if self.thread is not None:
            return
        self.thread = threading.Thread(target=self._run, name="BanchoIrcChatClient", daemon=True)
        self.thread.start()

    def stop(self):
        self.stop_event.set()
        if self.sock is not None:
            try:
                self.sock.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
            try:
                self.sock.close()
            except OSError:
                pass
        if self.thread is not None:
            self.thread.join(timeout=2.0)

    def _run(self):
        base_reconnect_delay = 15
        max_reconnect_delay = 180
        reconnect_delay = base_reconnect_delay
        while not self.stop_event.is_set():
            try:
                self._connect_and_read()
            except Exception as exc:
                if self.stop_event.is_set():
                    break
                status = f"IRC RETRYING IN {reconnect_delay}s: {type(exc).__name__}"
                print(f"[IRC] {status}: {exc}", flush=True)
                self.bridge.set_chat_status(status)
                self.stop_event.wait(reconnect_delay)
                reconnect_delay = base_reconnect_delay if self.connected else min(max_reconnect_delay, reconnect_delay * 2)

    def _connect_and_read(self):
        server = self.config["server"]
        port = self.config["port"]
        username = self.config["username"]
        channel = self.config["channel"]

        self.connected = False
        self.bridge.set_chat_status("IRC CONNECTING")
        print(f"[IRC] Connecting to {server}:{port} as {username}", flush=True)
        self.bridge.add_chat_message("Bridge", f"Connecting to room chat {channel}", "system")

        with socket.create_connection((server, port), timeout=20) as sock_obj:
            self.sock = sock_obj
            sock_obj.settimeout(1.0)
            self._send_raw(f"PASS {self.config['password']}")
            self._send_raw(f"NICK {username}")
            self._send_raw(f"USER {username} 0 * :{username}")

            buffer = ""
            joined = False
            welcome_received = False
            while not self.stop_event.is_set():
                try:
                    chunk = sock_obj.recv(4096)
                except socket.timeout:
                    continue

                if not chunk:
                    if not welcome_received:
                        raise ConnectionError("IRC socket closed before welcome. Check IRC password, duplicate IRC login, or temporary Bancho auth limit.")
                    raise ConnectionError("IRC socket closed")

                buffer += chunk.decode("utf-8", errors="replace")
                while "\n" in buffer:
                    line, buffer = buffer.split("\n", 1)
                    line = line.rstrip("\r")
                    if not line:
                        continue

                    if line.startswith("PING "):
                        self._send_raw("PONG " + line[5:])
                        continue

                    self._log_incoming(line)

                    parts = line.split(" ")
                    if len(parts) >= 2 and parts[1] == "001" and not joined:
                        welcome_received = True
                        self._send_raw(f"JOIN {channel}")
                        joined = True
                        print(f"[IRC] Login accepted. Joining room chat {channel}", flush=True)
                        self.bridge.set_chat_status(f"IRC JOINING {channel}")
                        continue

                    if len(parts) >= 2 and parts[1] in ("403", "464", "465", "473", "475"):
                        self._handle_irc_error(parts[1], line)
                        continue

                    if len(parts) >= 3 and parts[1] == "JOIN" and self._nick_from_prefix(parts[0]) == username:
                        self._mark_connected("JOIN")
                        continue

                    if len(parts) >= 4 and parts[1] == "366" and parts[3].lower() == channel.lower():
                        self._mark_connected("NAMES")
                        continue

                    parsed = self._parse_privmsg(line)
                    if parsed is not None:
                        sender, target, message = parsed
                        if target.lower() == channel.lower():
                            self._mark_connected("PRIVMSG")
                            message_kind = "bancho" if sender == "BanchoBot" else "chat"
                            self.bridge.add_chat_message(sender.replace("_", " "), message, message_kind)
                            self._handle_mp_bot_message(sender, message)

    def queue_mp_setup(self, setup):
        if not self.config.get("commandBotEnabled", True):
            print("[MP BOT] Command bot disabled in API.Json.", flush=True)
            self.bridge.add_chat_message("Bridge", "MP command bot is disabled.", "system")
            return

        if not isinstance(setup, dict):
            return

        beatmap_id = BossRaidBridge._to_int(setup.get("beatmapId", 0), 0)
        mode = self._normalize_mp_mode(setup.get("mode", ""))
        map_id = str(setup.get("mapId", ""))
        title = str(setup.get("title", "selected map"))
        if beatmap_id <= 0:
            message = f"Cannot send !mp setup for {map_id or title}: beatmapId is missing."
            print(f"[MP BOT] {message}", flush=True)
            self.bridge.add_chat_message("Bridge", message, "system")
            return

        setup_key = f"{map_id}:{beatmap_id}:{mode}"
        with self.bot_lock:
            if setup_key == self.bot_last_setup_key and self.bot_waiting_for_ready and not self.bot_start_sent:
                print(f"[MP BOT] Setup already active for {setup_key}; skipping duplicate.", flush=True)
                return

            self.bot_generation += 1
            generation = self.bot_generation
            self.bot_last_setup_key = setup_key
            self.bot_waiting_for_ready = False
            self.bot_timer_deadline = 0
            self.bot_timer_prompted = False
            self.bot_start_sent = False

        threading.Thread(
            target=self._run_mp_setup_sequence,
            args=(generation, beatmap_id, mode, title),
            name="MpCommandSetup",
            daemon=True,
        ).start()

    def _run_mp_setup_sequence(self, generation, beatmap_id, mode, title):
        if not self._wait_for_connected(timeout_seconds=30):
            print("[MP BOT] Cannot send setup: IRC room chat is not connected.", flush=True)
            self.bridge.add_chat_message("Bridge", "MP setup skipped: IRC room chat is not connected.", "system")
            return

        if not self._is_current_bot_generation(generation):
            return

        timer_seconds = int(self.config.get("mpTimerSeconds", 90))
        mods = self._build_mp_mods(mode)
        print(f"[MP BOT] Sending setup for {title}: beatmap={beatmap_id}, mods={mods}, timer={timer_seconds}", flush=True)
        self.bridge.add_chat_message("Bridge", f"MP setup: {title} / {mods} / timer {timer_seconds}s", "system")

        if not self.send_channel_message(f"!mp map {beatmap_id}"):
            return
        if not self._sleep_bot(generation, 1.0):
            return
        if not self.send_channel_message(f"!mp mods {mods}"):
            return
        if not self._sleep_bot(generation, 1.0):
            return
        if not self.send_channel_message(f"!mp timer {timer_seconds}"):
            return

        with self.bot_lock:
            if generation != self.bot_generation:
                return
            self.bot_waiting_for_ready = True
            self.bot_timer_deadline = time.monotonic() + timer_seconds
            self.bot_timer_prompted = False
            self.bot_start_sent = False

        threading.Thread(target=self._monitor_mp_ready_timer, args=(generation,), name="MpReadyTimer", daemon=True).start()

    def _monitor_mp_ready_timer(self, generation):
        while not self.stop_event.is_set():
            with self.bot_lock:
                if generation != self.bot_generation or not self.bot_waiting_for_ready or self.bot_start_sent:
                    return
                remaining = self.bot_timer_deadline - time.monotonic()
                if remaining <= 0:
                    self.bot_timer_prompted = True
                    reminder = self.config.get("readyReminderMessage", "모두 준비를 눌러주세요.")
                    print(f"[MP BOT] Ready timer expired. Sending reminder: {reminder}", flush=True)
                    break

            self.stop_event.wait(min(1.0, max(0.1, remaining)))
        else:
            return

        with self.bot_lock:
            should_send = (
                generation == self.bot_generation
                and self.bot_waiting_for_ready
                and not self.bot_start_sent
                and self.bot_timer_prompted
            )

        if should_send:
            self.send_channel_message(reminder)

    def _handle_mp_bot_message(self, sender, message):
        if sender != "BanchoBot" or not self.config.get("commandBotEnabled", True):
            return

        normalized = str(message or "").strip().lower()
        if not self._is_all_ready_message(normalized):
            return

        with self.bot_lock:
            if not self.bot_waiting_for_ready or self.bot_start_sent:
                return

            generation = self.bot_generation
            abort_timer = time.monotonic() < self.bot_timer_deadline and not self.bot_timer_prompted
            self.bot_start_sent = True
            self.bot_waiting_for_ready = False

        threading.Thread(target=self._start_match_after_ready, args=(generation, abort_timer), name="MpStartReady", daemon=True).start()

    def _start_match_after_ready(self, generation, abort_timer):
        if not self._is_current_bot_generation(generation):
            return

        if abort_timer:
            print("[MP BOT] All players ready. Aborting timer before start.", flush=True)
            if not self.send_channel_message("!mp aborttimer"):
                return
            if not self._sleep_bot(generation, 1.0):
                return
        else:
            print("[MP BOT] All players ready after timer. Starting match.", flush=True)

        start_delay = int(self.config.get("mpStartDelaySeconds", 5))
        if self.send_channel_message(f"!mp start {start_delay}"):
            self.bridge.add_chat_message("Bridge", f"All players ready. Sent !mp start {start_delay}.", "system")
            self._schedule_auto_in_game(generation, start_delay)

    def _schedule_auto_in_game(self, generation, start_delay):
        if not self.config.get("mpAutoInGameAfterStart", True):
            return

        extra_delay = max(0.0, BossRaidBridge._to_float(self.config.get("mpAutoInGameExtraDelaySeconds", 0.5), 0.5))
        delay = max(0.0, float(start_delay)) + extra_delay
        print(f"[MP BOT] Auto in-game transition scheduled in {delay:.1f}s.", flush=True)
        threading.Thread(
            target=self._auto_in_game_after_start,
            args=(generation, delay),
            name="MpAutoInGame",
            daemon=True,
        ).start()

    def _auto_in_game_after_start(self, generation, delay):
        if not self._sleep_bot(generation, delay):
            return

        if self.bridge.auto_start_map_if_ready():
            print("[MP BOT] Auto switched overlay to inGame.", flush=True)
            self.bridge.add_chat_message("Bridge", "Auto switched to In Game after !mp start.", "system")
        else:
            screen = self.bridge.snapshot().get("screen", "")
            print(f"[MP BOT] Auto in-game skipped because screen is {screen}.", flush=True)

    def send_manual_mp_command(self, message):
        command = normalize_mp_command(message)
        if not command:
            self.bridge.add_chat_message("Bridge", "Manual MP command skipped: type a !mp command.", "system")
            return False

        if not self.connected:
            print(f"[MP BOT] Cannot send manual command before IRC room chat is connected: {command}", flush=True)
            self.bridge.add_chat_message("Bridge", "Manual MP command skipped: IRC room chat is not connected.", "system")
            return False

        if self.send_channel_message(command):
            self.bridge.add_chat_message("Bridge", f"Sent manual MP command: {command}", "system")
            return True
        return False

    def send_channel_message(self, message):
        if not self.connected:
            print(f"[MP BOT] Cannot send before IRC room chat is connected: {message}", flush=True)
            return False

        self._send_raw(f"PRIVMSG {self.config['channel']} :{message}")
        print(f"[MP BOT SEND] {message}", flush=True)
        return True

    def _wait_for_connected(self, timeout_seconds):
        deadline = time.monotonic() + timeout_seconds
        while not self.stop_event.is_set() and time.monotonic() < deadline:
            if self.connected:
                return True
            self.stop_event.wait(0.2)
        return self.connected

    def _sleep_bot(self, generation, seconds):
        deadline = time.monotonic() + seconds
        while not self.stop_event.is_set() and time.monotonic() < deadline:
            if not self._is_current_bot_generation(generation):
                return False
            self.stop_event.wait(min(0.1, deadline - time.monotonic()))
        return self._is_current_bot_generation(generation)

    def _is_current_bot_generation(self, generation):
        with self.bot_lock:
            return generation == self.bot_generation

    @staticmethod
    def _normalize_mp_mode(mode):
        mode = str(mode or "").strip().upper()
        return mode or "NM"

    @staticmethod
    def _build_mp_mods(mode):
        mode = BanchoIrcChatClient._normalize_mp_mode(mode)
        if mode == "NM":
            return "NF"
        return f"NF {mode}"

    @staticmethod
    def _is_all_ready_message(message):
        return (
            "all players are ready" in message
            or "everyone is ready" in message
            or "all players ready" in message
        )

    def _mark_connected(self, source):
        if self.connected:
            return

        channel = self.config["channel"]
        self.connected = True
        print(f"[IRC] Room chat connected: {channel} ({source})", flush=True)
        self.bridge.set_chat_status(f"IRC CONNECTED {channel}")
        self.bridge.add_chat_message("Bridge", f"Connected to room chat {channel}", "system")

    def _handle_irc_error(self, code, line):
        labels = {
            "403": "no such channel",
            "464": "password rejected",
            "465": "you are banned from server",
            "473": "invite only channel",
            "475": "bad channel key",
        }
        label = labels.get(code, "IRC error")
        status = f"IRC ERROR: {label}"
        print(f"[IRC] {status}: {line}", flush=True)
        self.bridge.set_chat_status(status[:80])
        self.bridge.add_chat_message("Bridge", status, "system")

    def _log_incoming(self, line):
        if self.config.get("debugRawIrc"):
            print(f"[IRC RAW] {line}", flush=True)
            return

        channel = self.config["channel"]
        parsed = self._parse_privmsg(line)
        if parsed is not None:
            sender, target, message = parsed
            if target.lower() == channel.lower():
                print(f"[IRC CHAT] {sender.replace('_', ' ')}: {message}", flush=True)
            return

        parts = line.split(" ")
        if len(parts) < 2:
            if line.startswith("ERROR"):
                print(f"[IRC] {line}", flush=True)
            return

        command = parts[1]
        if command == "001":
            print(f"[IRC] Login accepted.", flush=True)
            return

        if command == "366" and len(parts) >= 4 and parts[3].lower() == channel.lower():
            print(f"[IRC] Joined channel list completed for {channel}", flush=True)
            return

        if command == "JOIN" and self._nick_from_prefix(parts[0]) == self.config["username"]:
            print(f"[IRC] Joined room chat {channel}", flush=True)
            return

        if command in ("403", "464", "465", "473", "475") or command.startswith("4") or command.startswith("5"):
            print(f"[IRC] {line}", flush=True)

    def _send_raw(self, line):
        if self.sock is None:
            return
        with self.send_lock:
            if self.config.get("debugRawIrc") or not line.startswith("PONG "):
                print(f"[IRC SEND] {self._redact(line)}", flush=True)
            self.sock.sendall((line + "\r\n").encode("utf-8"))

    @staticmethod
    def _redact(line):
        return "PASS ********" if line.startswith("PASS ") else line

    @staticmethod
    def _nick_from_prefix(prefix):
        return prefix.lstrip(":").split("!", 1)[0]

    @staticmethod
    def _parse_privmsg(line):
        if " PRIVMSG " not in line or " :" not in line:
            return None

        prefix, rest = line.split(" PRIVMSG ", 1)
        target, message = rest.split(" :", 1)
        return BanchoIrcChatClient._nick_from_prefix(prefix), target.strip(), message


BRIDGE = BossRaidBridge()


class BridgeRequestHandler(BaseHTTPRequestHandler):
    server_version = "BossRaidBridge/0.1"
    protocol_version = "HTTP/1.1"

    def handle(self):
        try:
            super().handle()
        except (BrokenPipeError, ConnectionAbortedError, ConnectionResetError):
            pass

    def log_message(self, fmt, *args):
        print(f"[{time.strftime('%H:%M:%S')}] {self.address_string()} {fmt % args}", flush=True)

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path == "/ws":
            self._handle_websocket()
        elif parsed.path == "/state":
            self._send_json(BRIDGE.snapshot())
        else:
            self._send_html(INDEX_HTML)

    def do_POST(self):
        parsed = urlparse(self.path)
        if parsed.path != "/command":
            self.send_error(404)
            return

        try:
            length = int(self.headers.get("Content-Length", "0"))
            body = self.rfile.read(length).decode("utf-8")
            command = json.loads(body)
            state = BRIDGE.apply_command(command)
            self._send_json({"ok": True, "state": state})
        except Exception as exc:
            self._send_json({"ok": False, "error": str(exc)}, status=400)

    def _send_json(self, payload, status=200):
        data = json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(data)
        self.close_connection = True

    def _send_html(self, html):
        data = html.encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(data)
        self.close_connection = True

    def _handle_websocket(self):
        key = self.headers.get("Sec-WebSocket-Key")
        if not key:
            self.send_error(400, "Missing Sec-WebSocket-Key")
            return

        accept = base64.b64encode(hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode("ascii")).digest()).decode("ascii")
        self.send_response(101, "Switching Protocols")
        self.send_header("Upgrade", "websocket")
        self.send_header("Connection", "Upgrade")
        self.send_header("Sec-WebSocket-Accept", accept)
        self.end_headers()

        client = self.request
        client.settimeout(None)
        BRIDGE.add_client(client)
        try:
            send_ws_text(client, BRIDGE.snapshot_json().encode("utf-8"))
            while True:
                opcode, payload = read_ws_frame(client)
                if opcode is None or opcode == 8:
                    break
                if opcode == 9:
                    send_ws_frame(client, 10, payload)
                elif opcode == 1 and payload:
                    try:
                        command = json.loads(payload.decode("utf-8"))
                        BRIDGE.apply_command(command)
                    except Exception:
                        pass
        except (OSError, ValueError):
            pass
        finally:
            BRIDGE.remove_client(client)
            try:
                client.close()
            except OSError:
                pass


def recv_exact(sock_obj, length):
    chunks = []
    remaining = length
    while remaining > 0:
        chunk = sock_obj.recv(remaining)
        if not chunk:
            raise ValueError("socket closed")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


def read_ws_frame(sock_obj):
    try:
        header = recv_exact(sock_obj, 2)
    except ValueError:
        return None, b""

    first, second = header[0], header[1]
    opcode = first & 0x0F
    masked = (second & 0x80) != 0
    length = second & 0x7F

    if length == 126:
        length = struct.unpack("!H", recv_exact(sock_obj, 2))[0]
    elif length == 127:
        length = struct.unpack("!Q", recv_exact(sock_obj, 8))[0]

    mask = recv_exact(sock_obj, 4) if masked else b""
    payload = recv_exact(sock_obj, length) if length else b""
    if masked:
        payload = bytes(byte ^ mask[index % 4] for index, byte in enumerate(payload))
    return opcode, payload


def send_ws_text(sock_obj, payload):
    send_ws_frame(sock_obj, 1, payload)


def send_ws_frame(sock_obj, opcode, payload):
    if isinstance(payload, str):
        payload = payload.encode("utf-8")
    length = len(payload)
    header = bytearray([0x80 | opcode])
    if length < 126:
        header.append(length)
    elif length < (1 << 16):
        header.append(126)
        header.extend(struct.pack("!H", length))
    else:
        header.append(127)
        header.extend(struct.pack("!Q", length))
    sock_obj.sendall(bytes(header) + payload)


def send_ws_client_text(sock_obj, payload):
    send_ws_client_frame(sock_obj, 1, payload)


def send_ws_client_frame(sock_obj, opcode, payload):
    if isinstance(payload, str):
        payload = payload.encode("utf-8")
    length = len(payload)
    mask = os.urandom(4)
    header = bytearray([0x80 | opcode])
    if length < 126:
        header.append(0x80 | length)
    elif length < (1 << 16):
        header.append(0x80 | 126)
        header.extend(struct.pack("!H", length))
    else:
        header.append(0x80 | 127)
        header.extend(struct.pack("!Q", length))
    masked_payload = bytes(byte ^ mask[index % 4] for index, byte in enumerate(payload))
    sock_obj.sendall(bytes(header) + mask + masked_payload)


INDEX_HTML = r"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Boss Raid Bridge</title>
  <style>
    :root {
      color-scheme: dark;
      font-family: "Segoe UI", Arial, sans-serif;
      background: #0b0d12;
      color: #eef2f7;
    }
    * { box-sizing: border-box; }
    body { margin: 0; background: #0b0d12; }
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      padding: 18px 22px;
      background: #121722;
      border-bottom: 1px solid #2b3444;
      position: sticky;
      top: 0;
      z-index: 5;
    }
    h1 { margin: 0; font-size: 22px; }
    main {
      display: grid;
      grid-template-columns: 340px 1fr 390px;
      gap: 16px;
      padding: 16px;
    }
    section {
      background: #121722;
      border: 1px solid #2b3444;
      border-radius: 8px;
      padding: 14px;
      min-width: 0;
    }
    h2 { margin: 0 0 12px; font-size: 16px; color: #aeb9c8; }
    button, select, input, textarea {
      width: 100%;
      border: 1px solid #334052;
      border-radius: 6px;
      background: #0e131d;
      color: #eef2f7;
      font: inherit;
      min-height: 38px;
      padding: 8px 10px;
    }
    button { cursor: pointer; font-weight: 700; }
    button:hover { border-color: #58c8ff; }
    .primary { background: #12364a; border-color: #287da9; }
    .danger { background: #40151a; border-color: #a52e3a; }
    .success { background: #123a24; border-color: #2f9a58; }
    .gold { background: #3d2d12; border-color: #b88224; }
    .grid { display: grid; gap: 8px; }
    .two { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .three { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    .row { display: grid; grid-template-columns: 1fr 130px; gap: 8px; align-items: center; margin-bottom: 8px; }
    .stat-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; }
    .stat {
      padding: 10px;
      border-radius: 6px;
      background: #0e131d;
      border: 1px solid #2b3444;
    }
    .stat span { display: block; color: #94a3b8; font-size: 12px; margin-bottom: 6px; }
    .stat strong { font-size: 18px; }
    .map-list { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; max-height: 690px; overflow: auto; }
    .map {
      padding: 10px;
      border-radius: 6px;
      background: #0e131d;
      border: 1px solid #2b3444;
      min-height: 78px;
    }
    .map.selected { border-color: #58c8ff; background: #102536; }
    .map.burger { border-color: #b88224; }
    .map.played { opacity: .48; }
    .map small { color: #94a3b8; display: block; margin-top: 4px; }
    .chat-list { display: grid; gap: 6px; max-height: 260px; overflow: auto; }
    .chat-line {
      padding: 8px 10px;
      border-radius: 6px;
      background: #0e131d;
      border: 1px solid #2b3444;
      line-height: 1.35;
      word-break: break-word;
    }
    .chat-line strong { color: #58c8ff; }
    .chat-line time { color: #94a3b8; font-size: 11px; margin-right: 8px; }
    .mp-command { display: grid; grid-template-columns: minmax(0, 1fr) 92px; gap: 8px; }
    #mpCommand { font-family: Consolas, monospace; }
    a { color: #58c8ff; }
    textarea { min-height: 240px; resize: vertical; font-family: Consolas, monospace; font-size: 12px; }
    label { display: block; color: #aeb9c8; font-size: 12px; font-weight: 700; margin: 8px 0 5px; }
    @media (max-width: 1180px) {
      main { grid-template-columns: 1fr; }
      .map-list { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
  <header>
    <h1>Boss Raid Bridge</h1>
    <div id="connection">connecting...</div>
  </header>
  <main>
    <section>
      <h2>Run Control</h2>
      <div class="grid two">
        <button onclick="cmd('set_screen', {screen:'standby'})">Standby</button>
        <button class="gold" onclick="cmd('assign_burgers')">Assign Burgers</button>
        <button class="gold" onclick="cmd('pick_round_maps')">Pick 8 Maps</button>
        <button class="primary" onclick="cmd('spin_mode')">Spin Mode</button>
        <button class="primary" onclick="cmd('spin_map')">Spin Map</button>
        <button class="gold" onclick="cmd('enter_difficulty_select')">Difficulty Select</button>
        <button class="gold" onclick="confirmDifficulty()">Confirm Difficulty</button>
        <button onclick="cmd('ready_map')">Map Ready</button>
        <button class="primary" onclick="cmd('send_mp_setup')">Send !mp Setup</button>
        <button class="success" onclick="cmd('start_map')">Start Map</button>
        <button class="danger" onclick="cmd('finish_map')">Finish</button>
        <button onclick="cmd('next_round')">Next Round</button>
        <button onclick="cmd('reload_settings')">Reload Setting.Json</button>
        <button class="danger" onclick="confirmReset()">Reset Event</button>
      </div>

      <label>Current Team</label>
      <select id="teamSelect" onchange="cmd('set_current_team', {teamId:this.value})"></select>

      <label>Difficulty</label>
      <select id="difficultySelect" onchange="cmd('set_difficulty', {difficulty:this.value})"></select>

      <h2 style="margin-top:18px">Scores</h2>
      <div id="scoreRows"></div>

      <h2 style="margin-top:18px">Room Chat</h2>
      <div class="grid two">
        <button onclick="cmd('add_chat_message', {sender:'Bridge', message:'Chat test message'})">Test Chat</button>
        <button onclick="cmd('clear_chat')">Clear Chat</button>
      </div>
      <label>Manual !mp Command</label>
      <div class="mp-command">
        <input id="mpCommand" placeholder="!mp aborttimer" autocomplete="off" onkeydown="handleMpCommandKey(event)">
        <button class="primary" onclick="sendMpCommand()">Send</button>
      </div>
    </section>

    <section>
      <h2>Live State</h2>
      <div class="stat-grid" id="stats"></div>
      <h2 style="margin-top:18px">Room Chat</h2>
      <div class="chat-list" id="chat"></div>
      <h2 style="margin-top:18px">Map Pool</h2>
      <div class="map-list" id="maps"></div>
    </section>

    <section>
      <h2>Map Pool JSON</h2>
      <p style="color:#94a3b8;margin-top:0">Edit then load. Keep ids unique. Up to 24 maps are used.</p>
      <textarea id="mapJson"></textarea>
      <button style="margin-top:8px" onclick="loadMaps()">Load Maps</button>
    </section>
  </main>

  <script>
    let state = null;
    let socket = null;

    function connect() {
      const proto = location.protocol === 'https:' ? 'wss:' : 'ws:';
      socket = new WebSocket(`${proto}//${location.host}/ws`);
      socket.onopen = () => document.getElementById('connection').textContent = 'Bridge websocket connected';
      socket.onclose = () => {
        document.getElementById('connection').textContent = 'Bridge websocket disconnected';
        setTimeout(connect, 1000);
      };
      socket.onmessage = event => {
        state = JSON.parse(event.data);
        render();
      };
    }

    async function cmd(type, extra = {}) {
      const response = await fetch('/command', {
        method: 'POST',
        headers: {'Content-Type': 'application/json'},
        body: JSON.stringify({type, ...extra})
      });
      const payload = await response.json();
      if (!payload.ok) alert(payload.error || 'Command failed');
      return payload;
    }

    function confirmReset() {
      if (confirm('Reset the whole event state?')) cmd('reset_event');
    }

    function confirmDifficulty() {
      const select = document.getElementById('difficultySelect');
      cmd('confirm_difficulty', {difficulty: select ? select.value : undefined});
    }

    function setScore(teamId, value) {
      cmd('set_team_score', {teamId, score: Number(value || 0)});
    }

    function renameTeam(teamId, value) {
      cmd('set_team_name', {teamId, name: value});
    }

    function loadMaps() {
      try {
        const maps = JSON.parse(document.getElementById('mapJson').value);
        cmd('load_maps', {maps});
      } catch (error) {
        alert(error.message);
      }
    }

    async function sendMpCommand() {
      const input = document.getElementById('mpCommand');
      const message = input ? input.value.trim() : '';
      if (!message) return;

      const payload = await cmd('send_mp_command', {message});
      if (payload && payload.ok && input) input.value = '';
    }

    function handleMpCommandKey(event) {
      if (event.key !== 'Enter') return;
      event.preventDefault();
      sendMpCommand();
    }

    function render() {
      if (!state) return;
      renderSelects();
      renderScores();
      renderStats();
      renderChat();
      renderMaps();
      document.getElementById('mapJson').value = JSON.stringify(state.mapPool, null, 2);
    }

    function renderSelects() {
      const teamSelect = document.getElementById('teamSelect');
      teamSelect.innerHTML = state.teams.map(team => `<option value="${esc(team.id)}"${team.id === state.currentTeamId ? ' selected' : ''}>${esc(team.name)}</option>`).join('');

      const difficultySelect = document.getElementById('difficultySelect');
      difficultySelect.innerHTML = state.difficulties.map(diff => `<option value="${esc(diff.id)}"${diff.id === state.difficulty ? ' selected' : ''}>${esc(diff.label)} / HP ${num(diff.bossHp)}</option>`).join('');
    }

    function renderScores() {
      const rows = state.teams.map(team => `
        <div class="row">
          <input value="${esc(team.name)}" onchange="renameTeam('${esc(team.id)}', this.value)">
          <input type="number" value="${team.score}" onchange="setScore('${esc(team.id)}', this.value)">
        </div>
      `);
      document.getElementById('scoreRows').innerHTML = rows.join('');
    }

    function renderStats() {
      const selected = state.mapPool.find(map => map.id === state.selectedMapId);
      const stats = [
        ['Screen', state.screen],
        ['Round', `${state.roundIndex + 1} / 8`],
        ['Prize', `${num(state.prizePool)} KRW`],
        ['Burgers', `${state.burgerCount} / miss ${state.burgerMissCount}`],
        ['Difficulty', state.difficulty],
        ['Boss HP', num(state.bossHp)],
        ['Total Score', num(state.totalScore)],
        ['Score Source', state.scoreSourceStatus || '-'],
        ['OBS', state.obsStatus || '-'],
        ['Chat', state.chatStatus || '-'],
        ['Selected Map', selected ? selected.title : '-'],
        ['Beatmap ID', selected && selected.beatmapId ? selected.beatmapId : '-']
      ];
      document.getElementById('stats').innerHTML = stats.map(([label, value]) => `<div class="stat"><span>${esc(label)}</span><strong>${esc(value)}</strong></div>`).join('');
    }

    function renderChat() {
      const messages = state.chatMessages || [];
      document.getElementById('chat').innerHTML = messages.length
        ? messages.map(line => `
          <div class="chat-line">
            <time>${esc(line.time || '')}</time><strong>${esc(line.sender || 'system')}</strong>: ${esc(line.message || '')}
          </div>
        `).join('')
        : '<div class="chat-line"><strong>Bridge</strong>: No room chat yet.</div>';
    }

    function renderMaps() {
      const selectedIds = new Set(state.selectedRoundMapIds || []);
      document.getElementById('maps').innerHTML = state.mapPool.map(map => {
        const classes = ['map'];
        if (map.id === state.selectedMapId) classes.push('selected');
        if (map.isBurger) classes.push('burger');
        if (map.played) classes.push('played');
        const round = selectedIds.has(map.id) ? 'round pool' : 'outside 8';
        return `
          <div class="${classes.join(' ')}">
            <strong>${esc(map.title)}</strong>
            <small>${esc(map.mode)} / ${esc(map.difficultyName)} / ${round}</small>
            <small>beatmapId: ${esc(map.beatmapId || '-')}</small>
            ${map.link ? `<small><a href="${esc(map.link)}" target="_blank" rel="noreferrer">map link</a></small>` : ''}
            <small>${map.isBurger ? 'BURGER' : ''} ${map.played ? 'PLAYED' : ''}</small>
          </div>
        `;
      }).join('');
    }

    function esc(value) {
      return String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[ch]));
    }

    function num(value) {
      return Number(value || 0).toLocaleString();
    }

    connect();
    fetch('/state').then(r => r.json()).then(payload => { state = payload; render(); });
  </script>
</body>
</html>
"""


def run_server(host=HOST, port=PORT):
    server = ThreadingHTTPServer((host, port), BridgeRequestHandler)
    irc_client = None
    score_client = None
    tosu_client = None
    obs_client = None
    bridge_config = load_bridge_config()
    api_config = load_api_config(bridge_config)
    if api_config is not None:
        irc_client = BanchoIrcChatClient(BRIDGE, api_config)
        BRIDGE.set_irc_client(irc_client)
        irc_client.start()

    tosu_config = load_tosu_config(bridge_config)
    if tosu_config is not None:
        tosu_client = TosuTourneyScoreClient(BRIDGE, tosu_config)
        tosu_client.start()
    else:
        score_config = load_tourney_ipc_config(bridge_config)
        if score_config is not None:
            score_client = TourneyIpcScoreClient(BRIDGE, score_config)
            score_client.start()

    obs_config = load_obs_websocket_config(bridge_config)
    if obs_config is not None:
        obs_client = ObsWebSocketVisibilityClient(BRIDGE, obs_config)
        obs_client.start()

    print("========================================", flush=True)
    print(" Boss Raid Bridge", flush=True)
    print("========================================", flush=True)
    print(f"Working root      : {PROJECT_ROOT}", flush=True)
    print(f"Setting.Json path : {SETTING_PATH}", flush=True)
    print(f"API.Json path     : {API_PATH}", flush=True)
    print(f"Boss Raid Bridge running: http://{host}:{port}")
    print(f"Unity WebSocket URL: ws://{host}:{port}/ws")
    print("Press Ctrl+C to stop.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down.")
    finally:
        if irc_client is not None:
            irc_client.stop()
        if score_client is not None:
            score_client.stop()
        if tosu_client is not None:
            tosu_client.stop()
        if obs_client is not None:
            obs_client.stop()
        BRIDGE.set_irc_client(None)
        server.server_close()


def self_test():
    bridge = BossRaidBridge(load_settings=False)
    assert bridge.snapshot()["screen"] == "standby"
    bridge.apply_command({"type": "assign_burgers"})
    assert sum(1 for raid_map in bridge.snapshot()["mapPool"] if raid_map["isBurger"]) == 8
    bridge.apply_command({"type": "pick_round_maps"})
    assert len(bridge.snapshot()["selectedRoundMapIds"]) == 8
    bridge.apply_command({"type": "enter_difficulty_select"})
    assert bridge.snapshot()["screen"] == "difficultySelect"
    bridge.apply_command({"type": "confirm_difficulty", "difficulty": "hard"})
    assert bridge.snapshot()["screen"] == "rouletteMode"
    assert bridge.snapshot()["difficulty"] == "hard"
    bridge.apply_command({"type": "spin_mode"})
    assert bridge.snapshot()["selectedMode"]
    bridge.apply_command({"type": "spin_map"})
    assert bridge.snapshot()["selectedMapId"]
    for team in bridge.snapshot()["teams"]:
        bridge.apply_command({"type": "set_team_score", "teamId": team["id"], "score": 500000})
    bridge.apply_command({"type": "set_difficulty", "difficulty": "easy"})
    bridge.apply_command({"type": "finish_map"})
    assert bridge.snapshot()["lastResult"] == "clear"
    bridge.apply_command({"type": "set_screen", "screen": "inGame"})
    assert bridge.apply_external_scores([123456, 234567, 0, 0], team_ids=["team-1", "team-2", "team-3", "team-4"], allowed_screens=["inGame"])
    assert bridge.snapshot()["teams"][0]["score"] == 123456
    assert bridge.snapshot()["teams"][1]["score"] == 234567
    assert bridge.snapshot()["totalScore"] == 358023
    assert bridge.apply_tosu_tourney_state(
        {
            "totalScore": {"left": 300000, "right": 120000},
            "clients": [
                {"ipcId": 0, "team": "left", "user": {"name": "P1"}, "play": {"score": 100000, "accuracy": 98.5, "combo": {"current": 120, "max": 200}, "hits": {"0": 1}}},
                {"ipcId": 1, "team": "left", "user": {"name": "P2"}, "play": {"score": 200000, "accuracy": 99.1, "combo": {"current": 180, "max": 240}, "hits": {"0": 0}}},
                {"ipcId": 3, "team": "right", "user": {"name": "R1"}, "play": {"score": 120000, "accuracy": 97.0, "combo": {"current": 90, "max": 150}, "hits": {"0": 2}}},
            ],
        },
        team_ids=["team-1", "team-2"],
        allowed_screens=["inGame"],
    )
    assert bridge.snapshot()["teams"][0]["score"] == 300000
    assert bridge.snapshot()["teams"][0]["players"][0]["score"] == 100000
    assert bridge.snapshot()["teams"][0]["players"][1]["name"] == "P2"
    assert bridge.snapshot()["teams"][1]["score"] == 120000
    auto_bridge = BossRaidBridge(load_settings=False)
    auto_bridge.apply_command({"type": "set_difficulty", "difficulty": "easy"})
    auto_bridge.apply_command({"type": "set_screen", "screen": "inGame"})
    auto_payload = {
        "state": {"number": 7, "name": "resultScreen"},
        "tourney": {
            "ipcState": 7,
            "totalScore": {"left": 700000, "right": 400000},
            "clients": [
                {"ipcId": 0, "team": "left", "user": {"name": "P1"}, "play": {"score": 700000, "accuracy": 98.5, "combo": {"current": 120, "max": 200}, "hits": {"0": 1}}},
                {"ipcId": 1, "team": "right", "user": {"name": "P2"}, "play": {"score": 400000, "accuracy": 99.1, "combo": {"current": 180, "max": 240}, "hits": {"0": 0}}},
            ],
        },
    }
    assert auto_bridge.apply_tosu_tourney_state(auto_payload["tourney"], team_ids=["team-1", "team-2"], allowed_screens=["inGame"])
    assert not auto_bridge.maybe_auto_finish_from_tosu(auto_payload, delay_seconds=5.0)
    auto_bridge.tosu_auto_finish_result_since -= 5.1
    assert auto_bridge.maybe_auto_finish_from_tosu(auto_payload, delay_seconds=5.0)
    assert auto_bridge.snapshot()["screen"] == "result"
    assert auto_bridge.snapshot()["lastResult"] == "clear"
    bridge.apply_command({"type": "ready_map"})
    assert bridge.auto_start_map_if_ready()
    assert bridge.snapshot()["screen"] == "inGame"
    assert not bridge.auto_start_map_if_ready()
    bridge.apply_command({"type": "set_screen", "screen": "standby"})
    assert not bridge.apply_external_scores([1, 2], team_ids=["team-1", "team-2"], allowed_screens=["inGame"])
    assert bridge.snapshot()["teams"][0]["score"] == 300000
    bridge.apply_command({"type": "next_round"})
    assert bridge.snapshot()["screen"] == "difficultySelect"
    assert bridge.snapshot()["selectedMode"] == ""
    assert bridge.snapshot()["selectedMapId"] == ""
    played_id = bridge.snapshot()["selectedMapId"]
    played_mode = bridge.snapshot()["selectedMode"]
    with bridge.lock:
        bridge.state["selectedMode"] = played_mode
        for raid_map in bridge.state["mapPool"]:
            if raid_map.get("mode") == played_mode:
                raid_map["played"] = raid_map.get("id") == played_id
        bridge.state["selectedRoundMapIds"] = [
            raid_map["id"] for raid_map in bridge.state["mapPool"] if raid_map.get("mode") == played_mode
        ]
    bridge.apply_command({"type": "spin_map"})
    assert bridge.snapshot()["selectedMapId"] != played_id
    bridge.apply_command({"type": "add_chat_message", "sender": "Tester", "message": "hello"})
    assert bridge.snapshot()["chatMessages"][-1]["message"] == "hello"
    bridge.apply_command({"type": "clear_chat"})
    assert bridge.snapshot()["chatMessages"] == []
    assert bridge._extract_beatmap_id({"beatmapId": "123456"}) == 123456
    assert bridge._extract_beatmap_id({"link": "https://osu.ppy.sh/beatmapsets/1576254#osu/3427307"}) == 3427307
    assert bridge._extract_beatmap_id({"link": "https://osu.ppy.sh/b/7654321"}) == 7654321
    assert TourneyIpcScoreClient._parse_score_lines("0\n1,234,567\nP2: 765432\n") == [0, 1234567, 765432]
    assert ObsWebSocketVisibilityClient._build_authentication(
        "supersecretpassword",
        "lM1GncleQOaCu9lT1yeUZhFYnqhsLLP1G5lAGo3ixaI=",
        "+IxH4CnCiqpX1rM9scsNynZzbOe4KhDeYcTNS3PDaeY=",
    ) == "1Ct943GAT+6YQUUX47Ia/ncufilbe6+oD6lY+5kaCu4="
    assert BanchoIrcChatClient._build_mp_mods("NM") == "NF"
    assert BanchoIrcChatClient._build_mp_mods("HD") == "NF HD"
    assert BanchoIrcChatClient._build_mp_mods("HR") == "NF HR"
    assert BanchoIrcChatClient._build_mp_mods("DT") == "NF DT"
    assert BanchoIrcChatClient._is_all_ready_message("all players are ready")
    assert normalize_mp_command(" !mp start 5\n") == "!mp start 5"
    assert normalize_mp_command("!mpabort") == ""
    assert normalize_mp_command("hello") == ""
    irc = BanchoIrcChatClient(bridge, {"server": "irc.ppy.sh", "port": 6667, "username": "Tester", "password": "secret", "channel": "#mp_1"})
    irc._mark_connected("TEST")
    assert bridge.snapshot()["chatStatus"] == "IRC CONNECTED #mp_1"
    assert bridge.snapshot()["chatMessages"][-1]["message"] == "Connected to room chat #mp_1"
    assert irc.send_manual_mp_command(" !mp aborttimer\n")
    assert bridge.snapshot()["chatMessages"][-1]["message"] == "Sent manual MP command: !mp aborttimer"
    print("Bridge self-test passed.")


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        self_test()
    else:
        host = HOST
        port = PORT
        if "--host" in sys.argv:
            host = sys.argv[sys.argv.index("--host") + 1]
        if "--port" in sys.argv:
            port = int(sys.argv[sys.argv.index("--port") + 1])
        run_server(host, port)
