# Provider semantic resources

Each localizable asset has a **stable semantic path** under `resources/provider/`.
Language is the **leaf filename**: `en.md` and `zh-CN.md`.

```text
resources/provider/
  role/manager/en.md
  role/manager/zh-CN.md
  role/engineer/en.md
  ...
```

Invariant identifiers (tool names, argument names, wire fields, enum literals, paths,
commands, `exit_code`) stay untranslated inside localized prose.

Every semantic directory carries both locale files, the same `{{placeholder}}` set,
and the same protocol identifiers in both languages. These are structural facts of the
resource format, not a gate to satisfy.

## One current account of the work

The public prompt catalog contains Orchestrator, Manager, Engineer, DevOps, and
Plan. Bookkeeper and Advisor have separate internal prompts. Sphinx is
program-controlled work, not another role to dispatch.
Retired role names must not resolve to an Engineer prompt or appear as public
catalog aliases. Historical decoding is not permission to load a live prompt.

The working chain is Manager → Engineer source work → Manager → fixed DevOps
execution and ordinary repair → Manager assessment. Read-only investigation is
an Engineer assignment. Fission expands one authorized Engineer assignment,
not Manager identity or DevOps ownership. Every projection keeps the difference
between source completion, current execution facts, and the user's acceptance.

Change the whole affected story, not only `role/`: `world/`, `library/`, `host/`,
`lifecycle/`, `runtime/`, caller-facing tool descriptions and denials, generated
JS examples must agree in both languages. Model pools choose
physical targets; they do not grant new roles or resurrect retired ones.

## Stage and evidence resources

`casebook/finalize` accompanies a completed Engineer trace. `casebook/refresh`
accompanies a stored case and a real related-file diff. Both are loaded with
the Bookkeeper law in the owner's bound language. Source observations are
access records, not an implementation patch. Maintenance status is not a new
repository investigation.

`tool/js-program/ultra-engineering`, `ultra-investigation`, and `ultra-devops`
are executable examples selected by the actual role and capabilities. The
read-only example must not show edits; a DevOps example must not reduce it to
an operator that hands source repairs to another role. No management or retired
role receives an office-specific filesystem example.

When editing these resources: prompts teach the model what it may do and what
the facts mean; they do not ask it to present proofs, maintain ledgers before
understanding the request, or grant acceptance on the user's behalf. An
explicit denial is not an affordance; do not copy old wording into a new role
law just to match a phrase somewhere else.
