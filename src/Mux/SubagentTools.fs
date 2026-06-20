module VibeFs.Mux.SubagentTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.Prompts
open VibeFs.Kernel.Subagent
open VibeFs.Kernel.SubagentIntents
open VibeFs.Kernel.ToolCatalog
open VibeFs.Kernel.Config
open VibeFs.Kernel.ReviewSession
open VibeFs.Shell.CallStore
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers

let private dateNow () = int (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())

let private disabledToolsForReviewer (toolNames: string array) : string array =
    deniedTools "reviewer" (Array.toList toolNames) |> Array.ofList

let private toolOptions (toolNames: string array) (role: string) (aiSettingsAgentId: string) : obj option =
    Some (
        createObj
            [ "experiments", box (createObj [ "subagentRole", box role; "toolPolicy", box (createObj [ "disabledTools", box (deniedTools role (Array.toList toolNames) |> Array.ofList) ]) ])
              "aiSettingsAgentId", box aiSettingsAgentId ])

let private abortableConfig (config: obj) (signal: obj) = Dyn.withKey config "abortSignal" signal

let private muxStrReq (desc: string) : obj =
    createObj [ "type", box "string"; "minLength", box 1; "description", box desc ]

let private muxStrArrayReq (desc: string) : obj =
    createObj
        [ "type", box "array"
          "minItems", box 1
          "items", createObj [ "type", box "string"; "minLength", box 1 ]
          "description", box desc ]

let private muxStrArrayOpt (desc: string) : obj =
    createObj
        [ "type", box "array"
          "items", createObj [ "type", box "string"; "minLength", box 1 ]
          "description", box desc ]

let private muxObjectSchema (properties: obj) (required: string array) : obj =
    createObj
        [ "type", box "object"
          "properties", properties
          "required", box required
          "additionalProperties", box false ]

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
    let bind
        (deps: obj)
        (toolNames: string array)
        (agentId: string)
        (title: string)
        (aiSettingsAgentId: string)
        (role: string)
        (buildPrompt: obj -> obj -> Async<string>)
        : obj -> obj -> JS.Promise<string> =
        fun config args ->
            async {
                match strField config "workspaceId" with
                | None -> return $"{title} requires workspaceId"
                | Some _ ->
                    let! prompt = buildPrompt config args
                    let opts = toolOptions toolNames role aiSettingsAgentId
                    return!
                        runMuxSubagent deps config agentId prompt title opts
                        |> Async.AwaitPromise
            }
            |> Async.StartAsPromise

    let bindParallel
        (deps: obj)
        (toolNames: string array)
        (agentId: string)
        (title: string)
        (aiSettingsAgentId: string)
        (role: string)
        (buildPrompts: obj -> obj -> Async<string array>)
        : obj -> obj -> JS.Promise<string> =
        fun config args ->
            async {
                match strField config "workspaceId" with
                | None -> return $"{title} requires workspaceId"
                | Some _ ->
                    let! prompts = buildPrompts config args
                    if prompts.Length = 0 then
                        return "Error: `intents` must be a non-empty array."
                    else
                        let controller = AbortController()
                        let opts = toolOptions toolNames role aiSettingsAgentId
                        let! reports =
                            prompts
                            |> Array.map (fun prompt ->
                                async {
                                    try
                                        let! r = runMuxSubagent deps (abortableConfig config controller.signal) agentId prompt title opts |> Async.AwaitPromise
                                        return Some r
                                    with _ ->
                                        controller.abort()
                                        return None
                                })
                            |> Async.Parallel
                        return joinReports (reports |> Array.choose id |> Array.toList)
            }
            |> Async.StartAsPromise

let private buildCoderPrompts (_config: obj) (args: obj) : Async<string array> =
    async {
        match parseCoderIntents (Dyn.get args "intents") with
        | Error _ -> return [||]
        | Ok intents -> return formatPrompt mimocode (Coder intents) |> List.toArray
    }

let coderTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "coder"
      description = description "coder"
      parameters = mkSchema (createObj [ "intents", box (muxCoderIntentsSchema Params.coderIntents); "tdd", box (strEnumProp Params.coderTdd [| "red"; "green" |]) ]) [| "intents"; "tdd" |]
      execute = Tool.bindParallel deps toolNames "exec" "Coder" "exec" "coder" buildCoderPrompts
      condition = None }

let private buildInvestigatorPrompts (_config: obj) (args: obj) : Async<string array> =
    async {
        match parseInvestigatorIntents (Dyn.get args "intents") with
        | Error _ -> return [||]
        | Ok intents -> return formatPrompt mimocode (Investigator intents) |> List.toArray
    }

let investigatorTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "investigator"
      description = description "investigator"
      parameters = mkSchema (createObj [ "intents", box (muxInvestigatorIntentsSchema Params.investigatorIntents) ]) [| "intents" |]
      execute = Tool.bindParallel deps toolNames "explore" "Investigator" "explore" "investigator" buildInvestigatorPrompts
      condition = None }

let private meditatorPromptFromArgs (config: obj) (args: obj) : Async<string> =
    async {
        let intent = defaultArg (strField args "intent") ""
        let files = requireStrArray args "files" |> List.ofArray
        let cwd = defaultArg (strField config "directory") ""
        let! results = VibeFs.Shell.WorkspaceFiles.readReverieFiles cwd files |> Async.AwaitPromise
        let sections =
            results
            |> List.map (fun r -> { file = r.filePath; content = r.content } : MeditatorFileSection)
        return formatPrompt mimocode (Meditator(intent, sections)) |> List.head
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

let private buildBrowserPrompt (_config: obj) (args: obj) : Async<string> =
    async {
        let intent = defaultArg (strField args "intent") ""
        return formatPrompt mimocode (Browser intent) |> List.head
    }

let browserTool (deps: obj) (toolNames: string array) : ToolDefinition =
    { name = "browser"
      description = description "browser"
      parameters = mkSchema (createObj [ "intent", box (strProp Params.browserIntent) ]) [| "intent" |]
      execute = Tool.bind deps toolNames "explore" "Browser" "explore" "browser" buildBrowserPrompt
      condition = None }

let submitReviewTool (deps: obj) (toolNames: string array) (callStore: CallStore) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) : ToolDefinition =
    { name = "submit_review"
      description = "Submit completed work for review. Creates a reviewer sub-agent that examines the changes against evaluation criteria and returns PASS or actionable feedback. Only works when session is in active loop mode."
      parameters = mkSchema (createObj [ "report", box (strProp "Detailed report of what was done"); "affectedFiles", box (strArrayProp "List of file paths that were modified or created") ]) [| "report"; "affectedFiles" |]
      execute = fun config args ->
          if strField config "workspaceId" = None then resolveStr "submit_review requires workspaceId"
          else
              let report = defaultArg (strField args "report") ""
              let affectedFiles = requireStrArray args "affectedFiles" |> List.ofArray
              let workspaceId = Dyn.str config "workspaceId"
              if not (reviewStore.tryLockReview workspaceId) then
                  if reviewStore.isReviewActive workspaceId then resolveStr "A review is already in progress for this session."
                  else resolveStr "You do not need review. Just continue with your work."
              else
                  async {
                      try
                          let originalTask = defaultArg (reviewStore.getReviewTask workspaceId) ""
                          let taskSection = if originalTask = "" then "" else "\n=== Original Task ===\n\n" + originalTask
                          let callId = workspaceId + "-review-" + string (dateNow ())
                          let verdictPromise = registerCallWithTimeout callStore callId 300000
                          let reviewPrompt =
                              ReviewerVerdictPrompts.reviewerVerdictInstructions
                              + "\n\n=== Change Report ===\n\n" + report
                              + "\n\n=== Affected Files ===\n\n" + String.concat "\n" affectedFiles
                              + "\n" + taskSection
                          let disabledTools = disabledToolsForReviewer toolNames
                          let experiments =
                              createObj
                                  [ "subagentRole", box "reviewer"
                                    "toolPolicy", box (createObj [ "disabledTools", box disabledTools ]) ]
                          let opts = createObj [ "aiSettingsAgentId", box "plan"; "experiments", box experiments ]
                          let! _ = delegateToSubAgent deps config "explore" reviewPrompt "Review" (Some opts) |> Async.AwaitPromise
                          let! verdict =
                              async {
                                  try
                                      let! args = verdictPromise |> Async.AwaitPromise
                                      let v = defaultArg (strField args "verdict") "" |> fun s -> s.Trim().ToLowerInvariant()
                                      let feedback = defaultArg (strField args "feedback") ""
                                      if v = "pass" then return Accepted
                                      elif v = "reject" then return Rejected feedback
                                      else return Rejected $"Reviewer returned unclear verdict: \"{v}\". Expected \"pass\" or \"reject\"."
                                  with ex ->
                                      return Rejected $"Reviewer timed out or failed: {ex.Message}"
                              }
                          match verdict with
                          | Accepted -> reviewStore.deactivateReview workspaceId
                          | _ -> ()
                          return formatReviewResult verdict
                      finally
                          reviewStore.unlockReview workspaceId
                  } |> Async.StartAsPromise
      condition = None }
