namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

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

    let private partsOf (raw: obj) : MessagePart array =
        if isNull raw || isNull raw?parts then
            [||]
        else
            try
                HostMessageCodec.decodeParts (unbox<obj array> raw?parts)
            with _ ->
                [||]

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
        let candidates =
            [ if not (isNull info) && not (isNull info?error) then
                  info?error?name
              else
                  null
              if not (isNull info) && not (isNull info?error) then
                  info?error?``type``
              else
                  null
              if not (isNull raw) && not (isNull raw?error) then
                  raw?error?name
              else
                  null
              if not (isNull raw) && not (isNull raw?error) then
                  raw?error?``type``
              else
                  null
              // Some host payloads put abort on the message root.
              if not (isNull info) then info?errorName else null
              if not (isNull raw) then raw?errorName else null ]

        candidates |> List.tryPick readString

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
        let field name =
            [ if isNull info then null else info?(name)
              if isNull raw then null else raw?(name) ]
            |> List.tryPick readString

        field "agent" = Some "compaction"
        || field "mode" = Some "compaction"
        || isTrue (if isNull info then null else info?summary)
        || isTrue (if isNull raw then null else raw?summary)

    /// PROMPT-011 anchor, wherever the Host kept it.
    ///
    /// `OpenCodePort` writes it onto the text part because that survives the Host
    /// round-trip more reliably than `body.metadata`; both spellings are read here so
    /// the recovery does not depend on which one a given Host version preserved.
    let private promptKeyOf (info: obj) (raw: obj) =
        let fromMetadata (source: obj) =
            if isNull source || isNull source?metadata then
                None
            else
                readString source?metadata?(PromptMetadataCodec.PromptKeyField)

        let fromParts () =
            if isNull raw || isNull raw?parts then
                None
            else
                try
                    unbox<obj array> raw?parts
                    |> Array.tryPick (fun part -> if isNull part then None else fromMetadata part)
                with _ ->
                    None

        [ fromMetadata info; fromMetadata raw ]
        |> List.tryPick id
        |> Option.orElseWith fromParts

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
                      Parts = partsOf raw }

    let projectMessages (rawMessages: obj array) =
        rawMessages |> Array.toList |> List.choose projectMessage

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

    type SdkSnapshotPort(client: obj, workspaceDirectory: string option) =
        let headersObj () =
            match workspaceDirectory with
            | Some dir -> createObj [ "x-opencode-directory", box dir ]
            | None -> createObj []

        interface ISessionSnapshotPort with
            member _.GetMessages(sessionId) =
                task {
                    try
                        let sessObj = client?session
                        let messagesFn = sessObj?messages

                        if isNull messagesFn then
                            return Error "session.messages unavailable on SDK client"
                        else
                            let sid = SessionId.value sessionId

                            let payload =
                                createObj
                                    [ "path", box (createObj [ "id", box sid ])
                                      "query",
                                      box (
                                          match workspaceDirectory with
                                          | Some dir -> createObj [ "directory", box dir ]
                                          | None -> createObj []
                                      )
                                      "headers", box (headersObj ()) ]

                            let! response = unbox<Task<obj>> (messagesFn?call (sessObj, payload))
                            return Ok(projectMessages (unwrapPayload response))
                    with ex ->
                        return Error ex.Message
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
                task {
                    try
                        let url = sprintf "%s/session/%s/message" cleanBase (SessionId.value sessionId)

                        let init = createObj [ "method", box "GET" ]
                        let! response = jsFetch url init
                        let status = unbox<int> response?status

                        if status < 200 || status >= 300 then
                            return Error(sprintf "HTTP %d" status)
                        else
                            let! text = unbox<Task<string>> (response?text ())

                            if String.IsNullOrWhiteSpace text then
                                return Ok []
                            else
                                return Ok(projectMessages (unwrapPayload (JS.JSON.parse text)))
                    with ex ->
                        return Error ex.Message
                }

    let create (input: obj) : ISessionSnapshotPort option =
        let workDir =
            if not (isNull input) && not (isNull input?directory) then
                let d = unbox<string> input?directory
                if String.IsNullOrWhiteSpace d then None else Some d
            else
                None

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
