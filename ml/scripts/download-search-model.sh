#!/usr/bin/env bash
# Downloads the embedding model of the search by meaning (EmbeddingGemma-300m, ONNX, 4-bit weights)
# into ml/models/semantic-search/. The files are too big for git (~200 MB), so the Dockerfile, the CI
# and every developer run this script once. The revision is pinned and every file is checked against
# its SHA-256, so the same bytes are used everywhere. Skips the files that are already there and correct.
#
# Usage (from anywhere, Git Bash on Windows works too): ml/scripts/download-search-model.sh [target-folder]
set -euo pipefail

REVISION=5090578d9565bb06545b4552f76e6bc2c93e4a66
BASE="https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX/resolve/$REVISION"
TARGET="${1:-$(cd "$(dirname "$0")/.." && pwd)/models/semantic-search}"

FILES=(
  "onnx/model_q4.onnx model_q4.onnx ad1dfee81a70f7944b9b9d1cc6e48075b832881cf33fab2f2b248be78f3f0043"
  "onnx/model_q4.onnx_data model_q4.onnx_data 599962c3143b040de2dd05e5975be3e9091dd067cacc6a8f7186e3203bab9e02"
  "tokenizer.model tokenizer.model 1299c11d7cf632ef3b4e11937501358ada021bbdf7c47638d13c0ee982f2e79c"
)

mkdir -p "$TARGET"
for entry in "${FILES[@]}"; do
  read -r source name sha <<< "$entry"
  file="$TARGET/$name"
  if [ -f "$file" ] && echo "$sha  $file" | sha256sum --check --status; then
    echo "ok        $name"
    continue
  fi
  echo "download  $name"
  curl -fsSL --retry 3 -o "$file.part" "$BASE/$source"
  if ! echo "$sha  $file.part" | sha256sum --check --status; then
    rm -f "$file.part"
    echo "SHA-256 of $name does not match, nothing was kept" >&2
    exit 1
  fi
  mv "$file.part" "$file"
done
