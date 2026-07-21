from __future__ import annotations

import argparse
import json
import mimetypes
import re
import sys
import unicodedata
import urllib.request
from pathlib import Path
from typing import Any


TOKEN_RE = re.compile(r"([A-Za-z0-9_]+)(?:\[(\d+)\])?")


def normalize_text(value: Any) -> str:
    text = "" if value is None else str(value)
    text = unicodedata.normalize("NFKD", text)
    text = text.encode("ascii", "ignore").decode("ascii")
    text = re.sub(r"[^A-Za-z0-9.]+", " ", text).strip().lower()
    return re.sub(r"\s+", " ", text)


def as_number(value: Any) -> float | None:
    if value is None:
        return None
    text = str(value).replace(" ", "").replace(",", ".")
    text = re.sub(r"[^0-9.\-]", "", text)
    if not text or text in {"-", ".", "-."}:
        return None
    try:
        return float(text)
    except ValueError:
        return None


def get_path(payload: Any, path: str) -> Any:
    current = payload
    for match in TOKEN_RE.finditer(path):
        key = match.group(1)
        index = match.group(2)
        if not isinstance(current, dict) or key not in current:
            return None
        current = current[key]
        if index is not None:
            if not isinstance(current, list):
                return None
            item_index = int(index)
            if item_index >= len(current):
                return None
            current = current[item_index]
    return current


def values_match(expected: Any, actual: Any) -> bool:
    if expected is None:
        return actual is None or normalize_text(actual) == ""
    if isinstance(expected, list):
        return isinstance(actual, list) and len(actual) >= len(expected)
    expected_number = as_number(expected)
    actual_number = as_number(actual)
    if expected_number is not None and actual_number is not None:
        return abs(expected_number - actual_number) <= max(0.01, abs(expected_number) * 0.002)
    expected_text = normalize_text(expected)
    actual_text = normalize_text(actual)
    return expected_text == actual_text or expected_text in actual_text or actual_text in expected_text


def load_cases(ground_truth_dir: Path) -> list[dict[str, Any]]:
    cases: list[dict[str, Any]] = []
    for path in sorted(ground_truth_dir.glob("*.json")):
        if path.name == "manifest.json":
            continue
        with path.open("r", encoding="utf-8") as handle:
            case = json.load(handle)
        case["_path"] = str(path)
        for required_key in ("case_id", "document_type", "expected", "required_paths"):
            if required_key not in case:
                raise ValueError(f"{path}: champ obligatoire absent `{required_key}`")
        cases.append(case)
    return cases


def load_actual(actual_dir: Path | None, case_id: str) -> dict[str, Any] | None:
    if actual_dir is None:
        return None
    path = actual_dir / f"{case_id}.json"
    if not path.exists():
        return None
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def resolve_sample_path(ground_truth_dir: Path, sample_file: str | None) -> Path | None:
    if not sample_file:
        return None
    raw_path = Path(sample_file)
    if raw_path.is_absolute():
        return raw_path
    return ground_truth_dir.parent / raw_path


def post_ocr(ocr_url: str, file_path: Path, document_type: str) -> dict[str, Any]:
    boundary = "----einvoicing-ocr-ground-truth"
    mime_type = mimetypes.guess_type(file_path.name)[0] or "application/octet-stream"
    file_bytes = file_path.read_bytes()
    parts = [
        (
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="document_type"\r\n\r\n'
            f"{document_type}\r\n"
        ).encode("utf-8"),
        (
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="file"; filename="{file_path.name}"\r\n'
            f"Content-Type: {mime_type}\r\n\r\n"
        ).encode("utf-8"),
        file_bytes,
        f"\r\n--{boundary}--\r\n".encode("utf-8"),
    ]
    body = b"".join(parts)
    request = urllib.request.Request(
        ocr_url,
        data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=180) as response:
        return json.loads(response.read().decode("utf-8"))


def evaluate_case(case: dict[str, Any], actual: dict[str, Any] | None) -> dict[str, Any]:
    expected = case["expected"]
    result = {
        "case_id": case["case_id"],
        "document_type": case["document_type"],
        "status": "missing_actual" if actual is None else "evaluated",
        "total": len(case["required_paths"]),
        "correct": 0,
        "missing": 0,
        "wrong": 0,
        "details": [],
    }
    if actual is None:
        return result

    for path in case["required_paths"]:
        expected_value = get_path(expected, path)
        actual_value = get_path(actual, path)
        if actual_value is None or normalize_text(actual_value) == "":
            result["missing"] += 1
            status = "missing"
        elif values_match(expected_value, actual_value):
            result["correct"] += 1
            status = "correct"
        else:
            result["wrong"] += 1
            status = "wrong"
        result["details"].append(
            {
                "path": path,
                "status": status,
                "expected": expected_value,
                "actual": actual_value,
            }
        )
    return result


def summarize(results: list[dict[str, Any]]) -> dict[str, Any]:
    evaluated = [item for item in results if item["status"] == "evaluated"]
    total = sum(item["total"] for item in evaluated)
    correct = sum(item["correct"] for item in evaluated)
    missing = sum(item["missing"] for item in evaluated)
    wrong = sum(item["wrong"] for item in evaluated)
    return {
        "cases_total": len(results),
        "cases_evaluated": len(evaluated),
        "fields_total": total,
        "fields_correct": correct,
        "fields_missing": missing,
        "fields_wrong": wrong,
        "accuracy": round(correct / total, 4) if total else None,
    }


def run_evaluation(cases: list[dict[str, Any]], actual_dir: Path | None) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    results = [evaluate_case(case, load_actual(actual_dir, case["case_id"])) for case in cases]
    return results, summarize(results)


def main() -> int:
    parser = argparse.ArgumentParser(description="Evaluate OCR JSON against multi-document ground truth.")
    parser.add_argument("--ground-truth", default="tests/ground_truth")
    parser.add_argument("--actual-dir", default=None, help="Dossier des sorties OCR courantes `{case_id}.json`.")
    parser.add_argument("--baseline-dir", default=None, help="Dossier des sorties OCR avant correction.")
    parser.add_argument("--ocr-url", default=None, help="URL locale /ocr/accounting/parse pour generer les sorties.")
    parser.add_argument("--out-dir", default=None, help="Dossier ou sauvegarder les sorties OCR generees.")
    parser.add_argument("--metrics-file", default=None)
    parser.add_argument("--min-accuracy", type=float, default=0.0)
    args = parser.parse_args()

    ground_truth_dir = Path(args.ground_truth).resolve()
    cases = load_cases(ground_truth_dir)

    out_dir = Path(args.out_dir).resolve() if args.out_dir else None
    if args.ocr_url and out_dir:
        out_dir.mkdir(parents=True, exist_ok=True)
        for case in cases:
            sample_path = resolve_sample_path(ground_truth_dir, case.get("sample_file"))
            if sample_path is None or not sample_path.exists():
                continue
            actual = post_ocr(args.ocr_url, sample_path, case["document_type"])
            (out_dir / f"{case['case_id']}.json").write_text(
                json.dumps(actual, ensure_ascii=False, indent=2),
                encoding="utf-8",
            )

    actual_dir = Path(args.actual_dir).resolve() if args.actual_dir else out_dir
    baseline_dir = Path(args.baseline_dir).resolve() if args.baseline_dir else None

    current_results, current_summary = run_evaluation(cases, actual_dir)
    payload: dict[str, Any] = {
        "ground_truth_cases": len(cases),
        "current": current_summary,
        "results": current_results,
    }

    if baseline_dir:
        baseline_results, baseline_summary = run_evaluation(cases, baseline_dir)
        before = baseline_summary.get("accuracy") or 0
        after = current_summary.get("accuracy") or 0
        payload["baseline"] = baseline_summary
        payload["delta"] = {
            "accuracy_before": before,
            "accuracy_after": after,
            "accuracy_gain": round(after - before, 4),
        }
        payload["baseline_results"] = baseline_results

    output = json.dumps(payload, ensure_ascii=False, indent=2)
    print(output)
    if args.metrics_file:
        Path(args.metrics_file).write_text(output, encoding="utf-8")

    accuracy = current_summary.get("accuracy")
    if accuracy is not None and accuracy < args.min_accuracy:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
