#!/bin/bash
# SpaceWall on/off. The bridge and the Plasma plugin stay loaded (no flash); off = audio capture stopped + picture shown.
D="$(dirname "$(readlink -f "$0")")"; FLAG="$HOME/.config/spacewall-ships.off"
if [ "$("$D/status.sh")" = "1" ]; then touch "$FLAG"; echo off
else rm -f "$FLAG"; "$D/start.sh" >/dev/null 2>&1; echo on; fi
