#!/bin/bash
# Switch Plasma to the SpaceWall wallpaper (the plugin stays loaded) and start the animation
QDBUS="$(command -v qdbus || command -v qdbus-qt5)"
[ -n "$QDBUS" ] || { echo "qdbus not found (sudo apt install qdbus-qt5)"; exit 1; }
rm -f "$HOME/.config/spacewall-ships.off"
"$QDBUS" org.kde.plasmashell /PlasmaShell org.kde.PlasmaShell.evaluateScript 'var d=desktops(); for (var i=0;i<d.length;i++){ d[i].wallpaperPlugin="org.spacewall.ships"; d[i].reloadConfig(); }'
"$(dirname "$(readlink -f "$0")")/start.sh"
echo "SpaceWall wallpaper on"
