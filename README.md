# SpaceWall Ships

An audio-reactive animated wallpaper for **KDE Plasma 5**: a violet nebula, falling rain and two small ships whose movements follow your music.
It reacts to whatever your computer is playing (Spotify, YouTube, games...), runs locally, and costs nothing.

- Quiet music: the ships drift in parallel. Louder: they jump between lanes, cross each other and do barrel-roll flips.
- Pitch sets how far apart the ships sit; bass hits push them forward; snare/voice accents make one ship jump; hi-hats trigger flips.
- A small button in your top panel turns the animation completely off (0% CPU) and back on.

## Requirements
- Ubuntu 24.04 or newer **with KDE Plasma 5** (Kubuntu, or Ubuntu with `kde-plasma-desktop`), or Linux Mint 22 with KDE. Plasma **5.27** is what it was built on.
- **Not supported:** GNOME, Plasma 6, Windows, macOS.
- Packages (the installer checks for them): `python3 pulseaudio-utils xdotool x11-utils iproute2 qdbus-qt5 libkf5service-bin qml-module-qtwebengine`
  (`pulseaudio-utils` also works with PipeWire).

## Install
```bash
git clone https://github.com/dghAyoub/spacewall-ships.git
cd spacewall-ships
./install.sh --install-deps     # --install-deps asks for your sudo password to install missing packages
```
Other options: `--no-activate` (copy everything but keep your current wallpaper), `--no-start`, `--no-plasma`.
It installs into `~/SpaceWall-ships`, adds the Plasma wallpaper plugin and the panel button, and starts an audio bridge at every login.

The very first switch to the wallpaper can flash white for about 2 seconds: Plasma loads its web engine. After that, turning it on and off does not flash.

## Use
- Click the small wallpaper icon with a dot in your top panel (green = on, grey = off). It may sit at the far left of the panel; drag it in *Edit Panel*.
- Your choice (on/off) is remembered after a restart.
- The picture shown when it is off is `~/SpaceWall-ships/assets/wallpaper-off.png`. Replace it with any 1920x1080 image you like, then switch the wallpaper plugin off and on again.
- Back to your old wallpaper: `~/SpaceWall-ships/normal-wallpaper.sh`. Switch to SpaceWall again: `~/SpaceWall-ships/spacewall-wallpaper.sh`.
- Tweaks live at the top of `~/SpaceWall-ships/index.html` (`FPS` default 30, `SENS` sensitivity, `BGS`/`NEBS` quality). Edit, then switch off and on.

## Remove
```bash
./uninstall.sh
```

## Good to know
- **CPU:** the animation page uses roughly 25-30% of one core on an old 2-core laptop while it is on, and 0% when it is off. Lower `FPS` in `index.html` if you need less.
- **Microphone icon:** while the animation is on, KDE shows its microphone icon. It is not your microphone: the bridge records your *speaker output* (the monitor of the default sound output) so it can hear the music. The icon disappears when you turn the animation off.
- **Fullscreen pause** (the animation pauses under a fullscreen game/video) needs an X11 session. On Wayland everything else works.
- **If you edit the plugin's QML**, Plasma caches it until plasmashell restarts: `kquitapp5 plasmashell; setsid nohup plasmashell >/dev/null 2>&1 &`. Changes to `index.html` and to the picture do not need that.
- **Nothing is sent anywhere.** The bridge listens on `127.0.0.1:8777` only.
- No sound reaction? Check that music is playing on the *default* output, and that `parec` works: `parec -d @DEFAULT_MONITOR@ --format=s16le | head -c 100 | wc -c`.

## License
MIT, see `LICENSE`.

Built together with an AI assistant (Claude).
