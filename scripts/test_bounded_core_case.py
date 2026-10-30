"""Small synthetic stream and launch-gate checks; never execute a Core case."""
import importlib.util
import json
import os
import socket
from pathlib import Path
import tempfile
import threading
import time
import types
import unittest
from unittest.mock import Mock, patch

spec = importlib.util.spec_from_file_location("bounded", Path(__file__).with_name("run-bounded-core-case.py"))
bounded = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bounded)


class BoundedCoreOutputTests(unittest.TestCase):
    def test_inventory_accounts_directories_fifo_socket_and_allocated_blocks(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "dir").mkdir()
            (root / "dir/data").write_bytes(b"X")
            os.mkfifo(root / "pipe", 0o600)
            with socket.socket(socket.AF_UNIX) as peer:
                peer.bind(str(root / "peer"))
                actual = bounded.inventory(root)
                paths = [root, root / "dir", root / "dir/data", root / "pipe", root / "peer"]
                self.assertEqual(1, actual["RegularFiles"])
                self.assertEqual(2, actual["Directories"])
                self.assertEqual(1, actual["Fifos"])
                self.assertEqual(1, actual["Sockets"])
                self.assertEqual(5, actual["Entries"])
                self.assertEqual(1, actual["LogicalBytes"])
                self.assertEqual(sum(p.lstat().st_blocks * 512 for p in paths), actual["AllocatedBytes"])

    def test_sparse_file_is_charged_logically_and_physically_separately(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            with (root / "sparse").open("wb") as stream:
                stream.truncate(1024 * 1024)
            actual = bounded.inventory(root)
            self.assertEqual(1024 * 1024, actual["LogicalBytes"])
            self.assertEqual(sum(p.lstat().st_blocks * 512 for p in [root, root / "sparse"]),
                             actual["AllocatedBytes"])

    def test_linked_directory_is_rejected_without_traversal(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "link").symlink_to("/proc", target_is_directory=True)
            with self.assertRaisesRegex(RuntimeError, "linked"):
                bounded.inventory(root)

    def settled_reader(self):
        return types.SimpleNamespace(done=Mock(), thread=Mock(ident=1, is_alive=Mock(return_value=False)),
                                     errors=[], eof=True, bytes=6)

    def test_missing_fixture_keeps_primary_and_writes_unknown_not_zero(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            original = RuntimeError("PUBLIC_PRIMARY")
            receipt = {"Outcome": "FailedOrUnconfirmed"}
            with self.assertRaises(BaseExceptionGroup) as caught:
                bounded.settle_case(receipt, self.settled_reader(), root / "missing", root / "receipt.json",
                                    time.monotonic(), original)
            self.assertIs(original, caught.exception.exceptions[0])
            saved = json.loads((root / "receipt.json").read_text())
            self.assertIsNone(saved["FixtureRetainedBytes"])
            self.assertTrue(saved["ReaderJoined"])

    def test_receipt_failure_preserves_original_without_false_saved_success(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            original = RuntimeError("PUBLIC_PRIMARY")
            receipt = {"Outcome": "FailedOrUnconfirmed"}
            with patch.object(bounded, "save", side_effect=OSError("PUBLIC_RECEIPT_FAILURE")):
                with self.assertRaises(BaseExceptionGroup) as caught:
                    bounded.settle_case(receipt, self.settled_reader(), root, root / "receipt.json",
                                        time.monotonic(), original)
            self.assertIs(original, caught.exception.exceptions[0])
            self.assertIsInstance(caught.exception.exceptions[1], OSError)
            self.assertEqual("Unconfirmed", receipt["ReceiptWriteStatus"])
            self.assertFalse((root / "receipt.json").exists())

    def test_receipt_write_and_close_errors_survive_together(self):
        target = Mock()
        target.open.return_value.write.side_effect = RuntimeError("PUBLIC_WRITE")
        target.open.return_value.close.side_effect = OSError("PUBLIC_CLOSE")
        with self.assertRaises(BaseExceptionGroup) as caught:
            bounded.save(target, {"Synthetic": True})
        self.assertIsInstance(caught.exception.exceptions[0], RuntimeError)
        self.assertIsInstance(caught.exception.exceptions[1], OSError)

    def test_writable_uid_map_alone_cannot_enable_unreviewed_reservation(self):
        pub = Mock()
        with patch.object(bounded.os, "statvfs", return_value=types.SimpleNamespace(f_flag=0)):
            with patch.object(Path, "mkdir") as mkdir:
                with self.assertRaisesRegex(RuntimeError, "resource proof is incomplete"):
                    bounded.execute(pub)
                mkdir.assert_not_called()
                pub.execute.assert_not_called()

    def stream(self, root):
        fifo = root / "fifo"
        os.mkfifo(fifo, 0o600)
        return bounded.TrxCapture(fifo, root / "captured")

    def test_exact_binary_prefix_and_reader_eof_are_preserved(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(bounded, "STREAM", 8):
            root = Path(folder)
            reader = self.stream(root)
            reader.thread.start()
            with (root / "fifo").open("wb", buffering=0) as writer:
                writer.write(b"\xff\x00abcdef")
            reader.finish()
            self.assertEqual(b"\xff\x00abcdef", reader.target.read_bytes())
            self.assertTrue(reader.eof)
            self.assertFalse(reader.thread.is_alive())

    def test_no_writer_cannot_be_reported_as_complete_empty_trx(self):
        with tempfile.TemporaryDirectory() as folder:
            reader = self.stream(Path(folder))
            reader.thread.start()
            with self.assertRaisesRegex(RuntimeError, "incomplete"):
                reader.finish()
            self.assertEqual(0, reader.bytes)
            self.assertFalse(reader.thread.is_alive())

    def test_overflow_retains_only_real_eight_byte_prefix_and_fails(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(bounded, "STREAM", 8):
            root = Path(folder)
            reader = self.stream(root)
            reader.thread.start()
            with (root / "fifo").open("wb", buffering=0) as writer:
                writer.write(b"PUBLIC_OVERFLOW_BYTES")
            with self.assertRaisesRegex(RuntimeError, "incomplete"):
                reader.finish()
            self.assertEqual(b"PUBLIC_O", reader.target.read_bytes())
            self.assertIn("exceeds reservation", reader.errors[0])

    def test_open_writer_after_settlement_is_not_eof(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            reader = self.stream(root)
            reader.thread.start()
            writer = os.open(root / "fifo", os.O_WRONLY)
            try:
                os.write(writer, b"PUBLIC")
                deadline = time.monotonic() + 1
                while not reader.bytes and time.monotonic() < deadline:
                    time.sleep(.01)
                self.assertEqual(6, reader.bytes)
                with self.assertRaisesRegex(RuntimeError, "incomplete"):
                    reader.finish()
                self.assertFalse(reader.eof)
                self.assertIn("writer still present", reader.errors[0])
            finally:
                os.close(writer)

    def test_readonly_uid_map_stops_before_any_execution_or_new_directory(self):
        pub = Mock()
        with patch.object(bounded.os, "statvfs", return_value=types.SimpleNamespace(f_flag=os.ST_RDONLY)):
            with patch.object(Path, "mkdir") as mkdir:
                with self.assertRaisesRegex(RuntimeError, "no child started"):
                    bounded.execute(pub)
                mkdir.assert_not_called()
                pub.execute.assert_not_called()


if __name__ == "__main__":
    unittest.main()
