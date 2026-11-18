#!/bin/bash
# prints 1 when the animation is switched on (bridge running and not switched off), else 0
[ -e "$HOME/.config/spacewall-ships.off" ] && { echo 0; exit 0; }
ss -tln 2>/dev/null | grep -q '127.0.0.1:8777' && echo 1 || echo 0
