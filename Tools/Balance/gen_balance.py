"""Generate StageSpawn + Monster sheets for GameData.xlsx from balance parameters.

usage: python -I gen_balance.py <source.xlsx> <out.xlsx> <params.json>
"""
import json, math, re, sys, zipfile
from xml.sax.saxutils import escape

TYPES = ["Slime", "Crab", "Spider", "Bat", "Skeleton", "Demon", "Ghost"]
KO = {"Bat": "박쥐", "Slime": "슬라임", "Crab": "게", "Ghost": "유령", "Skeleton": "스켈레톤", "Spider": "거미", "Demon": "악마"}
PREFAB = {t: t for t in TYPES}


def load_params(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def count_for(p, stage):
    slope = p["countA"] if stage <= 40 else (p["countB"] if stage <= 70 else p["countC"])
    return max(p["minCount"], int(math.floor(slope * stage + 0.5)))


def hp_at(p, stage):
    if "hpExpA" in p:
        v = p["hpFloor"] + p["hpExpA"] * math.exp(p["hpExpR"] * stage)
        if "tailMul" in p:
            t = (stage - p["tailStart"]) / float(p["tailEnd"] - p["tailStart"])
            t = max(0.0, min(1.0, t))
            v *= 1.0 + (p["tailMul"] - 1.0) * t
        return v
    return p["hpBase"] * (1.0 + p["hpLin"] * stage) ** p["hpPow"]


def atk_at(p, stage):
    return max(1, int(math.floor(p["atkBase"] + p["atkPerStage"] * stage + 0.5)))


def roster(p, stage):
    return [i for i, t in enumerate(TYPES) if stage >= p["unlock"][i] and p["weight"][i] > 0]


def split_counts(p, stage, n):
    idx = roster(p, stage)
    total = sum(p["weight"][i] for i in idx)
    raw = {i: n * p["weight"][i] / total for i in idx}
    base = {i: int(math.floor(raw[i])) for i in idx}
    rest = n - sum(base.values())
    order = sorted(idx, key=lambda i: raw[i] - base[i], reverse=True)
    for i in order[:rest]:
        base[i] += 1
    return base


def spawn_rows(p):
    rows = []
    for stage in range(1, 101):
        n = count_for(p, stage)
        counts = split_counts(p, stage, n)
        hp0 = hp_at(p, stage)
        atk0 = atk_at(p, stage)
        rounds = 3  # interleave: each type is spawned in up to 3 batches
        remaining = dict(counts)
        for r in range(rounds):
            for i in sorted(counts):
                left_rounds = rounds - r
                take = remaining[i] // left_rounds if left_rounds > 1 else remaining[i]
                if left_rounds > 1 and remaining[i] % left_rounds and r == 0:
                    take += 1
                if take <= 0:
                    continue
                remaining[i] -= take
                hp = max(1, int(round(hp0 * p["hpMul"][i])))
                atk = max(1, int(round(atk0 * p["atkMul"][i])))
                rows.append(("Stage_%03d" % (stage - 1), TYPES[i], take, hp, atk, p["gold"], p["exp"]))
    return rows


def cell_inline(ref, text):
    return '<c r="%s" t="inlineStr"><is><t>%s</t></is></c>' % (ref, escape(text))


def cell_num(ref, value):
    if isinstance(value, float) and value == int(value):
        value = int(value)
    return '<c r="%s"><v>%s</v></c>' % (ref, value)


def build_sheet(xml, header_cells, data_rows, cols):
    """Replace <sheetData> content: keep the header row, rewrite everything else."""
    m = re.search(r"<sheetData>(.*)</sheetData>", xml, re.S)
    header = re.search(r'<row r="1".*?</row>', m.group(1), re.S).group(0)
    body = [header]
    for r, vals in enumerate(data_rows, start=2):
        cells = []
        for c, v in enumerate(vals):
            ref = "%s%d" % (chr(ord("A") + c), r)
            cells.append(cell_inline(ref, v) if isinstance(v, str) else cell_num(ref, v))
        body.append('<row r="%d">%s</row>' % (r, "".join(cells)))
    xml = xml[: m.start(1)] + "".join(body) + xml[m.end(1):]
    last_col = chr(ord("A") + cols - 1)
    xml = re.sub(r'<dimension ref="[^"]*"/>', '<dimension ref="A1:%s%d"/>' % (last_col, len(data_rows) + 1), xml)
    # the sheet view may scroll far down; reset it
    xml = re.sub(r'topLeftCell="[A-Z]+\d+" ', "", xml)
    return xml


def main():
    src, out, params_path = sys.argv[1], sys.argv[2], sys.argv[3]
    p = load_params(params_path)
    zin = zipfile.ZipFile(src)
    sheet2 = zin.read("xl/worksheets/sheet2.xml").decode("utf-8")  # StageSpawn
    sheet4 = zin.read("xl/worksheets/sheet4.xml").decode("utf-8")  # Monster

    rows = spawn_rows(p)
    sheet2 = build_sheet(sheet2, None, [list(r) for r in rows], 7)

    mon_rows = []
    order = ["Bat", "Slime", "Crab", "Ghost", "Skeleton", "Spider", "Demon"]
    for name in order:
        i = TYPES.index(name)
        hp = max(1, int(round(hp_at(p, 1) * p["hpMul"][i])))
        atk = max(1, int(round(atk_at(p, 1) * p["atkMul"][i])))
        mon_rows.append([name, KO[name], PREFAB[name], hp, p["speed"][i], atk, p["gold"], p["exp"]])
    sheet4 = build_sheet(sheet4, None, mon_rows, 8)

    zout = zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED)
    for item in zin.infolist():
        data = zin.read(item.filename)
        if item.filename == "xl/worksheets/sheet2.xml":
            data = sheet2.encode("utf-8")
        elif item.filename == "xl/worksheets/sheet4.xml":
            data = sheet4.encode("utf-8")
        zout.writestr(item, data)
    zout.close()
    print("rows:", len(rows), "stages:", 100)
    for s in (1, 20, 21, 41, 56, 71, 81, 100):
        n = count_for(p, s)
        print("stage %3d n=%3d hp0=%6.1f atk=%d roster=%s" % (s, n, hp_at(p, s), atk_at(p, s), [TYPES[i] for i in roster(p, s)]))


if __name__ == "__main__":
    main()
