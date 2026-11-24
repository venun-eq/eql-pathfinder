#!/bin/sh
# Saves the Cloudflare token for the EQL Pathfinder community job, then runs the job once to check it.
# Run it on the NAS, from a terminal (docs/community-job.md, "The token"):
#   ssh -t <the NAS login> sh /share/CACHEDEV1_DATA/AppData/scheduler/eqlp-set-token.sh
# The token is never shown or logged; it goes only into eqlp.env (readable by you and the scheduler container).
# It rewrites its own two lines of eqlp.env, CF_ACCOUNT_ID and CF_API_TOKEN, and keeps every other line: EQLP_D1 (the
# database's ID, which the job stops without) lives there too, and writing the file whole would drop it.
F=${EQLP_ENV_FILE:-/share/CACHEDEV1_DATA/AppData/scheduler/eqlp.env}
# The account stays the one eqlp.env already has; a first run asks for it. This script holds no ID of its own.
ACCOUNT=$(sed -n 's/^CF_ACCOUNT_ID=//p' "$F" 2>/dev/null | tail -n 1)
if [ -z "$ACCOUNT" ]; then
  printf 'Paste the Cloudflare account ID and press Enter: '
  read -r ACCOUNT
fi
case "$ACCOUNT" in
  ""|*" "*) echo "That didn't look like an account ID, so nothing was saved. Run this again."; exit 1 ;;
esac
printf 'Paste the Cloudflare token and press Enter (nothing shows while you paste): '
stty -echo; read -r T; stty echo; echo
case "$T" in
  ""|*" "*) echo "That didn't look like a token, so nothing was saved. Run this again."; exit 1 ;;
esac
umask 077
# Written beside the file and moved over it, so a failure halfway leaves the old eqlp.env whole.
{
  if [ -f "$F" ]; then grep -v -e '^CF_ACCOUNT_ID=' -e '^CF_API_TOKEN=' "$F"; fi
  printf 'CF_ACCOUNT_ID=%s\nCF_API_TOKEN=%s\n' "$ACCOUNT" "$T"
} > "$F.new" && chmod 600 "$F.new" && mv "$F.new" "$F" || { echo "Couldn't write $F, so nothing was saved."; exit 1; }
if ! grep -q '^EQLP_D1=.' "$F"; then
  echo "Saved, but $F has no EQLP_D1 line (the D1 database's ID) and the job stops without one. Add it, then run the job."
  exit 1
fi
echo "Saved. Running the community job once to check it..."
/share/CACHEDEV1_DATA/.qpkg/container-station/bin/docker exec scheduler sh /scripts/eqlp-community.sh
if [ $? -eq 0 ]; then echo "All good: it will run every night at 4:40 AM."; else echo "The job failed (message above). Nothing else changed."; fi
