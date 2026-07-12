module Wanxiangshu.Tests.WarnTddOpencodeEnforcementTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.WarnTdd
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Opencode.HookSchema

module Dyn = Wanxiangshu.Shell.Dyn

// `requireWarnTdd` and `requireWarn` in src/Opencode/HookExecute.fs must
// call `setHookError` whenever a modification tool (resp. warn-required tool)
// omits the canonical acknowledgement. This file enumerates the entire
// modificationTools and warnRequiredTools set; any drift between Kernel SSOT
// and Opencode runtime MUST show up here as a failing check.

let private runOpencodeHook (tool: string) (args: obj) : JS.Promise<string> =
    let input =
        createObj
            [ "tool", box tool
              "sessionID", box "s-warn-enforce"
              "callID", box "c-warn-enforce"
              "args", box args ]

    let output = createObj [ "args", box args ]

    promise {
        try
            do! Wanxiangshu.Opencode.HookExecute.toolExecuteBefore input output
            return str output "error"
        with ex ->
            return ex.Message
    }

let private runWithWarnTdd (tool: string) : JS.Promise<string> =
    runOpencodeHook tool (createObj [ "warn_tdd", box canonicalValue ])

let private runWithBoth (tool: string) : JS.Promise<string> =
    runOpencodeHook tool (createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ])

let private runRaw (tool: string) : JS.Promise<string> = runOpencodeHook tool (createObj [])

// ── Direct per-tool cases ───────────────────────────────────────────────────

let opencodeRejectsCoderMissing () =
    promise {
        let! err = runRaw "coder"
        check "opencode coder missing warn_tdd rejects" (err <> "")
        check "opencode coder error mentions warn_tdd" (err.Contains "warn_tdd")
    }

let opencodeRejectsCoderMalformed () =
    promise {
        let! err = runOpencodeHook "coder" (createObj [ "warn_tdd", box "wrong" ])
        check "opencode coder malformed warn_tdd rejects" (err <> "")
    }

let opencodeAcceptsCoder () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

        let! err = runOpencodeHook "coder" args
        check "opencode coder canonical passes" (err = "")
        check "opencode coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
        check "opencode coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")
    }

let opencodeIgnoresNonModificationTool () =
    promise {
        let! err = runRaw "read"
        check "opencode read passes" (err = "")
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

        try
            do! Wanxiangshu.Opencode.HookExecute.toolExecuteBefore input output
            err <- str output "error"
        with ex ->
            err <- ex.Message

        check "opencode coder missing output.args still rejects missing warn_tdd" (err <> "")
        check "opencode coder error mentions warn_tdd when output.args absent" (err.Contains "warn_tdd")
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

        try
            do! Wanxiangshu.Opencode.HookExecute.toolExecuteBefore input output
            err <- str output "error"
        with ex ->
            err <- ex.Message

        check "opencode coder missing output.args rejects missing warn_tdd" (err <> "")
    }

let opencodeRejectsExecutorMissingWarn () =
    promise {
        let! err = runWithWarnTdd "executor"
        check "opencode executor missing warn rejects" (err <> "")
        check "opencode executor error mentions warn" (err.Contains "warn")
    }

let opencodeRejectsExecutorMalformedWarn () =
    promise {
        let! err = runOpencodeHook "executor" (createObj [ "warn_tdd", box canonicalValue; "warn", box "yes" ])
        check "opencode executor malformed warn accepts" (err = "")
    }

let opencodeAcceptsExecutor () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]

        let! err = runOpencodeHook "executor" args
        check "opencode executor canonical passes" (err = "")
        check "opencode executor warn removed from args" (Dyn.str args "warn" = "")
    }

let opencodeWriteDoesNotRequireWarn () =
    promise {
        let! err = runWithWarnTdd "write"
        check "opencode write does not require warn" (err = "")
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

            let! err = runOpencodeHook tool args
            check ("opencode " + tool + " missing warn_tdd rejects") (err <> "")
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

            let! err = runOpencodeHook tool args
            check ("opencode " + tool + " canonical fields pass") (err = "")
    }

let exhaustiveOpencodeWarn () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let args = createObj [ "warn_tdd", box canonicalValue ]
            let! err = runOpencodeHook tool args
            check ("opencode " + tool + " missing warn rejects") (err <> "")
    }

let exhaustiveOpencodeWarnAccepts () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let! err = runWithBoth tool
            check ("opencode " + tool + " canonical warn passes") (err = "")
    }

// ── warn_reuse: subagent tools must carry warn_reuse acknowledgement ──

let opencodeRejectsCoderMissingWarnReuse () =
    promise {
        let! err = runOpencodeHook "coder" (createObj [ "warn_tdd", box canonicalValue ])
        check "opencode coder missing warn_reuse rejects" (err <> "")
        check "opencode coder error mentions warn_reuse" (err.Contains "warn_reuse")
    }

let opencodeRejectsCoderMalformedWarnReuse () =
    promise {
        let! err = runOpencodeHook "coder" (createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box "wrong" ])
        check "opencode coder malformed warn_reuse accepts" (err = "")
    }

let opencodeAcceptsCoderWithWarnReuse () =
    promise {
        let args =
            createObj [ "warn_tdd", box canonicalValue; "warn_reuse", box warnReuseCanonicalValue ]

        let! err = runOpencodeHook "coder" args
        check "opencode coder canonical warn_reuse passes" (err = "")
        check "opencode coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
        check "opencode coder warn_reuse removed from args" (Dyn.str args "warn_reuse" = "")
    }

let opencodeNonSubagentIgnoresWarnReuse () =
    promise {
        let! err = runRaw "read"
        check "opencode read ignoring warn_reuse passes" (err = "")
    }

// ── Opencode Hook Schema warn_reuse injection tests ─────────────────────────

let opencodeHookSchemaInjectWarnReuseIntoEmptySchema () =
    let schema =
        createObj [ "type", box "object"; "properties", createObj [ "name", box (createObj []) ] ]

    injectWarnReuseIntoJsonSchema schema |> ignore
    let props = get schema "properties"
    check "warn_reuse property injected" (not (Dyn.isNullish (get props "warn_reuse")))
    let required = get schema "required"

    check
        "warn_reuse added to required"
        (isArray required
         && (required :?> obj array |> Array.exists (fun x -> string x = "warn_reuse")))

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
