from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from tools import verify


class VerificationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='arena verify tests ')
        self.root = Path(self.directory.name).resolve()

    def tearDown(self):
        self.assertEqual(self.root, Path(self.directory.name).resolve())
        self.assertEqual(self.root.parent, Path(tempfile.gettempdir()).resolve())
        self.assertTrue(self.root.name.startswith('arena verify tests '))
        self.directory.cleanup()

    def write(self, path, text=''):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding='utf-8')
        return target

    def test_zero_exit_without_xml_is_not_a_success(self):
        with self.assertRaises(verify.VerificationError):
            verify.read_test_result(self.root / 'missing.xml')

    def test_native_windows_log_encoding_preserves_version_and_error_markers(self):
        path = self.root / 'unity.log'
        path.write_bytes(b'Initialize engine version: 6000.3.6f1 (build)\nNative: \x88\xa4\xa5\xe2\nIOException: failure\n')
        text = verify.read_unity_log(path)
        self.assertIn('Initialize engine version: 6000.3.6f1 (', text)
        self.assertRegex(text, r'\b\w*Exception:')

    def test_empty_or_skipped_or_failed_suite_cannot_pass(self):
        for body in [
            '<test-run result="Passed" passed="0"/>',
            '<test-run result="Passed" passed="1" skipped="1"><test-case result="Passed"/><test-case result="Skipped"/></test-run>',
            '<test-run result="Failed" passed="0" failed="1"><test-case result="Failed"/></test-run>',
        ]:
            with self.subTest(body=body), self.assertRaises(verify.VerificationError):
                verify.read_test_result(self.write('results.xml', body))

    def test_complete_results_accept_localized_duration_without_fixed_test_count(self):
        path = self.write('results.xml', '<test-run result="Passed" passed="2" duration="1,25"><test-suite><test-case result="Passed"/><test-case result="Passed"/></test-suite></test-run>')
        self.assertEqual(verify.read_test_result(path), {'passed': 2, 'durationSeconds': 1.25})

    def test_git_snapshot_list_includes_unsaved_to_git_work_and_excludes_local_caches(self):
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        self.write('.gitignore', 'Library/\nLogs/\n')
        self.write('Assets/tracked.txt', 'old')
        self.write('Assets/deleted.txt', 'remove')
        subprocess.run(['git', '-C', str(self.root), 'add', '.'], check=True)
        self.write('Assets/tracked.txt', 'new content')
        (self.root / 'Assets/deleted.txt').unlink()
        self.write('Assets/new file.txt', 'untracked')
        self.write('Library/cache.txt')
        self.write('Logs/test.xml')
        self.assertEqual(set(verify.source_files(self.root)), {Path('.gitignore'), Path('Assets/tracked.txt'), Path('Assets/new file.txt')})

    def test_archive_preserves_nested_paths_and_does_not_overwrite_previous_delivery(self):
        self.write('Assets/path with spaces/content.txt', 'saved content')
        output = self.root / 'sources.zip'
        report = verify.create_archive(self.root, [Path('Assets/path with spaces/content.txt')], output)
        with zipfile.ZipFile(output) as archive:
            self.assertEqual(archive.read('Assets/path with spaces/content.txt'), b'saved content')
        self.assertEqual(report['sha256'], verify.sha256(output))
        with self.assertRaises(verify.VerificationError):
            verify.create_archive(self.root, [], output)

    def test_incomplete_player_is_rejected_before_packaging(self):
        self.write('Windows/Arena.exe')
        with self.assertRaises(verify.VerificationError):
            verify.package_player(self.root / 'Windows', self.root / 'player.zip')
        self.assertFalse((self.root / 'player.zip').exists())

    def test_player_packaging_excludes_debug_folders_and_rejects_test_assemblies(self):
        files = ['Arena.exe', 'UnityPlayer.dll', 'Arena_Data/Managed/Game.ECS.dll',
                 'Arena_Data/Managed/Game.Application.dll', 'Arena_Data/Managed/Game.UiModel.dll']
        for name in files:
            self.write('Windows/' + name, name)
        self.write('Windows/Arena_BackUpThisFolder_ButDontShipItWithYourGame/keep.txt')
        self.write('Windows/Arena_BurstDebugInformation_DoNotShip/debug.txt')
        verify.package_player(self.root / 'Windows', self.root / 'player.zip')
        with zipfile.ZipFile(self.root / 'player.zip') as archive:
            self.assertEqual(set(archive.namelist()), set(files))
        self.write('Windows/Arena_Data/Managed/Game.Tests.EditMode.dll')
        with self.assertRaises(verify.VerificationError):
            verify.package_player(self.root / 'Windows', self.root / 'bad.zip')

    def test_capture_gap_or_empty_capture_cannot_be_encoded_as_complete(self):
        frames = self.root / 'frames'
        frames.mkdir()
        with self.assertRaises(verify.VerificationError):
            verify.frame_count(frames)
        self.write('frames/frame-00000.png')
        self.write('frames/frame-00002.png')
        with self.assertRaises(verify.VerificationError):
            verify.frame_count(frames)
        self.write('frames/frame-00001.png')
        self.assertEqual(verify.frame_count(frames), 3)

    def test_failed_child_stage_stops_the_workflow(self):
        with self.assertRaisesRegex(verify.VerificationError, 'code 7'):
            verify.run_stage('expected-failure', [sys.executable, '-c', 'raise SystemExit(7)'], self.root, self.root / 'failure.log', 10)

    def test_timeout_terminates_the_owned_process(self):
        with patch.object(verify, 'stop_owned_process', wraps=verify.stop_owned_process) as cleanup:
            with self.assertRaisesRegex(verify.VerificationError, 'exceeded'):
                verify.run_stage('expected-timeout', [sys.executable, '-c', 'import time; time.sleep(60)'], self.root, self.root / 'timeout.log', 1)
            self.assertIsNotNone(cleanup.call_args.args[0].poll())


if __name__ == '__main__':
    unittest.main()
