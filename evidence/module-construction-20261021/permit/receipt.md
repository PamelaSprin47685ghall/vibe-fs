# Typed projection boundary repair

Scope: temporary overlay only. Root explicitly authorized the independent narrow Fable project. Main source, dist, stage and build remain untouched. Probe compiles actual copied Roles.fs, Model.fsi/fs and Surface.fs, plus one narrow F# module that uses the real internal `ExecutionAdmissionLease.Create` constructor and returns a plain DTO. It does not consume Fable Result/List representations from JS.

Actual counterexample: construct a new typed wrapper carrying the genuine lease's Owner/leaseId/fence, but a different credit, participant and target. Use the genuine observed execution identity when submitting it.

| owner variant | Target/Lifecycle | Commit | before-provider Release | genuine state after attempts |
|---|---|---|---|---|
| unchanged main baseline | rejected | StaleFence | StaleFence | Pending |
| first overlay | accepted | Applied | Applied | Committed / Released |
| repaired overlay | rejected | StaleFence | StaleFence | Pending |

Each variant also actually commits the real lease and reads back the same typed reference. Those positive controls pass. The strict contract runner exits0 for baseline,1 for the first overlay,0 for the repaired overlay under Node22; repaired overlay exits0 under Bun too. Original logs and all copied source variants remain here. Repaired Fable compile emits0 warnings; baseline has two preexisting arr.[idx] syntax warnings, first overlay had six implicit-obj warnings now resolved by local annotations.

Fix uses only the existing projection WeakMap. `permitOf` requires the map's raw->typed reference to be the incoming typed lease. Null returns UnknownLease, foreign projection WrongFence, copied typed wrapper StaleLease. Target/Lifecycle preserve typed rejection results; commit/release map rejected presentation to StaleFence and record once. The package still makes lifecycle/current-permit decisions; no second current-by-session/lifecycle/counter map was introduced. Target still maps the validated owner DTO.

Public exposure review: ModelRoutingSurface holds token->typed lease in a private WeakMap; clone/forged JS token is never registered, and no public API lets its caller choose a raw Owner. This blocks the present public attack path, but does not justify weakening internal typed constructor authority. Real Create call sites are the owner and two TransactionSurface fixture sites.

Removed dead CapacityLeaseId/CapacityFence `initial` and `next` from overlay Model.fs/.fsi. Retained `first` as a direct immutable value because the two ChatAdmission TransactionSurface fixtures actually use it. Retained value/ofValue for the real typed bridge. No counter seed or mutable minting remained in main.

Updated overlay: `/private/tmp/vibe-fs-mf05-permit-overlay`. Its three source patch files in `/private/tmp/vibe-fs-mf05-permit-audit` were refreshed. Control-pyramid over overlay reports0 nested decisions. This is narrow compiled source evidence, not a main requirement-suite or actual Host acceptance claim.
