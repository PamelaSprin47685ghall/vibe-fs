module VibeFs.Mux.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.Config
open VibeFs.Kernel.ReviewSession
open VibeFs.Shell.CallStore
open VibeFs.Shell.ReviewRuntime
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Kernel.Domain
open VibeFs.Kernel.HostTools

/// Mux host tool name universe. Sourced from ../mux/src/common/utils/tools/toolDefinitions.ts.
let private muxHostToolNames =
    [| "mux_agents_read"; "mux_agents_write"; "agent_skill_list"; "agent_skill_write"; "agent_skill_delete"
       "skills_catalog_search"; "skills_catalog_read"; "mux_config_read"; "mux_config_write"; "file_read"
       "attach_file"; "desktop_screenshot"; "desktop_move_mouse"; "desktop_click"; "desktop_double_click"
       "desktop_drag"; "desktop_scroll"; "desktop_type"; "desktop_key_press"; "agent_skill_read"
       "agent_skill_read_file"; "file_edit_replace_string"; "ask_user_question"; "propose_plan"; "bash"
       "task"; "task_await"; "task_apply_git_patch"; "task_terminate"; "task_list"; "agent_report"; "set_goal"
       "get_goal"; "complete_goal"; "heartbeat"; "todo_write"; "todo_read"; "review_pane_update"
       "review_pane_get"; "notify"; "analytics_query"; "web_fetch"; "web_search"; "google_search"; "url_context" |]

/// Map a Mux host tool name to the canonical name used by `canUseCanonical`.
/// Plugin tools are already canonical and pass through unchanged.
let private normalizeMuxToolName (toolName: string) : string =
    if toolName.StartsWith "file_edit_" then "edit"
    elif toolName = "file_read" then "read"
    elif toolName = "web_fetch" then "webfetch"
    elif toolName = "web_search" || toolName = "google_search" then "websearch"
    elif toolName = "todo_write" || toolName = "todo_read" then "todo"
    elif toolName.StartsWith "agent_skill_" || toolName.StartsWith "skills_catalog_" then "skill"
    elif toolName = "ask_user_question" then "question"
    elif toolName.StartsWith "task" then "task"
    else toolName

/// Emit a `toolPolicy.disabledTools` list in the Mux tool name universe while reusing
/// the canonical permission semantics from `canUseCanonical`.
let private disabledToolsForRole (toolNames: string array) (role: string) : string array =
    Array.append toolNames muxHostToolNames
    |> Array.distinct
    |> Array.filter (fun tool -> not (canUseCanonical role (normalizeMuxToolName tool)))

let private disabledToolsForReviewer (toolNames: string array) : string array =
    disabledToolsForRole toolNames "reviewer"

/// Mirrors opencode `canUseCanonical` for the requested role: emit a `toolPolicy.disabledTools`
/// list so the spawned child workspace strips everything except `agent_report` + `read`.
/// `subagentRole` is what the host binds to `roleScopedHostRemovals` and vibe-fs plugin policy.
let toolOptions (toolNames: string array) (role: string) (aiSettingsAgentId: string) : obj option =
    Some (createObj [ "experiments", box (createObj [ "subagentRole", box role; "toolPolicy", box (createObj [ "disabledTools", box (disabledToolsForRole toolNames role) ]) ]); "aiSettingsAgentId", box aiSettingsAgentId ])

let private abortableConfig (config: obj) (signal: obj) = Dyn.withKey config "abortSignal" signal

let private muxStrReq (desc: string) : obj =
    createObj [ "type", box "string"; "minLength", box 1; "description", box desc ]

let private muxStrArrayReq (desc: string) : obj =
    createObj [ "type", box "array"; "minItems", box 1; "items", createObj [ "type", box "string"; "minLength", box 1 ]; "description", box desc ]

let private muxStrArrayOpt (desc: string) : obj =
    createObj [ "type", box "array"; "items", createObj [ "type", box "string"; "minLength", box 1 ]; "description", box desc ]

let private muxObjectSchema (properties: obj) (required: string array) : obj =
    createObj [ "type", box "object"; "properties", properties; "required", box required; "additionalProperties", box false ]

let private muxCoderIntentsSchema (intentsDesc: string) : obj =
    let targetItem =
        muxObjectSchema
            (createObj [ "file", muxStrReq coderTargetFileDesc
                         "guide", muxStrReq coderTargetGuideDesc
                         "draft", createObj [ "type", box "string"; "description", box coderTargetDraftDesc ] ])
            [| "file"; "guide" |]
    let intentItem =
        muxObjectSchema
            (createObj
                [ "objective", muxStrReq coderObjectiveDesc
                  "background", muxStrReq coderBackgroundDesc
                  "do_not_touch", muxStrArrayOpt coderDoNotTouchDesc
                  "targets",
                  createObj
                      [ "type", box "array"
                        "minItems", box 1
                        "items", targetItem
                        "description", box coderTargetsDesc ] ])
            [| "objective"; "background"; "targets" |]
    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", intentItem
          "description", box intentsDesc ]

let private muxInvestigatorIntentsSchema (intentsDesc: string) : obj =
    let intentItem =
        muxObjectSchema
            (createObj
                [ "objective", muxStrReq investigatorObjectiveDesc
                  "background", muxStrReq investigatorBackgroundDesc
                  "questions", muxStrArrayReq investigatorQuestionsDesc
                  "entries", muxStrArrayOpt investigatorEntriesDesc ])
            [| "objective"; "background"; "questions" |]
    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", intentItem
          "description", box intentsDesc ]

module Tool =
    let bind (deps: obj) (toolNames: string array) (agentId: string) (title: string) (aiSettingsAgentId: string) (role: string) (buildPrompt: obj -> obj -> JS.Promise<string>) : obj -> obj -> JS.Promise<string> =
        fun config args ->
            promise {
                match strField config "workspaceId" with
                | None -> return $"{title} requires workspaceId"
                | Some _ ->
                    let! prompt = buildPrompt config args
                    return! runMuxSubagent deps config agentId prompt title (toolOptions toolNames role aiSettingsAgentId)
            }

    let bindParallel (deps: obj) (toolNames: string array) (agentId: string) (title: string) (aiSettingsAgentId: string) (role: string) (buildPrompts: obj -> obj -> JS.Promise<string array>) : obj -> obj -> JS.Promise<string> =
        fun config args ->
            promise {
                match strField config "workspaceId" with
                | None -> return $"{title} requires workspaceId"
                | Some _ ->
                    let! prompts = buildPrompts config args
                    if prompts.Length = 0 then return "Error: `intents` must be a non-empty array."
                    else
                        let controller = AbortController()
                        let opts = toolOptions toolNames role aiSettingsAgentId
                        let! reports =
                            prompts
                            |> Array.map (fun prompt ->
                                promise {
                                    try
                                        let! r = runMuxSubagent deps (abortableConfig config controller.signal) agentId prompt title opts
                                        return Some r
                                    with _ ->
                                        controller.abort()
                                        return None
                                })
                            |> Promise.all
                        return joinReports (reports |> Array.choose id |> Array.toList)
            }

let private buildPromptsFor parser constructor args =
    promise {
        match parser (Dyn.get args "intents") with
        | Error _ -> return [||]
        | Ok intents -> return formatPrompt Host.Mimocode (constructor intents) |> List.toArray
    }

let private buildCoderPrompts (_config: obj) (args: obj) : JS.Promise<string array> =
    buildPromptsFor parseCoderIntents Coder args

let coderTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "coder"
      description = description "coder"
      parameters = mkSchema (createObj [ "intents", box (muxCoderIntentsSchema Params.coderIntents); "tdd", box (strEnumProp Params.coderTdd [| "red"; "green" |]) ]) [| "intents"; "tdd" |]
      execute = Tool.bindParallel deps toolNames "exec" "Coder" "exec" "coder" buildCoderPrompts
      condition = None }

let private buildInvestigatorPrompts (_config: obj) (args: obj) : JS.Promise<string array> =
    buildPromptsFor parseInvestigatorIntents Investigator args

let investigatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "investigator"
      description = description "investigator"
      parameters = mkSchema (createObj [ "intents", box (muxInvestigatorIntentsSchema Params.investigatorIntents) ]) [| "intents" |]
      execute = Tool.bindParallel deps toolNames "explore" "Investigator" "explore" "investigator" buildInvestigatorPrompts
      condition = None }

let private meditatorPromptFromArgs (config: obj) (args: obj) : JS.Promise<string> =
    promise {
        let intent = defaultArg (strField args "intent") ""
        let files = requireStrArray args "files"
        let fileList = List.ofArray files
        let cwd =
            defaultArg (strField config "directory") ""
            |> fun value -> if value <> "" then value else defaultArg (strField config "cwd") ""
            |> fun value -> if value <> "" then value else defaultArg (strField config "workspacePath") ""
        let! results = VibeFs.Shell.WorkspaceFiles.readReverieFiles cwd fileList
        let sections =
            Array.zip files (List.toArray results)
            |> Array.map (fun (file, r) -> { file = file; content = r.content } : MeditatorFileSection)
            |> List.ofArray
        return formatPrompt Host.Mimocode (Meditator(intent, sections)) |> List.head
    }

let meditatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "meditator"
      description = description "meditator"
      parameters =
        mkSchema
            (createObj [ "intent", box (strProp Params.meditatorIntent); "files", box (strArrayProp Params.meditatorFiles) ])
            [| "intent"; "files" |]
      execute = Tool.bind deps toolNames "explore" "Meditator" "exec" "meditator" meditatorPromptFromArgs
      condition = None }

let private buildBrowserPrompt (_config: obj) (args: obj) : JS.Promise<string> =
    promise {
        let intent = defaultArg (strField args "intent") ""
        return formatPrompt Host.Mimocode (Browser intent) |> List.head
    }

let browserTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "browser"
      description = description "browser"
      parameters = mkSchema (createObj [ "intent", box (strProp Params.browserIntent) ]) [| "intent" |]
      execute = Tool.bind deps toolNames "explore" "Browser" "explore" "browser" buildBrowserPrompt
      condition = None }

let private awaitReviewVerdict (verdictPromise: JS.Promise<obj>) : JS.Promise<ReviewResult> =
    promise {
        try
            let! args = verdictPromise
            let v = defaultArg (strField args "verdict") "" |> fun s -> s.Trim().ToLowerInvariant()
            let feedback = defaultArg (strField args "feedback") ""
            if v = "pass" then return Accepted
            elif v = "reject" then return Rejected feedback
            else return Rejected $"Reviewer returned unclear verdict: \"{v}\". Expected \"pass\" or \"reject\"."
        with _ ->
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
            with _ ->
                return None
    }

let private syncReviewTaskFromHistory (deps: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (sessionID: string) : JS.Promise<string option> =
    promise {
        let! historyTask = tryGetHistoryTask deps sessionID
        historyTask |> Option.iter (syncReviewProjection reviewStore sessionID)
        return
            match historyTask with
            | Some task -> task
            | None -> reviewStore.getReviewTask sessionID
    }

let submitReviewTool (deps: obj) (toolNames: string array) (callStore: CallStore) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : ToolDefinition =
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

let returnReviewerTool (deps: obj) (callStore: CallStore) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : ToolDefinition =
    let tryResolvePendingReviewCall (sessionID: string) (resolution: obj) : bool =
        callStore.PendingCalls.Keys
        |> Seq.tryFind (fun k -> k.StartsWith(sessionID + "-review-"))
        |> Option.map (fun callId -> resolveCall callStore callId resolution)
        |> Option.defaultValue false

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
                      tryResolvePendingReviewCall sessionID resolution |> ignore
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
                                  tryResolvePendingReviewCall sessionID resolution |> ignore
                                  reviewStore.deactivateReview sessionID
                                  return "Verdict submitted."
                              else
                                  return doubleCheckPrompt (defaultArg (inferReviewTaskFromTexts texts) "")
                          with _ ->
                              return doubleCheckPrompt (defaultArg (reviewStore.getReviewTask sessionID) "")
          }
      condition = None }
