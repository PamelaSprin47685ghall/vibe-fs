module VibeFs.Tests.IntegrationOpencodeReviewSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ReviewSession
open VibeFs.Opencode.ReviewTools
open VibeFs.Shell.ReviewRuntime
open VibeFs.Shell.Dyn

let private opencodeTextMessage (sessionID: string) (id: string) (text: string) : obj =
    box {| info = createObj [ "id", box id; "agent", box "reviewer"; "sessionID", box sessionID; "role", box "assistant" ]
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let private mockClient (messages: obj array) : obj =
    createObj [
        "session",
        box (createObj [
            "messages",
            box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = messages |} }))
        ])
    ]

let private returnReviewerTool workspaceDir messages store : obj =
    let ctx = createObj [ "directory", box workspaceDir; "client", mockClient messages ]
    submitReviewResultTool ctx store

let private reviewerContext workspaceDir sessionID : obj =
    createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]

let private setPending (store: ReviewStore) sessionID (resolved: ReviewResult option ref) : unit =
    store.setPendingReview(sessionID, fun result -> resolved.Value <- Some result)

let firstPassNullFeedbackDoubleChecksSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-first-pass-"
    let sessionID = "opencode-return-reviewer-first-pass"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let history = [| opencodeTextMessage sessionID "activation" (buildLoopMessage "Implement feature X" [ "With-Review Mode is active." ]) |]
    let returnTool = returnReviewerTool workspaceDir history store
    let! result = ((get returnTool "execute") $ (createObj [ "feedback", box null ], reviewerContext workspaceDir sessionID)) |> unbox<JS.Promise<string>>
    check "opencode return_reviewer first null pass returns double-check prompt" (result.Contains "double-check:")
    check "opencode return_reviewer first null pass does not resolve pending review" (resolved.Value = None)
    do! rmAsync workspaceDir
}

let secondPassNullFeedbackResolvesAcceptedSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-second-pass-"
    let sessionID = "opencode-return-reviewer-second-pass"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let history =
        [| opencodeTextMessage sessionID "activation" (buildLoopMessage "Implement feature X" [ "With-Review Mode is active." ])
           opencodeTextMessage sessionID "double-check" "---\ndouble-check: confirmed\n---\nok" |]
    let returnTool = returnReviewerTool workspaceDir history store
    let! result = ((get returnTool "execute") $ (createObj [ "feedback", box null ], reviewerContext workspaceDir sessionID)) |> unbox<JS.Promise<string>>
    check "opencode return_reviewer second null pass reports submitted" (result.Contains "Verdict submitted.")
    check "opencode return_reviewer second null pass resolves accepted" (resolved.Value = Some Accepted)
    do! rmAsync workspaceDir
}

let nullStringFeedbackRejectsSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-null-string-"
    let sessionID = "opencode-return-reviewer-null-string"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let returnTool = returnReviewerTool workspaceDir [||] store
    let! result = ((get returnTool "execute") $ (createObj [ "feedback", box "null" ], reviewerContext workspaceDir sessionID)) |> unbox<JS.Promise<string>>
    check "opencode return_reviewer string null is rejection" (result.Contains "Verdict submitted.")
    check "opencode return_reviewer string null keeps explicit rejection text" (resolved.Value = Some (Rejected "null"))
    do! rmAsync workspaceDir
}

let run () : JS.Promise<unit> = promise {
    do! firstPassNullFeedbackDoubleChecksSpec ()
    do! secondPassNullFeedbackResolvesAcceptedSpec ()
    do! nullStringFeedbackRejectsSpec ()
}
