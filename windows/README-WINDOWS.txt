SpaceWall Ships for Windows  (v0.1.0, EXPERIMENTAL: tested on one PC)
=====================================================================
Audio-reactive wallpaper: nebula, rain, two ships that follow your music. No installation, no other app needed.
Needs: Windows 10/11 64-bit with the Edge WebView2 Runtime (already present on normal Windows 10/11).

WHAT HAS BEEN TESTED
  Tested on ONE PC: Windows 10 Pro 22H2, Intel HD Graphics 620, Realtek audio, one 1920x1080 screen.
  There it sat behind the desktop icons, ran at 30 frames per second, heard the speakers, and the ships reacted to the sound.
  NOT tested: Windows 11, other graphics cards, several monitors, other sound cards, and "Start with Windows" after a restart.
  The program is not signed, so Windows warns you the first time (see below).

FIRST START
  1. Extract the whole zip to a folder (e.g. Documents\SpaceWall). Do not run it from inside the zip.
  2. Double-click  1-RUN-ALL-TESTS.bat
     If SmartScreen says "Windows protected your PC": click "More info" then "Run anyway".
  3. You do not need to close other windows: the test minimizes them itself and restores them.
  4. Wait about 40 seconds without touching the mouse. One short quiet beep will play. A result box appears.
  5. The result is also in  %APPDATA%\SpaceWall\diagnostics.txt  (copied to the clipboard)
     and screenshots are in  %APPDATA%\SpaceWall\shots\  (they show your desktop: look before sharing).

AFTER THE TEST
  - The program picks the best placement mode for your PC automatically and keeps it.
  - Tray icon (near the clock, maybe under the ^ arrow): left-click = ON/OFF, right-click = menu
    (Mode A-E, Quality, Demo mode, Debug overlay, Start with Windows, Diagnostics, Quit).
  - To start it later: SpaceWall-start.bat (or tick "Start with Windows" in the tray menu).

IF SOMETHING FAILS
  - Black or no wallpaper: tray > Mode > "D: Bottom window" (always works, but hides desktop icons)
    or run-in-window.bat (a normal window, only to check the animation and the audio).
  - Window shows but no reaction to music: tray > Diagnostics, look at the "Audio" line.
  - Everything is logged in %APPDATA%\SpaceWall\log.txt.
  - Remove everything: uninstall.bat.

Source code and the Linux (KDE) version: https://github.com/dghAyoub/spacewall-ships   (MIT license; built with an AI assistant)
