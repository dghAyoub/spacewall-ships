import QtQuick 2.15
import QtQuick.Layouts 1.15
import org.kde.plasma.plasmoid 2.0
import org.kde.plasma.core 2.0 as PlasmaCore

Item {
    id: root
    property bool active: false
    property bool busy: false
    readonly property string dir: "@INSTALL_DIR@/"

    Plasmoid.preferredRepresentation: Plasmoid.compactRepresentation
    Plasmoid.toolTipMainText: "SpaceWall wallpaper: " + (active ? "ON" : "OFF")
    Plasmoid.toolTipSubText: busy ? "Switching..." : "Click to turn it " + (active ? "off (saves CPU)" : "on")
    Plasmoid.icon: "preferences-desktop-wallpaper"

    PlasmaCore.DataSource {
        id: exec
        engine: "executable"
        connectedSources: []
        onNewData: {
            var out = (data["stdout"] || "").trim()
            if (sourceName.indexOf("status.sh") >= 0) {
                if (!root.busy) root.active = (out === "1")
            } else {
                root.busy = false
                root.active = (out === "on")
            }
            disconnectSource(sourceName)
        }
    }

    Timer {
        interval: 4000; running: true; repeat: true; triggeredOnStart: true
        onTriggered: exec.connectSource(root.dir + "status.sh")
    }

    Plasmoid.compactRepresentation: MouseArea {
        id: area
        hoverEnabled: true
        Layout.minimumWidth: height
        Layout.preferredWidth: height
        onClicked: {
            if (root.busy) return
            root.busy = true
            exec.connectSource(root.dir + "toggle.sh")
        }

        PlasmaCore.IconItem {
            anchors.fill: parent
            anchors.margins: Math.round(parent.height * 0.12)
            source: "preferences-desktop-wallpaper"
            active: area.containsMouse
            opacity: root.active ? 1.0 : 0.45
        }
        Rectangle {                         // status dot: green = on, grey = off, amber = switching
            width: Math.max(6, parent.height * 0.26); height: width; radius: width / 2
            anchors.right: parent.right; anchors.bottom: parent.bottom
            anchors.margins: Math.round(parent.height * 0.06)
            color: root.busy ? "#f5a623" : (root.active ? "#3ddc84" : "#7a7a7a")
            border.color: "#101018"; border.width: 1
        }
    }
}
