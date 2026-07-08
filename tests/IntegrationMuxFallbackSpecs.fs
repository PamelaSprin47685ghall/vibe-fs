module Wanxiangshu.Tests.IntegrationMuxFallbackSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Mux.Plugin

/// Build Mux plugin deps with `directory`, `getChatHistory`, and a `nudge` that
/// captures dispatch text into a caller-owned ResizeArray.  Used by fallback
/// integration specs that need the fallback action executor (closed over `deps`)
/// to fire a `SendContinue` / `RecoverWithPrompt` nudge.
let private muxDepsWithNudgeCapture
    (sessionID: string)
    (directory: string)
    (getChatHistory: string -> JS.Promise<obj array>)
    (nudges: ResizeArray<string>)
    : obj =
    let nudgeFn =
        System.Func<obj, obj, JS.Promise<bool>>(fun _ msg ->
            promise {
                nudges.Add(string msg)
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

/// `session.error` with `errorType=APIError` / `statusCode=429` / `isRetryable=true`
/// must route through the Mux fallback event bridge: `muxEventTranslator` decodes
/// it as `SessionError`, `classifyError` returns `RetrySame`, and the state machine
/// fires `SendContinue` against the first model in the chain parsed from AGENTS.md.
/// Because `FallbackHookResult.Consumed = true`, NudgeRuntime is short-circuited
/// and exactly one nudge (the fallback's "continue openai/gpt-5") is dispatched.
let muxSessionErrorTriggersFallbackContinueSpec () =
    promise {
        let! tmpDir = mkdtempAsync "mux-fb-error-"

        do!
            writeFileAsync
                (tmpDir + "/AGENTS.md")
                "---\nmodels:\n  default:\n    - openai/gpt-5\n    - anthropic/claude-4\n---\n"

        let sessionID = "mux-fb-error-ws"
        let nudges = ResizeArray<string>()

        let deps =
            muxDepsWithNudgeCapture
                sessionID
                tmpDir
                (fun sid -> promise { return if sid = sessionID then [||] else [||] })
                nudges

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
        do! Promise.sleep 50

        check
            "Fallback consumed the error and dispatched one continue nudge via the fallback action executor"
            (nudges.Count = 1)

        let prompt = nudges.[0]

        check
            "Nudge text carries 'continue openai/gpt-5' (SendContinue against chain[0])"
            (prompt.Contains "continue openai/gpt-5")

        do! rmAsync tmpDir
    }

/// `stream-end` with `muxStopReason=tool_use_error` whose assistant text contains
/// an XML-call-as-text pattern must route through two layers:
///   1. `muxEventTranslator.IsSessionIdle` → `SessionIdle` fallback event.
///   2. State machine: `FallbackPhase.Idle + not TaskComplete` → `ScanToolCallAsText`.
///   3. `FetchMessages` (via deps.getChatHistory) yields assistant text containing
///      the tool call → `scanToolCallAsText` returns `Some recoveryPrompt`.
///   4. `RecoverWithPrompt(chain[0], recoveryPrompt)` → `invokeNudge`.
/// The test asserts exactly one nudge fires (the fallback recovery prompt), and
/// its text contains the `FallbackMessageCodec` recovery string "You produced the
/// tool call as raw text".
let muxStreamEndToolCallAsTextTriggersFallbackSpec () =
    promise {
        let! tmpDir = mkdtempAsync "mux-fb-toolcall-"

        do!
            writeFileAsync
                (tmpDir + "/AGENTS.md")
                "---\nmodels:\n  default:\n    - openai/gpt-5\n    - anthropic/claude-4\n---\n"

        let sessionID = "mux-fb-toolcall-ws"
        let nudges = ResizeArray<string>()

        // Chat history: one assistant message whose text contains an XML tool call
        // emitted as raw text.  `info.role = "assistant"` satisfies the
        // `scanToolCallAsText` discriminator.
        let assistantMsg =
            createObj
                [ "info", box (createObj [ "role", box "assistant" ])
                  "parts",
                  box
                      [| box
                             {| ``type`` = "text"
                                text = "<tool_call><name>read</name></tool_call>" |} |] ]

        let deps =
            muxDepsWithNudgeCapture
                sessionID
                tmpDir
                (fun sid -> promise { return if sid = sessionID then [| assistantMsg |] else [||] })
                nudges

        let reg = createRegistration deps
        let eventHook = get reg "eventHook"

        // Invocation helpers intentionally omit `nudge` so the NudgeRuntime path
        // (which runs only when fallback returns Consumed=false) cannot dispatch a
        // second nudge even if it reaches `sendNudgeMux`.
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
        do! Promise.sleep 50

        check "Exactly one nudge fires (fallback RecoverWithPrompt), not a second from NudgeRuntime" (nudges.Count = 1)

        check
            "Nudge carries the FallbackMessageCodec recovery prompt ('You produced the tool call as raw text')"
            (nudges.[0].Contains "You produced the tool call as raw text")

        do! rmAsync tmpDir
    }
