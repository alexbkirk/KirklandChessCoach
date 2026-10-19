"""Parse chess.com monthly PGN text into game records (shared by refresh.py)."""
import re

KW = re.compile(r'(Opening|Defense|Defence|Game|Gambit|Attack|System|Countergambit)$')


def family(name):
    w = name.split()
    for i, x in enumerate(w):
        if KW.match(x):
            j = i + 1
            if x == "Gambit" and j < len(w) and w[j] in ("Accepted", "Declined"): j += 1   # include "Accepted/Declined"
            return " ".join(w[:j])
    return " ".join(w[:2])


def tclass(tc):
    if "/" in tc: return "daily"
    m = re.match(r'(\d+)(?:\+(\d+))?', tc)
    t = int(m.group(1)) + 40 * int(m.group(2) or 0)
    return "bullet" if t < 180 else "blitz" if t < 600 else "rapid"   # chess.com: rapid = 10 min and up (10|0 counts as rapid)


def ending(term):
    t = term.lower()
    if "checkmate" in t: return "checkmate"
    if "resignation" in t: return "resignation"
    if "time" in t and "insufficient" not in t: return "timeout"
    if "abandon" in t: return "abandoned"
    if "agreement" in t: return "agreement"
    if "repetition" in t: return "repetition"
    if "stalemate" in t: return "stalemate"
    if "insufficient" in t: return "insufficient material"
    if "50" in t: return "50-move rule"
    return "other"


def parse_pgn_text(txt, me):
    me = me.lower()
    games = []
    for b in re.split(r'\n\n(?=\[Event )', txt.strip()):
        h = dict(re.findall(r'^\[(\w+) "(.*)"\]$', b, re.M))
        if "White" not in h: continue
        if h.get("Variant") and h["Variant"] != "Chess": continue   # skip Chess960 etc.
        moves = b.split("\n\n", 1)[1].strip() if "\n\n" in b else ""
        white = h["White"].lower() == me
        r = h.get("Result", "*")
        res = "draw" if r == "1/2-1/2" else ("win" if (r == "1-0") == white else "loss") if r in ("1-0", "0-1") else "other"
        slug = h.get("ECOUrl", "").rsplit("/", 1)[-1]
        name = re.split(r'-\d+\.', slug)[0].replace("-", " ").strip() or "Unknown"
        clean = re.sub(r'\s+', ' ', re.sub(r'\{[^}]*\}|\d+\.\.\.', '', moves)).strip()
        games.append(dict(
            id=h.get("Link", "").rstrip("/").rsplit("/", 1)[-1], url=h.get("Link", ""),
            date=h.get("UTCDate", h["Date"]).replace(".", "-"), time=h.get("UTCTime", ""),
            color="white" if white else "black", result=res, ending=ending(h.get("Termination", "")),
            timeControl=h.get("TimeControl", ""), timeClass=tclass(h.get("TimeControl", "")),
            myRating=int(h.get("WhiteElo" if white else "BlackElo", 0) or 0),
            oppRating=int(h.get("BlackElo" if white else "WhiteElo", 0) or 0),
            opponent=h["Black" if white else "White"], eco=h.get("ECO", ""),
            opening=name, family=family(name), plies=len(re.findall(r'\d+\.', clean)) * 2,
            tournament=h.get('Tournament') or None,
            moves=clean, pgn=b))
    return games
