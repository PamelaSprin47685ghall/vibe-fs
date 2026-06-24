module VibeFs.Opencode.KnowledgeGraphRuntimeIO

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell

open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Kernel.Messaging
open VibeFs.Opencode.MessagingCodec
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphPortLock
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Mux.AiSettings
open VibeFs.Shell.Dyn

let invoke1 (target: obj) (methodName: string) (arg: obj) : JS.Promise<obj> =
    unbox (target?(methodName)(arg))

/// Resolve the host session API object (create + prompt) from the knowledge graph client,
/// or None when the host does not expose it.
let sessionApiOf (client: obj) : obj option =
    if isNullish client then None
    else
        let session = get client "session"
        if isNullish session then None
        elif not (typeIs (get session "create") "function") then None
        elif not (typeIs (get session "prompt") "function") then None
        else Some session

let buildEntries (root: string) (drafts: KnowledgeGraphDraft list) : JS.Promise<Result<KnowledgeGraphEntry list, string>> =
    promise {
        let! projection = readProjection root
        let normalizedDrafts = normalizeDraftIds projection drafts
        let random = Random()
        return applyDrafts (allocateRandomHexId (fun () -> random.Next(0, 65536))) projection normalizedDrafts
    }

let submitForKind (portLockTimeoutMs: int64) (portLockRetryDelayMs: int) (todayStr: string) (root: string) (kind: KnowledgeGraphJobKind) (drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
    promise {
        let! lockResult = withKnowledgeGraphPortLock portLockTimeoutMs portLockRetryDelayMs root (fun () ->
            promise {
                let! entriesResult = buildEntries root drafts
                match entriesResult with
                | Error e -> return e
                | Ok entries ->
                    match kind with
                    | AppendAfterWork ->
                        do! appendEntries root todayStr entries
                        return $"Appended {entries.Length} knowledge graph entries."
                    | DailyRewrite date ->
                        do! rewriteDay root date entries
                        return $"Rewrote knowledge graph day {date}."
            })
        match lockResult with
        | Error e -> return e
        | Ok msg -> return msg
    }

let private jobMarkerPrompt (ctx: KnowledgeGraphJobContext) (promptText: string) : string =
    prependJobMarker ctx promptText

let private launchResultText (title: string) (childId: string) : string =
    $"Started {title} in background session {childId}."

let private failedLaunchResult (title: string) (reason: string) : string =
    $"Failed to start {title}: {reason}"

let private bookkeeperSessionTitle = "Bookkeeper"

let tryResolveJobContext (client: obj) (directory: string) (sessionID: string) : JS.Promise<KnowledgeGraphJobContext option> =
    promise {
        if sessionID.Trim() = "" || isNullish client then return None
        else
            try
                let! result = invoke1 (get client "session") "messages" (box {| path = box {| id = sessionID |}; query = box {| directory = directory |} |})
                let data = get result "data"
                if isNullish data || not (isArray data) then return None
                else
                    let messages = MessagingCodec.decodeMessages (unbox<obj array> data)
                    let texts =
                        messages
                        |> flatten
                        |> List.choose (fun fp ->
                            match fp.part with
                            | TextPart text -> Some text
                            | _ -> None)
                    return texts |> List.tryPick tryParseJobMarker
            with _ -> return None
    }

/// Load decoded typed messages for a session, mirroring tryResolveJobContext
/// but returning the full Message list instead of just a job marker.
let loadSessionMessages (client: obj) (directory: string) (sessionID: string) : JS.Promise<Message<obj> list> =
    promise {
        if sessionID.Trim() = "" || isNullish client then return []
        else
            try
                let! result = invoke1 (get client "session") "messages" (box {| path = box {| id = sessionID |}; query = box {| directory = directory |} |})
                let data = get result "data"
                if isNullish data || not (isArray data) then return []
                else
                    return MessagingCodec.decodeMessages (unbox<obj array> data)
            with _ -> return []
    }

let private parseModelString (modelString: string) : obj option =
    let slash = modelString.IndexOf('/')
    if slash <= 0 || slash >= modelString.Length - 1 then None
    else Some (box {| providerID = modelString.[0..slash-1]; modelID = modelString.[slash+1..] |})

let launchBackgroundSession (session: obj) (root: string) (parentID: string option) (kind: KnowledgeGraphJobKind) (title: string) (promptText: string) (aiSettings: DelegatedAiSettings) (_client: obj) (registry: ChildAgentRegistry) : JS.Promise<string> =
    promise {
        try
            let ctx = { workspaceRoot = root; kind = kind }
            let prompt = jobMarkerPrompt ctx promptText
            let createBody =
                box {|
                    query = box {| directory = root |}
                    body = box {| parentID = (match parentID with Some p -> box p | None -> box null); title = bookkeeperSessionTitle |}
                |}
            let! createResult = invoke1 session "create" createBody
            let childID = str (get createResult "data") "id"
            if childID = "" then
                return failedLaunchResult title "Failed to create child session"
            else
                registry.RegisterChildAgent(childID, "bookkeeper", parentID)
                let bodyFields =
                    [ yield "agent", box "bookkeeper"
                      yield "parts", box [| box {| ``type`` = "text"; text = prompt |} |]
                      match aiSettings.modelString with
                      | Some ms ->
                          match parseModelString ms with
                          | Some model -> yield "model", model
                          | None -> ()
                      | None -> ()
                      match aiSettings.thinkingLevel with
                      | Some level when level.Trim() <> "" -> yield "variant", box level
                      | _ -> () ]
                let promptBody = createObj [ "path", box {| id = childID |}; "body", createObj bodyFields ]
                let! _ = invoke1 session "prompt" promptBody
                return launchResultText title childID
        with ex ->
            return failedLaunchResult title (string ex)
    }

/// Fire-and-forget: build the prompt then launch the background session without
/// serializing unrelated bookkeeper sessions behind one another.
let queueBackgroundLaunch (client: obj) (startBackgroundJob: JS.Promise<unit> -> unit) (recordResult: string -> unit) (root: string) (parentID: string option) (kind: KnowledgeGraphJobKind) (title: string) (buildPrompt: unit -> JS.Promise<string>) (aiSettings: DelegatedAiSettings) (registry: ChildAgentRegistry) : unit =
    match sessionApiOf client with
    | None -> recordResult (failedLaunchResult title "host client is missing session.create/session.prompt APIs")
    | Some session ->
        promise {
            try
                let! promptText = buildPrompt ()
                let! result = launchBackgroundSession session root parentID kind title promptText aiSettings client registry
                recordResult result
            with ex ->
                recordResult (failedLaunchResult title (string ex))
        }
        |> startBackgroundJob
