# CUT-12 semantic mutants of the candidate's wanxiangli/chat-execution dist.
# Usage: python3 mutants.py <ChatExecutionLaw.js> <name>   (writes the mutant in place; caller restores)
import sys
M = {
  'pre-start-completed': ("} else if (sameAccepted && !equals(disposition_1, Disposition.Completed)) {", "} else if (sameAccepted) {"),
  'restart-other-run-keeps': ("        if (sameStarted) {\n          matchResult = 3;\n        } else {\n          matchResult = 4;", "        if (true) {\n          matchResult = 3;\n        } else {\n          matchResult = 4;"),
  'terminal-replay-ignores-disposition': ("sameStarted && equals(established_1, disposition_1)", "sameStarted"),
  'start-after-terminal': ("        case 2: {\n          matchResult = 4;\n          d = matchValue.fields[0];", "        case 2: {\n          matchResult = 1;\n          d = matchValue.fields[0];"),
  'settle-completed-allowed': ('return equals(attempted_1, Disposition.Completed) ? verdict("invalidDisposition") :', 'return false ? verdict("invalidDisposition") :'),
  'terminal-other-run-existing': ('!same ? verdict("providerRunConflict") : equals(', '!same ? verdict("existing") : equals('),
  'settle-started': ('phase.tag === 1 ? verdict("providerAlreadyStarted")', 'phase.tag === 1 ? verdict("append")'),
}
path, name = sys.argv[1], sys.argv[2]
s = open(path).read()
old, new = M[name]
assert s.count(old) == 1, (name, s.count(old))
open(path, 'w').write(s.replace(old, new))
