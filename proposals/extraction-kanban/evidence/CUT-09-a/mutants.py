# Semantic mutants of the compiled wanxiangli/authority admit law (0.36.0 dist/authority/Authority.js).
import sys
M = {
 "M1-humanroot-while-active":   ("    if (active == null) {\n      return Admission.ExternalRoot;", "    if (true) {\n      return Admission.ExternalRoot;"),
 "M2-claim-humanroot-admissible": ("      if (origin.fields[0].tag === 0) {\n        matchResult = 1;", "      if (false) {\n        matchResult = 1;"),
 "M3-replay-ignores-host-internal": (")) && !input.HostInternal) {", "))) {"),
 "M4-missing-physical-only-with-carrier": ("AdmissionLaw_managedCarrier(input) ? true : active != null", "AdmissionLaw_managedCarrier(input)"),
 "M5-no-claim-session-check": ("const matchValue = claim.SessionId === sessionId;", "const matchValue = true;"),
 "M6-omitted-participant-unmanaged": ("      if (active == null) {\n        matchResult = 4;\n      } else {\n        matchResult = 3;", "      if (active == null) {\n        matchResult = 4;\n      } else {\n        matchResult = 4;"),
}
src = open(sys.argv[1] + ".orig").read()
old, new = M[sys.argv[2]]
assert src.count(old) == 1, (sys.argv[2], src.count(old))
open(sys.argv[1], "w").write(src.replace(old, new))
print(" ".join(M) if len(sys.argv) > 3 else "applied " + sys.argv[2])
