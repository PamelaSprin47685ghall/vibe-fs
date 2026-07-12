module Wanxiangshu.Tests.WarnTddMuxEnforcementTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

// `requireWarnTddMux` and `requireWarnMux` in src/Mux/PluginCatalog.fs must
// call `setHookErrorMux` whenever a modification tool (resp. warn-required
// tool) omits the canonical acknowledgement. Mirrors the Opencode tests.

let private runMuxHook (tool: string) (args: obj) : JS.Promise<string * string list> =
    let input =
        createObj
            [ "tool", box tool
              "workspaceId", box "s-warn-enforce-mux"
              "toolCallId", box "c-warn-enforce-mux"
              "args", box args ]

    let output = createObj [ "args", box args ]

    promise {
        Wanxiangshu.Shell.ToolHookRuntime.clearSessionCompliance "s-warn-enforce-mux"
        do! Wanxiangshu.Mux.PluginCatalog.toolExecuteBefore input output
        let err = str output "error"

        let violations =
            match Wanxiangshu.Shell.ToolHookRuntime.tryGetCompliance "s-warn-enforce-mux" "c-warn-enforce-mux" with
            | Some env -> env.Violations
            | None -> []

        return (err, violations)
    }

let private runRaw (tool: string) : JS.Promise<string * string list> = runMuxHook tool (createObj [])

let private runWithWarnTdd (tool: string) : JS.Promise<string * string list> =
    runMuxHook tool (createObj [ "warn_tdd", box canonicalValue ])

let muxRejectsCoderMissing () =
    promise {
        let! err, violations = runRaw "coder"
        check "mux coder missing warn_tdd does not reject" (err = "")
        check "mux coder missing warn_tdd has violations" (violations.Length > 0)
        check "mux coder violations mention warn_tdd" (violations |> List.exists (fun x -> x.Contains "warn_tdd"))
    }

let muxRejectsCoderMalformed () =
    promise {
        let! err, violations = runMuxHook "coder" (createObj [ "warn_tdd", box "wrong" ])
        check "mux coder malformed warn_tdd does not reject" (err = "")
        check "mux coder malformed warn_tdd has violations" (violations.Length > 0)
    }

let muxAcceptsCoder () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

        let! err, violations = runMuxHook "coder" args
        check "mux coder canonical passes" (err = "")
        check "mux coder canonical has no violations" (violations.IsEmpty)
        check "mux coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
        check "mux coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")
    }

let muxIgnoresNonModificationTool () =
    promise {
        let! err, violations = runRaw "read"
        check "mux read passes" (err = "")
        check "mux read has no violations" (violations.IsEmpty)
    }

let muxRejectsExecutorMissingWarn () =
    promise {
        let! err, violations = runWithWarnTdd "executor"
        check "mux executor missing warn does not reject" (err = "")

        check
            "mux executor missing warn has violations"
            (violations |> List.exists (fun x -> x.Contains "warn: missing"))
    }

let muxAcceptsExecutor () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

        let! err, violations = runMuxHook "executor" args
        check "mux executor canonical passes" (err = "")
        check "mux executor canonical has no violations" (violations.IsEmpty)
        check "mux executor warn removed from args" (Dyn.str args "warn" = "")
    }

let muxWriteDoesNotRequireWarn () =
    promise {
        let! err, violations = runWithWarnTdd "write"
        check "mux write does not require warn" (err = "")
        check "mux write has no violations" (violations.IsEmpty)
    }

let exhaustiveMuxWarnTdd () : JS.Promise<unit> =
    promise {
        for tool in modificationTools do
            let args =
                if isWarnRequiredTool tool then
                    createObj [ "warn", box warnCanonicalValue ]
                else
                    createObj []

            let! err, violations = runMuxHook tool args
            check ("mux " + tool + " missing warn_tdd does not reject") (err = "")
            check ("mux " + tool + " missing warn_tdd has violations") (violations.Length > 0)
    }

let exhaustiveMuxWarnTddAccepts () : JS.Promise<unit> =
    promise {
        for tool in modificationTools do
            let args = createObj [ "warn_tdd", box canonicalValue ]

            if isWarnRequiredTool tool then
                args?warn <- box warnCanonicalValue

            if isSubagentTool tool then
                args?warn_reuse <- box warnReuseCanonicalValue

            let! err, violations = runMuxHook tool args
            check ("mux " + tool + " canonical fields pass") (err = "")
            check ("mux " + tool + " canonical fields have no violations") (violations.IsEmpty)
    }

let exhaustiveMuxWarn () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let args = createObj [ "warn_tdd", box canonicalValue ]
            let! err, violations = runMuxHook tool args
            check ("mux " + tool + " missing warn does not reject") (err = "")
            check ("mux " + tool + " missing warn has violations") (violations.Length > 0)
    }

let exhaustiveMuxWarnAccepts () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let args =
                createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

            let! err, violations = runMuxHook tool args
            check ("mux " + tool + " canonical warn passes") (err = "")
            check ("mux " + tool + " canonical warn has no violations") (violations.IsEmpty)
    }

// ── warn_reuse: subagent tools must carry warn_reuse acknowledgement ──

let muxRejectsCoderMissingWarnReuse () =
    promise {
        let! err, violations = runMuxHook "coder" (createObj [ "warn_tdd", box canonicalValue ])
        check "mux coder missing warn_reuse does not reject" (err = "")
        check "mux coder missing warn_reuse has violations" (violations.Length > 0)
        check "mux coder violations mention warn_reuse" (violations |> List.exists (fun x -> x.Contains "warn_reuse"))
    }

let muxRejectsCoderMalformedWarnReuse () =
    promise {
        let! err, violations =
            runMuxHook "coder" (createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box "wrong" ])

        check "mux coder malformed warn_reuse does not reject" (err = "")
        check "mux coder malformed warn_reuse has violations" (violations.Length > 0)
    }

let muxAcceptsCoderWithWarnReuse () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

        let! err, violations = runMuxHook "coder" args
        check "mux coder canonical warn_reuse passes" (err = "")
        check "mux coder canonical warn_reuse has no violations" (violations.IsEmpty)
        check "mux coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
        check "mux coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")
    }

let muxNonSubagentIgnoresWarnReuse () =
    promise {
        let! err, violations = runRaw "read"
        check "mux read ignoring warn_reuse passes" (err = "")
        check "mux read has no violations" (violations.IsEmpty)
    }

let run () : JS.Promise<unit> =
    promise {
        do! muxRejectsCoderMissing ()
        do! muxRejectsCoderMalformed ()
        do! muxAcceptsCoder ()
        do! muxIgnoresNonModificationTool ()
        do! muxRejectsExecutorMissingWarn ()
        do! muxAcceptsExecutor ()
        do! muxWriteDoesNotRequireWarn ()
        do! exhaustiveMuxWarnTdd ()
        do! exhaustiveMuxWarnTddAccepts ()
        do! exhaustiveMuxWarn ()
        do! exhaustiveMuxWarnAccepts ()
        do! muxRejectsCoderMissingWarnReuse ()
        do! muxRejectsCoderMalformedWarnReuse ()
        do! muxAcceptsCoderWithWarnReuse ()
        do! muxNonSubagentIgnoresWarnReuse ()
    }
