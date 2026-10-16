import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError, URLError
import xml.etree.ElementTree as ET

import github_stats as stats


def snapshot(day="2026-10-16", views=None):
    return {"repository": "Yadek/glow", "date": day, "observed_at": day + "T12:00:00Z",
            "stars": 3, "forks": 2, "downloads": 112,
            "traffic": {"views": views, "clones": None},
            "status": {"views": "ok" if views is not None else "unavailable", "clones": "unavailable"}}


class StatsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        Path("artifacts").mkdir(exist_ok=True)

    def test_releases_and_assets_paginate(self):
        def response(url, token):
            if url.endswith("/repos/Yadek/glow"):
                return {"stargazers_count": 3, "forks_count": 2}
            if "/releases?" in url:
                if url.endswith("&page=1"):
                    return [{"id": i, "assets_url": f"https://api.github.com/assets/{i}"} for i in range(100)]
                return [{"id": 100, "assets_url": "https://api.github.com/assets/100"}]
            if "/assets/0?" in url:
                return [{"download_count": 1}] * 100 if url.endswith("&page=1") else [{"download_count": 7}]
            if "/assets/100?" in url:
                return [{"download_count": 5}]
            return []
        with patch.object(stats, "get_json", side_effect=response):
            result = stats.fetch_snapshot("Yadek/glow", "fake", None)
        self.assertEqual((result["stars"], result["forks"], result["downloads"]), (3, 2, 112))

    def test_overlap_replaces_day(self):
        first = snapshot(views=[{"timestamp": "2026-10-15T00:00:00Z", "count": 4, "uniques": 2}])
        history = stats.merge_history(stats.empty_history("Yadek/glow"), first)
        second = snapshot(views=[{"timestamp": "2026-10-15T00:00:00Z", "count": 7, "uniques": 3}])
        result = stats.merge_history(history, second)
        self.assertEqual(result["traffic"]["views"]["2026-10-15"], {"count": 7, "uniques": 3})
        self.assertEqual(history["traffic"]["views"]["2026-10-15"]["count"], 4)

    def test_missing_traffic_preserves_history(self):
        history = stats.merge_history(stats.empty_history("Yadek/glow"), snapshot(views=[
            {"timestamp": "2026-10-15T00:00:00Z", "count": 4, "uniques": 2}]))
        result = stats.merge_history(history, snapshot())
        self.assertEqual(result["traffic"]["views"]["2026-10-15"]["count"], 4)
        self.assertEqual(result["collection_status"]["views"]["status"], "unavailable")

    def test_same_day_replaces_snapshot(self):
        history = stats.merge_history(stats.empty_history("Yadek/glow"), snapshot())
        changed = snapshot()
        changed["downloads"] = 100  # removed assets can reduce the observable total
        result = stats.merge_history(history, changed)
        self.assertEqual(len(result["snapshots"]), 1)
        self.assertEqual(result["snapshots"]["2026-10-16"]["downloads"], 100)

    def test_network_error_does_not_write(self):
        with tempfile.TemporaryDirectory(dir="artifacts") as folder:
            path = Path(folder) / "history.json"
            original = stats.empty_history("Yadek/glow")
            path.write_text(json.dumps(original), encoding="utf-8")
            before = path.read_bytes()
            with patch.object(stats, "fetch_snapshot", side_effect=URLError("offline")):
                with self.assertRaises(URLError):
                    stats.update("Yadek/glow", Path(folder), "fake", None)
            self.assertEqual(path.read_bytes(), before)

    def test_corrupt_history_aborts(self):
        with tempfile.TemporaryDirectory(dir="artifacts") as folder:
            path = Path(folder) / "history.json"
            path.write_text("broken", encoding="utf-8")
            with self.assertRaises(ValueError):
                stats.update("Yadek/glow", Path(folder), "fake", None)
            self.assertEqual(path.read_text(), "broken")

    def test_single_snapshot_is_one_point_per_metric(self):
        history = stats.merge_history(stats.empty_history("Yadek/glow"), snapshot())
        svg = ET.fromstring(stats.render_svg(history))
        self.assertEqual(len(svg.findall(".//{http://www.w3.org/2000/svg}circle")), 3)

    def test_gaps_are_not_zero(self):
        history = stats.empty_history("Yadek/glow")
        for day in ("2026-10-14", "2026-10-16"):
            history = stats.merge_history(history, snapshot(day))
        svg = ET.fromstring(stats.render_svg(history))
        self.assertEqual(len(svg.findall(".//{http://www.w3.org/2000/svg}polyline")), 0)
        self.assertEqual(history["traffic"]["clones"], {})

    def test_forbidden_traffic_is_unavailable(self):
        def response(url, token):
            if "/traffic/" in url:
                raise HTTPError(url, 403, "forbidden", {}, None)
            if "/releases?" in url:
                return []
            return {"stargazers_count": 3, "forks_count": 2}
        with patch.object(stats, "get_json", side_effect=response):
            result = stats.fetch_snapshot("Yadek/glow", "fake", "traffic")
        self.assertIsNone(result["traffic"]["views"])
        self.assertEqual(result["status"]["views"], "unavailable")

    def test_invalid_history_schema_is_rejected(self):
        bad = stats.empty_history("Yadek/glow")
        bad["snapshots"]["yesterday"] = {"stars": 0}
        with self.assertRaises(ValueError):
            stats.merge_history(bad, snapshot())


if __name__ == "__main__":
    Path("artifacts").mkdir(exist_ok=True)
    unittest.main()
