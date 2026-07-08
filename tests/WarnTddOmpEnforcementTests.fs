module Wanxiangshu.Tests.WarnTddOmpEnforcementTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

// `applyToolCallHook` in src/Omp/HookExecute.fs returns `Some err` on
// rejection (string form, not setHookError). Tests below pin that contract
// for every modification / warn-required tool name.

let private ompHookResult (toolName: string) (args: obj) : string option =
    Wanxiangshu.Omp.HookExecute.applyToolCallHook toolName args

let ompRejectsCoderMissing () =
    let result = ompHookResult "coder" (createObj [])
    check "omp coder missing warn_tdd returns Some" (Option.isSome result)
    check "omp coder error mentions warn_tdd" (result.IsSome && result.Value.Contains "warn_tdd")

let ompRejectsCoderMalformed () =
    let result = ompHookResult "coder" (createObj [ "warn_tdd", box "wrong" ])
    check "omp coder malformed warn_tdd returns Some" (Option.isSome result)

let ompAcceptsCoder () =
    let args = createObj [ "warn_tdd", box canonicalValue ]
    let result = ompHookResult "coder" args
    check "omp coder canonical returns None" (Option.isNone result)
    check "omp coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")

let ompIgnoresNonModificationTool () =
    check "omp read passes without warn_tdd" (ompHookResult "read" (createObj []) |> Option.isNone)

let ompRejectsExecutorMissingWarn () =
    let result = ompHookResult "executor" (createObj [ "warn_tdd", box canonicalValue ])
    check "omp executor missing warn returns Some" (Option.isSome result)
    check "omp executor warn error mentions warn" (result.IsSome && result.Value.Contains "warn")

let ompAcceptsExecutor () =
    let args =
        createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

    let result = ompHookResult "executor" args
    check "omp executor canonical returns None" (Option.isNone result)
    check "omp executor warn removed from args" (Dyn.str args "warn" = "")

let ompWriteDoesNotRequireWarn () =
    let result = ompHookResult "write" (createObj [ "warn_tdd", box canonicalValue ])
    check "omp write does not require warn" (Option.isNone result)

let exhaustiveOmpWarnTdd () =
    for tool in modificationTools do
        let args =
            if isWarnRequiredTool tool then
                createObj [ "warn", box warnCanonicalValue ]
            else
                createObj []

        let result = ompHookResult tool args
        check ("omp " + tool + " missing warn_tdd returns Some") (Option.isSome result)

let exhaustiveOmpWarnTddAccepts () =
    for tool in modificationTools do
        let args =
            if isWarnRequiredTool tool then
                createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]
            else
                createObj [ "warn_tdd", box canonicalValue ]

        let result = ompHookResult tool args
        check ("omp " + tool + " canonical fields return None") (Option.isNone result)

let exhaustiveOmpWarn () =
    for tool in warnRequiredTools do
        let args = createObj [ "warn_tdd", box canonicalValue ]
        let result = ompHookResult tool args
        check ("omp " + tool + " missing warn returns Some") (Option.isSome result)

let exhaustiveOmpWarnAccepts () =
    for tool in warnRequiredTools do
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

        let result = ompHookResult tool args
        check ("omp " + tool + " canonical warn returns None") (Option.isNone result)

// ── OmpToolSchema: schema injection (warn_tdd + warn into required) ────────
//
// Direct unit tests of `OmpToolSchema.coderParameters` / `executorParameters`
// would require constructing a fully-typed TypeBox instance; the actual
// schema-injection runtime is exercised end-to-end via
// `OmpCoverage2Tests.toolCallHandler_missingWarnTddBlocks` (passes through
// `toolCallHandler` → `applyToolCallHook` → schema validation). Keeping the
// OMP coverage at the hook layer here, leaving the schema-layer test to the
// existing integration suite.

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
