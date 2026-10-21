# CUT-28 mutants: rewrite the fission-group artifact the candidate actually loads, run
# fission-differential.mjs, restore. Usage: python3 mutants.py <node> <base-root> <cand-root>
import subprocess, sys, re, pathlib
node, base, cand = sys.argv[1:4]
here = pathlib.Path(__file__).parent
L = f"{cand}/wanxiangli/dist/fission-group/FissionGroupLaw.js"
mutants = [
  ("single-lane-admitted", 'const valid = laneCount >= 2 &&', 'const valid = laneCount >= 1 &&'),
  ("owner-twice", 'flag("ownerActive", input["ownerActive"]) ? reject("DuplicateOwnerActive")', 'false ? reject("DuplicateOwnerActive")'),
  ("lane-work-overwritten", 'return matchValue_1.tag === 2 ? reject("ConflictingLaneWork")', 'return matchValue_1.tag === 2 ? set$()'),
  ("sibling-replaced", 'case "mismatch":\n                case "absent":\n                  return reject("LaneSessionMismatch");', 'case "mismatch":\n                case "absent":\n                  return set$();'),
  ("lane-bounds-open", 'return lane >= laneCount_1;', 'return lane > laneCount_1;'),
  ("converge-without-all-work", 'if (length(worked) === laneCount && forAll(', 'if (length(worked) >= laneCount - 1 && forAll('),
  ("converge-without-broadcast", 'return deliveries.every((delivered) =>', 'return deliveries.length >= 0 || deliveries.every((delivered) =>'),
  ("takeover-overwritten", 'return matchValue_4.tag === 2 ? reject("ConflictingTakeover")', 'return matchValue_4.tag === 2 ? set$()'),
  ("other-lane-converges", 'return matchValue_7 === "match" ? set$() : matchValue_7 === "mismatch" ? reject("ConflictingTakeover")', 'return matchValue_7 === "match" ? set$() : matchValue_7 === "mismatch" ? set$()'),
  ("second-terminal", 'case 1:\n                return reject("TerminalAlreadySet");', 'case 1:\n                return set$();'),
  ("fail-after-converge", 'case "Converged": {\n                matchResult_1 = 2;', 'case "Converged": {\n                matchResult_1 = 1;'),
  ("foreign-completion", 'if (!flag("preFission", input["preFission"])) {', 'if (false) {'),
]
for name, old, new in mutants:
    src = pathlib.Path(L).read_text()
    assert src.count(old) == 1, (name, src.count(old))
    pathlib.Path(L).write_text(src.replace(old, new))
    try:
        r = subprocess.run([node, str(here / "fission-differential.mjs"), base, cand, "4000"], capture_output=True, text=True)
        found = re.findall(r'"differences": (\d+)', r.stdout)
        d = int(found[-1]) if found else -1
    finally:
        pathlib.Path(L).write_text(src)
    print(f"{name} differences={d} {'CAUGHT' if d != 0 else 'MISSED'}" + (" (candidate threw)" if d == -1 else ""))
