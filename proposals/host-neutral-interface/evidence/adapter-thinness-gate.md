# HNI-04 — the adapter-thinness gate (⑩), the port-purity gate (⑪) and the port-family gate (⑫)

Source: `scripts/checks/adapter-thinness.mjs`, run inside `scripts/checks/architecture.mjs`.
Baseline: `scripts/checks/adapter-thinness-baseline.json`.

## What gate ⑩ measures

For every **adapter file** (a `.fs` file under an `…/OpenCode/` or `…/Omp/` adapter folder), the gate counts the
distinct **product-domain namespaces** the file references — `Wanxiangshu.<Domain>…` where `<Domain>` is one of
Ablation, Change, Composition, Context, Execution, Git, Interaction, Mission, Participant, Persistence, Process,
Repository, Sphinx, Verification. It also counts the adapter files that reference **no** host-neutral port type at
all.

Current measurement: **151 adapter files, 40541 lines, product-reference 695, without-port 151.**

## What gate ⑪ measures

Every file under `src/Wanxiangshu/Host/Ports/**` — implementation (`.fs`) **and** signature (`.fsi`) — may
reference only `Foundation`, `Wanxiangshu.Host` and `System`, and no host vocabulary (`OpenCode`, `omp`). The
scope is **discovered from the sources**: a port family another lane lands under `Host/Ports/` is covered without
editing the gate. Current measurement: **0 violations.**

## The ratchet may only go down

`scripts/checks/adapter-thinness-baseline.json` records the per-file product-reference counts and the list of
adapter files without a port. A run fails when

- the product-reference **total** rose,
- a **baselined file** grew, or
- the number of adapter files without a neutral port rose.

A file that is **not** in the baseline is allowed to carry product-domain references while the totals stay within
the baseline. A lane that moves or splits a baselined adapter file therefore never has to edit the baseline
(PROTOCOL: the lanes never edit the baseline). The per-file rule still fires for a baselined file that grows, so
the growth of one file cannot be hidden by the shrinkage of another.

The baseline may only be **written on a clean run**: `--write-baseline` computes the decision
(`baselineWritePlan`) and refuses to touch the file when any violation exists, printing the violations and
`… was NOT modified`. A regression therefore cannot raise the ratchet, and the arch lane tightens the baseline
only when the tree is clean.

An **unreadable port file is a hard error**: the gate reads every port file it scans, so a file that cannot be
read aborts the run instead of silently shrinking the neutral-port vocabulary and hiding the file from the scope.

Regression test: `requirements/host-boundary/tests/034.test.mjs` (5 tests) — a violation refuses the
baseline write and leaves the baseline file byte-identical; a new port family is discovered from the sources and
covered by the purity scope; a per-file product-reference growth is reported even when the total is flat; an
adapter file that is not in the baseline is allowed while the totals stay within the baseline; the CLI refuses
`--write-baseline` on a regression.

## The TypeScript mirror cannot fall behind

`proposals/host-neutral-interface/adapters/omp/ports.ts` is **generated** from the F# port signatures by
`gen-ports.mjs` and verified by `mirror-check.mjs`. The port families are **discovered** from
`src/Wanxiangshu/Host/Ports/*.fsi`; each family's reserved port id is the `HNI P-xx` marker in its header, and a
family without that marker is a hard error. A family another lane lands therefore shows up in the mirror (or
fails the check) instead of silently escaping it — there is no hand-maintained family list any more.

## What gate ⑫ measures (port-family integrity)

The mirror generator keys on two things a port family carries: the `HNI P-xx` marker in its header and the
family's **file name** (the `.fsi` base name is the family, and it is also the TS mirror section name). Gate ⑫
checks both, so a drift is reported by the gate instead of by a failing build or a silently overwritten file:

- every family signature under `Host/Ports/` declares **exactly one** reserved id in an `HNI P-xx` header marker
  (the generator matches `/\bHNI (P-\d{2})\b/` and hard-errors on a family without one);
- **no two families claim the same id**;
- **no file directly under `Host/` takes the base name of a neutral family.** The tools lane's `Host/Tools.fs(i)`
  is the live case (drift D-07): it shares its name with P-04's `Host/Ports/Tools.fs(i)`, and the moment the lane
  moves it under `Host/Ports/` it overwrites the registration port. The lane must rename it first.

The scope is discovered from the sources, so a family another lane lands is covered without editing the gate.
Current measurement: **0 violations.**

Gate ⑫ does **not** check the namespace (drift D-05): a lane file that declares `namespace Wanxiangshu.Host`
is reported by nobody, because the namespace half of that drift needs the lane's own decision about which
namespace it should use (`Wanxiangshu.Host.Domain.<Lane>`), not a rule the gate can apply on its own. The
requirement is written down in `REGISTRY.md` instead.

## Honest limits of the heuristic

The gate is a **proxy**, not a proof. It does **not** establish that an adapter takes no product decision.

- It counts **namespace references**. A product decision written as an inline literal, a shape-agnostic call, a
  magic string or a copied algorithm is invisible to it.
- The "without-port" count is per **file**. A file that references a port type once but is otherwise product
  logic still counts as port-bearing.
- A product decision expressed in a file **outside** an adapter folder (a shared helper, a composition module) is
  not counted at all.
- The purity gate checks **references**, not semantics: a port file that spells out product behaviour without
  naming a product namespace passes.
- The retained legacy composition seams (the P-14 delegation-caller, P-15 delegation-command and P-17 fission
  port shapes) live in `src/Wanxiangshu/Host/*.fs`, **not** under `Host/Ports/`. They are RETAINED legacy shapes,
  not neutral ports, and are deliberately outside the strict purity scope — the gate does not pretend they are
  ports. Card HNI-14 tracks their convergence.
- `src/Wanxiangshu/Host/**` outside `Host/Ports/` is neutral/product host code, not adapter code, so it is not
  counted as "adapter" either; a future neutral-host adapter folder convention is not covered by this gate yet.
- Gate ⑫ checks the **identity** of a port family (its reserved id and its file name), not its content. It does
  not verify that a family declares the types its reserved id promises, nor that the id matches its REGISTRY row.

The counts are therefore a **downward-only tripwire** for the lanes: they catch the obvious regressions and
nothing more. They are not a measurement of adapter purity in general.
