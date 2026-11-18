import QtQuick 2.15
import QtWebEngine 1.10

// SpaceWall Ships wallpaper. Plugin and bridge stay loaded all the time (loading QtWebEngine freezes Plasma ~2 s = white flash).
// "off" = page unloaded + picture shown; "on" = the page is loaded and fades in over the picture.
Item {
    id: root
    readonly property string page: "http://127.0.0.1:8777/index.html"
    readonly property string state: "http://127.0.0.1:8777/state"
    readonly property string blank: "data:text/html,<html><body style='background:%2305030f'></body></html>"
    property bool on: false          // animation switched on (reported by the bridge)
    property bool warmed: false      // real page loaded once, hidden, so the first switch-on is instant

    Rectangle { anchors.fill: parent; color: "#05030f" }

    // picture shown whenever the animation is off (replace this file to change it)
    Image {
        anchors.fill: parent
        source: "file://@INSTALL_DIR@/assets/wallpaper-off.png"
        fillMode: Image.PreserveAspectCrop
        cache: false                         // re-read the picture from disk each time the wallpaper loads
    }

    WebEngineView {
        id: web
        anchors.fill: parent
        opacity: 0.01
        url: root.blank
        backgroundColor: "#05030f"
        enabled: false                       // no input here: Plasma keeps handling the desktop icons and the mouse
        settings.javascriptCanOpenWindows: false
        settings.focusOnNavigationEnabled: false
        onLoadingChanged: function(req) {
            if (req.status === WebEngineView.LoadSucceededStatus && root.on && web.url.toString() === root.page) show.restart()
        }
    }

    Timer { id: show; interval: 900; onTriggered: fadeIn.start() }
    NumberAnimation { id: fadeIn;  target: web; property: "opacity"; to: 1.0;  duration: 700; easing.type: Easing.InOutQuad }
    NumberAnimation { id: fadeOut; target: web; property: "opacity"; to: 0.01; duration: 500; easing.type: Easing.InOutQuad
        onStopped: if (!root.on) web.url = root.blank }
    Timer { id: warmEnd; interval: 5000; onTriggered: if (!root.on) web.url = root.blank }

    onOnChanged: {
        if (on) { warmEnd.stop(); fadeOut.stop(); if (web.url.toString() !== page) web.url = page; else show.restart() }
        else    { show.stop(); fadeIn.stop(); fadeOut.start() }
    }

    function check() {
        var x = new XMLHttpRequest()
        x.timeout = 1500
        x.onreadystatechange = function() {
            if (x.readyState !== 4) return
            var up = (x.status === 200 && x.responseText.trim() === "1")
            if (x.status === 200 && !root.warmed) {          // bridge reachable for the first time: warm the real page up, hidden
                root.warmed = true
                if (!up) { web.url = page; warmEnd.start() }
            }
            if (up !== root.on) root.on = up
        }
        x.open("GET", state)
        x.send()
    }
    Timer { interval: 2000; running: true; repeat: true; triggeredOnStart: true; onTriggered: root.check() }
}
