namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch.OpenCode

/// One message as the Host transcript has it.
///
/// `Id` is a raw wire address, deliberately untyped. SSOT has no generic message
/// identity: PROMPT-001 gives `role=user` a `PhysicalUserMessageId`, HOST-010
/// gives `role=assistant` a `ProviderRunIdentity`, and the two are not
/// interchangeable. A single typed id here would have to be one of them, so it
/// would be wrong for half the transcript.
///
/// The reconcile layer constructs the typed identity at the point where the role
/// is known. This is the Host-raw boundary the migration allows an adapter at.
[<RequireQualifiedAccess>]
type SnapshotToolPartState =
    | Pending
    | Completed of outputCanonical: string
    | Failed of errorCanonical: string

type SessionToolPart =
    { HostToolPartId: HostToolPartId
      ToolCallId: ToolCallId
      ToolName: string
      InputCanonical: string
      State: SnapshotToolPartState }

type SessionMessage =
    {
        Id: string
        Role: string
        Agent: string option
        Finish: string option
        ErrorName: string option
        Model: OpencodeModel option
        /// `parentID`. For an assistant message this is the user message that caused
        /// the provider run (HOST-010 binding, condition 3).
        ParentId: string option
        /// `time.completed` is set. The Host writes it only when the run ends or is
        /// interrupted, so an unset value at `messages.transform` time identifies the
        /// run about to be sent (REVIEW-010).
        Completed: bool
        /// The Host's own compaction pseudo-run, recognised by any of `agent`,
        /// `mode` = "compaction" or `summary` = true.
        ///
        /// Folded here rather than exposing three raw fields: "is this the compaction
        /// path" is one question, and its answer decides whether the seal binding is
        /// even attempted. Compaction triggers transform BEFORE creating its
        /// assistant message, so the binding cannot succeed there and must fail
        /// closed instead of guessing.
        IsCompaction: bool
        /// PROMPT-011 anchor: the `PromptKey` the Dispatcher wrote into Host metadata,
        /// when this message carries one. It is the only way an unresolved claim can be
        /// matched to a physical message after a restart.
        PromptKey: string option
        Parts: MessagePart array
        ToolParts: SessionToolPart array
    }

type ISessionSnapshotPort =
    abstract GetMessages: sessionId: SessionId -> Task<Result<SessionMessage list, string>>

module SessionSnapshotPort =

    let private readString (value: obj) =
        if isNull value then
            None
        else
            let text = unbox<string> value
            if String.IsNullOrWhiteSpace text then None else Some text

    let private infoOf (raw: obj) =
        if isNull raw then null
        elif not (isNull raw?info) then raw?info
        else raw

    let private decodePartsOrEmpty (parts: obj array) : MessagePart array =
        try
            HostMessageCodec.decodeParts parts
        with _ ->
            [||]

    let private partsOf (raw: obj) : MessagePart array =
        if isNull raw || isNull raw?parts then
            [||]
        else
            decodePartsOrEmpty (unbox<obj array> raw?parts)

    let private canonicalValue (value: obj) =
        if isNull value then
            "null"
        else
            CanonicalJson.canonicalJson value

    let private isToolKind (kind: string) =
        kind = "tool"
        || kind = "tool-call"
        || kind = "tool_call"
        || kind.StartsWith "tool-"

    /// Evidence → Decision: tool-part input wire fields.
    let private toolInputOf (part: obj) (state: obj) =
        if not (isNull state) && not (isNull state?input) then
            state?input
        elif not (isNull part?input) then
            part?input
        elif not (isNull part?args) then
            part?args
        else
            part?arguments

    let private completedOutputOf (state: obj) =
        if isNull state then null
        elif not (isNull state?output) then state?output
        else state?result

    let private failedErrorOf (state: obj) =
        if isNull state then null
        elif not (isNull state?error) then state?error
        elif not (isNull state?errorText) then state?errorText
        else state?output

    /// Evidence → Decision: Host tool-part status string → SnapshotToolPartState.
    let private toolStateOf (state: obj) (stateValue: string) =
        match stateValue with
        | "completed" -> SnapshotToolPartState.Completed(canonicalValue (completedOutputOf state))
        | "error" -> SnapshotToolPartState.Failed(canonicalValue (failedErrorOf state))
        | _ -> SnapshotToolPartState.Pending

    let private sessionToolPartOf (partId: string) (call: string) (name: string) (part: obj) (state: obj) =
        let stateValue =
            readString (if isNull state then null else state?status)
            |> Option.map (fun value -> value.ToLowerInvariant())
            |> Option.defaultValue "pending"

        { HostToolPartId = HostToolPartId.create partId
          ToolCallId = ToolCallId.create call
          ToolName = name
          InputCanonical = canonicalValue (toolInputOf part state)
          State = toolStateOf state stateValue }

    /// Evidence → Decision: named tool identity present → SessionToolPart.
    let private chooseNamedToolPart (part: obj) : SessionToolPart option =
        let state = if isNull part?state then null else part?state

        let callId =
            readString part?toolCallId
            |> Option.orElse (readString part?callID)
            |> Option.orElse (readString part?callId)

        let toolName = readString part?tool |> Option.orElse (readString part?name)
        let hostToolPartId = readString part?id |> Option.orElse callId

        match hostToolPartId, callId, toolName with
        | Some partId, Some call, Some name -> Some(sessionToolPartOf partId call name part state)
        | _ -> None

    /// Evidence → Decision: part kind is a tool wire form.
    let private chooseToolKindPart (part: obj) : SessionToolPart option =
        let kind =
            readString part?``type``
            |> Option.defaultValue ""
            |> fun value -> value.ToLowerInvariant()

        if not (isToolKind kind) then
            None
        else
            chooseNamedToolPart part

    let private trySessionToolPart (part: obj) : SessionToolPart option =
        if isNull part then None else chooseToolKindPart part

    let private decodeToolPartsOrEmpty (parts: obj array) : SessionToolPart array =
        try
            parts |> Array.choose trySessionToolPart
        with _ ->
            [||]

    let private toolPartsOf (raw: obj) : SessionToolPart array =
        if isNull raw || isNull raw?parts then
            [||]
        else
            decodeToolPartsOrEmpty (unbox<obj array> raw?parts)

    let private modelOf (info: obj) : OpencodeModel option =
        if isNull info then
            None
        elif not (isNull info?providerID) && not (isNull info?modelID) then
            Some
                { providerID = unbox<string> info?providerID
                  modelID = unbox<string> info?modelID
                  variant = None }
        elif not (isNull info?model) && not (isNull info?model?providerID) then
            Some
                { providerID = unbox<string> info?model?providerID
                  modelID = unbox<string> info?model?modelID
                  variant = None }
        else
            None

    let private errorNameOf (info: obj) (raw: obj) =
        // NOTE: this must stay plain sequential code. Fable miscompiles an
        // if-branch inside a list literal into a JS comma expression
        // `(value, [])` — the value is evaluated and discarded, so every
        // candidate reads as `None`. Measured on Host 1.18.9 error messages:
        // `info.error.name = "APIError"` decoded as `ErrorName = None`, which
        // turned a settled provider failure into `TurnUnknown`.
        let readError (error: obj) =
            readString (if isNull error then null else error?name)
            |> Option.orElseWith (fun () -> readString (if isNull error then null else error?``type``))

        readError (if isNull info then null else info?error)
        |> Option.orElseWith (fun () -> readError (if isNull raw then null else raw?error))
        |> Option.orElseWith (fun () -> readString (if isNull info then null else info?errorName))
        |> Option.orElseWith (fun () -> readString (if isNull raw then null else raw?errorName))

    /// `summary = true` in Host 1.18.9 is a boolean on assistant messages. User
    /// messages carry a summary OBJECT (`{ title?, body?, diffs }`), so a non-null
    /// test alone would treat every ordinary user message as a compaction
    /// pseudo-run — `judgeStartup` then refuses to start (HOST-006). Strict boolean
    /// equality is the only faithful reading of the SDK contract.
    let private isTrue (value: obj) =
        not (isNull value) && unbox<bool> value = true

    let private completedOf (info: obj) (raw: obj) =
        let timeOf (source: obj) =
            if isNull source || isNull source?time then
                null
            else
                source?time?completed

        not (isNull (timeOf info)) || not (isNull (timeOf raw))

    let private isCompactionOf (info: obj) (raw: obj) =
        // Same Fable constraint as `errorNameOf`: no if-branches inside list
        // literals (they compile to a discarded JS comma expression).
        let field name =
            match readString (if isNull info then null else info?(name)) with
            | Some value -> Some value
            | None -> readString (if isNull raw then null else raw?(name))

        field "agent" = Some "compaction"
        || field "mode" = Some "compaction"
        || isTrue (if isNull info then null else info?summary)
        || isTrue (if isNull raw then null else raw?summary)

    /// PROMPT-011 anchor, wherever the Host kept it.
    ///
    /// `OpenCodePort` writes it onto the text part because that survives the Host
    /// round-trip more reliably than `body.metadata`; both spellings are read here so
    /// the recovery does not depend on which one a given Host version preserved.
    let private promptKeyFromMetadata (source: obj) =
        if isNull source || isNull source?metadata then
            None
        else
            readString source?metadata?(PromptMetadataCodec.PromptKeyField)

    let private promptKeyFromPart (part: obj) =
        if isNull part then None else promptKeyFromMetadata part

    let private tryPickPromptKeyFromParts (parts: obj array) =
        try
            parts |> Array.tryPick promptKeyFromPart
        with _ ->
            None

    let private promptKeyFromParts (raw: obj) =
        if isNull raw || isNull raw?parts then
            None
        else
            tryPickPromptKeyFromParts (unbox<obj array> raw?parts)

    let private promptKeyOf (info: obj) (raw: obj) =
        promptKeyFromMetadata info
        |> Option.orElseWith (fun () -> promptKeyFromMetadata raw)
        |> Option.orElseWith (fun () -> promptKeyFromParts raw)

    let projectMessage (raw: obj) : SessionMessage option =
        if isNull raw then
            None
        else
            let info = infoOf raw

            match
                readString (if isNull info then null else info?id)
                |> Option.orElse (readString raw?id)
            with
            | None -> None
            | Some id ->
                let role =
                    readString (if isNull info then null else info?role)
                    |> Option.orElse (readString raw?role)
                    |> Option.defaultValue ""
                    |> fun value -> value.ToLowerInvariant()

                let agent =
                    readString (if isNull info then null else info?agent)
                    |> Option.orElse (readString raw?agent)

                let finish =
                    readString (if isNull info then null else info?finish)
                    |> Option.orElse (readString raw?finish)
                    |> Option.orElse (readString raw?finishReason)

                Some
                    { Id = id
                      Role = role
                      Agent = agent
                      Finish = finish
                      ErrorName = errorNameOf info raw
                      Model = modelOf info
                      ParentId =
                        readString (if isNull info then null else info?parentID)
                        |> Option.orElse (readString raw?parentID)
                      Completed = completedOf info raw
                      IsCompaction = isCompactionOf info raw
                      PromptKey = promptKeyOf info raw
                      Parts = partsOf raw
                      ToolParts = toolPartsOf raw }

    let projectMessages (rawMessages: obj array) =
        rawMessages |> Array.toList |> List.choose projectMessage

    type ToolCallLocation =
        { ProviderRun: ProviderRunIdentity
          HostToolPartId: HostToolPartId
          ToolCallId: ToolCallId
          ToolName: string
          InputCanonical: string
          State: SnapshotToolPartState }

    [<RequireQualifiedAccess>]
    type ToolCallLocationError =
        | Missing of toolCallId: ToolCallId
        | Ambiguous of toolCallId: ToolCallId

    /// Evidence → Decision: tool part id matches the callback → location.
    let private toolCallLocationOf
        (toolCallId: ToolCallId)
        (message: SessionMessage)
        (part: SessionToolPart)
        : ToolCallLocation option =
        if part.ToolCallId <> toolCallId then
            None
        else
            Some
                { ProviderRun = ProviderRunIdentity.create message.Id
                  HostToolPartId = part.HostToolPartId
                  ToolCallId = part.ToolCallId
                  ToolName = part.ToolName
                  InputCanonical = part.InputCanonical
                  State = part.State }

    let private assistantToolLocations (toolCallId: ToolCallId) (message: SessionMessage) =
        if message.Role <> "assistant" then
            []
        else
            message.ToolParts
            |> Array.toList
            |> List.choose (toolCallLocationOf toolCallId message)

    /// Resolve one tool callback through the Host's persisted assistant message.
    /// `callID` alone is not a ProviderRun binding; the enclosing assistant
    /// message and persisted ToolPart are the only admissible evidence.
    let locateToolCall
        (toolCallId: ToolCallId)
        (messages: SessionMessage list)
        : Result<ToolCallLocation, ToolCallLocationError> =
        let candidates = messages |> List.collect (assistantToolLocations toolCallId)

        match candidates with
        | [ location ] -> Ok location
        | [] -> Error(ToolCallLocationError.Missing toolCallId)
        | _ -> Error(ToolCallLocationError.Ambiguous toolCallId)

    [<Emit("Array.isArray($0)")>]
    let private isJsArray (value: obj) : bool = jsNative

    let private unwrapPayload (response: obj) : obj array =
        if isNull response then
            [||]
        elif isJsArray response then
            unbox<obj array> response
        elif not (isNull response?data) && isJsArray response?data then
            unbox<obj array> response?data
        elif
            not (isNull response?data)
            && not (isNull response?data?data)
            && isJsArray response?data?data
        then
            unbox<obj array> response?data?data
        else
            [||]

    /// HOST 1.18.9 `session.messages` defaults to `order = "desc"` unless pinned.
    /// Evidence: `packages/core/src/session.ts:308` (`requestedOrder = input.order ?? "desc"`).
    let private messagesQuery (workspaceDirectory: string option) =
        match workspaceDirectory with
        | Some dir -> createObj [ "directory", box dir; "order", box "asc" ]
        | None -> createObj [ "order", box "asc" ]

    let private messagesFromHttpText (text: string) =
        if String.IsNullOrWhiteSpace text then
            []
        else
            projectMessages (unwrapPayload (JS.JSON.parse text))

    let private nonBlankDirectory (directory: string) =
        if String.IsNullOrWhiteSpace directory then
            None
        else
            Some directory

    let private directoryOption (input: obj) =
        if isNull input || isNull input?directory then
            None
        else
            nonBlankDirectory (unbox<string> input?directory)

    type SdkSnapshotPort(client: obj, workspaceDirectory: string option) =
        let headersObj () =
            match workspaceDirectory with
            | Some dir -> createObj [ "x-opencode-directory", box dir ]
            | None -> createObj []

        interface ISessionSnapshotPort with
            member _.GetMessages(sessionId) =
                taskResult {
                    try
                        let sessObj = client?session
                        let messagesFn = sessObj?messages
                        do! Result.requireFalse "session.messages unavailable on SDK client" (isNull messagesFn)
                        let sid = SessionId.value sessionId

                        let payload =
                            createObj
                                [ "path", box (createObj [ "id", box sid ])
                                  "query", box (messagesQuery workspaceDirectory)
                                  "headers", box (headersObj ()) ]

                        let! response = unbox<Task<obj>> (messagesFn?call (sessObj, payload)) |> TaskResultCE.ofTask

                        return projectMessages (unwrapPayload response)
                    with ex ->
                        return! Error ex.Message
                }

    type HttpSnapshotPort(baseUrl: string) =
        let cleanBase =
            if baseUrl.EndsWith("/") then
                baseUrl.Substring(0, baseUrl.Length - 1)
            else
                baseUrl

        [<Emit("fetch($0, $1)")>]
        let jsFetch (url: string) (init: obj) : Task<obj> = jsNative

        interface ISessionSnapshotPort with
            member _.GetMessages(sessionId) =
                taskResult {
                    try
                        let url =
                            sprintf "%s/session/%s/message?order=asc" cleanBase (SessionId.value sessionId)

                        let init = createObj [ "method", box "GET" ]
                        let! response = jsFetch url init |> TaskResultCE.ofTask
                        let status = unbox<int> response?status
                        do! Result.requireTrue (sprintf "HTTP %d" status) (status >= 200 && status < 300)
                        let! text = unbox<Task<string>> (response?text ()) |> TaskResultCE.ofTask
                        return messagesFromHttpText text
                    with ex ->
                        return! Error ex.Message
                }

    let create (input: obj) : ISessionSnapshotPort option =
        let workDir = directoryOption input

        if isNull input then
            None
        elif not (isNull input?client) && not (isNull input?client?session) then
            Some(SdkSnapshotPort(input?client, workDir) :> ISessionSnapshotPort)
        elif not (isNull input?serverUrl) then
            Some(HttpSnapshotPort(unbox<string> input?serverUrl) :> ISessionSnapshotPort)
        elif not (isNull input?baseUrl) then
            Some(HttpSnapshotPort(unbox<string> input?baseUrl) :> ISessionSnapshotPort)
        elif not (isNull input?port) then
            Some(HttpSnapshotPort(sprintf "http://127.0.0.1:%d" (unbox<int> input?port)) :> ISessionSnapshotPort)
        else
            None
