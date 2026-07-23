#!/usr/bin/env bash
# Downloads the fonts the PDF generator embeds.
#
# Not committed: these are ~24 MB of binaries that would live in git history
# forever. Fetched into a gitignored folder instead, skipped when already
# present, so a normal build costs nothing after the first run.
set -euo pipefail

DEST="${1:-Akinai.Infrastructure/Fonts}"
UA="Mozilla/5.0 (X11; Linux x86_64)"
mkdir -p "$DEST"

# Noto Sans covers Latin, Vietnamese Extended and Cyrillic; Noto Sans SC adds
# Simplified Chinese and Hangul. Deliberately NOT the web's Zen Kaku Gothic New:
# it lacks Vietnamese Extended but claims enough coverage that the fallback
# never fires, so "So tien" came out with its tone marks scattered.
fetch() {
  local family="$1" weight="$2" out="$DEST/$3"
  if [ -s "$out" ]; then echo "  have $(basename "$out")"; return; fi
  local url
  url=$(curl -sf -m 30 -A "$UA" "https://fonts.googleapis.com/css2?family=${family}:wght@${weight}&display=swap" \
        | grep -oE 'https://[^)]+\.ttf' | head -1)
  [ -n "$url" ] || { echo "  !! no URL for $family $weight" >&2; exit 1; }
  curl -sf -m 300 -o "$out" "$url"
  echo "  got $(basename "$out") ($(du -h "$out" | cut -f1))"
}

# Noto Sans carries Vietnamese Extended (U+1EA0-U+1EF9) and Cyrillic, which
# Zen Kaku lacks — without it "Số tiền" decomposed into "Sô´tiêǹ".
fetch "Noto+Sans"           400 "NotoSans-Regular.ttf"
fetch "Noto+Sans"           700 "NotoSans-Bold.ttf"
fetch "Noto+Sans+SC"        400 "NotoSansSC-Regular.ttf"
fetch "Noto+Sans+SC"        700 "NotoSansSC-Bold.ttf"
