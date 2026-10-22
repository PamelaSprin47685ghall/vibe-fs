# CUT-28 sub-scope ② mutants on the fission-group artifact the candidate loads; runs
# topology-differential.mjs. Usage: python3 mutants-topology.py <node> <base-root> <cand-root>
import subprocess, sys, re, pathlib
node, base, cand = sys.argv[1:4]
here = pathlib.Path(__file__).parent
L = f"{cand}/wanxiangli/dist/fission-group/FissionGroupLaw.js"
mutants = [
  ("one-lane-request", 'if (length(prompts_1) < 2) {', 'if (length(prompts_1) < 1) {'),
  ("blank-lane-allowed", 'const matchValue = tryFindIndex(isNullOrWhiteSpace, prompts_1);', 'const matchValue = void 0;'),
  ("broadcast-skips-last", 'return intArray(toList(rangeDouble(0, 1, laneCount - 1)));', 'return intArray(toList(rangeDouble(0, 1, laneCount - 2)));'),
  ("affinity-out-of-range", 'lane_1 >= 0 && lane_1 < laneCount ? singleton(lane_1)', 'lane_1 >= 0 ? singleton(lane_1)'),
  ("ring-for-one", 'return intArray(laneCount_1 < 2 ? empty()', 'return intArray(laneCount_1 < 1 ? empty()'),
  ("final-lane-first", 'return laneCount_1 < 2 ? defaultOf() : laneCount_1 - 1;', 'return laneCount_1 < 2 ? defaultOf() : 0;'),
  ("successor-ignores-closed", 'tryFind((candidate) => !contains(candidate, closed),', 'tryFind((candidate) => true,'),
  ("successor-skips-self", 'toList(rangeDouble(1, 1, laneCount))', 'toList(rangeDouble(1, 1, laneCount - 1))'),
  ("failed-provider-fails-group", 'case "ProviderFailed":\n      case "DegenerationInterrupted":\n        return Object.freeze({\n          decision: "yield"', 'case "DegenerationInterrupted":\n        return Object.freeze({\n          decision: "yield"\n        });\n      case "ProviderFailed":\n        return Object.freeze({\n          decision: "fail"'),
  ("abort-yields", 'case "ExternalAbort":\n        return Object.freeze({\n          decision: "fail"', 'case "ExternalAbort":\n        return Object.freeze({\n          decision: "yield"'),
  ("ready-without-delivery", 'return deliveries.every((delivered) =>', 'return deliveries.length >= 0 || deliveries.every((delivered) =>'),
]
for name, old, new in mutants:
    src = pathlib.Path(L).read_text()
    assert src.count(old) == 1, (name, src.count(old))
    pathlib.Path(L).write_text(src.replace(old, new))
    try:
        r = subprocess.run([node, str(here / "topology-differential.mjs"), base, cand], capture_output=True, text=True)
        found = re.findall(r'"differences": (\d+)', r.stdout)
        d = int(found[0]) if found else -1
    finally:
        pathlib.Path(L).write_text(src)
    print(f"{name} differences={d} {'CAUGHT' if d != 0 else 'MISSED'}" + (" (candidate threw)" if d == -1 else ""))
