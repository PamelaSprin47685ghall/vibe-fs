module Wanxiangshu.Tests.IntegrationMuxFallbackSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.AsyncFlush
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.Plugin

/// One-shot deferred signal resolved after the first nudge text is recorded.
let private buildNudgeSignal () : (JS.Promise<unit> * (unit -> unit)) =
    let resolver = ref (fun () -> ())
    let p = Promise.create (fun resolve _ -> resolver.Value <- resolve)
    p, (fun () -> resolver.Value())

/// Build Mux deps whose nudge resolves the one-shot signal after recording.
let private buildMuxDeps
    (sessionID: string)
    (directory: string)
    (getChatHistory: string -> JS.Promise<obj array>)
    (nudges: ResizeArray<string>)
    (resolveSignal: unit -> unit)
    : obj =

    let nudgeFn =
        System.Func<obj, obj, JS.Promise<bool>>(fun _ msg ->
            promise {
                nudges.Add(string msg)
                resolveSignal ()
                return true
            })

    createObj
        [ "loadConfigOrDefault", box (fun () -> createObj [])
          "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
          "resolveAgentFrontmatter",
          box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
          "getChatHistory", box getChatHistory
          "nudge", box nudgeFn
          "directory", box directory ]

/// Fallback from retryable 429 error fires exactly one nudge.
let muxSessionErrorTriggersFallbackContinueSpec () =
    promise {
        let! tmpDir = mkdtempAsync "mux-fb-error-"

        do!
            writeFileAsync
                (tmpDir + "/AGENTS.md")
                "---\nmodels:\n  default:\n    - openai/gpt-5\n    - anthropic/claude-4\n---\n"

        let sessionID = "mux-fb-error-ws"
        let nudges = ResizeArray<string>()
        let nudgeObserved, resolveSignal = buildNudgeSignal ()

        let deps =
            buildMuxDeps
                sessionID
                tmpDir
                (fun sid -> promise { return if sid = sessionID then [||] else [||] })
                nudges
                resolveSignal

        let reg = createRegistration deps
        let eventHook = get reg "eventHook"

        let helpers =
            createObj [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] })) ]

        let ev =
            createObj
                [ "type", box "session.error"
                  "workspaceId", box sessionID
                  "properties",
                  box (
                      createObj
                          [ "errorType", box "APIError"
                            "statusCode", box "429"
                            "isRetryable", box "true" ]
                  ) ]

        do! (eventHook $ (ev, helpers)) |> unbox<JS.Promise<unit>>
        do! withTimeout nudgeObserved
        check "exactly one nudge dispatched" (nudges.Count = 1)
        check "nudge text contains 'continue openai/gpt-5'" (nudges.[0].Contains "continue openai/gpt-5")
        do! rmAsync tmpDir
    }

/// Fallback from tool-call-as-text recovery fires exactly one nudge.
let muxStreamEndToolCallAsTextTriggersFallbackSpec () =
    promise {
        let! tmpDir = mkdtempAsync "mux-fb-toolcall-"

        do!
            writeFileAsync
                (tmpDir + "/AGENTS.md")
                "---\nmodels:\n  default:\n    - openai/gpt-5\n    - anthropic/claude-4\n---\n"

        let sessionID = "mux-fb-toolcall-ws"
        let nudges = ResizeArray<string>()
        let nudgeObserved, resolveSignal = buildNudgeSignal ()

        let assistantMsg =
            createObj
                [ "info", box (createObj [ "role", box "assistant" ])
                  "parts",
                  box
                      [| box
                             {| ``type`` = "text"
                                text = "<tool_call><name>read</name> 后续" |} |] ]

        let getHistory sid =
            promise { return if sid = sessionID then [| assistantMsg |] else [||] }

        let deps = buildMuxDeps sessionID tmpDir getHistory nudges resolveSignal
        let reg = createRegistration deps
        let eventHook = get reg "eventHook"

        let helpers =
            createObj [ "getTodos", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box [||] })) ]

        let ev =
            createObj
                [ "type", box "stream-end"
                  "workspaceId", box sessionID
                  "properties",
                  box (
                      createObj
                          [ "metadata", box (createObj [ "muxStopReason", box "tool_use_error" ])
                            "parts",
                            box
                                [| box
                                       {| ``type`` = "text"
                                          text = "incomplete" |} |] ]
                  ) ]

        do! (eventHook $ (ev, helpers)) |> unbox<JS.Promise<unit>>
        do! withTimeout nudgeObserved
        check "exactly one nudge fires" (nudges.Count = 1)

        check
            "nudge contains FallbackMessageCodec recovery prompt"
            (nudges.[0].Contains "You produced the tool call as raw text")

        do! rmAsync tmpDir
    }
