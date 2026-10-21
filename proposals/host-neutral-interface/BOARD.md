# HNI board

Claim like the extraction kanban: edit the row on latest master (Owner, DOING), commit `board(HNI-NN): claim`,
land rebase + ff. Ratchets (architecture ⑧, ⑨, HNI-04 thinness) only go down.

| Card | Title | Lane | Prereq | Owner | State |
|---|---|---|---|---|---|
| HNI-01 | DESIGN + REGISTRY + BOARD skeleton | arch | — | eng-host-arch-goose | DONE |
| HNI-02 | Declare P-00 vocabulary + port `.fsi/.fs` files under `src/Wanxiangshu/Host/` (types only, no consumers) | arch | 01 | eng-host-arch-goose | DONE |
| HNI-03 | omp 18.12.4 feasibility evidence + omp adapter skeleton type-checked against the ports with offline fixtures | arch | 01 | eng-host-arch-goose | DONE (evidence/omp-feasibility.md; open O-1..O-4) |
| HNI-04 | Architecture gate: adapter thinness (product decisions in adapter folders; adapter files with no neutral port) + honest baseline | arch | 01 | eng-host-arch-goose | DONE (gate ⑩ 695 / 151, ⑪ 0) |
| HNI-05 | Fission: move product logic out of `Execution/Fission/OpenCode` behind P-02 | fission | 02 | (fission lane) | DOING |
| HNI-06 | Fork/children: `Execution/Delegation/Fork/OpenCode` + `Execution/*/OpenCode` behind P-01/P-03 | fork | 02 | (fork lane) | DOING |
| HNI-07 | Tools: ToolRegistry/ToolRuntimeScope/Codec behind P-04/P-05 | tools | 02 | (tools lane) | DOING |
| HNI-08 | Plugin host: Plugin/Host behind P-06..P-09, P-11, P-12; product = `HostPorts -> ProductHooks` | plugin-host | 02 | (plugin-host lane) | DOING |
| HNI-09 | Leftovers: Mission/Work, Repository, Sphinx V2 hosts, Context/Prefix, Interaction/Dispatch `OpenCode` folders | first free lane | 02 | — | BACKLOG |
| HNI-10 | Retire host-shaped Foundation names (`OpenCodePromptOptions`, `OpenCodeChildOptions`, `OpenCodeChildInfo`, `OpencodeModel`, `IOpenCodePort`) via aliases then deletion | arch | 05–08 | — | BACKLOG |
| HNI-11 | Drift review of lane port additions (names, shapes, HostError use, registry rows) | arch | 05–08 running | eng-host-arch-goose | DOING (continuous) |
| HNI-13 | omp open items O-1 (entry ids), O-2 (provider params encode), O-3 (Fable-built product bound to `activateOnOmp`), O-4 (`parentSession` may be a session file path, not an id) | arch | 08 | — | BACKLOG |
| HNI-14 | Converge lane domain ports (D-02, D-03, D-04 in REGISTRY.md): split P-14/P-15/P-17 into neutral HostPorts + Composition inputs; merge P-18 `HostToolContext` with `ToolInvocation`; move `ToolResultText` to a product module | arch + owning lanes | 05–08 | — | BACKLOG |
| HNI-12 | Final accounting: every REGISTRY row WIRED-OC + WIRED-OMP or explicitly RETAINED with reason; gate counts | arch | all | — | BACKLOG |

Lane worker ids: fill the Owner column when you claim (lanes started 06:05 UTC: fission, fork, tools, plugin-host).

## Gate ⑩/⑪ (HNI-04) — `scripts/checks/adapter-thinness.mjs`, run inside `architecture.mjs`
- ⑩a product-reference: distinct product-domain namespaces (`Wanxiangshu.<Execution|Interaction|Mission|Context|…>`, not
  `*.OpenCode`) referenced per adapter file (`OpenCode/**`, `**/OpenCode/**`, future `Omp/**`). Start **695** over 151 files.
- ⑩b without-port: adapter files that use no host-neutral port type. Start **151 / 151** (no adapter is wired yet).
- Totals only go down; a baselined file may not grow; moves/splits are free while totals hold. Improvements don't fail;
  the arch lane tightens with `node scripts/checks/adapter-thinness.mjs --write-baseline`.
- ⑪ port-purity (strict, 0): `Host/Ports/<family>.fs` may reference only Foundation/Host/System and no host vocabulary.
