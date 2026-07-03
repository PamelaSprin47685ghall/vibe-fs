module Wanxiangshu.Tests.IntegrationMuxCompactionResendSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Shell.Dyn

let muxCompactingTransformDoesNotResendAnchorPromptSpec () = promise {
    let sessionID = "mux-anchor-resend-session"
    let mutable history = ResizeArray<obj>()
    let promptsCaptured = ResizeArray<string>()
    let mockSession =
        createObj
            [ "prompt",
              box (System.Func<string, obj, JS.Promise<unit>>(fun sid promptText ->
                  promise {
                      let text = string promptText
                      if text <> "" then promptsCaptured.Add(text)
                      // simulate host persisting the anchor prompt as a user message
                      history.Add(muxTextMessage "persisted-anchor" "user" text)
                      return ()
                  })) ]
    let deps =
        createObj
            [ "loadConfigOrDefault", box (fun () -> createObj [])
              "findWorkspaceEntry", box (System.Func<obj, string, obj>(fun _ _ -> createObj [ "workspace", null ]))
              "resolveAgentFrontmatter",
              box (System.Func<obj, obj, string, JS.Promise<obj>>(fun _ _ _ -> Promise.lift (createObj [])))
              "nowUtc", box (System.Func<unit, System.DateTime>(fun () -> System.DateTime(2026, 6, 25)))
              "getChatHistory",
              box (System.Func<string, JS.Promise<obj array>>(fun sid ->
                  promise { return if sid = sessionID then history.ToArray() else [||] }))
              "session", box mockSession ]
    let reg = Wanxiangshu.Mux.Plugin.createRegistration deps
    let compactingTransform = get reg "compactingTransform"
    if isNullish compactingTransform then
        check "mux registration exposes compactingTransform" false
    else
        let todoInput report content status priority =
            createObj
                [ "ahaMoments", box report
                  "changesAndReasons", box ""
                  "gotchas", box ""
                  "lessonsAndConventions", box ""
                  "plan", box ""
                  "todos", box [| createObj [ "content", box content; "status", box status; "priority", box priority ] |] ]
        let todoOutput count = createObj [ "success", box true; "count", box count ]
        let messages =
            [| muxTextMessage "resend-anchor-user-1" "user" "plan phase"
               muxDynamicToolMessage "resend-anchor-1" "todo_write" "resend-call-a" (todoInput "resend planned phase" "Resend Plan" "in_progress" "high") (todoOutput 1)
               muxTextMessage "resend-anchor-user-2" "user" "implement phase"
               muxDynamicToolMessage "resend-anchor-2" "todo_write" "resend-call-b" (todoInput "resend implemented phase" "Resend Implement" "completed" "high") (todoOutput 1) |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box sessionID ]
        // first call: no history, must emit anchor prompt
        do! (compactingTransform $ (input, out)) |> unbox<JS.Promise<unit>>
        // second call: anchor prompt now persisted in history, must NOT resend
        do! (compactingTransform $ (input, out)) |> unbox<JS.Promise<unit>>
        check "mux compacting transform does not resend anchor prompt on repeated call" (promptsCaptured.Count = 0)
}
