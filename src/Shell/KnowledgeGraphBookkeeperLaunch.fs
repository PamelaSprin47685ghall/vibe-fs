module VibeFs.Shell.KnowledgeGraphBookkeeperLaunch

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.DelegatedAiSettings
open VibeFs.Shell.Dyn

let private invoke1 (target: obj) (methodName: string) (arg: obj) : JS.Promise<obj> =
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

let private jobMarkerPrompt (ctx: KnowledgeGraphJobContext) (promptText: string) : string =
    prependJobMarker ctx promptText

let private launchResultText (title: string) (childId: string) : string =
    $"Started {title} in background session {childId}."

let private failedLaunchResult (title: string) (reason: string) : string =
    $"Failed to start {title}: {reason}"

let private bookkeeperSessionTitle = "Bookkeeper"

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

/// Mux path: build prompt then delegate to sub-agent; results flow through the background sink.
let queueMuxBackgroundLaunch
    (deps: obj)
    (config: obj)
    (agentId: string)
    (title: string)
    (options: obj option)
    (startBackgroundJob: JS.Promise<unit> -> unit)
    (recordResult: string -> unit)
    (buildPrompt: unit -> JS.Promise<string>)
    (delegateToSubAgent: obj -> obj -> string -> string -> string -> obj option -> JS.Promise<string>)
    : unit =
    promise {
        try
            let! promptText = buildPrompt ()
            let! result = delegateToSubAgent deps config agentId promptText title options
            recordResult result
        with ex ->
            recordResult (failedLaunchResult title (string ex))
    }
    |> startBackgroundJob