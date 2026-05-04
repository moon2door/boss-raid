#!/usr/bin/env python3
import base64
import hashlib
import json
import random
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


class BossRaidBridge:
    def __init__(self, load_settings=True):
        self.lock = threading.RLock()
        self.clients = set()
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
            "teams": [
                {"id": "team-1", "name": "Team A", "score": 0, "color": {"r": 0.95, "g": 0.25, "b": 0.20, "a": 1.0}},
                {"id": "team-2", "name": "Team B", "score": 0, "color": {"r": 0.20, "g": 0.55, "b": 0.95, "a": 1.0}},
                {"id": "team-3", "name": "Team C", "score": 0, "color": {"r": 0.25, "g": 0.85, "b": 0.45, "a": 1.0}},
                {"id": "team-4", "name": "Team D", "score": 0, "color": {"r": 0.95, "g": 0.75, "b": 0.20, "a": 1.0}},
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
            player_label = ", ".join(players) if players else "-"
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
            }
            players = team.get("players")
            if isinstance(players, list):
                sanitized_team["players"] = [str(player)[:32] for player in players[:8]]
            sanitized.append(sanitized_team)
        return sanitized or self._default_state()["teams"]

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
                    "isBurger": bool(raid_map.get("isBurger", False)),
                    "played": bool(raid_map.get("played", False)),
                }
            )
        return sanitized[:24] or self._default_state()["mapPool"]

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
            total += max(0, team["score"])
        state["totalScore"] = total

        if not state.get("selectedRoundMapIds"):
            state["selectedRoundMapIds"] = [m["id"] for m in state.get("mapPool", [])]

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

    def apply_command(self, command):
        if not isinstance(command, dict):
            raise ValueError("command must be a json object")

        command_type = command.get("type", "")
        with self.lock:
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
            else:
                raise ValueError(f"unknown command type: {command_type}")

            self._normalize_locked()
            self.state["animationNonce"] = int(self.state.get("animationNonce", 0)) + 1

        self.broadcast()
        return self.snapshot()

    def _normalize_locked(self):
        self._normalize_state(self.state)

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
        if not candidates:
            candidates = self.state["mapPool"]
        modes = sorted({raid_map["mode"] for raid_map in candidates})
        if modes:
            self.state["selectedMode"] = random.choice(modes)
            self.state["selectedMapId"] = ""

    def _spin_map_locked(self):
        selected_mode = self.state.get("selectedMode", "")
        candidates = self._eligible_maps_locked(require_mode=True)
        if not candidates:
            candidates = [m for m in self.state["mapPool"] if m.get("mode") == selected_mode]
        if not candidates:
            candidates = self.state["mapPool"]
        if candidates:
            raid_map = random.choice(candidates)
            self.state["selectedMode"] = raid_map["mode"]
            self.state["selectedMapId"] = raid_map["id"]

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
        self.state["screen"] = "rouletteMode"
        self.state["lastResult"] = "none"
        self.state["resultMessage"] = ""
        self.state["selectedMapId"] = ""
        for team in self.state["teams"]:
            team["score"] = 0

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
    </section>

    <section>
      <h2>Live State</h2>
      <div class="stat-grid" id="stats"></div>
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

    function render() {
      if (!state) return;
      renderSelects();
      renderScores();
      renderStats();
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
        ['Selected Map', selected ? selected.title : '-']
      ];
      document.getElementById('stats').innerHTML = stats.map(([label, value]) => `<div class="stat"><span>${esc(label)}</span><strong>${esc(value)}</strong></div>`).join('');
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
    print("========================================", flush=True)
    print(" Boss Raid Bridge", flush=True)
    print("========================================", flush=True)
    print(f"Working root      : {PROJECT_ROOT}", flush=True)
    print(f"Setting.Json path : {SETTING_PATH}", flush=True)
    print(f"Boss Raid Bridge running: http://{host}:{port}")
    print(f"Unity WebSocket URL: ws://{host}:{port}/ws")
    print("Press Ctrl+C to stop.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down.")
    finally:
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
