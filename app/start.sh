#!/bin/bash
# SpaceWall: start the audio bridge. The Plasma wallpaper "SpaceWall Ships" displays the page.
cd "$(dirname "$0")"
if ss -tln 2>/dev/null | grep -q '127.0.0.1:8777'; then echo "bridge already running"; exit 0; fi
nohup python3 "$PWD/bridge.py" >/dev/null 2>&1 &
echo "SpaceWall bridge started"
