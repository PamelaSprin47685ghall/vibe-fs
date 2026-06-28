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

let private runMuxHook (tool: string) (args: obj) : JS.Promise<string> =
    let input = createObj [ "tool", box tool; "args", box args ]
    let output = createObj [ "args", box args ]
    promise {
        do! Wanxiangshu.Mux.PluginCatalog.toolExecuteBefore input output
        return str output "error"
    }

let private runRaw (tool: string) : JS.Promise<string> =
    runMuxHook tool (createObj [])

let private runWithWarnTdd (tool: string) : JS.Promise<string> =
    runMuxHook tool (createObj [ "warn_tdd", box canonicalValue ])

let muxRejectsCoderMissing () = promise {
    let! err = runRaw "coder"
    check "mux coder missing warn_tdd rejects" (err <> "")
    check "mux coder error mentions warn_tdd" (err.Contains "warn_tdd")
}

let muxRejectsCoderMalformed () = promise {
    let! err = runMuxHook "coder" (createObj [ "warn_tdd", box "wrong" ])
    check "mux coder malformed warn_tdd rejects" (err <> "")
}

let muxAcceptsCoder () = promise {
    let args = createObj [ "warn_tdd", box canonicalValue ]
    let! err = runMuxHook "coder" args
    check "mux coder canonical passes" (err = "")
    check "mux coder warn_tdd removed from args" (Dyn.str args "warn_tdd" = "")
}

let muxIgnoresNonModificationTool () = promise {
    let! err = runRaw "read"
    check "mux read passes" (err = "")
}

let muxRejectsExecutorMissingWarn () = promise {
    let! err = runWithWarnTdd "executor"
    check "mux executor missing warn rejects" (err <> "")
    check "mux executor error mentions warn" (err.Contains "warn")
}

let muxAcceptsExecutor () = promise {
    let args =
        createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]
    let! err = runMuxHook "executor" args
    check "mux executor canonical passes" (err = "")
    check "mux executor warn removed from args" (Dyn.str args "warn" = "")
}

let muxWriteDoesNotRequireWarn () = promise {
    let! err = runWithWarnTdd "write"
    check "mux write does not require warn" (err = "")
}

let exhaustiveMuxWarnTdd () : JS.Promise<unit> =
    promise {
        for tool in modificationTools do
            let args =
                if isWarnRequiredTool tool then
                    createObj [ "warn", box warnCanonicalValue ]
                else
                    createObj []
            let! err = runMuxHook tool args
            check ("mux " + tool + " missing warn_tdd rejects") (err <> "")
    }

let exhaustiveMuxWarnTddAccepts () : JS.Promise<unit> =
    promise {
        for tool in modificationTools do
            let args =
                if isWarnRequiredTool tool then
                    createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]
                else
                    createObj [ "warn_tdd", box canonicalValue ]
            let! err = runMuxHook tool args
            check ("mux " + tool + " canonical fields pass") (err = "")
    }

let exhaustiveMuxWarn () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let args = createObj [ "warn_tdd", box canonicalValue ]
            let! err = runMuxHook tool args
            check ("mux " + tool + " missing warn rejects") (err <> "")
    }

let exhaustiveMuxWarnAccepts () : JS.Promise<unit> =
    promise {
        for tool in warnRequiredTools do
            let args =
                createObj [ "warn_tdd", box canonicalValue; "warn", box warnCanonicalValue ]
            let! err = runMuxHook tool args
            check ("mux " + tool + " canonical warn passes") (err = "")
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
    }