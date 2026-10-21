# CUT-13 restart sub-loop mutants: rewrite the handle-lifecycle artifact the candidate actually loads,
# run recovery-differential.mjs, restore. Usage: python3 mutants-recovery.py <node> <base-root> <cand-root>
import subprocess, sys, re, pathlib
node, base, cand = sys.argv[1:4]
here = pathlib.Path(__file__).parent
L = f"{cand}/wanxiangli/dist/handle-lifecycle/HandleLifecycleLaw.js"
mutants = [
  ("durable-reason-lost", 'matchValue_1 === "HostSessionGone" ? matchValue_1 :', 'matchValue_1 === "HostSessionGone" ? "ParentCancelled" :'),
  ("retired-reopened", 'return resolution("blocked", singleton(["reason", "handle already retired"]));', 'return resolution("incomplete", empty());'),
  ("bad-proof-trusted", 'matchValue_2 === null || matchValue_2 === void 0 ? resolution("terminal"', 'true ? resolution("terminal"'),
  ("unreadable-blocks", 'case "Unreadable":\n            return resolution("incomplete", empty());', 'case "Unreadable":\n            return resolution("blocked", singleton(["reason", "unreadable"]));'),
  ("active-beats-abandon", 'return reason_1 == null ? contains(', 'return true ? contains('),
  ("abort-is-active", 'contains("SessionActive", observations_1,', 'contains("AbortedObserved", observations_1,'),
]
for name, old, new in mutants:
    src = pathlib.Path(L).read_text()
    assert src.count(old) == 1, (name, src.count(old))
    pathlib.Path(L).write_text(src.replace(old, new))
    try:
        r = subprocess.run([node, str(here / "recovery-differential.mjs"), base, cand], capture_output=True, text=True)
        found = re.findall(r'"differences": (\d+)', r.stdout)
        d = int(found[-1]) if found else -1
    finally:
        pathlib.Path(L).write_text(src)
    print(f"{name} differences={d} {'CAUGHT' if d != 0 else 'MISSED'}" + (" (candidate threw)" if d == -1 else ""))
