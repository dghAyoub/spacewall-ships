#!/bin/bash
# Removes SpaceWall Ships and puts your normal wallpaper back.
set -u
INSTALL_DIR="${SPACEWALL_DIR:-$HOME/SpaceWall-ships}"; PLASMA=1
for a in "$@"; do [ "$a" = "--no-plasma" ] && PLASMA=0; done
QDBUS="$(command -v qdbus || command -v qdbus-qt5 || true)"
if [ "$PLASMA" = 1 ] && [ -n "$QDBUS" ] && pgrep -x plasmashell >/dev/null; then
  "$QDBUS" org.kde.plasmashell /PlasmaShell org.kde.PlasmaShell.evaluateScript '
    var d = desktops(); for (var i = 0; i < d.length; i++) { d[i].wallpaperPlugin = "org.kde.image"; d[i].reloadConfig(); }
    var p = panels();
    for (var i = 0; i < p.length; i++) { var w = p[i].widgets(); for (var j = 0; j < w.length; j++) if (w[j].type == "org.spacewall.toggle") w[j].remove(); }' >/dev/null 2>&1
fi
[ -x "$INSTALL_DIR/stop.sh" ] && "$INSTALL_DIR/stop.sh" >/dev/null 2>&1
rm -rf "$HOME/.local/share/plasma/wallpapers/org.spacewall.ships" "$HOME/.local/share/plasma/plasmoids/org.spacewall.toggle"
rm -f "$HOME/.config/autostart/spacewall-ships.desktop" "$HOME/.config/spacewall-ships.off"
rm -rf "$INSTALL_DIR"
command -v kbuildsycoca5 >/dev/null && kbuildsycoca5 --noincremental >/dev/null 2>&1
echo "SpaceWall Ships removed. (Log out and in, or restart plasmashell, to clear the last cached bits.)"
