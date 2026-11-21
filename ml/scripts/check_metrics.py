#!/usr/bin/env python3
"""Compare freshly trained ml/models/*.json metadata against the committed
reference and fail if any tracked metric regressed.

Policy (from .github/workflows/ml.yml):
  - R2, accuracies (accuracy, microAccuracy, macroAccuracy) and F1 may drop
    by at most 0.02 in absolute value.
  - MAE may grow by at most 5% relative to the reference.
  - Every other number in testMetrics (MAPE, RMSE, logLoss, AUC, precision,
    recall) is informational and ignored by this check.

Usage: python3 check_metrics.py <reference-dir> <new-dir>
"""
import json
import os
import sys

ABSOLUTE_DROP_LIMIT = 0.02
MAE_GROWTH_FRACTION = 0.05

HIGHER_IS_BETTER = {"r2", "accuracy", "microAccuracy", "macroAccuracy", "f1"}
MAE_KEY = "mae"


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


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__)
        return 2

    ref_dir, new_dir = sys.argv[1], sys.argv[2]
    all_errors: list[str] = []

    for fname in sorted(os.listdir(ref_dir)):
        if not fname.endswith(".json"):
            continue
        ref_path = os.path.join(ref_dir, fname)
        new_path = os.path.join(new_dir, fname)
        if not os.path.exists(new_path):
            all_errors.append(f"{fname}: missing in {new_dir}")
            continue
        with open(ref_path, encoding="utf-8") as f:
            ref = json.load(f)
        with open(new_path, encoding="utf-8") as f:
            new = json.load(f)
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
