"""Local static replay server. Standard library only; no simulation or public bind."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
import os
from pathlib import Path
import socket
from urllib.parse import unquote, urlsplit
import webbrowser


class LocalReplayServer(ThreadingHTTPServer):
    # Windows SO_REUSEADDR can admit a second listener on the same port.
    allow_reuse_address = False

    def server_bind(self):
        if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        super().server_bind()


class ReplayHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def do_GET(self):
        # Always return the current local files, including after a viewer update.
        if 'If-Modified-Since' in self.headers:
            del self.headers['If-Modified-Since']
        if self.headers.get('Host', '').split(':')[0] not in ('127.0.0.1', 'localhost'):
            self.send_error(403, 'Localhost only')
            return
        parts = unquote(urlsplit(self.path).path).split('/')
        if any(part.startswith('.') and part not in ('.', '..') for part in parts):
            self.send_error(404)
            return
        if self.path == '/':
            self.send_response(302)
            self.send_header('Location', '/tools/replay-viewer/')
            self.end_headers()
            return
        if self.path == '/favicon.ico':
            self.send_response(204)
            self.end_headers()
            return
        super().do_GET()

    def list_directory(self, path):
        self.send_error(404, 'Open /tools/replay-viewer/')
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=0)
    parser.add_argument('--no-browser', action='store_true')
    parser.add_argument('--info-file', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    if not (root / 'tools/replay-viewer/recordings/172-s3-k4.json.gz').is_file():
        parser.error('Brak nagrania #172. Potrzebna jest pełna kopia plików tej gałęzi.')
    mimetypes.add_type('text/javascript', '.mjs')
    try:
        server = LocalReplayServer(('127.0.0.1', args.port), partial(ReplayHandler, directory=str(root)))
    except OSError as error:
        parser.exit(1, f'Nie można uruchomić lokalnego serwera: {error}\n')
    url = f'http://127.0.0.1:{server.server_port}/tools/replay-viewer/'
    print(f'\nStadion: {url}\nTylko ten komputer. Zatrzymanie: Ctrl+C.\n', flush=True)
    if args.info_file:
        args.info_file.write_text(json.dumps({'url': url, 'port': server.server_port, 'pid': os.getpid(), 'bind': '127.0.0.1'}), encoding='utf-8')
    if not args.no_browser and not webbrowser.open(url, new=2):
        print('Nie udało się otworzyć przeglądarki automatycznie. Otwórz powyższy adres.', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print('\nSerwer zatrzymany.')
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
