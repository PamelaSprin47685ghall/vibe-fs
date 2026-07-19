module Wanxiangshu.Tests.WarnTddOpencodeEnforcementTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Hosts.Opencode.HookSchemaDecode

module Dyn = Wanxiangshu.Runtime.Dyn

// `requireWarnTdd` and `requireWarn` in src/Opencode/HookExecute.fs must
// call `setHookError` whenever a modification tool (resp. warn-required tool)
// omits the canonical acknowledgement. This file enumerates the entire
// modificationTools and warnRequiredTools set; any drift between Kernel SSOT
// and Opencode runtime MUST show up here as a failing check.

let private runOpencodeHook (tool: string) (args: obj) : JS.Promise<string * string list> =
    let input =
        createObj
            [ "tool", box tool
              "sessionID", box "s-warn-enforce"
              "callID", box "c-warn-enforce"
              "args", box args ]

    let output = createObj [ "args", box args ]

    promise {
        try
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance "s-warn-enforce"
            do! Wanxiangshu.Hosts.Opencode.HookExecute.toolExecuteBefore input output
            let err = str output "error"

            let violations =
                match Wanxiangshu.Runtime.ToolHookRuntime.tryGetCompliance "s-warn-enforce" "c-warn-enforce" with
                | Some env -> env.Violations
                | None -> []

            return (err, violations)
        with ex ->
            return (ex.Message, [])
    }

let private runWithWarnTdd (tool: string) : JS.Promise<string * string list> =
    runOpencodeHook tool (createObj [ "warn_tdd", box canonicalValue ])

let private runWithBoth (tool: string) : JS.Promise<string * string list> =
    runOpencodeHook tool (createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ])

let private runRaw (tool: string) : JS.Promise<string * string list> = runOpencodeHook tool (createObj [])

// ── Direct per-tool cases ───────────────────────────────────────────────────

let opencodeRejectsCoderMissing () =
    promise {
        let! err, violations = runRaw "coder"
        check "opencode coder missing warn_tdd does not reject" (err = "")
        check "opencode coder missing warn_tdd has no violations" (violations.IsEmpty)
    }

let opencodeRejectsCoderMalformed () =
    promise {
        let! err, violations = runOpencodeHook "coder" (createObj [ "warn_tdd", box "wrong" ])
        check "opencode coder malformed warn_tdd does not reject" (err = "")
        check "opencode coder malformed warn_tdd has no violations" (violations.IsEmpty)
    }

let opencodeAcceptsCoder () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

        let! err, violations = runOpencodeHook "coder" args
        check "opencode coder canonical passes" (err = "")
        check "opencode coder canonical has no violations" (violations.IsEmpty)
        check "opencode coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
        check "opencode coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")
    }

let opencodeIgnoresNonModificationTool () =
    promise {
        let! err, violations = runRaw "read"
        check "opencode read passes" (err = "")
        check "opencode read has no violations" (violations.IsEmpty)
    }

/// Host may omit `output.args`; enforcement must still read `input.args` (or empty object).
let opencodeRejectsCoderWhenOutputArgsAbsent () =
    promise {
        let args = createObj []

        let input =
            createObj
                [ "tool", box "coder"
                  "sessionID", box "s-warn-enforce"
                  "callID", box "c-warn-enforce"
                  "args", box args ]

        let output = createObj []
        let mutable err = ""
        let mutable violations = []

        try
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance "s-warn-enforce"
            do! Wanxiangshu.Hosts.Opencode.HookExecute.toolExecuteBefore input output
            err <- str output "error"

            violations <-
                match Wanxiangshu.Runtime.ToolHookRuntime.tryGetCompliance "s-warn-enforce" "c-warn-enforce" with
                | Some env -> env.Violations
                | None -> []
        with ex ->
            err <- ex.Message

        check "opencode coder missing output.args does not reject" (err = "")
        check "opencode coder missing output.args has no violations" (violations.IsEmpty)
    }

let opencodeRejectsCoderMissingWarnWhenOutputArgsAbsent () =
    promise {
        let args = createObj []

        let input =
            createObj
                [ "tool", box "coder"
                  "sessionID", box "s-warn-enforce"
                  "callID", box "c-warn-enforce"
                  "args", box args ]

        let output = createObj []
        let mutable err = ""
        let mutable violations = []

        try
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance "s-warn-enforce"
            do! Wanxiangshu.Hosts.Opencode.HookExecute.toolExecuteBefore input output
            err <- str output "error"

            violations <-
                match Wanxiangshu.Runtime.ToolHookRuntime.tryGetCompliance "s-warn-enforce" "c-warn-enforce" with
                | Some env -> env.Violations
                | None -> []
        with ex ->
            err <- ex.Message

        check "opencode coder missing output.args does not reject missing warn_tdd" (err = "")
        check "opencode coder missing output.args has no violations" (violations.IsEmpty)
    }

let opencodeRejectsExecutorMissingWarn () =
    promise {
        let! err, violations = runWithWarnTdd "executor"
        check "opencode executor missing warn does not reject" (err = "")

        check "opencode executor missing warn has no violations" (violations.IsEmpty)
    }

let opencodeRejectsExecutorMalformedWarn () =
    promise {
        let! err, violations =
            runOpencodeHook "executor" (createObj [ "warn_tdd", box canonicalValue; "warn", box "yes" ])

        check "opencode executor malformed warn does not reject" (err = "")

        check "opencode executor malformed warn has no violations" (violations.IsEmpty)
    }

let opencodeAcceptsExecutor () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

        let! err, violations = runOpencodeHook "executor" args
        check "opencode executor canonical passes" (err = "")
        check "opencode executor canonical has no violations" (violations.IsEmpty)
        check "opencode executor warn removed from args" (Dyn.str args "warn" = "")
    }

let opencodeWriteDoesNotRequireWarn () =
    promise {
        let! err, violations = runWithWarnTdd "write"
        check "opencode write does not require warn" (err = "")
        // write requires warn_tdd (which is provided in runWithWarnTdd), so it should have no violations
        check "opencode write has no violations" (violations.IsEmpty)
    }

// ── Exhaustive matrices driven from Kernel SSOT ────────────────────────────

let exhaustiveOpencodeWarnTdd () : JS.Promise<unit> =
    promise {
        for tool in modificationTools do
            let args =
                if isWarnRequiredTool tool then
                    createObj [ "warn", box warnCanonicalValue ]
                else
                    createObj []

            let! err, violations = runOpencodeHook tool args
            check ("opencode " + tool + " missing warn_tdd does not reject") (err = "")
            check ("opencode " + tool + " missing warn_tdd has no violations") (violations.IsEmpty)
    }

let exhaustiveOpencodeWarnTddAccepts () : JS.Promise<unit> =
    promise {
        for tool in modificationTools do
            let args = createObj []
            args?warn_tdd <- box canonicalValue

            if isWarnRequiredTool tool then
                args?warn <- box warnCanonicalValue

            if isSubagentTool tool then
                args?warn_reuse <- box warnReuseCanonicalValue

            let! err, violations = runOpencodeHook tool args
            check ("opencode " + tool + " canonical fields pass") (err = "")
            check ("opencode " + tool + " canonical fields have no violations") (violations.IsEmpty)
    }

let exhaustiveOpencodeWarn () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let args = createObj [ "warn_tdd", box canonicalValue ]
            let! err, violations = runOpencodeHook tool args
            check ("opencode " + tool + " missing warn does not reject") (err = "")
            check ("opencode " + tool + " missing warn has no violations") (violations.IsEmpty)
    }

let exhaustiveOpencodeWarnAccepts () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let! err, violations = runWithBoth tool
            check ("opencode " + tool + " canonical warn passes") (err = "")
            check ("opencode " + tool + " canonical warn has no violations") (violations.IsEmpty)
    }

// ── warn_reuse: subagent tools must carry warn_reuse acknowledgement ──

let opencodeRejectsCoderMissingWarnReuse () =
    promise {
        let! err, violations = runOpencodeHook "coder" (createObj [ "warn_tdd", box canonicalValue ])
        check "opencode coder missing warn_reuse does not reject" (err = "")
        check "opencode coder missing warn_reuse has no violations" (violations.IsEmpty)
    }

let opencodeRejectsCoderMalformedWarnReuse () =
    promise {
        let! err, violations =
            runOpencodeHook "coder" (createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box "wrong" ])

        check "opencode coder malformed warn_reuse does not reject" (err = "")
        check "opencode coder malformed warn_reuse has no violations" (violations.IsEmpty)
    }

let opencodeAcceptsCoderWithWarnReuse () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

        let! err, violations = runOpencodeHook "coder" args
        check "opencode coder canonical warn_reuse passes" (err = "")
        check "opencode coder canonical warn_reuse has no violations" (violations.IsEmpty)
        check "opencode coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
        check "opencode coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")
    }

let opencodeNonSubagentIgnoresWarnReuse () =
    promise {
        let! err, violations = runRaw "read"
        check "opencode read ignoring warn_reuse passes" (err = "")
        check "opencode read has no violations" (violations.IsEmpty)
    }

// ── Opencode Hook Schema warn_reuse injection tests ─────────────────────────

let opencodeHookSchemaInjectWarnReuseIntoEmptySchema () =
    let schema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    injectWarnReuseIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "warn_reuse property injected" (not (Dyn.isNullish (get props "warn_reuse")))
    let prop = get props "warn_reuse"
    check "warn_reuse description is present" ((Dyn.str prop "description").Length > 0)
    let required = get schema "required"

    check
        "warn_reuse NOT added to required"
        (Dyn.isNullish required
         || not (required :?> obj array |> Array.exists (fun x -> string x = "warn_reuse")))

let opencodeHookSchemaInjectWarnReuseAlreadyPresent () =
    let schema =
        createObj
            [ "type", box "object"
              "properties", createObj [ "warn_reuse", box (createObj []) ]
              "required", box [| box "warn_reuse" |] ]

    injectWarnReuseIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "existing warn_reuse still present" (not (Dyn.isNullish (get props "warn_reuse")))

let opencodeHookSchemaInjectWarnReuseNullSchema () =
    let result = injectWarnReuseIntoJsonSchema null
    check "null schema returns null" (isNull result)

let run () : JS.Promise<unit> =
    promise {
        do! opencodeRejectsCoderMissing ()
        do! opencodeRejectsCoderMalformed ()
        do! opencodeAcceptsCoder ()
        do! opencodeIgnoresNonModificationTool ()
        do! opencodeRejectsCoderWhenOutputArgsAbsent ()
        do! opencodeRejectsCoderMissingWarnWhenOutputArgsAbsent ()
        do! opencodeRejectsExecutorMissingWarn ()
        do! opencodeRejectsExecutorMalformedWarn ()
        do! opencodeAcceptsExecutor ()
        do! opencodeWriteDoesNotRequireWarn ()
        do! exhaustiveOpencodeWarnTdd ()
        do! exhaustiveOpencodeWarnTddAccepts ()
        do! exhaustiveOpencodeWarn ()
        do! exhaustiveOpencodeWarnAccepts ()
        do! opencodeRejectsCoderMissingWarnReuse ()
        do! opencodeRejectsCoderMalformedWarnReuse ()
        do! opencodeAcceptsCoderWithWarnReuse ()
        do! opencodeNonSubagentIgnoresWarnReuse ()
        opencodeHookSchemaInjectWarnReuseIntoEmptySchema ()
        opencodeHookSchemaInjectWarnReuseAlreadyPresent ()
        opencodeHookSchemaInjectWarnReuseNullSchema ()
    }
