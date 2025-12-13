#!/usr/bin/env python3
"""Convert PC1 (t, pc1) CSV files to normalized (phase, pc1) CSVs.

- Input:
    docs/data/PC1-hand-kinematics/PC1_MCP/*.csv
    docs/data/PC1-hand-kinematics/PC1_PIP/*.csv
  Each row is assumed to be:
    t, pc1
  with possible header / comment rows that are skipped.

- Output:
    For each input foo.csv, a sibling foo_phase.csv is created with:
        phase,pc1
        0.000000,-0.123456
        ...

`t` が昇順・降順に揃っていない場合も、数値でソートしてから
phase を計算する。

本スクリプトでは、論文 Fig.3 の時間軸を前提に

    t_min = -0.2
    t_max = +0.2

とみなし、どのファイルでも同じ範囲を 0〜1 に正規化する。
"""

import csv
from pathlib import Path

# このスクリプトがやっていること（日本語まとめ）:
# - 論文から拾った PC1 生データ CSV（1列目 t, 2列目 pc1）を読む。
# - 行ごとの t がバラバラな順序でも、数値としてソートし直す。
# - t の範囲を論文の時間軸に合わせて
#     t_min = -0.2
#     t_max = +0.2
#   と仮定し、
#     phase = (t - t_min) / (t_max - t_min)
#   を計算して 0〜1 に正規化する（t=0 がちょうど phase=0.5 になる）。
# - 結果を
#     phase,pc1
#     0.000000, ...
#     ...
#     1.000000, ...
#   という形式の *_phase.csv として書き出す。
# - これにより、Unity 側では phase を 0〜1 の進行度として扱い、
#   `AnimationCurve` で PC1 の波形をそのまま利用できるようになる。

# このスクリプト自身が置かれているディレクトリ
DATA_DIR = Path(__file__).resolve().parent
MCP_DIR = DATA_DIR / "PC1_MCP"
PIP_DIR = DATA_DIR / "PC1_PIP"


def read_t_pc1_rows(path: Path):
    """Read (t, pc1) rows from a CSV file, skipping headers/comments.

    Returns a list of (t, pc1) tuples.
    """
    rows: list[tuple[float, float]] = []
    with path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        for row in reader:
            if not row:
                continue
            first = row[0].strip()
            if not first or first.startswith("#"):
                # コメント行など
                continue
            try:
                t = float(first)
                pc1 = float(row[1])
            except (ValueError, IndexError):
                # ヘッダ行（t,pc1 など）や不正な行はスキップ
                continue
            rows.append((t, pc1))
    return rows


def write_phase_pc1_csv(out_path: Path, t_pc1_rows: list[tuple[float, float]]):
    """Write normalized (phase, pc1) CSV.

    phase = (t - t_min) / (t_max - t_min)
    t がバラバラな順序でも、ソートしてから正規化する。

    t_min, t_max は論文 Fig.3 の時間軸に合わせて
    固定値 -0.2 / +0.2 とする（データごとの微小なズレには引きずられない）。
    """
    if not t_pc1_rows:
        print(f"[WARN] no data rows in input for {out_path}")
        return

    # t でソートしてから正規化（min/max は固定）
    t_pc1_rows = sorted(t_pc1_rows, key=lambda x: x[0])
    t_min = -0.2
    t_max = 0.2

    if t_max == t_min:
        print(f"[WARN] t_max == t_min for {out_path}, skip normalization")
        return

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["phase", "pc1"])
        for t, pc1 in t_pc1_rows:
            phase = (t - t_min) / (t_max - t_min)
            writer.writerow([f"{phase:.6f}", f"{pc1:.6f}"])


def convert_dir(dir_path: Path):
    if not dir_path.is_dir():
        print(f"[INFO] skip missing dir: {dir_path}")
        return

    for in_path in dir_path.glob("*.csv"):
        if in_path.stem.endswith("_phase"):
            # すでに変換済みのものはスキップ
            continue

        out_path = in_path.with_name(in_path.stem + "_phase" + in_path.suffix)
        print(f"[INFO] {in_path.name} -> {out_path.name}")

        rows = read_t_pc1_rows(in_path)
        write_phase_pc1_csv(out_path, rows)


def main() -> None:
    convert_dir(MCP_DIR)
    convert_dir(PIP_DIR)


if __name__ == "__main__":
    main()
