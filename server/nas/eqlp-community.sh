# EQL Pathfinder community numbers, every 6 hours in the NAS scheduler container (crontab: 40 4,10,16,22 * * *).
# Every friend's shared crawls (Cloudflare D1) -> community/v1.json in the update bucket, which the app reads for Ask.
# The token lives in /scripts/eqlp.env (made by eqlp-set-token.sh). Failures go to ntfy homelab-alerts.
. /scripts/common.sh
if [ ! -f /scripts/eqlp.env ]; then log "no /scripts/eqlp.env yet: run eqlp-set-token.sh on the NAS"; exit 0; fi
set -a; . /scripts/eqlp.env; set +a
out=$(/scripts/eqlp/EqlPathfinder.Cli community --upload --out /state/eqlp-community.json 2>&1); rc=$?
log "$out"
[ $rc -ne 0 ] && notify homelab-alerts "EQL community job failed" "$(echo "$out" | tail -3)" high warning
exit $rc
