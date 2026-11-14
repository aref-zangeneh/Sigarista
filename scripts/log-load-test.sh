#!/usr/bin/env bash
# Load / acceptance test for the lightweight logging infrastructure.
#
#   ./scripts/log-load-test.sh                     # default: 300 requests against localhost:8080
#   BASE_URL=https://sugarista.ir REQUESTS=1000 ./scripts/log-load-test.sh
#   ./scripts/log-load-test.sh --resources         # also sample docker stats (CPU/RAM of the api container)
#   ./scripts/log-load-test.sh --cut-seq           # Seq outage drill: enable Seq pointing at a DEAD endpoint, verify nothing breaks
#
# What it measures (acceptance criteria):
#   • p50/p95/p99 request latency and RPS  → "no noticeable latency increase"
#   • every request must succeed           → "logging outage never fails requests"
#   • docker stats averages (optional)     → "RAM bounded and predictable"
#
# Before/after comparison recipe (criteria 1):
#   1. BEFORE activating the new logging build: run this script on the old
#      deployment, note the numbers.
#   2. Deploy the Serilog build, run again, compare p95/p99 and docker stats.
#   3. The numbers are also printed at the end for copy-paste into the report.

set -euo pipefail

BASE_URL="${BASE_URL:-http://127.0.0.1:8080}"
REQUESTS="${REQUESTS:-300}"
CONCURRENCY="${CONCURRENCY:-8}"
MODE="load"
[[ "${1:-}" == "--resources" ]] && MODE="resources"
[[ "${1:-}" == "--cut-seq" ]]   && MODE="cut-seq"

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
fail() { printf '\n\033[1;31m!! %s\033[0m\n' "$*" >&2; exit 1; }

command -v curl >/dev/null || fail "curl is required"

TMPDIR_RUN="$(mktemp -d /tmp/sgr-load-XXXXXX)"
trap 'rm -rf "$TMPDIR_RUN"' EXIT

# ── 0. Seq outage drill ─────────────────────────────────────────────────────
if [ "$MODE" = "cut-seq" ]; then
    say "Seq outage drill"
    echo "This mode verifies the API stays healthy while its logging target is DEAD."
    echo
    echo "1) On the server (or local compose), set the dead-Seq env BEFORE the next run:"
    echo "     LoggingInfrastructure__Seq__Enabled=true"
    echo "     LoggingInfrastructure__Seq__Url=http://10.255.255.1:5341   # unroutable"
    echo "2) Restart ONLY the api container:  docker compose up -d api"
    echo "3) Run this script again WITHOUT --cut-seq and confirm:"
    echo "     • every request returned 2xx"
    echo "     • docker compose logs api shows [serilog-selflog] noise but NO crashes"
    echo
    echo "Current health check of the API:"
    curl -fsS "$BASE_URL/health" >/dev/null 2>&1 && echo "  /health -> OK" || echo "  /health -> UNREACHABLE"
    exit 0
fi

# ── 1. warm up ──────────────────────────────────────────────────────────────
say "Warming up ($BASE_URL)"
for _ in 1 2 3; do curl -s -o /dev/null "$BASE_URL/health" || true; done

# ── 2. optional resource sampler ────────────────────────────────────────────
STATS_PID=""
if [ "$MODE" = "resources" ]; then
    say "Sampling docker stats (api container) while the load runs"
    (
        docker stats --no-stream --format '{{.Name}} {{.CPUPerc}} {{.MemUsage}}' \
            | grep -E '^sigarista-api' >> "$TMPDIR_RUN/stats.csv"
    ) &
    STATS_PID=$!
    # Continuous sampling in a loop:
    (
        while kill -0 "$STATS_PID" 2>/dev/null; do
            docker stats --no-stream --format '{{.Name}} {{.CPUPerc}} {{.MemUsage}}' 2>/dev/null \
                | grep -E '^sigarista-api' >> "$TMPDIR_RUN/stats.csv" || true
            sleep 1
        done
    ) &
    SAMPLER_PID=$!
fi

# ── 3. run the load ─────────────────────────────────────────────────────────
say "Running $REQUESTS requests (concurrency $CONCURRENCY)"
rm -f "$TMPDIR_RUN/latency" "$TMPDIR_RUN/codes"
run_one() {
    # alternate between the two hot endpoints, record total ms + status
    local path="/api/menu"
    if (( RANDOM % 3 == 0 )); then path="/health"; fi
    local out
    out=$(curl -s -o /dev/null -w '%{time_total} %{http_code}' "$BASE_URL$path") || out="0 000"
    echo "$out" >> "$TMPDIR_RUN/raw"
}
export -f run_one
export TMPDIR_RUN BASE_URL

seq "$REQUESTS" | xargs -P "$CONCURRENCY" -I {} bash -c 'run_one' >/dev/null 2>&1

awk '{ print $1*1000 }' "$TMPDIR_RUN/raw" | sort -n > "$TMPDIR_RUN/latency"
awk '{ print $2 }'        "$TMPDIR_RUN/raw" | sort | uniq -c > "$TMPDIR_RUN/codes"

TOTAL_MS=$(awk '{ s += $1 } END { print s }' "$TMPDIR_RUN/latency")
COUNT=$(wc -l < "$TMPDIR_RUN/latency" | tr -d ' ')
[ "$COUNT" -gt 0 ] || fail "no requests were recorded"

P50=$(awk -v n="$COUNT" 'NR == int(n*0.50)+1 { print; exit }' "$TMPDIR_RUN/latency")
P95=$(awk -v n="$COUNT" 'NR == int(n*0.95)+1 { print; exit }' "$TMPDIR_RUN/latency")
P99=$(awk -v n="$COUNT" 'NR == int(n*0.99)+1 { print; exit }' "$TMPDIR_RUN/latency")
RPS=$(python3 -c "print(f'{$COUNT / ($TOTAL_MS/1000):.1f}' if $TOTAL_MS > 0 else 'inf')")

if [ -n "$SAMPLER_PID" ] && kill -0 "$SAMPLER_PID" 2>/dev/null; then kill "$SAMPLER_PID" 2>/dev/null || true; fi

say "Results"
echo "  requests : $COUNT"
echo "  rps      : $RPS"
echo "  p50      : ${P50} ms"
echo "  p95      : ${P95} ms"
echo "  p99      : ${P99} ms"
echo "  statuses :"
sed 's/^/    /' "$TMPDIR_RUN/codes"

FAILED=$(awk '$2 !~ /^2/ { s += $1 } END { print s+0 }' "$TMPDIR_RUN/codes")
if [ "$FAILED" -gt 0 ]; then
    fail "$FAILED requests did not return 2xx — logging must never fail user requests"
fi
echo "  ✓ all requests returned 2xx (logging outage / queue pressure never fails requests)"

if [ "$MODE" = "resources" ] && [ -s "$TMPDIR_RUN/stats.csv" ]; then
    say "docker stats samples (api)"
    head -1 "$TMPDIR_RUN/stats.csv"
    echo "  samples: $(wc -l < "$TMPDIR_RUN/stats.csv" | tr -d ' ')"
    python3 - "$TMPDIR_RUN/stats.csv" <<'PYEOF'
import sys
rows = [line.split() for line in open(sys.argv[1]) if line.strip()]
if rows:
    cpus = [float(r[1].rstrip('%')) for r in rows if len(r) >= 3]
    mems = [float(r[2].split('/')[0].rstrip('MiBGiB') or 0) for r in rows if len(r) >= 3]
    if cpus:
        print(f"  cpu avg: {sum(cpus)/len(cpus):.1f}%  max: {max(cpus):.1f}%")
    if mems:
        unit = 'MiB'
        print(f"  mem avg: {sum(mems)/len(mems):.0f} {unit}  max: {max(mems):.0f} {unit}")
PYEOF
fi

say "Copy these into the before/after acceptance report"
echo "  BASE=$BASE_URL REQUESTS=$COUNT RPS=$RPS P50=${P50}ms P95=${P95}ms P99=${P99}ms FAILED=0"
