module VibeFs.Opencode.WikiRuntimeIO

open System
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.WikiRuntimeState
open VibeFs.Opencode.MessagingCodec
open VibeFs.Opencode.SessionIo
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.WikiFiles
open VibeFs.Shell.WikiPortLock
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Mux.AiSettings

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

let submitForKind (portLockTimeoutMs: int64) (portLockRetryDelayMs: int) (todayStr: string) (root: string) (kind: WikiJobKind) (drafts: WikiDraft list) : JS.Promise<string> =
    withWikiPortLock portLockTimeoutMs portLockRetryDelayMs root (fun () ->
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

let private jobMarkerPrompt (ctx: WikiJobContext) (promptText: string) : string =
    renderJobMarker ctx + "\n\n" + promptText

let private promptParts (ctx: WikiJobContext) (promptText: string) : obj array =
    [| box {| ``type`` = "text"; text = jobMarkerPrompt ctx promptText |} |]

let private launchResultText (title: string) (childId: string) : string =
    $"Started {title} in background session {childId}."

let private failedLaunchResult (title: string) (reason: string) : string =
    $"Failed to start {title}: {reason}"

let tryResolveJobContext (client: obj) (directory: string) (sessionID: string) : JS.Promise<WikiJobContext option> =
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

let launchBackgroundSession (session: obj) (root: string) (parentID: string option) (kind: WikiJobKind) (title: string) (promptText: string) (aiSettings: DelegatedAiSettings) (client: obj) (registry: ChildAgentRegistry) : JS.Promise<string> =
    promise {
        try
            let ctx = { workspaceRoot = root; kind = kind }
            let! childId =
                startSubagentSession registry client
                    { agent = "bookkeeper"
                      title = title
                      prompt = jobMarkerPrompt ctx promptText
                      directory = root
                      sessionID = defaultArg parentID ""
                      tools = null
                      aiSettings = aiSettings }
            return launchResultText title childId
        with ex ->
            return failedLaunchResult title (string ex)
    }

/// Fire-and-forget: build the prompt then launch the background session inside
/// the runtime commandQueue. `applyCmd` mutates the runtime state cell and is
/// serialized with the rest of the commandQueue work.
let queueBackgroundLaunch (client: obj) (commandQueue: SerialQueue) (recordResult: string -> unit) (root: string) (parentID: string option) (kind: WikiJobKind) (title: string) (buildPrompt: unit -> JS.Promise<string>) (aiSettings: DelegatedAiSettings) (registry: ChildAgentRegistry) : unit =
    match sessionApiOf client with
    | None -> recordResult (failedLaunchResult title "host client is missing session.create/session.prompt APIs")
    | Some session ->
        commandQueue.Enqueue(fun () ->
            promise {
                try
                    let! promptText = buildPrompt ()
                    let! result = launchBackgroundSession session root parentID kind title promptText aiSettings client registry
                    recordResult result
                with ex ->
                    recordResult (failedLaunchResult title (string ex))
            })
        |> Promise.start
