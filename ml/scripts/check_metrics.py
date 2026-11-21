#!/usr/bin/env python3
"""Compare freshly trained ml/models/*.json metadata against the committed
reference and fail if any tracked metric regressed.

Policy (from .github/workflows/ml.yml):
  - R2, accuracies (accuracy, microAccuracy, macroAccuracy) and F1 may drop
    by at most 0.02 in absolute value.
  - MAE may grow by at most 5% relative to the reference.
  - Every other number in testMetrics (MAPE, RMSE, logLoss, AUC, precision,
    recall) is informational and ignored by this check.

The directories are fixed on purpose (reference in .ci/reference-models, new
in ml/models, both relative to the repository root): the script takes no
command-line input and reads nothing outside the repository, so an attacker
who can only influence the CI invocation cannot redirect it at an arbitrary
path on the runner.

Usage: python3 check_metrics.py
"""
import json
import sys
from pathlib import Path

ABSOLUTE_DROP_LIMIT = 0.02
MAE_GROWTH_FRACTION = 0.05

HIGHER_IS_BETTER = {"r2", "accuracy", "microAccuracy", "macroAccuracy", "f1"}
MAE_KEY = "mae"

# Repository root computed from this file's own location:
#   <repo>/ml/scripts/check_metrics.py  ->  parents[2] == <repo>
REPO_ROOT = Path(__file__).resolve().parent.parent.parent
REFERENCE_DIR = REPO_ROOT / ".ci" / "reference-models"
NEW_DIR = REPO_ROOT / "ml" / "models"


def check(name: str, old: dict, new: dict) -> list[str]:
    errors: list[str] = []
    for key, old_value in old.items():
        new_value = new.get(key)
        if new_value is None:
            continue
        if key in HIGHER_IS_BETTER:
            if new_value < old_value - ABSOLUTE_DROP_LIMIT:
                errors.append(
                    f"{name}: {key} dropped from {old_value} to {new_value} "
                    f"(> {ABSOLUTE_DROP_LIMIT} absolute)"
                )
        elif key == MAE_KEY and old_value > 0:
            if new_value > old_value * (1 + MAE_GROWTH_FRACTION):
                errors.append(
                    f"{name}: {key} grew from {old_value} to {new_value} "
                    f"(> {MAE_GROWTH_FRACTION * 100:.0f}% relative)"
                )
    return errors


def load_json_safely(path: Path) -> dict:
    """Resolve the path and refuse to open anything outside the repository root."""
    resolved = path.resolve()
    if not resolved.is_relative_to(REPO_ROOT):
        raise RuntimeError(
            f"Refusing to read {resolved}: outside the repository root {REPO_ROOT}."
        )
    with resolved.open(encoding="utf-8") as f:
        return json.load(f)


def list_json_files(directory: Path) -> list[Path]:
    """Return only *.json files directly in `directory` (no recursion, no symlinks)."""
    if not directory.is_dir():
        return []
    files: list[Path] = []
    for entry in sorted(directory.iterdir()):
        if entry.is_file() and not entry.is_symlink() and entry.suffix == ".json":
            files.append(entry)
    return files


def main() -> int:
    if len(sys.argv) != 1:
        print("This script takes no arguments. Reference and new directories are fixed.", file=sys.stderr)
        return 2

    if not REFERENCE_DIR.is_dir():
        print(f"Reference directory not found: {REFERENCE_DIR}", file=sys.stderr)
        return 2

    if not NEW_DIR.is_dir():
        print(f"New-models directory not found: {NEW_DIR}", file=sys.stderr)
        return 2

    all_errors: list[str] = []
    for ref_path in list_json_files(REFERENCE_DIR):
        fname = ref_path.name
        new_path = NEW_DIR / fname
        if not new_path.is_file():
            all_errors.append(f"{fname}: missing in {NEW_DIR.relative_to(REPO_ROOT)}")
            continue
        ref = load_json_safely(ref_path)
        new = load_json_safely(new_path)
        all_errors.extend(
            check(fname, ref.get("testMetrics", {}), new.get("testMetrics", {}))
        )

    if all_errors:
        print("Metric regressions:")
        for e in all_errors:
            print(f"  {e}")
        return 1

    print("All tracked metrics are within tolerance of the committed reference.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
