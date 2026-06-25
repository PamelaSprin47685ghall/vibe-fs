module VibeFs.Tests.IntegrationOpencodeReviewSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.ReviewSession.Types
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

let private execVerdict (returnTool: obj) (args: obj) ctx : JS.Promise<string> =
    ((get returnTool "execute") $ (args, ctx)) |> unbox<JS.Promise<string>>

let firstPassDoubleChecksSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-first-pass-"
    let sessionID = "opencode-return-reviewer-first-pass"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let history = [| opencodeTextMessage sessionID "activation" (buildLoopMessage "Implement feature X" [ "With-Review Mode is active." ]) |]
    let returnTool = returnReviewerTool workspaceDir history store
    let! result = execVerdict returnTool (createObj [ "verdict", box "PASS"; "feedback", box null ]) (reviewerContext workspaceDir sessionID)
    check "opencode return_reviewer first PASS returns double-check prompt" (result.Contains "double-check:")
    check "opencode return_reviewer first PASS does not resolve pending review" (resolved.Value = None)
    do! rmAsync workspaceDir
}

let secondPassResolvesAcceptedSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-second-pass-"
    let sessionID = "opencode-return-reviewer-second-pass"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let history =
        [| opencodeTextMessage sessionID "activation" (buildLoopMessage "Implement feature X" [ "With-Review Mode is active." ])
           opencodeTextMessage sessionID "double-check" "---\ndouble-check: confirmed\n---\nok" |]
    let returnTool = returnReviewerTool workspaceDir history store
    let! result = execVerdict returnTool (createObj [ "verdict", box "PASS"; "feedback", box null ]) (reviewerContext workspaceDir sessionID)
    check "opencode return_reviewer second PASS reports submitted" (result.Contains "Verdict submitted.")
    check "opencode return_reviewer second PASS resolves accepted" (resolved.Value = Some Accepted)
    do! rmAsync workspaceDir
}

let rejectResolvesRejectedSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-reject-"
    let sessionID = "opencode-return-reviewer-reject"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let returnTool = returnReviewerTool workspaceDir [||] store
    let! result = execVerdict returnTool (createObj [ "verdict", box "REJECT"; "feedback", box "missing tests" ]) (reviewerContext workspaceDir sessionID)
    check "opencode return_reviewer REJECT reports submitted" (result.Contains "Verdict submitted.")
    check "opencode return_reviewer REJECT resolves rejected with feedback" (resolved.Value = Some (Rejected "missing tests"))
    do! rmAsync workspaceDir
}

let rejectEmptyFeedbackStillRejectsSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-reject-empty-"
    let sessionID = "opencode-return-reviewer-reject-empty"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let returnTool = returnReviewerTool workspaceDir [||] store
    let! result = execVerdict returnTool (createObj [ "verdict", box "REJECT"; "feedback", box null ]) (reviewerContext workspaceDir sessionID)
    check "opencode return_reviewer REJECT empty feedback reports submitted" (result.Contains "Verdict submitted.")
    check "opencode return_reviewer REJECT empty feedback still rejects" (resolved.Value = Some (Rejected ""))
    do! rmAsync workspaceDir
}

let invalidVerdictNudgesSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-return-reviewer-invalid-"
    let sessionID = "opencode-return-reviewer-invalid"
    let store = createReviewStore ()
    let resolved = ref None
    setPending store sessionID resolved
    let returnTool = returnReviewerTool workspaceDir [||] store
    let! result = execVerdict returnTool (createObj [ "verdict", box "null"; "feedback", box null ]) (reviewerContext workspaceDir sessionID)
    check "opencode return_reviewer invalid verdict does not resolve" (resolved.Value = None)
    check "opencode return_reviewer invalid verdict uses codec error" (result.Contains "return_reviewer" && result.Contains "verdict")
    do! rmAsync workspaceDir
}

let run () : JS.Promise<unit> = promise {
    do! firstPassDoubleChecksSpec ()
    do! secondPassResolvesAcceptedSpec ()
    do! rejectResolvesRejectedSpec ()
    do! rejectEmptyFeedbackStillRejectsSpec ()
    do! invalidVerdictNudgesSpec ()
}
