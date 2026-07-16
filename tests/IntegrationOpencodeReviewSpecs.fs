module Wanxiangshu.Tests.IntegrationOpencodeReviewSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Runtime.LoopMessages
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Hosts.Opencode.ReviewTools
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn

let private opencodeTextMessage (sessionID: string) (id: string) (text: string) : obj =
    box
        {| info =
            createObj
                [ "id", box id
                  "agent", box "reviewer"
                  "sessionID", box sessionID
                  "role", box "assistant" ]
           parts = [| box {| ``type`` = "text"; text = text |} |] |}

let private mockClient (messages: obj array) : obj =
    createObj
        [ "session",
          box (
              createObj
                  [ "messages",
                    box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box {| data = messages |} })) ]
          ) ]

let private returnReviewerTool workspaceDir messages store : obj =
    let ctx = createObj [ "directory", box workspaceDir; "client", mockClient messages ]
    let scope = Wanxiangshu.Runtime.RuntimeScope.create ()
    submitReviewResultTool ctx store scope

let private reviewerContext workspaceDir sessionID : obj =
    createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]

let private setPending (store: ReviewStore) sessionID (resolved: ReviewResult option ref) : unit =
    store.setPendingReview (sessionID, (fun result -> resolved.Value <- Some result))

let private execVerdict (returnTool: obj) (args: obj) ctx : JS.Promise<string> =
    ((get returnTool "execute") $ (args, ctx)) |> unbox<JS.Promise<string>>

let firstPassDoubleChecksSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-return-reviewer-first-pass-"
        let sessionID = "opencode-return-reviewer-first-pass"
        let store = createReviewStore ()
        let resolved = ref None
        setPending store sessionID resolved

        let history =
            [| opencodeTextMessage
                   sessionID
                   "activation"
                   (buildLoopMessage "Implement feature X" [ "With-Review Mode is active." ]) |]

        let returnTool = returnReviewerTool workspaceDir history store

        let! result =
            execVerdict
                returnTool
                (createObj [ "verdict", box "PERFECT"; "feedback", box null ])
                (reviewerContext workspaceDir sessionID)

        check "opencode return_reviewer first PERFECT returns double-check prompt" (result.Contains "double-check:")
        check "opencode return_reviewer first PERFECT does not resolve pending review" (resolved.Value = None)
        do! rmAsync workspaceDir
    }

let secondPassResolvesAcceptedSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-return-reviewer-second-pass-"
        let sessionID = "opencode-return-reviewer-second-pass"
        let store = createReviewStore ()
        let resolved = ref None
        setPending store sessionID resolved

        let history =
            [| opencodeTextMessage
                   sessionID
                   "activation"
                   (buildLoopMessage "Implement feature X" [ "With-Review Mode is active." ])
               opencodeTextMessage sessionID "double-check" "---\ndouble-check: confirmed\n---\nok" |]

        let returnTool = returnReviewerTool workspaceDir history store

        let! result =
            execVerdict
                returnTool
                (createObj [ "verdict", box "PERFECT"; "feedback", box null ])
                (reviewerContext workspaceDir sessionID)

        check "opencode return_reviewer second PERFECT reports submitted" (result.Contains "Verdict submitted.")

        check
            "opencode return_reviewer second PERFECT asks stop"
            (result.Contains "Please stop the session immediately.")

        check "opencode return_reviewer second PERFECT resolves accepted" (resolved.Value = Some(Accepted ""))
        do! rmAsync workspaceDir
    }

let reviseResolvesNeedsRevisionSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-return-reviewer-revise-"
        let sessionID = "opencode-return-reviewer-revise"
        let store = createReviewStore ()
        let resolved = ref None
        setPending store sessionID resolved
        let returnTool = returnReviewerTool workspaceDir [||] store

        let! result =
            execVerdict
                returnTool
                (createObj [ "verdict", box "REVISE"; "feedback", box "missing tests" ])
                (reviewerContext workspaceDir sessionID)

        check "opencode return_reviewer REVISE reports submitted" (result.Contains "Verdict submitted.")

        check "opencode return_reviewer REVISE asks stop" (result.Contains "Please stop the session immediately.")

        check
            "opencode return_reviewer REVISE resolves needs_revision with feedback"
            (resolved.Value = Some(NeedsRevision "missing tests"))

        do! rmAsync workspaceDir
    }

let reviseEmptyFeedbackStillNeedsRevisionSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-return-reviewer-revise-empty-"
        let sessionID = "opencode-return-reviewer-revise-empty"
        let store = createReviewStore ()
        let resolved = ref None
        setPending store sessionID resolved
        let returnTool = returnReviewerTool workspaceDir [||] store

        let! result =
            execVerdict
                returnTool
                (createObj [ "verdict", box "REVISE"; "feedback", box null ])
                (reviewerContext workspaceDir sessionID)

        check "opencode return_reviewer REVISE empty feedback reports submitted" (result.Contains "Verdict submitted.")

        check "opencode return_reviewer REVISE empty asks stop" (result.Contains "Please stop the session immediately.")

        check
            "opencode return_reviewer REVISE empty feedback still needs_revision"
            (resolved.Value = Some(NeedsRevision ""))

        do! rmAsync workspaceDir
    }

let invalidVerdictNudgesSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-return-reviewer-invalid-"
        let sessionID = "opencode-return-reviewer-invalid"
        let store = createReviewStore ()
        let resolved = ref None
        setPending store sessionID resolved
        let returnTool = returnReviewerTool workspaceDir [||] store

        let! result =
            execVerdict
                returnTool
                (createObj [ "verdict", box "null"; "feedback", box null ])
                (reviewerContext workspaceDir sessionID)

        check "opencode return_reviewer invalid verdict does not resolve" (resolved.Value = None)

        check
            "opencode return_reviewer invalid verdict uses codec error"
            (result.Contains "return_reviewer" && result.Contains "verdict")

        do! rmAsync workspaceDir
    }

let run () : JS.Promise<unit> =
    promise {
        do! firstPassDoubleChecksSpec ()
        do! secondPassResolvesAcceptedSpec ()
        do! reviseResolvesNeedsRevisionSpec ()
        do! reviseEmptyFeedbackStillNeedsRevisionSpec ()
        do! invalidVerdictNudgesSpec ()
    }
