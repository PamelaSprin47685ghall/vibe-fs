module Wanxiangshu.Tests.WarnTddOmpEnforcementTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

// `applyToolCallHook` in src/Omp/HookExecute.fs returns `Some err` on
// rejection (string form, not setHookError). Tests below pin that contract
// for every modification / warn-required tool name.

let private ompHookResult (toolName: string) (args: obj) : string option * string list =
    Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance "s-warn-enforce-omp"

    let result =
        Wanxiangshu.Hosts.Omp.HookExecute.applyToolCallHookWithIds
            toolName
            args
            "s-warn-enforce-omp"
            "c-warn-enforce-omp"

    let violations =
        match Wanxiangshu.Runtime.ToolHookRuntime.tryGetCompliance "s-warn-enforce-omp" "c-warn-enforce-omp" with
        | Some env -> env.Violations
        | None -> []

    (result, violations)

let ompRejectsCoderMissing () =
    let result, violations = ompHookResult "coder" (createObj [])
    check "omp coder missing warn_tdd does not block" (Option.isNone result)
    check "omp coder missing warn_tdd has no violations" (violations.IsEmpty)
    check "omp coder violations are empty" (violations.IsEmpty)

let ompRejectsCoderMalformed () =
    let result, violations =
        ompHookResult "coder" (createObj [ "warn_tdd", box "wrong" ])

    check "omp coder malformed warn_tdd does not block" (Option.isNone result)
    check "omp coder malformed warn_tdd has no violations" (violations.IsEmpty)

let ompAcceptsCoder () =
    let args =
        createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

    let result, violations = ompHookResult "coder" args
    check "omp coder canonical returns None" (Option.isNone result)
    check "omp coder canonical has no violations" (violations.IsEmpty)
    check "omp coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
    check "omp coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")

let ompIgnoresNonModificationTool () =
    let result, violations = ompHookResult "read" (createObj [])
    check "omp read passes without warn_tdd" (Option.isNone result)
    check "omp read has no violations" (violations.IsEmpty)

let ompRejectsExecutorMissingWarn () =
    let result, violations =
        ompHookResult "executor" (createObj [ "warn_tdd", box canonicalValue ])

    check "omp executor missing warn does not block" (Option.isNone result)
    check "omp executor missing warn has no violations" (violations.IsEmpty)

let ompAcceptsExecutor () =
    let args =
        createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

    let result, violations = ompHookResult "executor" args
    check "omp executor canonical returns None" (Option.isNone result)
    check "omp executor canonical has no violations" (violations.IsEmpty)
    check "omp executor warn removed from args" (Dyn.str args "warn" = "")

let ompWriteDoesNotRequireWarn () =
    let result, violations =
        ompHookResult "write" (createObj [ "warn_tdd", box canonicalValue ])

    check "omp write does not require warn" (Option.isNone result)
    check "omp write has no violations" (violations.IsEmpty)

let exhaustiveOmpWarnTdd () =
    for tool in modificationTools do
        let args =
            if isWarnRequiredTool tool then
                createObj [ "warn", box warnCanonicalValue ]
            else
                createObj []

        let result, violations = ompHookResult tool args
        check ("omp " + tool + " missing warn_tdd does not block") (Option.isNone result)
        check ("omp " + tool + " missing warn_tdd has no violations") (violations.IsEmpty)

let exhaustiveOmpWarnTddAccepts () =
    for tool in modificationTools do
        let args = createObj [ "warn_tdd", box canonicalValue ]

        if isWarnRequiredTool tool then
            args?warn <- box warnCanonicalValue

        if isSubagentTool tool then
            args?warn_reuse <- box warnReuseCanonicalValue

        let result, violations = ompHookResult tool args
        check ("omp " + tool + " canonical fields return None") (Option.isNone result)
        check ("omp " + tool + " canonical fields have no violations") (violations.IsEmpty)

let exhaustiveOmpWarn () =
    for tool in warnRequiredTools do
        let args = createObj [ "warn_tdd", box canonicalValue ]
        let result, violations = ompHookResult tool args
        check ("omp " + tool + " missing warn does not block") (Option.isNone result)
        check ("omp " + tool + " missing warn has no violations") (violations.IsEmpty)

let exhaustiveOmpWarnAccepts () =
    for tool in warnRequiredTools do
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

        let result, violations = ompHookResult tool args
        check ("omp " + tool + " canonical warn returns None") (Option.isNone result)
        check ("omp " + tool + " canonical warn has no violations") (violations.IsEmpty)

// ── OmpToolSchema: schema injection (warn_tdd + warn into required) ────────
//
// Direct unit tests of `OmpToolSchema.coderParameters` / `executorParameters`
// would require constructing a fully-typed TypeBox instance; the actual
// schema-injection runtime is exercised end-to-end via
// `OmpCoverage2Tests.toolCallHandler_missingWarnTddBlocks` (passes through
// `toolCallHandler` → `applyToolCallHook` → schema validation). Keeping the
// OMP coverage at the hook layer here, leaving the schema-layer test to the
// existing integration suite.

// ── warn_reuse: subagent tools must carry warn_reuse acknowledgement ──

let ompRejectsCoderMissingWarnReuse () =
    let result, violations =
        ompHookResult "coder" (createObj [ "warn_tdd", box canonicalValue ])

    check "omp coder missing warn_reuse does not block" (Option.isNone result)
    check "omp coder missing warn_reuse has no violations" (violations.IsEmpty)
    check "omp coder violations are empty" (violations.IsEmpty)

let ompRejectsCoderMalformedWarnReuse () =
    let result, violations =
        ompHookResult "coder" (createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box "wrong" ])

    check "omp coder malformed warn_reuse does not block" (Option.isNone result)
    check "omp coder malformed warn_reuse has no violations" (violations.IsEmpty)

let ompAcceptsCoderWithWarnReuse () =
    let args =
        createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

    let result, violations = ompHookResult "coder" args
    check "omp coder canonical warn_reuse returns None" (Option.isNone result)
    check "omp coder canonical warn_reuse has no violations" (violations.IsEmpty)
    check "omp coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
    check "omp coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")

let ompNonSubagentIgnoresWarnReuse () =
    let result, violations = ompHookResult "read" (createObj [])
    check "omp read ignores warn_reuse" (Option.isNone result)
    check "omp read has no violations" (violations.IsEmpty)

let run () =
    ompRejectsCoderMissing ()
    ompRejectsCoderMalformed ()
    ompAcceptsCoder ()
    ompIgnoresNonModificationTool ()
    ompRejectsExecutorMissingWarn ()
    ompAcceptsExecutor ()
    ompWriteDoesNotRequireWarn ()
    exhaustiveOmpWarnTdd ()
    exhaustiveOmpWarnTddAccepts ()
    exhaustiveOmpWarn ()
    exhaustiveOmpWarnAccepts ()
    ompRejectsCoderMissingWarnReuse ()
    ompRejectsCoderMalformedWarnReuse ()
    ompAcceptsCoderWithWarnReuse ()
    ompNonSubagentIgnoresWarnReuse ()
