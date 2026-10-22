# CUT-12 sub-loops 1 & 4 mutants: rewrite the library artifact the candidate actually loads, run
# the matching differential, restore. Usage: python3 mutants-12b.py <node> <base-root> <cand-root>
import subprocess, sys, re, pathlib
node, base, cand = sys.argv[1:4]
here = pathlib.Path(__file__).parent
A = f"{cand}/wanxiangli/dist/association/SessionAssociationLaw.js"
C = f"{cand}/wanxiangli/dist/chat-execution/ChatExecutionLaw.js"
mutants = [
  ("self-link-allowed", A, "if (main === satellite) {", "if (false) {", "association-differential.mjs"),
  ("recursion-allowed", A, "if (mainEntry.tag === 1) {\n          matchResult = 0;", "if (false) {\n          matchResult = 0;", "association-differential.mjs"),
  ("other-companion-allowed", A, "if (existing !== satellite) {", "if (false) {", "association-differential.mjs"),
  # Satellite-kind mismatch (fields[1] !== kind) is unreachable while SatelliteKind has one case
  # (Companion); that mutant is equivalent, so the conflict mutant targets a Work session claimed as satellite.
  ("work-as-satellite-allowed", A, "if (satelliteEntry.tag === 0) {\n              matchResult_1 = 2;", "if (false) {\n              matchResult_1 = 2;", "association-differential.mjs"),
  ("stale-not-first", C, "if (stale) {\n    return ignore$0027(\"StalePhysicalEvidence\");", "if (false) {\n    return ignore$0027(\"StalePhysicalEvidence\");", "recovery-differential.mjs"),
  ("no-new-write-trusted", C, "case \"NoNewWrite\":\n            return manual(\"PersistenceOutcomeUnknown\");", "case \"NoNewWrite\":\n            return ignore$0027(\"RecoveryAlreadyCommitted\");", "recovery-differential.mjs"),
  ("alive-other-run-kept", C, "if (same()) {\n                    matchResult = 2;", "if (true) {\n                    matchResult = 2;", "recovery-differential.mjs"),
  ("absent-resumes-started", C, "case \"Absent\":\n                  return decision(\"resumePreProvider\", empty());", "case \"Absent\":\n                  return manual(\"PhysicalOutcomeUnknown\");", "recovery-differential.mjs"),
]
for name, path, old, new, script in mutants:
    src = pathlib.Path(path).read_text()
    assert src.count(old) >= 1, (name, src.count(old))
    pathlib.Path(path).write_text(src.replace(old, new))
    try:
        out = subprocess.run([node, str(here / script), base, cand], capture_output=True, text=True).stdout
        d = int(re.findall(r'"differences": (\d+)', out)[-1])
    finally:
        pathlib.Path(path).write_text(src)
    print(f"{name} differences={d} {'CAUGHT' if d else 'MISSED'}")
