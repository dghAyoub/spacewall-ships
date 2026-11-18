#!/bin/bash
# Switch Plasma back to your normal image wallpaper (your own picture is still saved in Plasma's settings)
QDBUS="$(command -v qdbus || command -v qdbus-qt5)"
[ -n "$QDBUS" ] || { echo "qdbus not found (sudo apt install qdbus-qt5)"; exit 1; }
"$QDBUS" org.kde.plasmashell /PlasmaShell org.kde.PlasmaShell.evaluateScript 'var d=desktops(); for (var i=0;i<d.length;i++){ d[i].wallpaperPlugin="org.kde.image"; d[i].reloadConfig(); }'
"$(dirname "$(readlink -f "$0")")/stop.sh" >/dev/null 2>&1
echo "normal wallpaper restored"
