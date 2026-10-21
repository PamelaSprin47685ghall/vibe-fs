# HNI-04 — the adapter-thinness gate (⑩) and the port-purity gate (⑪)

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
- a **baselined file** grew,
- a **newly added** adapter file carries product-domain references at all, or
- the number of adapter files without a neutral port rose.

The baseline may only be **written on a clean run**: `--write-baseline` computes the decision
(`baselineWritePlan`) and refuses to touch the file when any violation exists, printing the violations and
`… was NOT modified`. A regression therefore cannot raise the ratchet, and the arch lane tightens the baseline
only when the tree is clean.

Regression test: `requirements/host-boundary/tests/034.test.mjs` (4 assertions) — a violation refuses the
baseline write and leaves the baseline file byte-identical; a new port family is discovered from the sources and
covered by the purity scope; a per-file product-reference growth is reported even when the total is flat; the
CLI refuses `--write-baseline` on a regression.

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

The counts are therefore a **downward-only tripwire** for the lanes: they catch the obvious regressions and
nothing more. They are not a measurement of adapter purity in general.
