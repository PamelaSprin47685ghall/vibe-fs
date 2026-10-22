# CUT-12 sub-loop 3 mutants: rewrite the attachment-lease artifact the candidate actually loads, run
# lease-differential.mjs, restore. Usage: python3 mutants-12c.py <node> <base-root> <cand-root>
import subprocess, sys, re, pathlib
node, base, cand = sys.argv[1:4]
here = pathlib.Path(__file__).parent
L = f"{cand}/wanxiangli/dist/attachment-lease/AttachmentLeaseLaw.js"
mutants = [
  ("replacement-keeps-vanished-association", 'create("Replacement", true)', 'create("Replacement", false)'),
  ("title-ignored", "equals(child_1.Agent, agent) && equals(child_1.Title, title)", "equals(child_1.Agent, agent)"),
  ("no-association-replaces", 'return create("Created", false);', 'return create("Replacement", true);'),
  ("reused-child-aborted", 'originOf(origin) !== "Reused"', '(originOf(origin), true)'),
  ("duplicate-reused", 'reject("Ambiguous", hint_1)', 'Object.freeze({ verdict: "reuse", child: hint_1 })'),
  ("conflict-reused", 'reject("Conflicting", hint_1)', 'Object.freeze({ verdict: "reuse", child: hint_1 })'),
]
for name, old, new in mutants:
    src = pathlib.Path(L).read_text()
    assert src.count(old) == 1, (name, src.count(old))
    pathlib.Path(L).write_text(src.replace(old, new))
    try:
        out = subprocess.run([node, str(here / "lease-differential.mjs"), base, cand], capture_output=True, text=True).stdout
        d = int(re.findall(r'"differences": (\d+)', out)[-1])
    finally:
        pathlib.Path(L).write_text(src)
    print(f"{name} differences={d} {'CAUGHT' if d else 'MISSED'}")
