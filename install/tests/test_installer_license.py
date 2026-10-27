import codecs
from pathlib import Path
import unittest


class InstallerLicenseTests(unittest.TestCase):
    def test_license_has_explicit_unicode_encoding_and_crlf_lines(self):
        data = (Path(__file__).parents[1] / "licence.txt").read_bytes()
        self.assertTrue(data.startswith((codecs.BOM_UTF8, codecs.BOM_UTF16_LE)),
                        "NSIS license must identify its Unicode encoding independently of the system code page")
        encoding = "utf-16" if data.startswith(codecs.BOM_UTF16_LE) else "utf-8-sig"
        text = data.decode(encoding)
        self.assertIn("消极压制 最终用户许可协议", text)
        self.assertIn("MIT", text)
        self.assertNotIn("\ufffd", text)
        self.assertNotIn("\r", text.replace("\r\n", ""))
        self.assertNotIn("\n", text.replace("\r\n", ""))


if __name__ == "__main__":
    unittest.main()
