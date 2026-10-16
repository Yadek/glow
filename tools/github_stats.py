"""Collect observed GitHub metrics; keep UTC history without invented samples."""
import argparse
import copy
from datetime import date, datetime, timezone
from html import escape
import json
import os
from pathlib import Path
import re
import sys
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

API = "https://api.github.com"


def get_json(url, token):
    if not url.startswith(API + "/"):
        raise ValueError("Unexpected API host")
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "Glow-repository-statistics",
               "X-GitHub-Api-Version": "2026-03-10"}
    if token:
        headers["Authorization"] = "Bearer " + token
    with urlopen(Request(url, headers=headers), timeout=20) as response:
        payload = response.read(20 * 1024 * 1024 + 1)
        if len(payload) > 20 * 1024 * 1024:
            raise ValueError("API response too large")
        return json.loads(payload)


def pages(url, token):
    for page in range(1, 10001):
        items = get_json(f"{url}?per_page=100&page={page}", token)
        if not isinstance(items, list):
            raise ValueError("Expected an API list")
        yield from items
        if len(items) < 100:
            return
    raise ValueError("Pagination limit exceeded")


def fetch_snapshot(repository, token, traffic_token):
    root = f"{API}/repos/{repository}"
    repo = get_json(root, token)
    downloads = 0
    for release in pages(root + "/releases", token):
        # Draft assets aren't public downloads. Include stable releases and prereleases.
        if release.get("draft", False):
            continue
        for asset in pages(release["assets_url"], token):
            downloads += number(asset["download_count"])
    now = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    result = {"repository": repository, "date": now[:10], "observed_at": now,
              "stars": number(repo["stargazers_count"]), "forks": number(repo["forks_count"]),
              "downloads": downloads, "traffic": {}, "status": {}}
    for metric in ("views", "clones"):
        result["traffic"][metric] = None
        result["status"][metric] = "unavailable"
        if not traffic_token:
            continue
        try:
            payload = get_json(root + f"/traffic/{metric}?per=day", traffic_token)
            values = payload[metric]
            if not isinstance(values, list):
                raise ValueError("Invalid traffic response")
            for item in values:
                day_key(item["timestamp"][:10])
                number(item["count"])
                number(item["uniques"])
            result["traffic"][metric] = values
            result["status"][metric] = "ok"
        except HTTPError as error:
            result["status"][metric] = "unavailable" if error.code in (403, 404) else "error"
        except (URLError, TimeoutError, ValueError, KeyError, TypeError):
            result["status"][metric] = "error"
    return result


def number(value):
    if type(value) is not int or value < 0:
        raise ValueError("Expected a nonnegative integer")
    return value


def day_key(value):
    if not isinstance(value, str) or not re.fullmatch(r"\d{4}-\d{2}-\d{2}", value):
        raise ValueError("Expected an ISO UTC date")
    date.fromisoformat(value)
    return value


def empty_history(repository):
    return {"schema_version": 1, "repository": repository, "snapshots": {},
            "traffic": {"views": {}, "clones": {}}, "collection_status": {}}


def validate_history(history, repository):
    try:
        if history["schema_version"] != 1 or history["repository"].lower() != repository.lower():
            raise ValueError("History version or repository mismatch")
        if not isinstance(history["collection_status"], dict):
            raise ValueError("Invalid status")
        for day, sample in history["snapshots"].items():
            day_key(day)
            if datetime.fromisoformat(sample["observed_at"].replace("Z", "+00:00")).date().isoformat() != day:
                raise ValueError("Snapshot timestamp mismatch")
            for metric in ("downloads", "stars", "forks"):
                number(sample[metric])
        for metric in ("views", "clones"):
            for day, sample in history["traffic"][metric].items():
                day_key(day)
                number(sample["count"])
                number(sample["uniques"])
    except (KeyError, TypeError, AttributeError) as error:
        raise ValueError("Invalid history schema; refusing to overwrite it") from error


def merge_history(history, snapshot):
    validate_history(history, snapshot["repository"])
    result = copy.deepcopy(history)
    day = day_key(snapshot["date"])
    result["snapshots"][day] = {key: snapshot[key] for key in ("observed_at", "stars", "forks", "downloads")}
    for metric in ("views", "clones"):
        for sample in snapshot["traffic"].get(metric) or []:
            result["traffic"][metric][day_key(sample["timestamp"][:10])] = {
                "count": number(sample["count"]), "uniques": number(sample["uniques"])}
        result["collection_status"][metric] = {"observed_at": snapshot["observed_at"],
                                               "status": snapshot["status"][metric]}
    validate_history(result, snapshot["repository"])
    return result


def render_svg(history):
    validate_history(history, history["repository"])
    out = ['<svg xmlns="http://www.w3.org/2000/svg" width="960" height="640" viewBox="0 0 960 640" role="img" aria-labelledby="title description">',
           '<title id="title">Glow repository statistics</title>',
           '<desc id="description">Observed daily totals and daily traffic. Gaps indicate missing data. Release downloads count assets, not people.</desc>',
           '<rect width="960" height="640" rx="24" fill="#141923"/>',
           '<g font-family="Segoe UI,Arial,sans-serif" fill="#eef2ff">',
           '<text x="28" y="42" font-size="24" font-weight="600">Glow · repository activity</text>']
    snapshots = history["snapshots"]
    latest = snapshots[max(snapshots)] if snapshots else None
    subtitle = "Updated " + latest["observed_at"] if latest else "Waiting for the first successful collection"
    out.append(f'<text x="28" y="70" font-size="12" fill="#a8b4cb">{escape(subtitle)}</text>')

    def chart(metric, title, x, y, w, color, traffic=False):
        values = history["traffic"][metric] if traffic else snapshots
        series = [(date.fromisoformat(day), data["count"] if traffic else data[metric])
                  for day, data in sorted(values.items())]
        current = f"{series[-1][1]:,}" if series else "No data"
        out.extend([f'<rect x="{x}" y="{y}" width="{w}" height="200" rx="16" fill="#202838" stroke="#354057"/>',
                    f'<text x="{x+16}" y="{y+28}" font-size="14" fill="#b9c6df">{escape(title)}</text>',
                    f'<text x="{x+16}" y="{y+60}" font-size="25" font-weight="600">{current}</text>'])
        if not series:
            out.append(f'<text x="{x+16}" y="{y+115}" font-size="12" fill="#a8b4cb">Awaiting API access / first sample</text>')
            return
        first, last = series[0][0], series[-1][0]
        top = max(1, max(value for _, value in series))
        span = (last - first).days
        segments, segment = [], []
        previous = None
        for day, value in series:
            px = x + 32 + ((day - first).days / span if span else .5) * (w - 64)
            py = y + 153 - value / top * 65
            if previous is not None and (day - previous).days > 1:
                segments.append(segment)
                segment = []
            segment.append(f"{px:.2f},{py:.2f}")
            out.append(f'<circle cx="{px:.2f}" cy="{py:.2f}" r="2.8" fill="{color}"><title>{day}: {value}</title></circle>')
            previous = day
        segments.append(segment)
        for points in segments:
            if len(points) > 1:
                out.append(f'<polyline points="{" ".join(points)}" fill="none" stroke="{color}" stroke-width="2"/>')
        out.append(f'<text x="{x+16}" y="{y+184}" font-size="11" fill="#a8b4cb">{first} → {last} · max {top:,}</text>')
        if traffic:
            status = history["collection_status"].get(metric, {})
            freshness = "Latest API: " + status.get("status", "unknown")
            out.append(f'<text x="{x+w-175}" y="{y+28}" font-size="10" fill="#a8b4cb">{escape(freshness)}</text>')

    chart("downloads", "Release asset downloads", 28, 94, 290, "#9bafff")
    chart("stars", "Stars", 335, 94, 290, "#ffd586")
    chart("forks", "Forks", 642, 94, 290, "#99d9cf")
    chart("views", "Daily page views", 28, 312, 443, "#a9baff", True)
    chart("clones", "Daily clones", 489, 312, 443, "#99d9cf", True)
    for y, line in ((544, "Totals begin at the first observation. Historical daily traffic is preserved from available API responses."),
                    (565, "Traffic API covers the last 14 days; missing access or missed days never becomes zero."),
                    (586, "Downloads count release files, including prereleases — not unique people or installations."),
                    (607, "Daily unique visitors/cloners are stored in JSON and are never added into a lifetime unique total.")):
        out.append(f'<text x="28" y="{y}" font-size="11" fill="#a8b4cb">{escape(line)}</text>')
    out.append("</g></svg>")
    return "\n".join(out) + "\n"


def update(repository, data_dir, token, traffic_token):
    history_file = data_dir / "history.json"
    history = json.loads(history_file.read_text(encoding="utf-8")) if history_file.exists() else empty_history(repository)
    validate_history(history, repository)
    merged = merge_history(history, fetch_snapshot(repository, token, traffic_token))
    json_text = json.dumps(merged, indent=2, ensure_ascii=False, sort_keys=True) + "\n"
    svg_text = render_svg(merged)
    data_dir.mkdir(parents=True, exist_ok=True)
    # Both outputs must be generated successfully before either is replaced.
    for filename, content in (("history.json", json_text), ("overview.svg", svg_text)):
        (data_dir / (filename + ".tmp")).write_text(content, encoding="utf-8")
    for filename in ("history.json", "overview.svg"):
        os.replace(data_dir / (filename + ".tmp"), data_dir / filename)
    return merged


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True)
    parser.add_argument("--data-dir", type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.repository):
        parser.error("Expected OWNER/REPOSITORY")
    try:
        result = update(args.repository, args.data_dir, os.getenv("GH_TOKEN", ""), os.getenv("GH_TRAFFIC_TOKEN") or None)
        print("Collected real samples; traffic:", {key: value["status"] for key, value in result["collection_status"].items()})
    except (OSError, ValueError, KeyError, TypeError) as error:
        # No response headers, bodies or tokens are printed.
        print(f"Statistics collection failed ({type(error).__name__}); no commit should be made.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
