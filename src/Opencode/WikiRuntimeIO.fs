module VibeFs.Opencode.WikiRuntimeIO

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.WikiRuntimeState
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.WikiFiles
open VibeFs.Shell.WikiPortLock
open VibeFs.Shell.ChildAgentRegistry

let invoke1 (target: obj) (methodName: string) (arg: obj) : JS.Promise<obj> =
    unbox (target?(methodName)(arg))

/// Resolve the host session API object (create + prompt) from the wiki client,
/// or None when the host does not expose it.
let sessionApiOf (client: obj) : obj option =
    if isNullish client then None
    else
        let session = get client "session"
        if isNullish session then None
        elif not (typeIs (get session "create") "function") then None
        elif not (typeIs (get session "prompt") "function") then None
        else Some session

let buildEntries (root: string) (drafts: WikiDraft list) : JS.Promise<WikiEntry list> =
    promise {
        let! projection = readProjection root
        let normalizedDrafts = normalizeDraftIds projection drafts
        match applyDrafts (fun knownIds ->
                  let random = Random()
                  let rec loop attempts =
                      if attempts > 65536 then failwith "wiki id space exhausted"
                      else
                          let candidate = sprintf "%04x" (random.Next(0, 65536))
                          if Set.contains candidate knownIds then loop (attempts + 1) else candidate
                  loop 0) projection normalizedDrafts with
        | Ok entries -> return entries
        | Error error -> return raise (exn error)
    }

let submitForKind (todayStr: string) (root: string) (kind: WikiJobKind) (drafts: WikiDraft list) : JS.Promise<string> =
    withWikiPortLock root (fun () ->
        promise {
            let! entries = buildEntries root drafts
            match kind with
            | AppendAfterWork ->
                do! appendEntries root todayStr entries
                return $"Appended {entries.Length} wiki entries."
            | DailyRewrite date ->
                do! rewriteDay root date entries
                return $"Rewrote wiki day {date}."
            | WeeklyRewrite throughDate ->
                do! rewriteSnapshot root throughDate entries
                do! deleteDayFilesThrough root throughDate
                return $"Rewrote wiki snapshot through {throughDate}."
        })

/// Run the create+prompt IO for a background bookkeeper session. `applyCmd`
/// mutates the runtime state cell (RegisterJobCmd on create, RemoveJobCmd on
/// failure). Serialized by the caller inside the runtime commandQueue.
let launchBackgroundSession (session: obj) (root: string) (kind: WikiJobKind) (title: string) (promptText: string) (applyCmd: WikiCommand -> unit) (registry: ChildAgentRegistry) : JS.Promise<unit> =
    promise {
        let mutable sessionId = ""
        try
            let createBody =
                box {| query = box {| directory = root |}
                       body = box {| parentID = box null; title = title |} |}
            let! created = invoke1 session "create" createBody
            let childId = str (get created "data") "id"
            if childId <> "" then
                sessionId <- childId
                applyCmd (RegisterJobCmd (childId, { workspaceRoot = root; kind = kind }))
                registry.RegisterChildAgent(childId, "bookkeeper", None)
                let promptBody =
                    box {| path = box {| id = childId |}
                           body = box {| agent = "bookkeeper"
                                         parts = [| box {| ``type`` = "text"; text = promptText |} |]
                                         tools = box (createObj [ "submit_wiki", box true ]) |} |}
                do! invoke1 session "prompt" promptBody |> Promise.map ignore
        with _ ->
            if sessionId <> "" then
                registry.UnregisterChildAgent(sessionId)
                applyCmd (RemoveJobCmd sessionId)
    }

/// Fire-and-forget: build the prompt then launch the background session inside
/// the runtime commandQueue. When the job finishes (success or failure), clear
/// the optional scheduled-maintenance dedup key so the next cycle may retrigger.
/// `applyCmd` mutates the runtime state cell and is serialized with the rest of
/// the commandQueue work.
let queueBackgroundLaunch (client: obj) (commandQueue: SerialQueue) (applyCmd: WikiCommand -> unit) (root: string) (kind: WikiJobKind) (title: string) (buildPrompt: unit -> JS.Promise<string>) (maintenanceKey: string option) (registry: ChildAgentRegistry) : unit =
    match sessionApiOf client with
    | None -> ()
    | Some session ->
        commandQueue.Enqueue(fun () ->
            promise {
                try
                    let! promptText = buildPrompt ()
                    do! launchBackgroundSession session root kind title promptText applyCmd registry
                with _ -> ()
                maintenanceKey |> Option.iter (fun key -> applyCmd (CompleteLaunchCmd key))
            })
        |> Promise.start
