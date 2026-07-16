module Wanxiangshu.Tests.IntegrationMuxToolSpecsHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Shell.EventLogCodec
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

let muxEventHookAbortDeactivatesReviewSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let sessionID = "mux-abort-session"
        muxActivateReviewForTest reg sessionID "review-task"
        check "mux event hook abort starts with active review" (muxIsReviewActiveForTest reg sessionID)
        let eventHook = get reg "eventHook"

        if isNullish eventHook then
            check "mux registration exposes eventHook" false
        else
            let event = createObj [ "type", box "stream-abort"; "workspaceId", box sessionID ]
            do! (eventHook $ (event, createObj [])) |> unbox<JS.Promise<unit>>
            check "mux event hook abort deactivates review" (not (muxIsReviewActiveForTest reg sessionID))
    }

let muxToolExecuteBeforeSetsUiLabelSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let before = get reg "tool.execute.before"
        check "mux registration exposes tool.execute.before" (not (isNullish before))

        let intentOne =
            createObj
                [ "objective", box "Refactor module X"
                  "background", box "cleanup"
                  "targets", box [| createObj [ "file", box "src/x.ts"; "guide", box "split file" ] |] ]

        let intentTwo =
            createObj
                [ "objective", box "Add tests"
                  "background", box "coverage"
                  "targets", box [| createObj [ "file", box "src/x.test.ts"; "guide", box "add cases" ] |] ]

        let args =
            createObj
                [ "intents", box [| intentOne; intentTwo |]
                  "warn_tdd", box "i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated"
                  "warn_reuse", box "this-task-is-not-suitable-to-be-completed-via-continue-tool" ]

        let input = createObj [ "tool", box "coder"; "args", box args ]
        do! (before $ (input, createObj [ "args", box args ])) |> unbox<JS.Promise<unit>>
        let ui = str args "ui_"
        check "mux tool.execute.before sets ui_ label" (ui.Contains "Refactor module X" && ui.Contains "Add tests")
    }

let muxSystemTransformClearsOutputLengthSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let transform = get reg "systemTransform"
        check "mux registration exposes systemTransform" (not (isNullish transform))
        let system = createObj [ "content", box "long system prompt"; "length", box 1000 ]
        let output = createObj [ "system", box system ]
        do! (transform $ (createObj [], output)) |> unbox<JS.Promise<unit>>

        check
            "mux systemTransform preserves system when deps has no directory"
            ((unbox<int> (get system "length")) = 1000)
    }

let muxToolSchemasAreCleanStaticallyButInjectedDynamicallySpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let tools = unbox<obj[]> (get reg "tools")

        let findTool (name: string) =
            tools |> Array.tryFind (fun t -> Dyn.str t "name" = name)

        let staticRequired (toolDef: obj) : string array =
            if isNullish toolDef then
                [||]
            else
                let params_ = Dyn.get toolDef "parameters"

                if isNullish params_ then
                    [||]
                else
                    let req = Dyn.get params_ "required"
                    if Dyn.isArray req then unbox<string[]> req else [||]

        let staticProperties (toolDef: obj) : obj =
            if isNullish toolDef then
                null
            else
                let params_ = Dyn.get toolDef "parameters"

                if isNullish params_ then
                    null
                else
                    Dyn.get params_ "properties"
        // coder: no warn_tdd in raw static BuiltinTools schema
        let staticCoder =
            Wanxiangshu.Mux.SubagentTools.coderTool (createObj []) [| "coder" |]

        let staticCoderProps = staticProperties (box staticCoder)
        check "coder static BuiltinTools schema has no warn_tdd" (isNullish (Dyn.get staticCoderProps "warn_tdd"))
        // registered coder: warn_tdd injected into schema properties and required
        let coder = findTool "coder"
        check "coder tool exists" (not (isNullish coder))
        let coderProps = staticProperties coder
        check "registered coder schema has warn_tdd" (not (isNullish (Dyn.get coderProps "warn_tdd")))
        check "registered coder required has NO warn_tdd" (not (staticRequired coder |> Array.contains "warn_tdd"))

        check
            "registered coder warn_tdd is soft-required"
            (Dyn.truthy (Dyn.get (Dyn.get coderProps "warn_tdd") "required_"))
        // executor: no warn or warn_tdd in raw static BuiltinTools schema
        let staticExec =
            Wanxiangshu.Mux.BuiltinTools.executorTool
                (createObj [])
                [| "executor" |]
                (Wanxiangshu.Shell.RuntimeScope.create ())

        let staticExecProps = staticProperties (box staticExec)
        check "executor static BuiltinTools schema has no warn" (isNullish (Dyn.get staticExecProps "warn"))
        check "executor static BuiltinTools schema has no warn_tdd" (isNullish (Dyn.get staticExecProps "warn_tdd"))
        // registered executor: warn and warn_tdd injected
        let executor = findTool "executor"
        check "executor tool exists" (not (isNullish executor))
        let execProps = staticProperties executor
        check "registered executor schema has warn" (not (isNullish (Dyn.get execProps "warn")))
        check "registered executor schema has warn_tdd" (not (isNullish (Dyn.get execProps "warn_tdd")))
        check "registered executor required has NO warn" (not (staticRequired executor |> Array.contains "warn"))

        check
            "registered executor required has NO warn_tdd"
            (not (staticRequired executor |> Array.contains "warn_tdd"))

        check "registered executor warn is soft-required" (Dyn.truthy (Dyn.get (Dyn.get execProps "warn") "required_"))

        check
            "registered executor warn_tdd is soft-required"
            (Dyn.truthy (Dyn.get (Dyn.get execProps "warn_tdd") "required_"))
        // write (staticWrite): no warn_tdd in raw BuiltinTools.writeTool schema
        let staticWrite = Wanxiangshu.Mux.BuiltinTools.writeTool (createObj [])
        let staticWriteProps = staticProperties (box staticWrite)
        check "staticWrite has no warn_tdd" (isNullish (Dyn.get staticWriteProps "warn_tdd"))
        // registered write: warn_tdd injected into schema properties and required
        let write = findTool "write"
        check "write tool exists" (not (isNullish write))
        let writeProps = staticProperties write
        check "registered write schema has warn_tdd" (not (isNullish (Dyn.get writeProps "warn_tdd")))
        check "registered write required has NO warn_tdd" (not (staticRequired write |> Array.contains "warn_tdd"))

        check
            "registered write warn_tdd is soft-required"
            (Dyn.truthy (Dyn.get (Dyn.get writeProps "warn_tdd") "required_"))
        // dynamic injection hook must be present
        let hook = get reg "tool.execute.before"
        check "tool.execute.before hook is present for dynamic warn/warn_tdd injection" (not (isNullish hook))
    }

[<Import("readFile", "node:fs/promises")>]
let private readFileText (path: string) (encoding: string) : JS.Promise<string> = jsNative

/// `/loop` slash command must write event log (`.wanxiangshu.ndjson`) under
/// `deps.directory`, not `process.cwd()`.  TDD-red: current implementation in
/// `SlashCommands.fs:createLoopOnlyCommand` uses `nodeProcess?cwd()`.
let muxLoopSlashCommandWritesEventLogUnderDepsDirectorySpec () =
    promise {
        let! workspaceDir = mkdtempAsync "mux-loop-eventlog-"
        let sessionID = "mux-loop-eventlog-session"
        let taskText = "Refactor authentication module"

        let deps =
            createObj
                [ "directory", box workspaceDir
                  "loadConfigOrDefault", box (fun () -> createObj [])
                  "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
                  "resolveAgentFrontmatter",
                  box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj []))) ]

        let reg = createRegistration deps
        let slashCmds = unbox<obj[]> (get reg "slashCommands")
        let loopCmd = slashCmds |> Array.find (fun c -> Dyn.str c "key" = "loop")
        let execute = get loopCmd "execute"
        let! _result = (execute $ (sessionID, taskText)) |> unbox<JS.Promise<string>>
        let path = eventPath workspaceDir

        let! text =
            promise {
                try
                    return! readFileText path "utf-8"
                with _ ->
                    return ""
            }

        check "event log under workspaceDir contains loop_activated" (text.Contains eventKindLoopActivated)
        check "event log under workspaceDir contains task text" (text.Contains taskText)
        do! rmAsync workspaceDir
    }

/// `tool.execute.after` must detect repeated identical tool+args+output and set
/// error on the 3rd identical call (`LivelockGuard.defaultMaxRepeats` = 3).
let muxToolExecuteAfterBlocksRepeatedIdenticalCallSpec () =
    promise {
        let reg = sharedMuxRegistration ()

        let scope =
            unbox<Wanxiangshu.Shell.RuntimeScope.RuntimeScope> (Dyn.get reg "__runtimeScope")

        let sessionID = "mux-livelock-blocks-" + System.Guid.NewGuid().ToString("N")
        Wanxiangshu.Shell.LivelockGuard.cleanup scope sessionID

        let after = get reg "tool.execute.after"
        check "mux registration exposes tool.execute.after" (not (isNullish after))
        let args = createObj [ "language", box "shell"; "command", box "echo hi" ]

        let input =
            createObj [ "tool", box "executor"; "sessionID", box sessionID; "args", box args ]

        let output = createObj [ "output", box "hi" ]
        do! (after $ (input, output)) |> unbox<JS.Promise<unit>>
        check "1st identical call: no livelock error" (Dyn.str output "error" = "")
        do! (after $ (input, output)) |> unbox<JS.Promise<unit>>
        check "2nd identical call: no livelock error" (Dyn.str output "error" = "")
        do! (after $ (input, output)) |> unbox<JS.Promise<unit>>
        let err = Dyn.str output "error"
        check "3rd identical call: livelock error set" (err <> "")
        check "3rd identical call: error mentions livelock" (err.Contains "livelock")
    }

/// `tool.execute.after` must detect repeated identical tool call and set error on 3rd call
/// even if the control parameters (warn, warn_tdd, warn_reuse, amend) differ.
let muxToolExecuteAfterBlocksRepeatedCallIgnoringControlsSpec () =
    promise {
        let reg = sharedMuxRegistration ()

        let scope =
            unbox<Wanxiangshu.Shell.RuntimeScope.RuntimeScope> (Dyn.get reg "__runtimeScope")

        let sessionID =
            "mux-livelock-ignore-controls-" + System.Guid.NewGuid().ToString("N")

        Wanxiangshu.Shell.LivelockGuard.cleanup scope sessionID

        let after = get reg "tool.execute.after"
        check "mux registration exposes tool.execute.after for controls test" (not (isNullish after))

        // 1st call: normal args
        let args1 = createObj [ "language", box "shell"; "command", box "echo hi" ]

        let input1 =
            createObj [ "tool", box "executor"; "sessionID", box sessionID; "args", box args1 ]

        let output1 = createObj [ "output", box "hi" ]
        do! (after $ (input1, output1)) |> unbox<JS.Promise<unit>>
        check "1st call: no livelock error" (Dyn.str output1 "error" = "")

        // 2nd call: args with warn
        let args2 =
            createObj
                [ "language", box "shell"
                  "command", box "echo hi"
                  "warn", box "some-warn-val" ]

        let input2 =
            createObj [ "tool", box "executor"; "sessionID", box sessionID; "args", box args2 ]

        let output2 = createObj [ "output", box "hi" ]
        do! (after $ (input2, output2)) |> unbox<JS.Promise<unit>>
        check "2nd call: no livelock error" (Dyn.str output2 "error" = "")

        // 3rd call: args with warn_tdd and warn_reuse
        let args3 =
            createObj
                [ "language", box "shell"
                  "command", box "echo hi"
                  "warn_tdd", box "some-tdd-val"
                  "warn_reuse", box "some-reuse-val" ]

        let input3 =
            createObj [ "tool", box "executor"; "sessionID", box sessionID; "args", box args3 ]

        let output3 = createObj [ "output", box "hi" ]
        do! (after $ (input3, output3)) |> unbox<JS.Promise<unit>>
        let err = Dyn.str output3 "error"
        check "3rd call with different controls: livelock error set" (err <> "")
        check "3rd call with different controls: error mentions livelock" (err.Contains "livelock")
    }

/// `tool.execute.after` must map single-line tool output containing both
/// "error" and "network" (case-insensitive) to `output.error = "network
/// connection lost"`.  TDD-red: current Mux `toolExecuteAfter` is noop.
let muxToolExecuteAfterMapsNetworkErrorSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let after = get reg "tool.execute.after"
        check "mux registration exposes tool.execute.after for network error" (not (isNullish after))

        let input =
            createObj
                [ "tool", box "webfetch"
                  "sessionID", box "mux-net-session"
                  "args", box (createObj []) ]
        // Non-network output must not set error
        let cleanOutput = createObj [ "output", box "success" ]
        do! (after $ (input, cleanOutput)) |> unbox<JS.Promise<unit>>
        check "non-network output: error remains empty" (Dyn.str cleanOutput "error" = "")
        // Single-line output with "error" + "network" -> map to error field
        let netOutput = createObj [ "output", box "error: network connection refused" ]
        do! (after $ (input, netOutput)) |> unbox<JS.Promise<unit>>
        check "network error output: error field set" (Dyn.str netOutput "error" = "network connection lost")
    }
