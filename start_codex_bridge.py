#!/usr/bin/env python3
"""Start the official Codex App Server for BONELAB AI Agent."""

from __future__ import annotations

import argparse
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
        completed = subprocess.run(
            [codex, "app-server", "--listen", endpoint],
            check=False,
        )
        return completed.returncode
    except KeyboardInterrupt:
        print("\nCodex App Server stopped.")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
