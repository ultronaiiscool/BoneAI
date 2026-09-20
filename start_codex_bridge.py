#!/usr/bin/env python3
"""Start the official Codex App Server for BONELAB AI Agent."""

from __future__ import annotations

import argparse
import ctypes
import os
import shutil
import subprocess
import sys
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
        description="Start the localhost-only Codex App Server used by BONELAB AI Agent."
    )
    parser.add_argument("--port", type=int, default=4500)
    parser.add_argument("--parent-pid", type=int, default=0, help=argparse.SUPPRESS)
    args = parser.parse_args()

    if not 1 <= args.port <= 65535:
        parser.error("--port must be between 1 and 65535")

    if is_ready(args.port):
        print(f"Codex App Server is already ready on 127.0.0.1:{args.port}.")
        return 0

    codex = shutil.which("codex")
    if codex is None:
        print(
            "Codex was not found on PATH. Install/open the Codex desktop app and sign in, "
            "then run this script again.",
            file=sys.stderr,
        )
        return 1

    endpoint = f"ws://127.0.0.1:{args.port}"
    print(f"Starting the official Codex App Server at {endpoint}")
    print("Keep this window open while using BONELAB AI Agent. Press Ctrl+C to stop.")

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
        handle = ctypes.windll.kernel32.OpenProcess(
            process_query_limited_information, False, pid
        )
        if not handle:
            return False
        ctypes.windll.kernel32.CloseHandle(handle)
        return True
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


if __name__ == "__main__":
    raise SystemExit(main())
