"""Verify a fresh source snapshot and package a Windows release. Python 3.10+, stdlib only."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
from uuid import uuid4
import xml.etree.ElementTree as ET
import zipfile


class VerificationError(RuntimeError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise VerificationError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def git(project: Path, *arguments: str) -> bytes:
    return subprocess.check_output(['git', '-C', str(project), *arguments], stderr=subprocess.PIPE)


def source_files(project: Path) -> list[Path]:
    paths = git(project, 'ls-files', '-z', '--cached', '--others', '--exclude-standard').split(b'\0')
    generated = {'library', 'logs', 'builds', 'build', 'temp', 'obj', 'usersettings', '.git', '__pycache__'}
    result = []
    for value in sorted(set(paths)):
        if not value:
            continue
        relative = Path(os.fsdecode(value))
        require(not relative.is_absolute() and '..' not in relative.parts, f'Invalid source path: {relative}')
        if relative.parts[0].casefold() in generated or '__pycache__' in relative.parts or relative.suffix == '.pyc':
            continue
        path = project / relative
        require(not path.is_symlink() and path.resolve() == path.absolute(), f'Source links are unsupported: {relative}')
        if not path.exists():  # A tracked deletion in the current working tree.
            continue
        require(path.is_file(), f'Source must be a file (initialize submodules first): {relative}')
        result.append(relative)
    return result


def snapshot(project: Path, destination: Path) -> dict:
    require(not destination.exists(), f'Snapshot already exists: {destination}')
    files = source_files(project)
    names = {path.as_posix() for path in files}
    required = {'Assets/Scenes/Bootstrap.unity', 'Assets/Scenes/Menu.unity', 'Assets/Scenes/Game.unity',
                'Assets/Game/Resources/GameConfig.asset', 'Assets/Game/Resources/Shockwave.asset',
                'Assets/Game/Resources/ArenaMaterial.mat', 'Packages/manifest.json', 'Packages/packages-lock.json',
                'Packages/com.scellecs.morpeh/package.json', 'ProjectSettings/ProjectVersion.txt', 'tools/verify.py'}
    require(required <= names, f'Missing source files: {sorted(required - names)}')
    destination.mkdir(parents=True)
    records = []
    for relative in files:
        source, target = project / relative, destination / relative
        digest = sha256(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        require(sha256(target) == digest, f'Source changed while copying: {relative}')
        records.append({'path': relative.as_posix(), 'bytes': target.stat().st_size, 'sha256': digest})
    require(files == source_files(project) and all(sha256(project / item['path']) == item['sha256'] for item in records),
            'Sources changed during snapshot creation; rerun after saving edits.')
    fingerprint = hashlib.sha256(json.dumps(records, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    return {'gitHead': git(project, 'rev-parse', 'HEAD').decode().strip(),
            'workingTreeDirty': bool(git(project, 'status', '--porcelain', '--untracked-files=all').strip()),
            'treeSha256': fingerprint, 'files': records}


def read_unity_log(path: Path) -> str:
    # Native Windows helpers can append OEM-encoded diagnostics to Unity's UTF-8 log.
    # Keep ASCII version/error markers intact; machine-readable XML/JSON remains strict.
    return path.read_text(encoding='utf-8', errors='replace')


def read_test_result(path: Path) -> dict:
    require(path.is_file(), f'Test runner did not produce XML: {path}')
    root = ET.parse(path).getroot()
    cases = root.findall('.//test-case')
    passed = int(root.get('passed', '0'))
    require(root.tag == 'test-run' and root.get('result') == 'Passed' and len(cases) > 0 and passed == len(cases)
            and all(case.get('result') == 'Passed' for case in cases)
            and all(int(root.get(key, '0')) == 0 for key in ('failed', 'skipped', 'inconclusive')),
            f'Test run failed, skipped cases or ran no tests: {path}')
    return {'passed': passed, 'durationSeconds': float(root.get('duration', '0').replace(',', '.'))}


def create_archive(root: Path, paths: list[Path], output: Path) -> dict:
    require(not output.exists(), f'Archive already exists: {output}')
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for relative in sorted(paths):
            require(not relative.is_absolute() and '..' not in relative.parts, f'Invalid archive path: {relative}')
            archive.write(root / relative, relative.as_posix())
    with zipfile.ZipFile(output) as archive:
        require(archive.testzip() is None, f'ZIP integrity check failed: {output}')
    return {'file': output.name, 'bytes': output.stat().st_size, 'sha256': sha256(output), 'files': len(paths)}


def package_player(build: Path, output: Path) -> dict:
    paths = [path.relative_to(build) for path in build.rglob('*') if path.is_file()
             and not any('DoNotShip' in part or 'ButDontShipItWithYourGame' in part for part in path.parts)]
    names = {path.as_posix() for path in paths}
    required = {'Arena.exe', 'UnityPlayer.dll', 'Arena_Data/Managed/Game.ECS.dll',
                'Arena_Data/Managed/Game.Application.dll', 'Arena_Data/Managed/Game.UiModel.dll'}
    require(required <= names, f'Incomplete Windows player: {sorted(required - names)}')
    require(not any('Game.Tests.' in name for name in names), 'Test assemblies found in the release player.')
    return create_archive(build, paths, output)


def frame_count(directory: Path) -> int:
    paths = sorted(directory.glob('frame-*.png'))
    require(bool(paths) and [p.name for p in paths] == [f'frame-{i:05d}.png' for i in range(len(paths))],
            'Capture is empty or has missing frames.')
    return len(paths)


def start_process(arguments: list[str], cwd: Path, stream, environment: dict | None = None) -> subprocess.Popen:
    options = {}
    if os.name == 'nt':
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
        options['startupinfo'] = startup
    return subprocess.Popen(arguments, cwd=cwd, env=environment, stdout=stream, stderr=subprocess.STDOUT, **options)


def stop_owned_process(process: subprocess.Popen) -> None:
    if process.poll() is not None:
        return
    if os.name == 'nt':
        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    else:
        process.terminate()
    process.wait(timeout=30)


def run_stage(label: str, arguments: list[str], cwd: Path, log: Path, timeout: int, environment: dict | None = None) -> dict:
    print(f'[{label}] started; log: {log}', flush=True)
    started = time.monotonic()
    with log.open('w', encoding='utf-8') as stream:
        process = start_process(arguments, cwd, stream, environment)
        next_update = 30
        try:
            while process.poll() is None:
                elapsed = time.monotonic() - started
                require(elapsed < timeout, f'{label} exceeded {timeout}s; see {log}')
                if elapsed >= next_update:
                    print(f'[{label}] running: {elapsed:.0f}s', flush=True)
                    next_update += 30
                time.sleep(0.5)
            require(process.returncode == 0, f'{label} exited with code {process.returncode}; see {log}')
        finally:
            stop_owned_process(process)
    result = {'durationSeconds': round(time.monotonic() - started, 3), 'exitCode': process.returncode}
    print(f'[{label}] passed in {result["durationSeconds"]}s', flush=True)
    return result


def smoke_player(player: Path, directory: Path, timeout: int) -> dict:
    import ctypes
    from ctypes import wintypes
    print('[player-smoke] starting graphical player', flush=True)
    user32 = ctypes.WinDLL('user32', use_last_error=True)
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user32.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
    user32.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
    started = time.monotonic()
    log = directory / 'player-smoke.log'
    with (directory / 'player-smoke-process.log').open('w', encoding='utf-8') as stream:
        process = start_process([str(player), '-logFile', str(log)], player.parent, stream)
        try:
            time.sleep(min(8, timeout))
            require(time.monotonic() - started < timeout, f'Player smoke exceeded {timeout}s; see {log}')
            require(process.poll() is None, f'Player exited before the smoke check; see {log}')
            closed = []

            @callback_type
            def close(window, _):
                owner = wintypes.DWORD()
                user32.GetWindowThreadProcessId(window, ctypes.byref(owner))
                if owner.value == process.pid:
                    closed.append(window)
                    user32.PostMessageW(window, 0x0010, 0, 0)  # WM_CLOSE, only this owned process.
                return True

            user32.EnumWindows(close, 0)
            require(bool(closed), 'Graphical player did not create a window.')
            remaining = max(0.1, timeout - (time.monotonic() - started))
            require(process.wait(timeout=min(30, remaining)) == 0, f'Player failed on shutdown; see {log}')
        finally:
            stop_owned_process(process)
    text = read_unity_log(log)
    require('GfxDevice:' in text and 'Input System module state changed to: Shutdown.' in text,
            f'Player initialization/shutdown markers missing; see {log}')
    require(not re.search(r'\b\w*Exception:|\b(?:Error|Assertion failed):', text), f'Player logged an error; see {log}')
    print('[player-smoke] passed', flush=True)
    return {'durationSeconds': round(time.monotonic() - started, 3), 'exitCode': 0}


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--unity', type=Path, help='Unity.exe; default: Hub installation matching ProjectVersion.txt')
    parser.add_argument('--output', type=Path, help='Parent for a new timestamped run; default: Logs/verification')
    parser.add_argument('--capture', action='store_true', help='Capture PlayMode frames and encode H.264 (requires FFmpeg).')
    parser.add_argument('--ffmpeg', type=Path, help='FFmpeg executable; otherwise searched on PATH.')
    parser.add_argument('--timeout', type=int, default=1200, help='Timeout per stage in seconds (default: 1200).')
    return parser.parse_args()


def main() -> int:
    args = parse_arguments()
    require(os.name == 'nt', 'The Windows build verification currently requires Windows and an interactive desktop for PlayMode.')
    require(args.timeout > 0, '--timeout must be positive.')
    project = args.project.resolve()
    match = re.search(r'^m_EditorVersion: (.+)$', (project / 'ProjectSettings/ProjectVersion.txt').read_text(), re.MULTILINE)
    require(match is not None, 'ProjectVersion.txt does not specify a Unity version.')
    version = match[1].strip()
    unity = (args.unity or Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Unity/Hub/Editor' / version / 'Editor/Unity.exe').resolve()
    require(unity.is_file(), f'Unity {version} was not found: {unity}; specify --unity.')
    ffmpeg = str(args.ffmpeg.resolve()) if args.ffmpeg else shutil.which('ffmpeg')
    require(not args.capture or (ffmpeg and Path(ffmpeg).is_file()), '--capture requires FFmpeg on PATH or --ffmpeg.')
    output = (args.output or project / 'Logs/verification').resolve()
    if output.is_relative_to(project):
        relative = output.relative_to(project)
        require(bool(relative.parts) and relative.parts[0].casefold() in {'logs', 'builds'},
                '--output inside the source project must be under Logs or Builds to avoid including the run in its own snapshot.')
    run = output / (datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + uuid4().hex[:8])
    # A fresh directory is never reused or recursively deleted by this workflow.
    run.mkdir(parents=True, exist_ok=False)
    report = {'status': 'running', 'startedUtc': datetime.now(timezone.utc).isoformat(), 'unityVersion': version,
              'cleanLibrary': True, 'captureRequested': args.capture, 'stages': {}, 'artifacts': {}}
    write_json(run / 'verification.json', report)
    print(f'Verification directory: {run}', flush=True)
    try:
        work = run / 'project'
        sources = snapshot(project, work)
        write_json(run / 'source-manifest.json', sources)
        report['sourceTreeSha256'] = sources['treeSha256']
        report['gitHead'] = sources['gitHead']
        report['workingTreeDirty'] = sources['workingTreeDirty']
        require(not (work / 'Library').exists(), 'Snapshot unexpectedly contains Library.')
        report['artifacts']['sources'] = create_archive(work, [Path(item['path']) for item in sources['files']], run / 'Arena-Sources.zip')
        report['stages']['toolTests'] = run_stage('tool-tests', [sys.executable, '-m', 'unittest', 'discover', '-s', 'tools/tests'],
                                               work, run / 'tool-tests.log', args.timeout)
        base = [str(unity), '-projectPath', str(work)]
        environment = os.environ.copy()
        environment.pop('ARENA_CAPTURE_DIR', None)
        if args.capture:
            environment['ARENA_CAPTURE_DIR'] = str(run / 'frames')
        for platform in ('EditMode', 'PlayMode'):
            label = platform.lower()
            arguments = base + (['-batchmode', '-nographics'] if platform == 'EditMode' else [])
            arguments += ['-runTests', '-testPlatform', platform, '-testResults', str(run / f'{label}.xml'), '-logFile', str(run / f'{label}.log')]
            report['stages'][label] = run_stage(label, arguments, work, run / f'{label}-process.log', args.timeout, environment)
            suite = read_test_result(run / f'{label}.xml')
            report['stages'][label].update({'passed': suite['passed'], 'testDurationSeconds': suite['durationSeconds']})
            require(f'Initialize engine version: {version} (' in read_unity_log(run / f'{label}.log'),
                    f'Unity version does not match ProjectVersion.txt ({version}).')
            write_json(run / 'verification.json', report)
        player = run / 'Windows/Arena.exe'
        report['stages']['build'] = run_stage('build', base + ['-batchmode', '-nographics', '-quit', '-executeMethod',
            'Game.Editor.BuildCommands.BuildWindows', '-arena-build-output', str(player), '-logFile', str(run / 'build.log')],
            work, run / 'build-process.log', args.timeout, environment)
        require(player.is_file(), 'Build did not create Arena.exe.')
        report['stages']['benchmark'] = run_stage('benchmark', [str(player), '-batchmode', '-nographics', '-arena-benchmark',
            '-arena-benchmark-output', str(run / 'benchmark.json'), '-logFile', str(run / 'benchmark.log')],
            player.parent, run / 'benchmark-process.log', args.timeout)
        benchmark = json.loads((run / 'benchmark.json').read_text(encoding='utf-8'))
        require(len(benchmark.get('cases', [])) == 6 and benchmark.get('development') is False, 'Incomplete release benchmark.')
        report['stages']['playerSmoke'] = smoke_player(player, run, args.timeout)
        report['artifacts']['player'] = package_player(player.parent, run / 'Arena-Windows.zip')
        if args.capture:
            count = frame_count(run / 'frames')
            video = run / 'Arena-playthrough.mp4'
            report['stages']['video'] = run_stage('video', [ffmpeg, '-hide_banner', '-loglevel', 'error', '-framerate', '30', '-i',
                str(run / 'frames/frame-%05d.png'), '-vf', 'scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2', '-c:v', 'libx264', '-preset', 'medium', '-crf', '23',
                '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(video)], work, run / 'video.log', args.timeout)
            run_stage('video-decode', [ffmpeg, '-hide_banner', '-loglevel', 'error', '-i', str(video), '-f', 'null', '-'],
                      work, run / 'video-decode.log', args.timeout)
            report['artifacts']['video'] = {'file': video.name, 'bytes': video.stat().st_size, 'sha256': sha256(video),
                                            'frames': count, 'durationSeconds': count / 30, 'fps': 30, 'width': 1920, 'height': 1080}
        report['status'] = 'passed'
        print(f'Verification passed. Artifacts and reports: {run}', flush=True)
        return 0
    except BaseException as error:
        report['status'] = 'failed'
        report['error'] = str(error) or type(error).__name__
        raise
    finally:
        report['finishedUtc'] = datetime.now(timezone.utc).isoformat()
        write_json(run / 'verification.json', report)


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (VerificationError, OSError, subprocess.SubprocessError, ValueError, ET.ParseError) as error:
        print(f'Verification failed: {error}', file=sys.stderr)
        sys.exit(1)
