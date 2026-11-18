#!/bin/bash
pid=$(ss -tlnp 2>/dev/null | grep '127.0.0.1:8777' | sed -n 's/.*pid=\([0-9]*\).*/\1/p' | head -1)
if [ -n "$pid" ]; then kill "$pid" && echo "bridge stopped (wallpaper goes blank until it runs again; use ./normal-wallpaper.sh to go back)"; else echo "not running"; fi
