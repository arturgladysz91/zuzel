"""Loopback-only stadium server. Runs the fixed H25/FAST50 tool and serves recordings."""
import argparse
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import mimetypes
import os
from pathlib import Path
import re
import shutil
import socket
import subprocess
import sys
import threading
from datetime import datetime, timezone
from urllib.parse import unquote, urlsplit
import uuid
import webbrowser

from export_h25 import export, history_passes, load_gzip, write_replay

VIEWER = Path(__file__).resolve().parent
ROOT = VIEWER.parents[1]
PROJECT = ROOT / 'tools/autonomous-traffic191/autonomous-traffic191.csproj'
ENGINE = PROJECT.parent / 'bin/Release/net8.0/autonomous-traffic191.dll'
HISTORY = {
    '191-nominal': ('dynamic', '#191 — Bieg 1, czterech zawodników'),
    '191-gearing': ('alternate', '#191 — Bieg 2, inne przełożenia'),
}
STUDY_193 = {
    '193-four-stock': '#193 · 4 zawodników · baza #192',
    '193-four-exit': '#193 · 4 zawodników · prototyp',
    '193-alternate-stock': '#193 · inne przełożenia · baza #192',
    '193-alternate-exit': '#193 · inne przełożenia · prototyp',
    '193-solo-stock': '#193 · solo · baza',
    '193-solo-exit': '#193 · solo · prototyp',
    '193-two-stock': '#193 · 2 zawodników · nagranie diagnostyczne',
}
STUDY_194 = {
    '194-solo-inside': '#194 · solo · wejście od wewnętrznej',
    '194-solo-wide': '#194 · solo · wejście szerokie',
    '194-four-stock': '#192 · baza nominalna #194',
    '194-four-nominal': '#194 · 4 zawodników · szeroka prosta',
    '194-four-alternate': '#194 · inne przełożenia · szeroka prosta',
    '194-alternate-stock': '#191 · baza drugiej obsady #194',
}
STUDY_196 = {'196-szybka-prosta': '#196 — szybka prosta, test natywny',
             '199-swobodna': '#199 — wariant A, swobodna optymalizacja',
             '199-solo': '#199 — nauka techniki jazdy solo',
             '199-bazowy-b': '#199 — szeroka prosta, pierwszy wariant B',
             '199-swobodna-inny-stan': '#199 — swobodna linia, zmieniony stan',
             '199-inny-stan': '#199 — szeroka prosta, zmieniony stan',
             '199-drugi-luk': '#199 — szeroka prosta, przejęcie przed drugim łukiem',

             '198-autonomia': '#198 — szybka autonomia, stabilne planowanie',
             '200-solo': '#200 — zoptymalizowana technika jazdy solo',
             '200-b': '#200 — szybsza szeroka prosta',
             '200-inny-start': '#200 — C z przesuniętego startu',
             '200-inne-przelozenie': '#200 — C z przełożeniem 8,4:1',
             '200-potwierdzenie': '#200 — niezależna próba C',
             '198-solo': '#198 — pełne solo od startu',
             '198-czterech': '#198 — czterech zawodników',

             '197-autonomiczna': '#197 — autonomiczna szybka jazda',
             '197-solo-dnf': '#197 — pełne solo, DNF',
}
RACE_ID = re.compile(r'[0-9a-f]{32}\Z')
STATIC_SUFFIXES = {'.html', '.mjs', '.js', '.css', '.json', '.csv', '.gz', '.svg', '.png', '.webp', '.jpg', '.md'}


class RaceService:
    def __init__(self, archive, history, portable=False):
        self.portable = portable
        self.archive = archive.resolve()
        self.history = history.resolve()
        self.study = (ROOT / 'Nagrania' if portable else ROOT.parents[1] / 'outputs/badanie193/stadion').resolve()
        self.study194 = (ROOT / 'Nagrania' if portable else VIEWER / 'recordings/h25').resolve()
        self.geometry = load_gzip(VIEWER / 'recordings/172-s3-k4.json.gz')['geometry']
        self.lock = threading.Lock()
        self.history_lock = threading.Lock()
        self.engine_ready = False

    def entries(self):
        history = [dict(id=key, label=label) for key, (arm, label) in HISTORY.items()
                   if self.history_path(key, arm).is_file()]
        present = []
        if self.archive.is_dir():
            for folder in self.archive.iterdir():
                if RACE_ID.fullmatch(folder.name) and folder.is_dir():
                    try:
                        entry = json.loads((folder / 'recording.json').read_text(encoding='utf-8'))
                        if entry['id'] == folder.name and (folder / 'replay.json.gz').is_file():
                            present.append(entry)
                    except (OSError, ValueError, KeyError):
                        continue
        present.sort(key=lambda e: e['created'], reverse=True)
        study = [dict(id=key, label=label) for key, label in STUDY_193.items()
                 if (self.study / (key + '.json.gz')).is_file()]
        wide = [dict(id=key, label=label) for key, label in STUDY_194.items()
                if (self.study194 / (key + '.json.gz')).is_file()]
        native = [dict(id=key, label=label) for key, label in STUDY_196.items()
                  if (self.study194 / (key + '.json.gz')).is_file()]
        return present + history + study + wide + native

    def history_path(self, key, arm):
        return self.history / ((key if self.portable else arm) + '.json.gz')

    def recording(self, key):
        if RACE_ID.fullmatch(key):
            return self.archive / key / 'replay.json.gz'
        if key in STUDY_193:
            return self.study / (key + '.json.gz')
        if key in STUDY_194 or key in STUDY_196:
            return self.study194 / (key + '.json.gz')
        if key not in HISTORY:
            return None
        arm, _ = HISTORY[key]
        raw = self.history_path(key, arm)
        if not raw.is_file():
            return None
        if self.portable:
            return raw
        with self.history_lock:
            cache = self.archive / '.history'
            cache.mkdir(parents=True, exist_ok=True)
            path = cache / (key+'.json.gz')
            dependencies = [raw, VIEWER / 'export_h25.py', VIEWER / 'recordings/172-s3-k4.json.gz',
                            raw.parent.parent / 'wyniki.json']
            if not path.is_file() or any(p.is_file() and p.stat().st_mtime_ns > path.stat().st_mtime_ns
                                        for p in dependencies):
                temp = path.with_suffix('.temporary')
                write_replay(temp, export(raw, self.geometry, recorded_passes=history_passes(self.history, arm)))
                temp.replace(path)
            return path

    def run(self, ratios, controller="base"):
        if not self.lock.acquire(blocking=False):
            raise RuntimeError('Inny bieg jest już obliczany. Poczekaj na jego zakończenie.')
        try:
            return self._run(ratios, controller)
        finally:
            self.lock.release()

    def engine_command(self):
        if self.portable:
            runtime = ROOT / 'runtime' / 'dotnet'
            dotnet = runtime / 'dotnet.exe'
            engine = ROOT / 'simulator' / ENGINE.name
            if not dotnet.is_file() or not engine.is_file():
                raise ValueError('Paczka jest niekompletna. Rozpakuj ponownie cały plik ZIP.')
            # The bundled muxer resolves the framework from its own directory.
            # Exclude external startup hooks, probing paths and roll-forward overrides.
            excluded = ('DOTNET_ROOT', 'DOTNET_STARTUP_HOOKS', 'DOTNET_ADDITIONAL_DEPS',
                        'DOTNET_SHARED_STORE', 'DOTNET_ROLL_FORWARD', 'DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX')
            env = {name: value for name, value in os.environ.items()
                   if name.upper() not in excluded and not name.upper().startswith('DOTNET_ROOT_')}
            env.update(DOTNET_ROOT=str(runtime), DOTNET_MULTILEVEL_LOOKUP='0')
            return [str(dotnet), str(engine)], env

        dotnet = shutil.which('dotnet')
        if not dotnet:
            raise ValueError('Brak .NET SDK. Zainstaluj .NET 8 SDK, aby obliczać nowe biegi.')
        if not self.engine_ready:
            if not PROJECT.is_file():
                raise ValueError('Brak lokalnego symulatora #191. Uruchom stadion z aktualnego drzewa repozytorium.')
            self.command([sys.executable, str(PROJECT.parent / 'prepare.py')], 'przygotowania symulatora', 30)
            self.command([dotnet, 'build', str(PROJECT), '-c', 'Release', '--verbosity', 'quiet',
                          '-p:NuGetAudit=false', '-p:RestoreLockedMode=true', '--warnaserror'],
                         'budowania symulatora', 120)
            self.engine_ready = True
        return [dotnet, str(ENGINE)], None

    def _run(self, ratios, controller):
        engine, env = self.engine_command()
        self.archive.mkdir(parents=True, exist_ok=True)
        key = uuid.uuid4().hex
        staging = self.archive / ('.'+key)
        staging.mkdir()
        config = staging / 'config.json'
        config.write_text(json.dumps({'Ratios': ratios, 'Controller': controller}), encoding='utf-8')
        self.command([*engine, '--stadium-race', str(config), str(staging)], 'obliczania biegu', 120, env=env)
        result = load_result(staging / 'race.result.json')
        if result.get('Controller', 'base') != controller:
            raise ValueError('Symulator uruchomił inny wariant sterowania.')
        study = '#200 · autonomiczna technika solo' if controller == 'speedway200' else '#194 · szeroka prosta' if controller == 'wide194' else '#192 · FAST50'
        payload = export(staging / 'race.json.gz', self.geometry, study=study)
        write_replay(staging / 'replay.json.gz', payload)
        created = datetime.now(timezone.utc)
        label = f'{created.astimezone():%d.%m %H:%M:%S} · {len(ratios)} zawodników · {"/".join(f"{r:g}" for r in ratios)}:1'
        label += ' · #200' if controller == 'speedway200' else ' · #194' if controller == 'wide194' else ' · #192'
        entry = dict(id=key, created=created.isoformat(), riders=len(ratios), ratios=ratios,
                     label=label, controller=controller, complete=result['Complete'], outcome=result['Outcome'],
                     fingerprint=result['Fingerprint'], nativeSha256=payload['sourceSha256'])
        (staging / 'recording.json').write_text(json.dumps(entry, ensure_ascii=False), encoding='utf-8')
        staging.rename(self.archive / key)
        return entry

    @staticmethod
    def command(args, stage, timeout, env=None):
        try:
            result = subprocess.run(args, cwd=ROOT, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT, timeout=timeout, text=True, errors='replace', env=env)
        except subprocess.TimeoutExpired as exc:
            raise ValueError(f'Przekroczono limit czasu {stage}.') from exc
        if result.returncode:
            print(result.stdout, file=sys.stderr, flush=True)
            raise ValueError(f'Błąd podczas {stage} (kod {result.returncode}). Szczegóły w oknie serwera.')


def load_result(path):
    return json.loads(path.read_text(encoding='utf-8'))


class LocalReplayServer(ThreadingHTTPServer):
    allow_reuse_address = False

    def server_bind(self):
        if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
        super().server_bind()


class ReplayHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    @property
    def host(self):
        return f'127.0.0.1:{self.server.server_port}'

    @property
    def valid_host(self):
        return self.headers.get('Host') == self.host

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('X-Frame-Options', 'DENY')
        super().end_headers()

    def send_json(self, status, value):
        data = json.dumps(value, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        if self.command != 'HEAD':
            self.wfile.write(data)

    def do_POST(self):
        if not self.valid_host or self.headers.get('Origin') != f'http://{self.host}' or \
                self.headers.get('X-Stadium-Request') != 'run':
            self.send_json(403, {'error': 'Uruchamianie biegu jest dostępne tylko ze strony lokalnego stadionu.'})
            return
        if urlsplit(self.path).path != '/api/races':
            self.send_json(404, {'error': 'Nieznana operacja stadionu.'})
            return
        try:
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= 512 or self.headers.get_content_type() != 'application/json':
                raise ValueError('Oczekiwano małej konfiguracji JSON.')
            data = json.loads(self.rfile.read(length))
            if not isinstance(data, dict) or not {'ratios'} <= set(data) <= {'ratios', 'controller'}:
                raise ValueError('Oczekiwano konfiguracji biegu.')
            ratios = data['ratios']
            controller = data.get('controller', 'base')
            if controller not in ('base', 'wide194', 'speedway200'):
                raise ValueError('Nieznany wariant sterowania.')
            if controller == 'speedway200' and ratios != [8]:
                raise ValueError('C #200 wymaga solo z przełożeniem 8:1.')
            if not isinstance(ratios, list) or len(ratios) not in (1, 2, 4) or any(
                    type(r) not in (int, float) or not 8 <= r <= 10 or abs(r*10-round(r*10)) > 1e-8 for r in ratios):
                raise ValueError('Wybierz 2 lub 4 przełożenia od 8:1 do 10:1, z krokiem 0,1.')
        except (ValueError, OverflowError) as exc:
            self.send_json(400, {'error': str(exc)})
            return
        try:
            self.send_json(201, self.server.races.run(ratios, controller))
        except RuntimeError as exc:
            self.send_json(409, {'error': str(exc)})
        except ValueError as exc:
            self.send_json(500, {'error': str(exc)})
        except (OSError, KeyError, IndexError, TypeError) as exc:
            print(f'Błąd zapisu biegu: {exc}', file=sys.stderr, flush=True)
            self.send_json(500, {'error': 'Nie można zapisać lub odczytać telemetrii. Szczegóły w oknie serwera.'})

    def do_GET(self):
        self.read_request()

    def do_HEAD(self):
        self.read_request()

    def read_request(self):
        if not self.valid_host:
            self.send_error(403)
            return
        path = unquote(urlsplit(self.path).path)
        if path == '/api/races':
            self.send_json(200, self.server.races.entries())
            return
        if path.startswith('/api/races/'):
            try:
                source = self.server.races.recording(path.removeprefix('/api/races/'))
                if source is None or not source.is_file():
                    self.send_json(404, {'error': 'Nagranie nie istnieje lub jest niekompletne.'})
                    return
                with source.open('rb') as stream:
                    self.send_response(200)
                    self.send_header('Content-Type', 'application/gzip')
                    self.send_header('Content-Length', str(source.stat().st_size))
                    self.end_headers()
                    if self.command != 'HEAD':
                        shutil.copyfileobj(stream, self.wfile)
            except (BrokenPipeError, ConnectionResetError):
                pass
            except (OSError, ValueError, KeyError, IndexError, TypeError) as exc:
                print(f'Błąd odczytu nagrania: {exc}', file=sys.stderr, flush=True)
                self.send_json(500, {'error': 'Nie można odczytać nagrania. Sprawdź pliki źródłowe.'})
            return
        if path == '/':
            self.send_response(302)
            self.send_header('Location', '/tools/replay-viewer/')
            self.end_headers()
            return
        if path == '/favicon.ico':
            self.send_response(204)
            self.end_headers()
            return
        if not self.allowed_static(path):
            self.send_error(404)
            return
        if 'If-Modified-Since' in self.headers:
            del self.headers['If-Modified-Since']
        if self.command == 'GET' and path in ('/tools/replay-viewer/', '/tools/replay-viewer/index.html'):
            self.server.viewer_opened.set()
        reader = super().do_HEAD if self.command == 'HEAD' else super().do_GET
        reader()

    @staticmethod
    def allowed_static(path):
        parts = path.strip('/').split('/')
        if any(not p or p.startswith('.') or '\\' in p or ':' in p for p in parts):
            return False
        if path == '/tools/replay-viewer/':
            return True
        for prefix in ('tools/replay-viewer', 'docs/experiments'):
            if path.startswith('/'+prefix+'/'):
                directory = ROOT / prefix
                file = ROOT.joinpath(*parts).resolve()
                return directory in file.parents and file.suffix in STATIC_SUFFIXES and file.is_file()
        return False

    def list_directory(self, path):
        self.send_error(404)
        return None


def open_viewer(server, url):
    opened = webbrowser.open(url, new=2)
    if opened and server.viewer_opened.wait(4):
        return
    # Some Windows installations report success without a registered HTTP
    # handler. Try a locally installed browser if nothing opened the viewer.
    if os.name == 'nt':
        for root in dict.fromkeys(os.environ.get(name, '') for name in ('ProgramFiles', 'ProgramFiles(x86)', 'LOCALAPPDATA')):
            if not root:
                continue
            for suffix in ('Google/Chrome/Application/chrome.exe', 'Microsoft/Edge/Application/msedge.exe'):
                browser = Path(root) / suffix
                if browser.is_file():
                    try:
                        subprocess.Popen([str(browser), url], cwd=ROOT, stdin=subprocess.DEVNULL,
                                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                        if server.viewer_opened.wait(6):
                            return
                    except OSError:
                        continue
    if not server.viewer_opened.is_set():
        print('Nie udało się otworzyć przeglądarki automatycznie. Otwórz powyższy adres.', flush=True)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--port', type=int, default=0)
    p.add_argument('--no-browser', action='store_true')
    p.add_argument('--info-file', type=Path)
    p.add_argument('--portable', action='store_true')
    p.add_argument('--archive', type=Path)
    p.add_argument('--history', type=Path)
    args = p.parse_args()
    archive = args.archive or (ROOT / 'Archiwum' if args.portable else VIEWER / '.race-archive')
    history = args.history or (ROOT / 'Nagrania' if args.portable else ROOT.parents[1] / 'outputs/badanie191/pomiar')
    mimetypes.add_type('text/javascript', '.mjs')
    # New setup uses Python 3.9; keep the same interpreter for native preparation.
    with LocalReplayServer(('127.0.0.1', args.port), ReplayHandler) as server:
        server.races = RaceService(archive, history, args.portable)
        server.viewer_opened = threading.Event()
        url = f'http://127.0.0.1:{server.server_port}/tools/replay-viewer/'
        print(f'\nStadion: {url}\nArchiwum: {archive.resolve()}\nZatrzymanie: Ctrl+C.\n', flush=True)
        if args.info_file:
            args.info_file.write_text(json.dumps(dict(url=url, port=server.server_port, pid=os.getpid(), bind='127.0.0.1')),
                                      encoding='utf-8')
        if not args.no_browser:
            threading.Thread(target=open_viewer, args=(server, url), daemon=True).start()
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            print('\nSerwer zatrzymany.')


if __name__ == '__main__':
    main()

