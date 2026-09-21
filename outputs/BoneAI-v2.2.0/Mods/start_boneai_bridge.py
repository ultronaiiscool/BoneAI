#!/usr/bin/env python3
"""Start the official Codex App Server for BoneAI."""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import time
import zipfile
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
    parser.add_argument("--install-update", type=Path, help=argparse.SUPPRESS)
    parser.add_argument("--expected-sha256", default="", help=argparse.SUPPRESS)
    parser.add_argument("--game-dir", type=Path, help=argparse.SUPPRESS)
    args = parser.parse_args()

    if args.install_update:
        if not args.game_dir or not args.expected_sha256:
            parser.error("update installation requires --game-dir and --expected-sha256")
        return install_update(args.install_update, args.expected_sha256, args.game_dir, args.parent_pid)

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


def install_update(archive: Path, expected_sha256: str, game_dir: Path, parent_pid: int) -> int:
    """Wait for BONELAB to exit, then atomically install a verified BoneAI release."""
    while parent_is_running(parent_pid):
        time.sleep(0.5)
    archive = archive.resolve()
    game_dir = game_dir.resolve()
    actual = hashlib.sha256(archive.read_bytes()).hexdigest()
    if actual.lower() != expected_sha256.lower():
        print("BoneAI update digest changed; refusing installation.", file=sys.stderr)
        return 2
    allowed = {
        "Mods/BoneAI.dll", "Mods/start_boneai_bridge.py",
        "UserLibs/BoneAI.Catalogs.dll", "Plugins/BoneAI.Updater.dll",
        "README.md", "INSTALL-FIRST.md", "CHANGELOG.md", "manifest.json", "icon.png",
    }
    with tempfile.TemporaryDirectory(prefix="boneai-update-") as temp_name:
        temp = Path(temp_name)
        with zipfile.ZipFile(archive) as package:
            members = [name.replace("\\", "/").strip("/") for name in package.namelist() if not name.endswith("/")]
            unexpected = [name for name in members if name not in allowed or ".." in Path(name).parts]
            if unexpected:
                print(f"BoneAI update contains unexpected files: {unexpected}", file=sys.stderr)
                return 3
            package.extractall(temp)
        backup = game_dir / "UserData" / "BoneAI" / "Backup"
        backup.mkdir(parents=True, exist_ok=True)
        for relative in members:
            source = temp / Path(relative)
            destination = game_dir / Path(relative)
            destination.parent.mkdir(parents=True, exist_ok=True)
            if destination.exists():
                backup_target = backup / Path(relative)
                backup_target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(destination, backup_target)
            staged = destination.with_suffix(destination.suffix + ".boneai-new")
            shutil.copy2(source, staged)
            os.replace(staged, destination)
    print("BoneAI update installed successfully.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
