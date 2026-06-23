module VibeFs.Mux.ReviewToolsMux

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.ReviewPrompts
open VibeFs.Kernel.ReviewSession
open VibeFs.Kernel.Domain
open VibeFs.Shell.CallStore
open VibeFs.Shell.ReviewRuntime
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.SubagentTools
open VibeFs.Shell
open VibeFs.Shell.Dyn

let private awaitReviewVerdict (verdictPromise: JS.Promise<obj>) : JS.Promise<ReviewResult> =
    promise {
        try
            let! args = verdictPromise
            let v = defaultArg (strField args "verdict") "" |> fun s -> s.Trim().ToLowerInvariant()
            let feedback = defaultArg (strField args "feedback") ""
            if v = "pass" then return Accepted
            elif v = "reject" then return Rejected feedback
            else return Rejected $"Reviewer returned unclear verdict: \"{v}\". Expected \"pass\" or \"reject\"."
        with ex ->
            return Terminated
    }

let private extractHistoryTexts (history: obj array) : string list =
    history
    |> Array.toList
    |> List.collect (fun item ->
        if Dyn.typeIs item "string" then [ string item ]
        else
            let texts = ResizeArray<string>()
            let content = Dyn.str item "content"
            if content <> "" then texts.Add(content)
            let text = Dyn.str item "text"
            if text <> "" then texts.Add(text)
            let parts = Dyn.get item "parts"
            if not (Dyn.isNullish parts) && Dyn.isArray parts then
                for p in (parts :?> obj array) do
                    let partText = Dyn.str p "text"
                    if partText <> "" then texts.Add(partText)
            List.ofSeq texts)

let private tryGetHistoryTask (deps: obj) (sessionID: string) : JS.Promise<string option option> =
    promise {
        let getHistory = if Dyn.isNullish deps then null else Dyn.get deps "getChatHistory"
        if sessionID = "" || Dyn.isNullish getHistory then
            return None
        else
            try
                let! history = unbox<JS.Promise<obj array>> (getHistory $ sessionID)
                return Some(inferReviewTaskFromTexts (extractHistoryTexts history))
            with ex ->
                return None
    }

let private syncReviewTaskFromHistory (deps: obj) (reviewStore: ReviewStore) (sessionID: string) : JS.Promise<string option> =
    promise {
        let! historyTask = tryGetHistoryTask deps sessionID
        historyTask |> Option.iter (syncReviewProjection reviewStore sessionID)
        return
            match historyTask with
            | Some task -> task
            | None -> reviewStore.getReviewTask sessionID
    }

let submitReviewTool (deps: obj) (toolNames: string array) (callStore: CallStore) (reviewStore: ReviewStore) : ToolDefinition =
    { name = "submit_review"
      description = "Submit completed work for review. Creates a reviewer sub-agent that examines the changes against evaluation criteria and returns PASS or actionable feedback. Only works when session is in active With-Review Mode."
      parameters = mkSchema (createObj [ "report", box (strProp "Detailed report of what was done"); "affectedFiles", box (strArrayProp "List of file paths that were modified or created") ]) [| "report"; "affectedFiles" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "submit_review requires workspaceId"
          else
              promise {
                  let report = defaultArg (strField args "report") ""
                  let affectedFiles = requireStrArray args "affectedFiles" |> List.ofArray
                  let workspaceId = Dyn.str config "workspaceId"
                  let! resolvedTask = syncReviewTaskFromHistory deps reviewStore workspaceId
                  if not (reviewStore.tryLockReview workspaceId) then
                      return
                          if reviewStore.isReviewActive workspaceId then "A review is already in progress for this session."
                          else "You do not need review. Just continue with your work."
                  else
                      try
                          let originalTask = defaultArg resolvedTask ""
                          let callId = workspaceId + "-review-" + string (Domain.nowMs ())
                          let verdictPromise = registerCallWithTimeout callStore callId 300000
                          let reviewPrompt = reviewSubmissionVerdictPrompt originalTask report affectedFiles callId
                          let experiments = createObj [ "subagentRole", box "reviewer"; "toolPolicy", box (createObj [ "disabledTools", box (disabledToolsForReviewer toolNames) ]) ]
                          let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
                          try
                              let! _ = delegateToSubAgent deps config "explore" reviewPrompt "Review" (Some opts)
                              let! verdict = awaitReviewVerdict verdictPromise
                              match verdict with
                              | Accepted | Terminated -> reviewStore.deactivateReview workspaceId
                              | Rejected _ -> ()
                              return formatReviewResult verdict
                          with ex ->
                              reviewStore.deactivateReview workspaceId
                              return! Promise.reject ex
                      finally
                          reviewStore.unlockReview workspaceId
              }
      condition = None }

let returnReviewerTool (deps: obj) (callStore: CallStore) (reviewStore: ReviewStore) : ToolDefinition =
    let tryResolvePendingReviewCall (sessionID: string) (resolution: obj) : JS.Promise<bool> =
        resolveFirstMatchingAsync callStore (sessionID + "-review-") resolution

    { name = "return_reviewer"
      description = "Submit a review verdict for the active review call. feedback:null/empty accepts; non-empty feedback rejects."
      parameters = mkSchema (createObj [ "feedback", box (createObj [ "type", box "string"; "description", box "Null/empty to accept; detailed feedback to reject" ]) ]) [||]
      execute = fun config args ->
          promise {
              let sessionID = Dyn.str config "sessionID"
              if sessionID = "" then return "return_reviewer requires sessionID"
              else
                  let feedback = defaultArg (strField args "feedback") ""
                  let verdict = defaultArg (strField args "verdict") "" |> fun s -> s.Trim().ToLowerInvariant()
                  let isReject = verdict = "reject" || feedback <> ""
                  if isReject then
                      let resolution = createObj [ "verdict", box "reject"; "feedback", box feedback ]
                      do! tryResolvePendingReviewCall sessionID resolution |> Promise.map ignore
                      return "Verdict submitted."
                  else
                      let getHistory = if Dyn.isNullish deps then null else Dyn.get deps "getChatHistory"
                      if Dyn.isNullish getHistory then
                          return doubleCheckPrompt (defaultArg (reviewStore.getReviewTask sessionID) "")
                      else
                          try
                              let! history = unbox<JS.Promise<obj array>> (getHistory $ sessionID)
                              let texts = extractHistoryTexts history
                              syncReviewProjection reviewStore sessionID (inferReviewTaskFromTexts texts)
                              if hasDoubleCheckAnchor texts then
                                  let resolution = createObj [ "verdict", box "pass"; "feedback", box "" ]
                                  do! tryResolvePendingReviewCall sessionID resolution |> Promise.map ignore
                                  reviewStore.deactivateReview sessionID
                                  return "Verdict submitted."
                              else
                                  return doubleCheckPrompt (defaultArg (inferReviewTaskFromTexts texts) "")
                          with ex ->
                              return doubleCheckPrompt (defaultArg (reviewStore.getReviewTask sessionID) "")
          }
      condition = None }