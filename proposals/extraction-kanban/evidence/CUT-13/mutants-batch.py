# CUT-13 SyncDelegate sub-loop mutants: rewrite the delegate-batch artifact the candidate actually loads,
# run batch-differential.mjs, restore. Usage: python3 mutants-batch.py <node> <base-root> <cand-root>
import subprocess, sys, re, pathlib
node, base, cand = sys.argv[1:4]
here = pathlib.Path(__file__).parent
L = f"{cand}/wanxiangli/dist/delegate-batch/DelegateBatchLaw.js"
mutants = [
  ("active-ignored", 'if (active) {', 'if (false) {'),
  ("single-joins-pending", '}) : reject("BatchPending");', '}) : Object.freeze({ verdict: "single" });'),
  ("run-ignored", 'p.ProviderRun === batch_1.ProviderRun && equals(', 'equals('),
  ("order-ignored", '&& equals(p.CallOrder, order)) ?', ') ?'),
  ("duplicate-counted", 'return reject("DuplicateCall");', 'return Object.freeze({ verdict: "wait", open: false });'),
  ("ready-early", '})) !== length(order) ? Object.freeze({', '})) < length(order) - 1 ? Object.freeze({'),
  ("never-opens", '[[true, empty()]]', '[[false, empty()]]'),
  ("duplicate-order-allowed", '})) !== length(order)) ? true : !contains(batch_1.CurrentCall', '})) !== length(order) && false) ? true : !contains(batch_1.CurrentCall'),
  ("observed-ignores-role", 'if (r === role) {', 'if (true) {'),
  ("observed-ignores-current", '}) ? frozenArray(order) : defaultOf();', '}) || true ? frozenArray(order) : defaultOf();'),
]
for name, old, new in mutants:
    src = pathlib.Path(L).read_text()
    assert src.count(old) == 1, (name, src.count(old))
    pathlib.Path(L).write_text(src.replace(old, new))
    try:
        r = subprocess.run([node, str(here / "batch-differential.mjs"), base, cand, "3000"], capture_output=True, text=True)
        found = re.findall(r'"differences": (\d+)', r.stdout)
        d = int(found[-1]) if found else -1
    finally:
        pathlib.Path(L).write_text(src)
    print(f"{name} differences={d} {'CAUGHT' if d != 0 else 'MISSED'}" + (" (candidate threw)" if d == -1 else ""))
