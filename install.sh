#!/bin/bash
# SpaceWall Ships installer: audio-reactive wallpaper for KDE Plasma 5 (Ubuntu 24.04 / Kubuntu / Linux Mint 22 KDE).
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
INSTALL_DIR="${SPACEWALL_DIR:-$HOME/SpaceWall-ships}"
ACTIVATE=1; START=1; PLASMA=1; DEPS=0

usage() { cat <<'U'
Usage: ./install.sh [options]
  --install-deps   install missing packages with "sudo apt install" (asks for your password)
  --no-activate    install everything, but do not switch your wallpaper or add the panel button
  --no-start       do not start the audio bridge now
  --no-plasma      only copy files (no Plasma changes at all)
  -h, --help       this help
Install folder: $HOME/SpaceWall-ships (override with SPACEWALL_DIR=/some/path ./install.sh)
U
}
for a in "$@"; do case "$a" in
  --install-deps) DEPS=1 ;; --no-activate) ACTIVATE=0 ;; --no-start) START=0 ;;
  --no-plasma) PLASMA=0; ACTIVATE=0 ;; -h|--help) usage; exit 0 ;;
  *) echo "unknown option: $a"; usage; exit 1 ;; esac; done

say() { printf '\n== %s\n' "$*"; }
[ "$(id -u)" = 0 ] && { echo "Please run this as your normal user, not as root."; exit 1; }

# ---- 1. dependencies ----
say "Checking requirements"
missing=()
need() { command -v "$1" >/dev/null 2>&1 || missing+=("$2"); }
need python3 python3; need parec pulseaudio-utils; need xdotool xdotool; need xprop x11-utils; need ss iproute2
find_qdbus() { for c in qdbus qdbus-qt5 /usr/lib/qt5/bin/qdbus; do if command -v "$c" >/dev/null 2>&1 && "$c" >/dev/null 2>&1; then command -v "$c"; return; fi; done; }
[ -n "$(find_qdbus)" ] || missing+=(qdbus-qt5)      # must really run, not only exist (a bare qtchooser stub is not enough)
if [ "$PLASMA" = 1 ]; then
  need kbuildsycoca5 libkf5service-bin
  ls /usr/lib/*/qt5/qml/QtWebEngine/libqtwebengineplugin.so >/dev/null 2>&1 || missing+=(qml-module-qtwebengine)
fi
if [ "${#missing[@]}" -gt 0 ]; then
  echo "Missing packages: ${missing[*]}"
  if [ "$DEPS" = 1 ]; then sudo apt-get install -y "${missing[@]}" || exit 1
  else echo "Install them with:   sudo apt install ${missing[*]}"; echo "or run:              ./install.sh --install-deps"; exit 1; fi
fi
QDBUS="$(find_qdbus)"

if [ "$PLASMA" = 1 ]; then
  pgrep -x plasmashell >/dev/null || { echo "plasmashell is not running. Log in to a KDE Plasma session and run this again."; exit 1; }
  ver="$(plasmashell --version 2>/dev/null | awk '{print $2}')"
  case "$ver" in 5.*) echo "KDE Plasma $ver: ok" ;;
    *) echo "KDE Plasma 5 is required (found: ${ver:-unknown}). Plasma 6 and GNOME are not supported."; exit 1 ;; esac
fi

# ---- 2. files ----
say "Installing to $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"
cp -r "$HERE/app/." "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR"/*.sh "$INSTALL_DIR/bridge.py"
render() { sed "s#@INSTALL_DIR@#${INSTALL_DIR}#g" "$1" > "$2"; }

if [ "$PLASMA" = 1 ]; then
  say "Installing the Plasma wallpaper plugin and the panel button"
  PW="$HOME/.local/share/plasma/wallpapers/org.spacewall.ships"; PT="$HOME/.local/share/plasma/plasmoids/org.spacewall.toggle"
  mkdir -p "$PW/contents/ui" "$PT/contents/ui"
  cp "$HERE/plasma/wallpaper/metadata.json" "$PW/"; render "$HERE/plasma/wallpaper/contents/ui/main.qml" "$PW/contents/ui/main.qml"
  cp "$HERE/plasma/widget/metadata.json" "$PT/";    render "$HERE/plasma/widget/contents/ui/main.qml"    "$PT/contents/ui/main.qml"
  kbuildsycoca5 --noincremental >/dev/null 2>&1 || true
fi

say "Setting up autostart"
mkdir -p "$HOME/.config/autostart"
cat > "$HOME/.config/autostart/spacewall-ships.desktop" <<DESK
[Desktop Entry]
Type=Application
Name=SpaceWall Ships bridge
Comment=Audio bridge for the SpaceWall Ships Plasma wallpaper
Exec=$INSTALL_DIR/autostart.sh
Terminal=false
DESK
rm -f "$HOME/.config/spacewall-ships.off"

# ---- 3. start + activate ----
if [ "$START" = 1 ]; then say "Starting the audio bridge"; "$INSTALL_DIR/start.sh"; fi

if [ "$ACTIVATE" = 1 ]; then
  say "Switching your wallpaper to SpaceWall and adding the panel button"
  sleep 1
  "$QDBUS" org.kde.plasmashell /PlasmaShell org.kde.PlasmaShell.evaluateScript '
    var d = desktops();
    for (var i = 0; i < d.length; i++) { d[i].wallpaperPlugin = "org.spacewall.ships"; d[i].reloadConfig(); }
    var p = panels();
    if (p.length > 0) {
      var w = p[0].widgets(), has = false;
      for (var j = 0; j < w.length; j++) if (w[j].type == "org.spacewall.toggle") has = true;
      if (!has) p[0].addWidget("org.spacewall.toggle");
    }' >/dev/null 2>&1 || echo "(could not talk to plasmashell: switch the wallpaper by hand, see README)"
fi

cat <<DONE

Done.
 - Wallpaper: $( [ "$ACTIVATE" = 1 ] && echo "switched to SpaceWall Ships (the first switch can flash white for ~2 s: Plasma is loading its web engine)" || echo "not switched (run $INSTALL_DIR/spacewall-wallpaper.sh when you want it)" )
 - Panel button: the small wallpaper icon with a green/grey dot turns the animation on and off (it may sit at the far left of your top panel; drag it in Edit Panel).
 - Play some music and watch.
 - Back to your old wallpaper: $INSTALL_DIR/normal-wallpaper.sh      Remove everything: ./uninstall.sh
DONE
