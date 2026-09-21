#!/usr/bin/env python3
"""Start the official Codex App Server for BoneAI."""

from __future__ import annotations

import argparse
import ctypes
import os
import shutil
import subprocess
import sys
from pathlib import Path
from urllib.error import URLError
from urllib.request import urlopen


def is_ready(port: int) -> bool:
    try:
        with urlopen(f"http://127.0.0.1:{port}/readyz", timeout=1) as response:
            return response.status == 200
    except (OSError, URLError):
        return False


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Start the localhost-only Codex App Server used by BoneAI."
    )
    parser.add_argument("--port", type=int, default=4500)
    parser.add_argument("--parent-pid", type=int, default=0, help=argparse.SUPPRESS)
    args = parser.parse_args()

    if not 1 <= args.port <= 65535:
        parser.error("--port must be between 1 and 65535")

    if is_ready(args.port):
        print(f"Codex App Server is already ready on 127.0.0.1:{args.port}.")
        return 0

    codex = find_codex()
    if codex is None:
        print(
            "Codex was not found on PATH or in the Codex desktop installation under "
            "%LOCALAPPDATA%\\OpenAI\\Codex\\bin. Install/open Codex and sign in, "
            "then start BONELAB again.",
            file=sys.stderr,
            flush=True,
        )
        return 1

    endpoint = f"ws://127.0.0.1:{args.port}"
    print(f"Using Codex executable: {codex}", flush=True)
    print(f"Starting the official Codex App Server at {endpoint}", flush=True)
    print("BoneAI bridge is ready. Keep this window open while playing BONELAB.", flush=True)

    try:
        process = subprocess.Popen([codex, "app-server", "--listen", endpoint])
        while process.poll() is None:
            if args.parent_pid and not parent_is_running(args.parent_pid):
                process.terminate()
                try:
                    process.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    process.kill()
                return 0
            try:
                process.wait(timeout=0.5)
            except subprocess.TimeoutExpired:
                pass
        return process.returncode or 0
    except KeyboardInterrupt:
        print("\nCodex App Server stopped.")
        return 0


def parent_is_running(pid: int) -> bool:
    if pid <= 0:
        return True
    if os.name == "nt":
        process_query_limited_information = 0x1000
        still_active = 259
        handle = ctypes.windll.kernel32.OpenProcess(
            process_query_limited_information, False, pid
        )
        if not handle:
            return False
        exit_code = ctypes.c_ulong()
        success = ctypes.windll.kernel32.GetExitCodeProcess(
            handle, ctypes.byref(exit_code)
        )
        ctypes.windll.kernel32.CloseHandle(handle)
        return bool(success) and exit_code.value == still_active
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def find_codex() -> str | None:
    on_path = shutil.which("codex") or shutil.which("codex.exe")
    if on_path:
        return on_path

    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        return None

    roots = [
        Path(local_app_data) / "OpenAI" / "Codex" / "bin",
        Path(local_app_data) / "Programs" / "OpenAI Codex",
        Path(local_app_data) / "Programs" / "Codex",
    ]
    candidates: list[Path] = []
    for root in roots:
        if not root.is_dir():
            continue
        candidates.extend(path for path in root.glob("**/codex.exe") if path.is_file())

    if not candidates:
        return None
    candidates.sort(key=lambda path: path.stat().st_mtime, reverse=True)
    return str(candidates[0])


if __name__ == "__main__":
    raise SystemExit(main())
