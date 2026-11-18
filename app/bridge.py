#!/usr/bin/env python3
"""SpaceWall bridge: serves index.html on http://127.0.0.1:8777 and streams system audio to it.

 - GET /index.html   the wallpaper page (loaded by the Plasma wallpaper plugin)
 - GET /audio        server-sent events: raw 16-bit mono PCM (base64) from the default output's monitor
 - pauses the animation while a fullscreen window (game/video) is in front

Binds to 127.0.0.1 only. Stop it with: pkill -f SpaceWall-ships/bridge.py
"""
import base64, os, queue, select, signal, shutil, subprocess, sys, threading, time
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler

HERE = os.path.dirname(os.path.abspath(__file__))
PORT = int(os.environ.get('SPACEWALL_PORT', '8777'))
clients, lock, state = set(), threading.Lock(), {'paused': False}
FLAG = os.path.expanduser('~/.config/spacewall-ships.off')   # exists = animation switched off


def is_on():
    return not os.path.exists(FLAG)


def broadcast(msg):
    with lock:
        for q in list(clients):
            try:
                q.put_nowait(msg)
            except queue.Full:
                pass


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **k):
        super().__init__(*a, directory=HERE, **k)

    def log_message(self, *a):
        pass

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def do_GET(self):
        if self.path.split('?')[0] == '/audio':
            return self.audio()
        if self.path.split('?')[0] == '/state':
            body = b'1' if is_on() else b'0'
            self.send_response(200); self.send_header('Content-Type', 'text/plain'); self.send_header('Content-Length', '1'); self.end_headers()
            self.wfile.write(body)
            return
        super().do_GET()

    def audio(self):
        self.send_response(200)
        self.send_header('Content-Type', 'text/event-stream')
        self.send_header('Connection', 'keep-alive')
        self.end_headers()
        q = queue.Queue(maxsize=64)
        with lock:
            clients.add(q)
        try:
            self.wfile.write(('event: pause\ndata: %d\n\n' % (1 if state['paused'] else 0)).encode())
            self.wfile.flush()
            while True:
                try:
                    m = q.get(timeout=10)
                except queue.Empty:
                    m = ': keepalive\n\n'
                self.wfile.write(m.encode())
                self.wfile.flush()
        except Exception:
            pass
        finally:
            with lock:
                clients.discard(q)


def default_monitor():
    try:
        sink = subprocess.check_output(['pactl', 'get-default-sink'], text=True, timeout=3).strip()
        return sink + '.monitor'
    except Exception:
        return '@DEFAULT_MONITOR@'


def audio_thread():
    if not shutil.which('parec'):
        print('parec not found: no live audio')
        return
    p = None
    while True:
        if not is_on():                               # switched off: stop recording completely
            if p:
                p.terminate(); p = None
            time.sleep(0.5)
            continue
        if p is None or p.poll() is not None:
            mon = default_monitor()
            try:
                p = subprocess.Popen(['parec', '-d', mon, '--format=s16le', '--rate=44100', '--channels=1',
                                      '--latency-msec=10', '--client-name=SpaceWall'],
                                     stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
                print('capturing audio from', mon, flush=True)
            except Exception as e:
                print('audio error:', e, flush=True); p = None; time.sleep(2); continue
        r, _, _ = select.select([p.stdout], [], [], 0.5)
        if not r:
            continue
        data = p.stdout.read(1764)                    # ~20 ms of audio
        if not data:
            p = None; time.sleep(2); continue         # default output changed or parec died: retry
        if clients and not state['paused']:
            broadcast('data: %s\n\n' % base64.b64encode(data).decode())


def fullscreen_thread():
    while True:
        try:
            wid = subprocess.check_output(['xdotool', 'getactivewindow'], text=True, timeout=1).strip()
            st = subprocess.check_output(['xprop', '-id', wid, '_NET_WM_STATE'], text=True, timeout=1)
            fs = '_NET_WM_STATE_FULLSCREEN' in st
        except Exception:
            fs = False
        if fs != state['paused']:
            state['paused'] = fs
            broadcast('event: pause\ndata: %d\n\n' % (1 if fs else 0))
        time.sleep(1.5)


if __name__ == '__main__':
    threading.Thread(target=audio_thread, daemon=True).start()
    threading.Thread(target=fullscreen_thread, daemon=True).start()
    srv = ThreadingHTTPServer(('127.0.0.1', PORT), Handler)
    srv.daemon_threads = True
    signal.signal(signal.SIGTERM, lambda *_: os._exit(0))
    print('SpaceWall bridge on http://127.0.0.1:%d/index.html' % PORT, flush=True)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
