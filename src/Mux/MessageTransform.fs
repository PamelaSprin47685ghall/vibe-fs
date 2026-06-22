module VibeFs.Mux.MessageTransform

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Config
open VibeFs.Kernel.LoopMessages
open VibeFs.Kernel.CapsFormat
open VibeFs.Kernel.MagicProjection
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.Wiki
open VibeFs.Kernel.WikiRuntimeState
open VibeFs.Mux.MagicTodo
open VibeFs.Mux.MessagingCodec
open VibeFs.Mux.ReadDedup
open VibeFs.Opencode.CapsPrelude
open VibeFs.Shell.FileSys
open VibeFs.Shell.WorkspaceFiles
open VibeFs.Shell.ReviewRuntime
open VibeFs.Mux.WikiTools

let private alwaysExcludedAgents = set [ "browser"; "investigator"; "executor"; "title"; "bookkeeper" ]
let private childWorkspaceExcludedAgents = set [ "exec"; "explore" ]

let private capsUserPrefix = "caps-synth-user-"
let private capsAssistantPrefix = "caps-synth-assistant-"

let private replaceArrayInPlace (target: obj array) (source: obj array) : unit =
    if System.Object.ReferenceEquals(target, source) then ()
    else
        let targetObj = box target
        targetObj?length <- 0
        for item in source do
            targetObj?push(item) |> ignore

let private messageId (msg: obj) : string =
    Dyn.str msg "id"

let private isPrefixed (prefix: string) (msg: obj) : bool =
    let id = messageId msg
    id <> "" && id.StartsWith prefix

let private hasExistingCapsMessages (messages: obj array) : bool =
    messages.Length > 0 && isPrefixed capsUserPrefix messages.[0]

let private stripExistingCapsMessages (messages: obj array) : obj array =
    if not (hasExistingCapsMessages messages) then messages
    else
        messages
        |> Array.skipWhile (fun msg ->
            let id = messageId msg
            id <> "" && id.StartsWith "caps-synth-")

let private extractTexts (messages: obj array) : string seq =
    messages
    |> Seq.collect (fun msg ->
        let parts = Dyn.get msg "parts"
        if Dyn.isNullish parts || not (Dyn.isArray parts) then Seq.empty
        else
            (parts :?> obj array)
            |> Seq.choose (fun part ->
                if Dyn.str part "type" = "text" then
                    let text = Dyn.str part "text"
                    if text <> "" then Some text else None
                elif Dyn.str part "type" = "dynamic-tool" then
                    let output = Dyn.get part "output"
                    if not (Dyn.isNullish output) then
                        let text = Dyn.str output "content"
                        if text <> "" then Some text else None
                    else None
                else None))

let private reconstructReviewState (reviewStore: ReviewStore) (sessionID: string) (messages: obj array) : unit =
    if sessionID = "" then ()
    else
        match reviewStore.getReviewState sessionID with
        | Some _ -> ()
        | None ->
            match inferReviewTaskFromTexts (extractTexts messages) with
            | Some task ->
                reviewStore.activateReview(sessionID, task, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            | None -> ()

let private magicSession = MagicSession()

let private buildTextPart (text: string) : obj =
    createObj [ "type", box "text"; "text", box text; "state", box "done" ]

let private buildMuxMessage (id: string) (role: string) (parts: obj array) : obj =
    createObj [ "id", box id; "role", box role; "parts", box parts ]

let private buildUserMessage (userId: string) (preludeText: string option) : obj =
    let text =
        match preludeText with
        | Some prelude when prelude.Trim() <> "" -> prelude.Trim() + "\n\n" + thinkWrapped
        | _ -> thinkWrapped
    buildMuxMessage userId "user" [| buildTextPart text |]

let private buildCapsAssistantMessage (id: string) (parentId: string) (capsFiles: CapsFile list) (fp: string) : obj =
    let parts =
        capsFiles
        |> List.mapi (fun index cap ->
            createObj
                [ "type", box "dynamic-tool"
                  "toolCallId", box $"caps-fr-{fp}-{index}"
                  "toolName", box "file_read"
                  "state", box "output-available"
                  "input", box (createObj [ "path", box cap.filePath ])
                  "output",
                  box
                      (createObj
                          [ "success", box true
                            "file_size", box cap.content.Length
                            "modifiedTime", box "1970-01-01T00:00:00.000Z"
                            "lines_read", box (cap.content.Split('\n').Length)
                            "content", box (formatReadOutput cap.filePath cap.content) ]) ])
        |> Array.ofList
    buildMuxMessage id "assistant" parts

let private findFirstRealMessage (messages: obj array) : obj option =
    messages
    |> Array.tryFind (fun msg ->
        let id = messageId msg
        id <> "" && not (id.StartsWith capsUserPrefix) && not (id.StartsWith capsAssistantPrefix))

let private buildCapsMessages
    (messages: obj array)
    (directory: string)
    (capsFiles: CapsFile list)
    (preludeText: string option)
    : obj array =
    match findFirstRealMessage messages with
    | None -> messages
    | Some _ ->
        let existingStripped = stripExistingCapsMessages messages
        if existingStripped.Length = 0 then messages
        else
            let fp = stableFingerprint sha256HexTruncated capsFiles
            let userId = $"{capsUserPrefix}{fp}"
            let assistantId = $"{capsAssistantPrefix}{fp}"
            let userMsg = buildUserMessage userId preludeText
            let assistantMsgs =
                if capsFiles.IsEmpty then [||]
                else [| buildCapsAssistantMessage assistantId userId capsFiles fp |]
            Array.concat [| [| userMsg |]; assistantMsgs; existingStripped |]

let private findWorkspaceEntry (deps: obj) (workspaceId: string) : obj =
    if Dyn.isNullish deps || workspaceId = "" then null
    else
        let loadConfig = Dyn.get deps "loadConfigOrDefault"
        let findEntry = Dyn.get deps "findWorkspaceEntry"
        if Dyn.isNullish loadConfig || Dyn.isNullish findEntry then null
        else
            try
                let configFile = loadConfig $ ()
                findEntry $ (configFile, workspaceId)
            with _ -> null

let private isChildWorkspace (deps: obj) (workspaceId: string) : bool =
    let entry = findWorkspaceEntry deps workspaceId
    if Dyn.isNullish entry then false
    else
        let workspace = Dyn.get entry "workspace"
        not (Dyn.isNullish workspace) && Dyn.str workspace "parentWorkspaceId" <> ""

let messagesTransform
    (deps: obj)
    (wikiRuntime: MuxWikiRuntime)
    (reviewStore: ReviewStore)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let messages = Dyn.get output "messages"
        if Dyn.isNullish messages || not (Dyn.isArray messages) then ()
        else
            let messagesArr = messages :?> obj array
            if messagesArr.Length = 0 then ()
            else
                let agent =
                    let explicit = Dyn.str input "agent"
                    if explicit <> "" then explicit else Dyn.str input "effectiveAgentId"
                let sessionID =
                    let explicit = Dyn.str input "sessionID"
                    if explicit <> "" then explicit else Dyn.str input "workspaceId"
                let directory =
                    let explicit = Dyn.str input "directory"
                    if explicit <> "" then explicit else Dyn.str input "workspacePath"
                reconstructReviewState reviewStore sessionID messagesArr
                let excluded =
                    Set.contains agent alwaysExcludedAgents
                    || (isChildWorkspace deps sessionID && Set.contains agent childWorkspaceExcludedAgents)
                let typedMessages = decodeMessages sessionID messagesArr
                let cleanedMessages = stripSyntheticBySource typedMessages
                let backlog = magicSession.GetOrRebuildBacklog(sessionID, cleanedMessages)
                let afterMagic =
                    if excluded then cleanedMessages
                    else projectMagicFor magicSession.Host cleanedMessages backlog false sessionID
                let encoded = encodeMessages afterMagic
                let deduped = if excluded then encoded else deduplicateReadOutputsWithSeenByPath Map.empty encoded
                let! capsFiles =
                    if excluded || directory = "" then Promise.lift ([]: CapsFile list)
                    else findCapsFiles directory
                let! wikiPrelude =
                    if not excluded && directory <> "" && canUse agent "fetch_wiki" then
                        wikiRuntime.BuildPreludeForSession(sessionID, directory)
                    else
                        Promise.lift (None: string option)
                let final = buildCapsMessages deduped directory capsFiles wikiPrelude
                replaceArrayInPlace messagesArr final
    }
