# HNI board

Claim like the extraction kanban: edit the row on latest master (Owner, DOING), commit `board(HNI-NN): claim`,
land rebase + ff. Ratchets (architecture ⑧, ⑨, HNI-04 thinness) only go down.

| Card | Title | Lane | Prereq | Owner | State |
|---|---|---|---|---|---|
| HNI-01 | DESIGN + REGISTRY + BOARD skeleton | arch | — | eng-host-arch-goose | DONE |
| HNI-02 | Declare P-00 vocabulary + port `.fsi/.fs` files under `src/Wanxiangshu/Host/` (types only, no consumers) | arch | 01 | eng-host-arch-goose | DONE |
| HNI-03 | omp 18.12.4 feasibility evidence + omp adapter skeleton type-checked against the ports with offline fixtures | arch | 01 | eng-host-arch-goose | DONE (evidence/omp-feasibility.md; open O-1..O-5) |
| HNI-04 | Architecture gate: adapter thinness (product decisions in adapter folders; adapter files with no neutral port) + honest baseline | arch | 01 | eng-host-arch-goose | DONE (gate ⑩ 695 / 151, ⑪ 0, ⑫ 0) |
| HNI-05 | Fission: move product logic out of `Execution/Fission/OpenCode` behind P-02 | fission | 02 | (fission lane) | DOING |
| HNI-06 | Fork/children: `Execution/Delegation/Fork/OpenCode` + `Execution/*/OpenCode` behind P-01/P-03 (+ P-13..P-15) | fork | 02 | eng-hni-fork-goose | DOING (fork lane: 5 commits landed on master `58660d4d9..e16e72c34` + slice B `JoinRules`; gate ⑩ green 675/146 — HorizonSurface 2→1, HostSignalBootstrap 14→13, no baseline edit, arch re-baseline request withdrawn; NOT built/tested — only the non-compiling gates were run) |
| HNI-07 | Tools: ToolRegistry/ToolRuntimeScope/Codec behind P-04/P-05 | tools | 02 | (tools lane) | DOING |
| HNI-08 | Plugin host: Plugin/Host behind P-06..P-09, P-11, P-12; product = `HostPorts -> ProductHooks` | plugin-host | 02 | (plugin-host lane) | DOING |
| HNI-09 | Leftovers: Mission/Work, Repository, Sphinx V2 hosts, Context/Prefix, Interaction/Dispatch `OpenCode` folders | domains (this lane) | 02 | domain-cores | DOING |
| HNI-10 | Retire host-shaped Foundation names (`OpenCodePromptOptions`, `OpenCodeChildOptions`, `OpenCodeChildInfo`, `OpencodeModel`, `IOpenCodePort`) via aliases then deletion | arch | 05–08 | — | BACKLOG |
| HNI-11 | Drift review of lane port additions (names, shapes, HostError use, registry rows) | arch | 05–08 running | eng-host-arch-goose | DOING (continuous) — D-05/D-06/D-07 recorded in REGISTRY.md; gate ⑫ (port-family identity) added in `f214ece07`; the tools lane's `Host/Tools.fs` must be renamed before it moves under `Host/Ports/` (D-07) |
| HNI-13 | omp open items O-1 (entry ids), O-2 (provider params encode), O-3 (Fable-built product bound to `activateOnOmp`), O-4 (`parentSession` may be a session file path, not an id), O-5 (`AgentSession.sessionId` is the *provider* id, not the persisted one — the neutral `SessionId` is `sessionManager.getSessionId()`) | arch | 08 | — | BACKLOG |
| HNI-14 | Converge lane domain ports (D-02, D-03, D-04 in REGISTRY.md): split P-14/P-15/P-17 into neutral HostPorts + Composition inputs; merge P-18 `HostToolContext` with `ToolInvocation`; move `ToolResultText` to a product module | arch + owning lanes | 05–08 | — | BACKLOG |
| HNI-12 | Final accounting: every REGISTRY row WIRED-OC + WIRED-OMP or explicitly RETAINED with reason; gate counts | arch | all | — | BACKLOG |

Lane worker ids: fill the Owner column when you claim (lanes started 06:05 UTC: fission, fork, tools, plugin-host).

## Gate ⑩/⑪/⑫ (HNI-04) — `scripts/checks/adapter-thinness.mjs`, run inside `architecture.mjs`
- ⑩a product-reference: distinct product-domain namespaces (`Wanxiangshu.<Execution|Interaction|Mission|Context|…>`, not
  `*.OpenCode`) referenced per adapter file (`OpenCode/**`, `**/OpenCode/**`, future `Omp/**`). Start **695** over 151 files.
- ⑩b without-port: adapter files that use no host-neutral port type. Start **151 / 151** (no adapter is wired yet).
- Totals only go down; a baselined file may not grow; moves/splits are free while totals hold. Improvements don't fail;
  the arch lane tightens with `node scripts/checks/adapter-thinness.mjs --write-baseline`.
- ⑪ port-purity (strict, 0): `Host/Ports/<family>.fs` may reference only Foundation/Host/System and no host vocabulary.
- ⑫ port-family integrity (strict, 0): every family under `Host/Ports/` declares **exactly one** reserved id (`HNI P-xx` in its
  header); no two families claim the same id; no file directly under `Host/` may take the base name of a neutral family
  (the **D-07** check). The scope is discovered from the sources, so a family another lane lands is covered without
  editing the gate. ⑫ deliberately does **not** gate the namespace (drift **D-05**): the namespace is the lane's own
  decision, so it stays a written requirement in `REGISTRY.md` (7-step landing checklist), not a rule the gate guesses at.
