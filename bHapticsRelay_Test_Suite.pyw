#!/usr/bin/env python3
"""
Test Suite for bHapticsRelay v0.3.0
-----------------------------------
A GUI for testing bHapticsRelay via Websocket
Last updated: 9/2/2025

Usage:
  1) Start bHapticsRelay in WebSocket mode (ensure Port is set and app is running).
  2) Run this script. Enter ws://host:port (e.g., ws://127.0.0.1:49600), click Connect.
  3) Use tabs to send commands; log shows any replies.
"""

import base64
import hashlib
import json
import os
import queue
import random
import socket
import ssl
import struct
import threading
import time
import tkinter as tk
from urllib.parse import urlparse
from datetime import datetime
from tkinter import ttk, messagebox

version = "0.3.0"
default_ws_url = "ws://127.0.0.1:49600"

# ----------------------------------------------------------------------
# Minimal WebSocket client (text frames only; single-frame; RFC6455)
# Implemented here as a mini-client to prevent needing any dependencies.
# ----------------------------------------------------------------------
class MinimalWSClient:
    def __init__(self, on_log):
        self._sock = None  # type: socket.socket | ssl.SSLSocket | None
        self._lock = threading.Lock()
        self._stop = threading.Event()
        self._recv_thr = None
        self._on_log = on_log

    # ---- public API ----
    def connect(self, url: str):
        with self._lock:
            if self._sock is not None:
                self._on_log("WARN", "Already connected.")
                return
            try:
                self._sock = self._handshake(url)
                self._sock.settimeout(0.5)
                self._stop.clear()
                self._recv_thr = threading.Thread(target=self._recv_loop, name="WS-Receiver", daemon=True)
                self._recv_thr.start()
                self._on_log("INFO", f"Connected to {url}")
            except Exception as e:
                self._sock = None
                self._on_log("ERROR", f"Failed to connect: {e}")

    def send(self, text: str) -> bool:
        with self._lock:
            if self._sock is None:
                self._on_log("ERROR", "Not connected.")
                return False
            try:
                self._send_frame(0x1, text.encode("utf-8"))  # text frame
                self._on_log("SEND", text)
                return True
            except Exception as e:
                self._on_log("ERROR", f"Send failed: {e}")
                return False

    def disconnect(self):
        with self._lock:
            if self._sock is None:
                self._on_log("WARN", "Already disconnected.")
                return
            try:
                # send close frame
                try:
                    self._send_frame(0x8, b"")
                except Exception:
                    pass
                self._stop.set()
                try:
                    self._sock.shutdown(socket.SHUT_RDWR)
                except Exception:
                    pass
                try:
                    self._sock.close()
                except Exception:
                    pass
            finally:
                self._sock = None
                self._on_log("INFO", "Disconnected")

    # ---- internals ----
    def _handshake(self, url: str):
        u = urlparse(url)
        if u.scheme not in ("ws", "wss"):
            raise ValueError("URL must start with ws:// or wss://")
        host = u.hostname or "localhost"
        port = u.port or (443 if u.scheme == "wss" else 80)
        path = u.path or "/"
        if u.query:
            path += "?" + u.query

        raw_sock = socket.create_connection((host, port), timeout=3)
        if u.scheme == "wss":
            ctx = ssl.create_default_context()
            raw_sock = ctx.wrap_socket(raw_sock, server_hostname=host)

        # Build handshake
        key = base64.b64encode(os.urandom(16)).decode("ascii")
        headers = [
            f"GET {path} HTTP/1.1",
            f"Host: {host}:{port}",
            "Upgrade: websocket",
            "Connection: Upgrade",
            f"Sec-WebSocket-Key: {key}",
            "Sec-WebSocket-Version: 13",
            "Origin: http://localhost",
            "\r\n",
        ]
        raw_sock.sendall("\r\n".join(headers).encode("ascii"))

        # Read response headers
        resp = b""
        while b"\r\n\r\n" not in resp:
            chunk = raw_sock.recv(4096)
            if not chunk:
                break
            resp += chunk
        head, _, _ = resp.partition(b"\r\n\r\n")
        lines = head.decode("iso-8859-1").split("\r\n")
        if not lines or "101" not in lines[0]:
            raise ConnectionError(f"Handshake failed: {lines[0] if lines else 'no response'}")
        # Validate accept
        acc = None
        for ln in lines[1:]:
            if ln.lower().startswith("sec-websocket-accept:"):
                acc = ln.split(":", 1)[1].strip()
                break
        if not acc:
            raise ConnectionError("Missing Sec-WebSocket-Accept")
        expected = base64.b64encode(hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()).decode("ascii")
        if acc != expected:
            raise ConnectionError("Bad Sec-WebSocket-Accept")
        return raw_sock

    def _send_frame(self, opcode: int, payload: bytes):
        if self._sock is None:
            raise RuntimeError("Socket not connected")
        fin = 0x80
        b1 = fin | (opcode & 0x0F)
        mask_bit = 0x80
        n = len(payload)
        header = bytearray([b1])
        if n < 126:
            header.append(mask_bit | n)
        elif n < (1 << 16):
            header.append(mask_bit | 126)
            header.extend(struct.pack("!H", n))
        else:
            header.append(mask_bit | 127)
            header.extend(struct.pack("!Q", n))
        mask = os.urandom(4)
        header.extend(mask)
        masked = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
        self._sock.sendall(header + masked)

    def _recv_loop(self):
        while not self._stop.is_set():
            try:
                opcode, data = self._recv_frame()
                if opcode is None:
                    continue
                if opcode == 0x1:  # text
                    try:
                        msg = data.decode("utf-8", errors="replace")
                    except Exception:
                        msg = data.decode("utf-8", errors="replace")
                    self._on_log("RECV", msg)
                elif opcode == 0x8:  # close
                    # echo close and break
                    try:
                        self._send_frame(0x8, b"")
                    except Exception:
                        pass
                    break
                elif opcode == 0x9:  # ping
                    try:
                        self._send_frame(0xA, data)  # pong
                    except Exception:
                        pass
                elif opcode == 0xA:  # pong
                    pass
                else:
                    # ignore binary/continuation for this simple client
                    pass
            except socket.timeout:
                continue
            except Exception as e:
                if not self._stop.is_set():
                    self._on_log("ERROR", f"Receive loop error: {e}")
                break
        self._on_log("INFO", "Receiver thread exiting")

    def _recv_exact(self, n: int) -> bytes:
        buf = b""
        while len(buf) < n:
            chunk = self._sock.recv(n - len(buf))
            if not chunk:
                raise ConnectionError("Socket closed")
            buf += chunk
        return buf

    def _recv_frame(self):
        if self._sock is None:
            return (None, b"")
        # Read header
        b1b2 = self._sock.recv(2)
        if not b1b2:
            return (None, b"")
        if len(b1b2) < 2:
            b1b2 += self._recv_exact(2 - len(b1b2))
        b1, b2 = b1b2
        fin = (b1 & 0x80) != 0
        opcode = b1 & 0x0F
        masked = (b2 & 0x80) != 0
        length = b2 & 0x7F
        if length == 126:
            length = struct.unpack("!H", self._recv_exact(2))[0]
        elif length == 127:
            length = struct.unpack("!Q", self._recv_exact(8))[0]
        mask = b""
        if masked:
            mask = self._recv_exact(4)
        payload = self._recv_exact(length) if length else b""
        if masked and payload:
            payload = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
        return (opcode, payload)

# -----------------
# Tkinter-based GUI
# -----------------
class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(f"bHapticsRelay v{version} Test Suite")
        self.geometry("1280x720")  # default fits 1280x720
        self.minsize(960, 600)

        self.ws_client = MinimalWSClient(self._append_log_safe)

        # build UI
        self._build_header()
        self._build_tabs()
        self._build_log()

        self.log_queue = queue.Queue()
        self.msg_queue = queue.Queue()
        self.after(50, self._drain_log_queue)

        self.protocol("WM_DELETE_WINDOW", self._on_close)

    # ---------- lifecycle ----------
    def _on_close(self):
        try:
            self.ws_client.disconnect()
        except Exception:
            pass
        self.destroy()

    # ---------- UI builders ----------
    def _build_header(self):
        frm = ttk.Frame(self, padding=8)
        frm.pack(fill="x")

        ttk.Label(frm, text="WebSocket URL:").pack(side="left")
        self.url_var = tk.StringVar(value=default_ws_url)
        url_entry = ttk.Entry(frm, textvariable=self.url_var, width=40)
        url_entry.pack(side="left", padx=6)

        self.connect_btn = ttk.Button(frm, text="Connect", command=self._on_connect)
        self.connect_btn.pack(side="left", padx=4)

        self.disconnect_btn = ttk.Button(frm, text="Disconnect", command=self._on_disconnect)
        self.disconnect_btn.pack(side="left", padx=4)

        ttk.Separator(self, orient="horizontal").pack(fill="x", pady=4)

    def _build_tabs(self):
        pw = ttk.Panedwindow(self, orient="horizontal")
        pw.pack(fill="both", expand=True, padx=8, pady=8)

        left = ttk.Frame(pw)
        right = ttk.Frame(pw, width=260)
        pw.add(left, weight=4)
        pw.add(right, weight=1)

        nb = ttk.Notebook(left)
        nb.pack(fill="both", expand=True)

        self.tab_events = ttk.Frame(nb)
        self.tab_low = ttk.Frame(nb)
        self.tab_state = ttk.Frame(nb)
        self.tab_device = ttk.Frame(nb)
        self.tab_map = ttk.Frame(nb)
        self.tab_raw = ttk.Frame(nb)

        nb.add(self.tab_events, text="Events")
        nb.add(self.tab_low, text="Low-level")
        nb.add(self.tab_state, text="State & Control")
        nb.add(self.tab_device, text="Device & Player")
        nb.add(self.tab_map, text="Mappings & Info")
        nb.add(self.tab_raw, text="Raw Command")

        self._build_tab_events(self.tab_events)
        self._build_tab_low(self.tab_low)
        self._build_tab_state(self.tab_state)
        self._build_tab_device(self.tab_device)
        self._build_tab_map(self.tab_map)
        self._build_tab_raw(self.tab_raw)

        self._build_effects_panel(right)

    def _build_effects_panel(self, root):
        wrap = ttk.Frame(root, padding=6)
        wrap.pack(fill="both", expand=True)

        hdr = ttk.Frame(wrap)
        hdr.pack(fill="x", pady=(0, 6))
        ttk.Label(hdr, text="Available Effects", font=("Segoe UI", 10, "bold")).pack(side="left")
        ttk.Button(hdr, text="Refresh", command=self._fetch_effects).pack(side="right")

        list_frame = ttk.Frame(wrap)
        list_frame.pack(fill="x")

        self.MAX_EFFECT_ROWS = 12
        self.effects_list = tk.Listbox(list_frame, height=self.MAX_EFFECT_ROWS, exportselection=False)
        vbar = ttk.Scrollbar(list_frame, orient="vertical", command=self.effects_list.yview)
        self.effects_list.configure(yscrollcommand=vbar.set)

        self.effects_list.grid(row=0, column=0, sticky="ew")
        vbar.grid(row=0, column=1, sticky="ns")
        list_frame.grid_columnconfigure(0, weight=1)

        self.effects_list.bind("<Double-1>", lambda e: self._use_selected_effect())

        ttk.Button(wrap, text="Use Selected", command=self._use_selected_effect).pack(fill="x", pady=(6, 0))

    def _build_log(self):
        frm = ttk.Frame(self, padding=(8, 0, 8, 8))
        frm.pack(fill="both", expand=False)

        ttk.Label(frm, text="Log (sent / received):").pack(anchor="w")
        self.log_text = tk.Text(frm, height=12, wrap="word")
        self.log_text.pack(fill="both", expand=True)
        self.log_text.configure(state="disabled")

        btns = ttk.Frame(frm)
        btns.pack(fill="x", pady=4)
        ttk.Button(btns, text="Clear Log", command=self._clear_log).pack(side="left")

    # ---------- Tabs content ----------
    def _build_tab_events(self, root):
        pad = dict(padx=6, pady=4)

        # play
        sec1 = ttk.LabelFrame(root, text="play(eventId)")
        sec1.pack(fill="x", **pad)
        self.play_event = tk.StringVar(value="heartbeat")
        self._grid_entries(sec1, [("eventId", self.play_event, 24)])
        ttk.Button(sec1, text="Send", command=self._send_play).grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

        # playParam (reqId 0 = server auto)
        sec2 = ttk.LabelFrame(root, text="playParam(eventId, reqId, intensity, duration, angleX, offsetY)")
        sec2.pack(fill="x", **pad)
        self.pp_event = tk.StringVar(value="jump")
        self.pp_req = tk.IntVar(value=0)
        self.pp_int = tk.DoubleVar(value=1.0)
        self.pp_dur = tk.DoubleVar(value=1.0)
        self.pp_ang = tk.DoubleVar(value=0.0)
        self.pp_off = tk.DoubleVar(value=0.0)
        self._grid_entries(sec2, [
            ("eventId", self.pp_event, 24),
            ("reqId (0=auto)", self.pp_req, 10),
            ("intensity", self.pp_int, 10),
            ("duration", self.pp_dur, 10),
            ("angleX", self.pp_ang, 10),
            ("offsetY", self.pp_off, 10),
        ])
        ttk.Button(sec2, text="Send", command=self._send_play_param).grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

        # playWithStartTime (reqId 0 = server auto)
        sec3 = ttk.LabelFrame(root, text="playWithStartTime(eventId, reqId, startMillis, intensity, duration, angleX, offsetY)")
        sec3.pack(fill="x", **pad)
        self.pw_event = tk.StringVar(value="jump")
        self.pw_req = tk.IntVar(value=0)
        self.pw_start = tk.IntVar(value=250)
        self.pw_int = tk.DoubleVar(value=1.0)
        self.pw_dur = tk.DoubleVar(value=1.0)
        self.pw_ang = tk.DoubleVar(value=0.0)
        self.pw_off = tk.DoubleVar(value=0.0)
        self._grid_entries(sec3, [
            ("eventId", self.pw_event, 24),
            ("reqId (0=auto)", self.pw_req, 10),
            ("startMillis", self.pw_start, 10),
            ("intensity", self.pw_int, 10),
            ("duration", self.pw_dur, 10),
            ("angleX", self.pw_ang, 10),
            ("offsetY", self.pw_off, 10),
        ])
        ttk.Button(sec3, text="Send", command=self._send_play_with_start).grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

        # playLoop (NO reqId per v0.3.0 server; server auto-generates)
        sec4 = ttk.LabelFrame(root, text="playLoop(eventId, intensity, duration, angleX, offsetY, interval, maxCount)")
        sec4.pack(fill="x", **pad)
        self.pl_event = tk.StringVar(value="slash_lh")
        self.pl_int = tk.DoubleVar(value=1.0)
        self.pl_dur = tk.DoubleVar(value=0.5)
        self.pl_ang = tk.DoubleVar(value=0.0)
        self.pl_off = tk.DoubleVar(value=0.0)
        self.pl_interval = tk.IntVar(value=200)
        self.pl_max = tk.IntVar(value=0)
        self._grid_entries(sec4, [
            ("eventId", self.pl_event, 24),
            ("intensity", self.pl_int, 10),
            ("duration", self.pl_dur, 10),
            ("angleX", self.pl_ang, 10),
            ("offsetY", self.pl_off, 10),
            ("interval", self.pl_interval, 10),
            ("maxCount", self.pl_max, 10),
        ])
        ttk.Button(sec4, text="Send", command=self._send_play_loop).grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

        # pause/resume/getEventTime
        sec5 = ttk.LabelFrame(root, text="pause / resume / getEventTime")
        sec5.pack(fill="x", **pad)
        self.pe_event = tk.StringVar(value="slash_lh")
        self._grid_entries(sec5, [("eventId", self.pe_event, 24)])
        btnrow = ttk.Frame(sec5)
        btnrow.grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))
        ttk.Button(btnrow, text="pause", command=lambda: self._send_csv(f"pause,{self.pe_event.get()}"))\
            .pack(side="left", padx=2)
        ttk.Button(btnrow, text="resume", command=lambda: self._send_csv(f"resume,{self.pe_event.get()}"))\
            .pack(side="left", padx=2)
        ttk.Button(btnrow, text="getEventTime", command=lambda: self._send_csv(f"getEventTime,{self.pe_event.get()}"))\
            .pack(side="left", padx=2)

    def _build_tab_low(self, root):
        pad = dict(padx=6, pady=4)

        # playDot (server auto-generates reqId)
        sec1 = ttk.LabelFrame(root, text="playDot(position, durationMillis, motors)")
        sec1.pack(fill="x", **pad)
        self.dot_pos = tk.IntVar(value=1)
        self.dot_dur = tk.IntVar(value=40)
        self.dot_motors = tk.StringVar(value="0;1;5;7")
        self._grid_entries(sec1, [
            ("position", self.dot_pos, 10),
            ("durationMillis", self.dot_dur, 10),
            ("motors (; or | separated)", self.dot_motors, 28),
        ])
        ttk.Button(sec1, text="Send", command=self._send_play_dot).grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

        # playWaveform (REQUIRES reqId per v0.3.0 server)
        sec2 = ttk.LabelFrame(root, text="playWaveform(reqId, position, motorValues, playTimeValues, shapeValues)")
        sec2.pack(fill="x", **pad)
        self.wf_req = tk.IntVar(value=1001)
        self.wf_pos = tk.IntVar(value=2)
        self.wf_motor = tk.StringVar(value="100;80;60")
        self.wf_play = tk.StringVar(value="10;10;10")
        self.wf_shape = tk.StringVar(value="0;0;0")
        self._grid_entries(sec2, [
            ("reqId", self.wf_req, 10),
            ("position", self.wf_pos, 10),
            ("motorValues", self.wf_motor, 28),
            ("playTimeValues", self.wf_play, 28),
            ("shapeValues", self.wf_shape, 28),
        ])
        ttk.Button(sec2, text="Send", command=self._send_play_waveform).grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

        # playPath (server auto-generates reqId)
        sec3 = ttk.LabelFrame(root, text="playPath(position, xValues, yValues, intensityValues)")
        sec3.pack(fill="x", **pad)
        self.ppath_pos = tk.IntVar(value=3)
        self.ppath_x = tk.StringVar(value="0.1;0.5;0.8")
        self.ppath_y = tk.StringVar(value="0.0;0.2;0.4")
        self.ppath_int = tk.StringVar(value="30;60;90")
        self._grid_entries(sec3, [
            ("position", self.ppath_pos, 10),
            ("xValues", self.ppath_x, 28),
            ("yValues", self.ppath_y, 28),
            ("intensityValues", self.ppath_int, 28),
        ])
        ttk.Button(sec3, text="Send", command=self._send_play_path).grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

    def _build_tab_state(self, root):
        pad = dict(padx=6, pady=4)

        sec = ttk.LabelFrame(root, text="Playback State & Control")
        sec.pack(fill="x", **pad)

        # stop
        self.stop_req = tk.IntVar(value=1234)
        self._grid_entries(sec, [("requestId", self.stop_req, 12)])
        ttk.Button(sec, text="stop", command=lambda: self._send_csv(f"stop,{self.stop_req.get()}"))\
            .grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))

        # stopByEventId / stopAll
        ttk.Separator(root, orient="horizontal").pack(fill="x", padx=8, pady=6)
        sec2 = ttk.LabelFrame(root, text="Stop & Queries")
        sec2.pack(fill="x", **pad)

        self.stop_event = tk.StringVar(value="slash_lh")
        self._grid_entries(sec2, [("eventId", self.stop_event, 24)])
        row = ttk.Frame(sec2)
        row.grid(row=1, column=0, columnspan=99, sticky="e", pady=(4,0))
        ttk.Button(row, text="stopByEventId", command=lambda: self._send_csv(f"stopByEventId,{self.stop_event.get()}"))\
            .pack(side="left", padx=2)
        ttk.Button(row, text="stopAll", command=lambda: self._send_csv("stopAll"))\
            .pack(side="left", padx=2)

        # isPlaying / isPlayingByRequestId / isPlayingByEventId
        ttk.Separator(root, orient="horizontal").pack(fill="x", padx=8, pady=6)
        sec3 = ttk.LabelFrame(root, text="Playing Queries")
        sec3.pack(fill="x", **pad)

        # isPlaying button
        ttk.Button(sec3, text="isPlaying", command=lambda: self._send_csv("isPlaying")).grid(row=0, column=0, sticky="w", padx=4)

        # by requestId
        self.q_req = tk.IntVar(value=1234)
        ttk.Label(sec3, text="requestId").grid(row=0, column=1, sticky="e")
        ttk.Entry(sec3, textvariable=self.q_req, width=12).grid(row=0, column=2, sticky="w")
        ttk.Button(sec3, text="isPlayingByRequestId", command=lambda: self._send_csv(f"isPlayingByRequestId,{self.q_req.get()}"))\
            .grid(row=0, column=3, padx=6)

        # by eventId
        self.q_evt = tk.StringVar(value="heartbeat")
        ttk.Label(sec3, text="eventId").grid(row=0, column=4, sticky="e")
        ttk.Entry(sec3, textvariable=self.q_evt, width=24).grid(row=0, column=5, sticky="w")
        ttk.Button(sec3, text="isPlayingByEventId", command=lambda: self._send_csv(f"isPlayingByEventId,{self.q_evt.get()}"))\
            .grid(row=0, column=6, padx=6)

    def _build_tab_device(self, root):
        pad = dict(padx=6, pady=4)

        sec = ttk.LabelFrame(root, text="Device & Player")
        sec.pack(fill="x", **pad)

        # isbHapticsConnected / ping / pingAll / swapPosition
        self.dev_pos = tk.IntVar(value=1)
        ttk.Label(sec, text="position").grid(row=0, column=0, sticky="e")
        ttk.Entry(sec, textvariable=self.dev_pos, width=12).grid(row=0, column=1, sticky="w")
        ttk.Button(sec, text="isbHapticsConnected", command=lambda: self._send_csv(f"isbHapticsConnected,{self.dev_pos.get()}"))\
            .grid(row=0, column=2, padx=4)

        self.dev_addr = tk.StringVar(value="00:11:22:33:44:55")
        ttk.Label(sec, text="address").grid(row=0, column=3, sticky="e")
        ttk.Entry(sec, textvariable=self.dev_addr, width=24).grid(row=0, column=4, sticky="w")
        ttk.Button(sec, text="ping", command=lambda: self._send_csv(f"ping,{self.dev_addr.get()}"))\
            .grid(row=0, column=5, padx=4)
        ttk.Button(sec, text="pingAll", command=lambda: self._send_csv("pingAll"))\
            .grid(row=0, column=6, padx=4)
        ttk.Button(sec, text="swapPosition", command=lambda: self._send_csv(f"swapPosition,{self.dev_addr.get()}"))\
            .grid(row=0, column=7, padx=4)

        # Player info
        ttk.Separator(root, orient="horizontal").pack(fill="x", padx=8, pady=6)
        sec2 = ttk.LabelFrame(root, text="Player")
        sec2.pack(fill="x", **pad)

        ttk.Button(sec2, text="isPlayerInstalled", command=lambda: self._send_csv("isPlayerInstalled")).grid(row=0, column=0, padx=4)
        ttk.Button(sec2, text="isPlayerRunning", command=lambda: self._send_csv("isPlayerRunning")).grid(row=0, column=1, padx=4)

        self.launch_flag = tk.BooleanVar(value=True)
        ttk.Checkbutton(sec2, text="launch=True", variable=self.launch_flag).grid(row=0, column=2, padx=4)
        ttk.Button(sec2, text="launchPlayer", command=lambda: self._send_csv(f"launchPlayer,{str(self.launch_flag.get()).lower()}"))\
            .grid(row=0, column=3, padx=4)

        ttk.Button(sec2, text="getDeviceInfoJson", command=lambda: self._send_json_cmd("getDeviceInfoJson")).grid(row=0, column=4, padx=8)

    def _build_tab_map(self, root):
        pad = dict(padx=6, pady=4)
        sec = ttk.LabelFrame(root, text="Mappings / Info")
        sec.pack(fill="x", **pad)
        ttk.Button(sec, text="getHapticMappingsJson", command=lambda: self._send_json_cmd("getHapticMappingsJson")).grid(row=0, column=0, padx=6)
        ttk.Button(sec, text="bHapticsGetHapticMappings (alias)", command=lambda: self._send_json_cmd("bHapticsGetHapticMappings")).grid(row=0, column=1, padx=6)

    def _build_tab_raw(self, root):
        pad = dict(padx=6, pady=4)
        sec = ttk.LabelFrame(root, text="Raw CSV Command")
        sec.pack(fill="x", **pad)
        self.raw_cmd = tk.StringVar(value="play,heartbeat")
        ttk.Entry(sec, textvariable=self.raw_cmd, width=80).grid(row=0, column=0, sticky="we", padx=4)
        ttk.Button(sec, text="Send", command=lambda: self._send_csv(self.raw_cmd.get())).grid(row=0, column=1, padx=4)

    # ---------- Helpers ----------
    def _grid_entries(self, parent, specs):
        # specs: list of (label, var, width)
        for col, (label, var, width) in enumerate(specs):
            ttk.Label(parent, text=label).grid(row=0, column=col*2, sticky="e")
            ttk.Entry(parent, textvariable=var, width=width).grid(row=0, column=col*2 + 1, sticky="w", padx=2)
        # make columns stretch a bit on resize
        for col in range(len(specs) * 2):
            parent.grid_columnconfigure(col, weight=1 if col % 2 == 1 else 0)

    def _fetch_effects(self):
        self._send_csv("getHapticMappingsJson")

    def _use_selected_effect(self):
        sel = self.effects_list.curselection()
        if not sel:
            return
        name = self.effects_list.get(sel[0]).strip()

        for var in (
            self.play_event,   # Events: play
            self.pp_event,     # Events: playParam
            self.pw_event,     # Events: playWithStartTime
            self.pl_event,     # Events: playLoop
            self.pe_event,     # Events: pause/resume/getEventTime
            self.stop_event,   # State: stopByEventId
            self.q_evt,        # State: isPlayingByEventId
        ):
            var.set(name)

        # if raw command is 'play,<old>', keep it in sync
        if hasattr(self, "raw_cmd") and self.raw_cmd.get().startswith("play,"):
            self.raw_cmd.set(f"play,{name}")


    def _maybe_update_effect_list(self, payload: str):
        # We expect getHapticMappingsJson to return a JSON list; be tolerant.
        try:
            data = json.loads(payload)
        except Exception:
            return
        if not isinstance(data, list):
            return
        names = set()
        def grab(d):
            for k in ("eventId", "key", "name", "EventId", "Name"):
                v = d.get(k)
                if isinstance(v, str):
                    names.add(v)
        for item in data:
            if isinstance(item, str):
                names.add(item)
            elif isinstance(item, dict):
                grab(item)
                for v in item.values():
                    if isinstance(v, dict):
                        grab(v)
        if names:
            self.effects_list.delete(0, "end")
            for n in sorted(names):
                self.effects_list.insert("end", n)
            self.effects_list.configure(height=min(len(names), self.MAX_EFFECT_ROWS))

    # ---------- Button handlers ----------
    def _on_connect(self):
        url = self.url_var.get().strip()
        if not url:
            messagebox.showerror("Error", "Please enter a WebSocket URL.")
            return
        self.ws_client.connect(url)
        # fetch effects shortly after connect
        self.after(500, self._fetch_effects)

    def _on_disconnect(self):
        self.ws_client.disconnect()

    def _send_csv(self, command):
        self.ws_client.send(command)

    def _send_json_cmd(self, name):
        self._send_csv(name)

    # Events tab
    def _send_play(self):
        evt = self.play_event.get().strip()
        if not evt:
            messagebox.showwarning("Input", "eventId is required")
            return
        self._send_csv(f"play,{evt}")

    def _send_play_param(self):
        evt = self.pp_event.get().strip()
        self._send_csv(
            f"playParam,{evt},{self.pp_req.get()},{self.pp_int.get()},{self.pp_dur.get()},{self.pp_ang.get()},{self.pp_off.get()}"
        )

    def _send_play_with_start(self):
        evt = self.pw_event.get().strip()
        self._send_csv(
            f"playWithStartTime,{evt},{self.pw_req.get()},{self.pw_start.get()},{self.pw_int.get()},{self.pw_dur.get()},{self.pw_ang.get()},{self.pw_off.get()}"
        )

    def _send_play_loop(self):
        evt = self.pl_event.get().strip()
        self._send_csv(
            f"playLoop,{evt},{self.pl_int.get()},{self.pl_dur.get()},{self.pl_ang.get()},{self.pl_off.get()},{self.pl_interval.get()},{self.pl_max.get()}"
        )

    # Low-level tab
    def _send_play_dot(self):
        self._send_csv(
            f"playDot,{self.dot_pos.get()},{self.dot_dur.get()},{self._norm_list(self.dot_motors.get())}"
        )

    def _send_play_waveform(self):
        # If user left reqId as 0, generate a simple local id to avoid server/native issues.
        wf_req = int(self.wf_req.get())
        if wf_req == 0:
            wf_req = int(time.time() * 1000) & 0x7FFFFFFF
        self._send_csv(
            f"playWaveform,{wf_req},{self.wf_pos.get()},{self._norm_list(self.wf_motor.get())},{self._norm_list(self.wf_play.get())},{self._norm_list(self.wf_shape.get())}"
        )

    def _send_play_path(self):
        self._send_csv(
            f"playPath,{self.ppath_pos.get()},{self._norm_list(self.ppath_x.get())},{self._norm_list(self.ppath_y.get())},{self._norm_list(self.ppath_int.get())}"
        )

    # ---------- Logging ----------
    def _append_log_safe(self, level, msg):
        ts = datetime.now().strftime("%H:%M:%S.%f")[:-3]
        self.log_queue.put((level, f"[{ts}] {level}: {msg}"))
        if level == "RECV":
            self.msg_queue.put(msg)  # raw payload for parsers (effects, etc.)

    def _drain_log_queue(self):
        try:
            while True:
                level, line = self.log_queue.get_nowait()
                self._append_log(line)
        except queue.Empty:
            pass
        # parse any incoming JSON for effect names
        try:
            while True:
                raw = self.msg_queue.get_nowait()
                self._maybe_update_effect_list(raw)
        except queue.Empty:
            pass
        self.after(50, self._drain_log_queue)

    def _append_log(self, line):
        self.log_text.configure(state="normal")
        self.log_text.insert("end", line + "\n")
        self.log_text.see("end")
        self.log_text.configure(state="disabled")

    def _clear_log(self):
        self.log_text.configure(state="normal")
        self.log_text.delete("1.0", "end")
        self.log_text.configure(state="disabled")

    # ---------- Utils ----------
    @staticmethod
    def _norm_list(s: str) -> str:
        s = s.strip()
        if not s:
            return s
        return s.replace(",", ";").replace("|", ";")


# -----------------------------
# main
# -----------------------------
if __name__ == "__main__":
    app = App()

    import atexit
    def _cleanup():
        try:
            app.ws_client.disconnect()
        except Exception:
            pass
    atexit.register(_cleanup)

    app.mainloop()

