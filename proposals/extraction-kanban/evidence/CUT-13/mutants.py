# CUT-13 sub-loop 1 mutants: rewrite the handle-lifecycle artifact the candidate actually loads, run
# handle-differential.mjs, restore. Usage: python3 mutants.py <node> <base-root> <cand-root>
import subprocess, sys, re, pathlib
node, base, cand = sys.argv[1:4]
here = pathlib.Path(__file__).parent
L = f"{cand}/wanxiangli/dist/handle-lifecycle/HandleLifecycleLaw.js"
mutants = [
  ("second-completion-overwrites", 'phase.tag === 1 ? reject("AlreadyCompleted") : phase.tag === 0 ? set$(Phase.CompletedAwaitingJoin)', 'phase.tag === 1 ? set$(Phase.CompletedAwaitingJoin) : phase.tag === 0 ? set$(Phase.CompletedAwaitingJoin)'),
  ("abandon-after-retire", 'phase.tag === 1 ? set$(Phase.Abandoned) : reject("HandleIsRetired")', 'phase.tag === 1 ? set$(Phase.Abandoned) : set$(Phase.Abandoned)'),
  ("active-retires", 'phase.tag === 0 ? reject("NotCompleted") : phase.tag === 1 ? set$(Phase.Retired)', 'phase.tag === 0 ? set$(Phase.Retired) : phase.tag === 1 ? set$(Phase.Retired)'),
  ("false-completion-revokes-any", 'flag("cellMatches", input["cellMatches"]) ? set$(Phase.Active)', '(flag("cellMatches", input["cellMatches"]), true) ? set$(Phase.Active)'),
  ("legacy-ignored", 'if (legacy) {', 'if (false) {'),
  ("cancelled-completes-work", 'completion.Kind !== "Cancelled" && completion.HasRef', 'completion.HasRef'),
  ("blank-consumption-accepted", 'isNullOrWhiteSpace(consumptionId) ? reject("ConsumptionMismatch")', 'false ? reject("ConsumptionMismatch")'),
  ("consume-any-completion", 'cellEqualsExpected ? set$(Phase.Retired) : reject("ConsumptionMismatch")', 'set$(Phase.Retired)'),
  ("abandoned-consumed-with-payload", 'expected.Kind === "Cancelled" && !expected.HasRef && !expected.HasDigest', 'expected.Kind === "Cancelled"'),
]
for name, old, new in mutants:
    src = pathlib.Path(L).read_text()
    assert src.count(old) == 1, (name, src.count(old))
    pathlib.Path(L).write_text(src.replace(old, new))
    try:
        out = subprocess.run([node, str(here / "handle-differential.mjs"), base, cand], capture_output=True, text=True).stdout
        d = int(re.findall(r'"differences": (\d+)', out)[-1])
    finally:
        pathlib.Path(L).write_text(src)
    print(f"{name} differences={d} {'CAUGHT' if d else 'MISSED'}")
