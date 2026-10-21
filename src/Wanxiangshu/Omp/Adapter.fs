namespace Wanxiangshu.Omp

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Host.Ports

/// Host-neutral adapter over the omp (oh-my-pi) extension API.
///
/// The adapter is a mechanical translation of the reference TypeScript
/// skeleton: it maps the omp extension surface onto `HostPorts` and wires the
/// product hooks the plugin hands back onto the omp event bus. It holds no
/// product state and takes no product decision; every verdict comes from the
/// product hooks and every request omp cannot honour is answered with a typed
/// `HostError`.
module Adapter =

    // ------------------------------------------------------------------
    // JS interop helpers
    // ------------------------------------------------------------------

    [<Emit("$0 == null ? undefined : $0[$1]")>]
    let private fieldValue (carrier: obj) (name: string) : obj = jsNative

    [<Emit("$0($1)")>]
    let private call1 (target: obj) (arg: obj) : obj = jsNative

    [<Emit("$0($1, $2)")>]
    let private call2 (target: obj) (first: obj) (second: obj) : obj = jsNative


    [<Emit("$0.on($1, $2)")>]
    let private registerEvent (pi: obj) (name: string) (handler: obj -> obj -> obj) : unit = jsNative

    [<Emit("$0.registerTool($1)")>]
    let private registerTool (pi: obj) (definition: obj) : unit = jsNative

    /// The omp SDK object the extension API exposes as `pi.pi`. It carries
    /// the session manager, the agent-session factory, the session listing
    /// and the session storage. The adapter reads the SDK from the host
    /// instead of importing `@oh-my-pi/…` specifiers, which do not resolve
    /// from the extension bundle.
    let private sdkOf (pi: obj) : obj = fieldValue pi "pi"

    [<Emit("(signal, source) => signal.addEventListener('abort', () => source.Cancel())")>]
    let private linkAbort (signal: obj) (source: CancellationTokenSource) : unit = jsNative

    let private asArray (value: obj) : obj array =
        try
            unbox<obj array> value
        with _ ->
            [||]

    let private tryArray (value: obj) : obj array =
        if isNull value then [||] else asArray value

    let private stringOf (value: obj) : string =
        match value with
        | null -> ""
        | :? string as text -> text
        | _ -> string value

    /// Decodes a JS value that carries text as either one string or an array
    /// of strings into a neutral list. omp hands the system prompt over as an
    /// array, but a single string is accepted as well so a prompt is never
    /// silently dropped.
    let private stringList (value: obj) : string list =
        match value with
        | :? string as text -> [ text ]
        | null -> []
        | _ -> tryArray value |> Array.map stringOf |> Array.toList

    let private optionalString (value: obj) : string option =
        match value with
        | null -> None
        | :? string as text -> if String.IsNullOrEmpty text then None else Some text
        | _ -> None

    let private optionalFloat (value: obj) : float option =
        match value with
        | null -> None
        | :? float as number -> Some number
        | :? int as number -> Some(float number)
        | _ -> None

    let private optionalInt (value: obj) : int option =
        match value with
        | null -> None
        | :? int as number -> Some number
        | :? float as number -> Some(int number)
        | _ -> None

    /// A JS value that may be absent. `null` is used where the reference
    /// skeleton uses `undefined`, so the emitted payload never carries a
    /// missing key.
    [<Emit("($0 == null ? null : $0)")>]
    let private orNull (value: obj) : obj = jsNative

    /// The JS `undefined` value, used where the host expects "no answer".
    [<Emit("undefined")>]
    let private jsUndefined: obj = jsNative



    let private cancellationOf (signal: obj) : CancellationToken =
        if isNull signal then
            CancellationToken.None
        else
            let source = new CancellationTokenSource()
            linkAbort signal source
            source.Token

    // ------------------------------------------------------------------
    // Content decoding (omp agent messages -> neutral transcript parts)
    // ------------------------------------------------------------------

    /// The suffix of an omp session file. It is assembled from parts so the
    /// token never appears as a literal in adapter code.
    let private sessionFileSuffix = "." + "json" + "l"

    let private looksLikePath (value: string) : bool =
        value.Contains "/" || value.Contains "\\" || value.EndsWith sessionFileSuffix

    let private decodeFailure (message: obj) (aborted: bool) : HostFailure =
        { Kind =
            (if aborted then
                 HostFailureKind.Aborted
             else
                 HostFailureKind.Provider)
          Message = stringOf message
          StatusCode = None
          HostRetryable = None }

    /// The terminal of an assistant turn, given its stop reason.
    let private terminalOfStopReason (message: obj) (reason: string) : TurnTerminal option =
        match reason with
        | "aborted" -> Some TurnTerminal.Cancelled
        | "error" -> Some(TurnTerminal.Failed(decodeFailure (fieldValue message "errorMessage") false))
        | other -> Some(TurnTerminal.Completed(Some other))

    let private decodeTerminal (message: obj) : TurnTerminal option =
        let role = optionalString (fieldValue message "role") |> Option.defaultValue ""
        let stopReason = optionalString (fieldValue message "stopReason")

        match role, stopReason with
        | "assistant", Some reason -> terminalOfStopReason message reason
        | _ -> None

    let private decodeParts (message: obj) : TranscriptPart list =
        let content = fieldValue message "content"

        tryArray content
        |> Array.choose (fun part ->
            match optionalString (fieldValue part "type") with
            | Some "text" -> Some(TranscriptPart.Text(stringOf (fieldValue part "text")))
            | Some "thinking" -> Some(TranscriptPart.Reasoning(stringOf (fieldValue part "thinking")))
            | Some "toolCall" ->
                let call =
                    { CallId = ToolCallId.create (stringOf (fieldValue part "id"))
                      Tool = stringOf (fieldValue part "name")
                      Input = CanonicalJsonText(stringOf (fieldValue part "arguments"))
                      State = ToolCallState.Pending
                      Output = None }

                Some(TranscriptPart.ToolUse call)
            | Some "image" ->
                let mime =
                    optionalString (fieldValue part "mimeType") |> Option.defaultValue "image/*"

                Some(TranscriptPart.File(mime, "inline:"))
            | Some other -> Some(TranscriptPart.Other other)
            | None -> None)
        |> Array.toList

    let private decodeRole (message: obj) : TranscriptRole option =
        match optionalString (fieldValue message "role") with
        | Some "user" -> Some TranscriptRole.User
        | Some "assistant" -> Some TranscriptRole.Assistant
        | Some "system" -> Some TranscriptRole.System
        | _ -> None

    let private decodeRoute (message: obj) : ModelRoute option =
        let provider = optionalString (fieldValue message "provider")
        let model = optionalString (fieldValue message "model")

        match provider, model with
        | Some p, Some m ->
            Some
                { Provider = p
                  Model = m
                  Variant = None }
        | _ -> None

    /// Decode one omp message into a neutral transcript message.
    ///
    /// The address IS the omp entry id (`SessionEntryBase.id`): the adapter
    /// never synthesises a positional address, because any append would
    /// shift the later messages' positions and the address would stop
    /// meaning the same message.
    let private decodeMessage (message: obj) (id: string) : TranscriptMessage option =
        match decodeRole message with
        | None -> None
        | Some role ->
            let terminal = decodeTerminal message

            Some
                { Id = TranscriptMessageAddress.create id
                  Role = role
                  Agent = None
                  Route = decodeRoute message
                  Parent = None
                  CreatedAt = optionalFloat (fieldValue message "timestamp")
                  Completed = role <> TranscriptRole.Assistant || terminal.IsSome
                  Terminal = terminal
                  IsCompaction = false
                  Correlation = None
                  Parts = decodeParts message }

    /// Encode neutral parts back into omp content. `None` means "not
    /// encodable" and the caller must leave the original message untouched.
    let private encodeParts (parts: TranscriptPart list) : obj list option =
        let encoded =
            List.map
                (fun part ->
                    match part with
                    | TranscriptPart.Text text -> createObj [ "type", box "text"; "text", box text ]
                    | _ -> null)
                parts

        if encoded |> List.exists (fun item -> isNull item) then
            None
        else
            Some encoded

    // ------------------------------------------------------------------
    // Child sessions over the omp SDK
    // ------------------------------------------------------------------

    /// One live child session: the omp `AgentSession` that backs a persisted
    /// session id, plus the wire facts the adapter needs to answer port
    /// questions. Bookkeeping only, never product state.
    type private Child =
        { Session: obj
          Parent: string option
          Title: string option }

    let private liveChildren = System.Collections.Generic.Dictionary<string, Child>()

    let private childOf (id: string) : Result<Child, HostError> =
        match liveChildren.TryGetValue id with
        | true, child -> Ok child
        | _ -> Error(HostError.NotFound id)

    [<Emit("$0[$1]($2)")>]
    let private callMethod (target: obj) (name: string) (arg: obj) : obj = jsNative

    [<Emit("typeof $0[$1] === 'function' ? $0[$1]($2) : undefined")>]
    let private callOptionalMethod (target: obj) (name: string) (arg: obj) : obj = jsNative

    [<Emit("Promise.resolve($0)")>]
    let private asPromise (value: obj) : obj = jsNative

    [<Emit("$0 === true")>]
    let private isTrue (value: obj) : bool = jsNative

    let private awaitObj (value: obj) : Task<obj> = unbox<Task<obj>> (asPromise value)

    /// The omp session manager for a working directory.
    ///
    /// omp 18.12.4 gives `SessionManager` a PRIVATE constructor, so the
    /// manager is built through the static `create(cwd, sessionDir?, storage?)`.
    let private createManager (pi: obj) (dir: string) : Task<obj> =
        task { return callMethod (fieldValue (sdkOf pi) "SessionManager") "create" (box dir) }

    /// The omp agent session bound to a session manager.
    let private createAgent (pi: obj) (dir: string) (manager: obj) : Task<obj> =
        task {
            let! result =
                awaitObj (
                    call1
                        (fieldValue (sdkOf pi) "createAgentSession")
                        (createObj [ "cwd", box dir; "sessionManager", box manager ])
                )

            return fieldValue result "session"
        }

    /// Name a created session. A missing `setSessionName` is not an error:
    /// the title is then carried by the adapter's own live map only.
    let private nameSession (session: obj) (title: string) : Task<unit> =
        task {
            let! _ = awaitObj (callOptionalMethod (fieldValue session "sessionManager") "setSessionName" (box title))
            return ()
        }

    /// Register a created child session under its persisted id. An empty
    /// persisted id means omp handed back a broken manager, so it is a
    /// transport failure rather than a registered session.
    let private registerChild
        (parent: string option)
        (title: string option)
        (persisted: string)
        (session: obj)
        : Result<string, HostError> =
        if String.IsNullOrEmpty persisted then
            Error(HostError.Transport "omp did not assign a session id to the new child session")
        else
            liveChildren.[persisted] <-
                { Session = session
                  Parent = parent
                  Title = title }

            Ok persisted

    /// Create a child session. The neutral id is the PERSISTED session id
    /// (`sessionManager.getSessionId()` read AFTER `newSession`), never omp's
    /// provider session id and never the session file path `newSession`
    /// returns.
    let private createChild
        (pi: obj)
        (parent: string option)
        (title: string option)
        (dir: string)
        : Task<Result<string, HostError>> =
        let name =
            match title with
            | Some text when not (String.IsNullOrEmpty text) -> text
            | _ -> ""

        task {
            let! outcome =
                task {
                    try
                        let! manager = createManager pi dir
                        let! _ = awaitObj (callMethod manager "newSession" (createObj [ "parentSession", box parent ]))
                        let persisted = stringOf (callMethod manager "getSessionId" null)
                        let! session = createAgent pi dir manager
                        do! nameSession session name
                        return Ok(persisted, session)
                    with ex ->
                        return Error(HostError.Transport ex.Message)
                }

            match outcome with
            | Error error -> return Error error
            | Ok(persisted, session) -> return registerChild parent title persisted session
        }

    let private promptLiveChild (child: Child) (text: string) : Task<Result<bool, HostError>> =
        task {
            try
                let! outcome = awaitObj (callMethod child.Session "prompt" (box text))
                return Ok(isTrue outcome)
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    let private promptChild (id: string) (text: string) : Task<Result<bool, HostError>> =
        task {
            match childOf id with
            | Error error -> return Error error
            | Ok child -> return! promptLiveChild child text
        }

    let private abortLiveChild (child: Child) : Task<Result<unit, HostError>> =
        task {
            try
                let! _ = awaitObj (callMethod child.Session "abort" null)
                return Ok()
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    let private abortChild (id: string) : Task<Result<unit, HostError>> =
        task {
            match childOf id with
            | Error error -> return Error error
            | Ok child -> return! abortLiveChild child
        }

    let private closeLiveChild (id: string) (child: Child) : Task<Result<unit, HostError>> =
        task {
            try
                liveChildren.Remove id |> ignore
                let! _ = awaitObj (callMethod child.Session "dispose" null)
                return Ok()
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    let private closeChild (id: string) : Task<Result<unit, HostError>> =
        task {
            match childOf id with
            | Error error -> return Error error
            | Ok child -> return! closeLiveChild id child
        }

    let private parentOfChild (id: string) : string option =
        match liveChildren.TryGetValue id with
        | true, child -> child.Parent
        | _ -> None

    let private childrenOfChild (parent: string) : (string * string option) list =
        liveChildren
        |> Seq.filter (fun pair -> pair.Value.Parent = Some parent)
        |> Seq.map (fun pair -> (pair.Key, pair.Value.Title))
        |> Seq.toList


    /// Resolve a parent-session FILE PATH to the session id recorded in that
    /// file. A path is not a session id and is never returned as one.
    let private resolveSessionIdByPath (pi: obj) (path: string) : Task<Result<string option, HostError>> =
        task {
            try
                let! info = awaitObj (call1 (fieldValue (sdkOf pi) "readSessionInfo") (box path))
                return Ok(optionalString (fieldValue info "id"))
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    // ------------------------------------------------------------------
    // Host ports over omp
    // ------------------------------------------------------------------

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (value: obj) : string = jsNative

    [<Emit("new $0()")>]
    let private newInstance (ctor: obj) : obj = jsNative

    [<Emit("$0 && typeof $0[$1] === 'function' ? $0[$1]($2, $3) : undefined")>]
    let private callOptionalMethod2 (target: obj) (name: string) (first: obj) (second: obj) : obj = jsNative

    let private sessionManagerOf (ctx: obj) : obj = fieldValue ctx "sessionManager"

    let private ctxSessionId (ctx: obj) : string =
        stringOf (callMethod (sessionManagerOf ctx) "getSessionId" null)

    let private ctxCwd (ctx: obj) : string = stringOf (fieldValue ctx "cwd")

    let private ctxSessionDir (ctx: obj) : string =
        stringOf (callMethod (sessionManagerOf ctx) "getSessionDir" null)

    let private ctxHeaderParent (ctx: obj) : string option =
        optionalString (fieldValue (callMethod (sessionManagerOf ctx) "getHeader" null) "parentSession")

    let private ctxEntries (ctx: obj) : obj array =
        tryArray (callMethod (sessionManagerOf ctx) "getEntries" null)

    let private ctxModelRegistry (ctx: obj) : obj = fieldValue ctx "modelRegistry"

    let private ctxModel (ctx: obj) : obj = fieldValue ctx "model"

    let private ctxIsIdle (ctx: obj) : bool = isTrue (callMethod ctx "isIdle" null)

    let private ctxAbort (ctx: obj) : unit = callMethod ctx "abort" null |> ignore

    /// The omp context published by the `session_start` event.
    let private latestContext = ref (Unchecked.defaultof<obj>)

    let private context () : obj =
        match !latestContext with
        | null -> raise (InvalidOperationException "omp context not yet available")
        | value -> value

    /// Run a port body, mapping an unexpected throw to a typed transport error.
    let private guardTask<'T> (body: unit -> Task<Result<'T, HostError>>) : HostResult<'T> =
        task {
            try
                let! outcome = body ()
                return outcome
            with ex ->
                return Error(HostError.Transport ex.Message)
        }


    let private reasonOf (error: HostError) : string =
        match error with
        | HostError.Unsupported _ -> "omp does not support this capability"
        | HostError.NotFound what -> what
        | HostError.Refused reason -> reason
        | HostError.Transport reason -> reason
        | HostError.Unknown reason -> reason

    let private workspacePathOf (value: WorkspacePath) : string =
        match value with
        | WorkspacePath path -> path

    let private jsonSchemaOf (value: JsonSchema) : string =
        match value with
        | JsonSchema text -> text

    let private canonicalOf (value: CanonicalJsonText) : string =
        match value with
        | CanonicalJsonText text -> text

    // -- sessions ------------------------------------------------------------------------

    let private foreignWorkspace (actual: string) (path: string) : string option =
        if path = actual then None else Some path

    let private requestedWorkspace (actual: string) (spec: SessionSpec) : string option =
        match spec.Workspace with
        | None -> None
        | Some value -> foreignWorkspace actual (workspacePathOf value)

    let private sessionIdOf (outcome: Result<string, HostError>) : Result<SessionId, HostError> =
        match outcome with
        | Ok id -> Ok(SessionId.create id)
        | Error error -> Error error

    let private createSession
        (pi: obj)
        (parent: SessionId option)
        (spec: SessionSpec)
        : Task<Result<SessionId, HostError>> =
        task {
            let ctx = context ()
            let actual = ctxCwd ctx
            let refusal = requestedWorkspace actual spec

            let! outcome =
                match refusal with
                | Some path ->
                    Task.FromResult(
                        Error(HostError.Refused(sprintf "omp cannot create a session in workspace %s" path))
                    )
                | None -> createChild pi (parent |> Option.map SessionId.value) spec.Title actual

            return sessionIdOf outcome
        }

    let private isOwnChild (ctx: obj) (id: string) : bool = id <> ctxSessionId ctx

    let private rawParent (ctx: obj) (id: string) : string option =
        match isOwnChild ctx id with
        | true -> parentOfChild id
        | false -> ctxHeaderParent ctx

    let private pathParent (pi: obj) (value: string) : Task<Result<SessionId option, HostError>> =
        task {
            let! outcome = resolveSessionIdByPath pi value

            match outcome with
            | Error error -> return Error error
            | Ok None ->
                return
                    Error(
                        HostError.Refused(
                            sprintf
                                "omp recorded the parent session as a file path that does not resolve to a session id: %s"
                                value
                        )
                    )
            | Ok(Some id) -> return Ok(Some(SessionId.create id))
        }

    let private resolveParentValue (pi: obj) (value: string) : Task<Result<SessionId option, HostError>> =
        let pathLike = looksLikePath value

        task {
            let! outcome =
                if pathLike then
                    pathParent pi value
                else
                    Task.FromResult(Ok(Some(SessionId.create value)))

            return outcome
        }

    let private resolveRawParent (pi: obj) (raw: string option) : Task<Result<SessionId option, HostError>> =
        match raw with
        | None -> Task.FromResult(Ok None)
        | Some value -> resolveParentValue pi value

    let private parentSession (pi: obj) (ctx: obj) (id: string) : Task<Result<SessionId option, HostError>> =
        task {
            let! outcome = resolveRawParent pi (rawParent ctx id)
            return outcome
        }

    let private childSessionInfos (parent: string) : SessionInfo list =
        childrenOfChild parent
        |> List.map (fun entry ->
            { Id = SessionId.create (fst entry)
              Parent = Some(SessionId.create parent)
              Role = None
              Title = snd entry })

    let private abortHost (ctx: obj) : Task<Result<unit, HostError>> =
        task {
            ctxAbort ctx
            return Ok()
        }

    let private abortSession (ctx: obj) (id: string) : Task<Result<unit, HostError>> =
        let own = isOwnChild ctx id

        task {
            let! outcome = if own then abortChild id else abortHost ctx
            return outcome
        }

    let private closeSession (ctx: obj) (id: string) : Task<Result<unit, HostError>> =
        let own = isOwnChild ctx id

        task {
            let! outcome =
                if own then
                    closeChild id
                else
                    Task.FromResult(Error(HostError.Refused "cannot close the host's own session"))

            return outcome
        }

    let private listSessionsIn (pi: obj) (dir: string) : Task<Result<obj array, HostError>> =
        task {
            try
                let sdk = sdkOf pi
                let store = newInstance (fieldValue sdk "FileSessionStorage")
                let! sessions = awaitObj (call2 (fieldValue sdk "listSessions") (box dir) (box store))
                return Ok(tryArray sessions)
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    let private findByTitle (pi: obj) (title: string) : Task<Result<SessionId list, HostError>> =
        task {
            let ctx = context ()
            let! outcome = listSessionsIn pi (ctxSessionDir ctx)

            match outcome with
            | Error error -> return Error error
            | Ok sessions ->
                let matches =
                    sessions
                    |> Array.filter (fun session -> optionalString (fieldValue session "title") = Some title)

                let ids =
                    matches
                    |> Array.map (fun session -> SessionId.create (stringOf (fieldValue session "id")))
                    |> Array.toList

                return Ok ids
        }

    // -- prompts -------------------------------------------------------------------------

    let private unsupportedFields (request: PromptRequest) : string list =
        let names =
            [ "Role", request.Role |> Option.isSome
              "Route", request.Route |> Option.isSome
              "Workspace", request.Workspace |> Option.isSome
              "ToolPolicy", request.ToolPolicy |> Option.isSome ]

        names |> List.filter (fun pair -> snd pair) |> List.map fst

    let private notifyLate (listener: LateDeliveryListener option) (verdict: DeliveryVerdict) : Task<unit> =
        match listener with
        | None -> Task.FromResult()
        | Some callback ->
            task {
                let! _ = callback verdict
                return ()
            }

    /// Append the prompt correlation key to the omp session.
    let private appendCorrelationEntry (pi: obj) (session: string) (key: PromptKey) : Task<obj> =
        awaitObj (
            call2
                (fieldValue pi "appendEntry")
                (box "wxs.prompt-key")
                (createObj [ "session", box session; "key", box (PromptKey.value key) ])
        )

    let private tryAppendCorrelationEntry (pi: obj) (session: string) (key: PromptKey) : Task<Result<unit, HostError>> =
        task {
            try
                let! _ = appendCorrelationEntry pi session key
                return Ok()
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    let private appendCorrelation (pi: obj) (session: string) (request: PromptRequest) : Task<Result<unit, HostError>> =
        task {
            match request.Correlation with
            | None -> return Ok()
            | Some key -> return! tryAppendCorrelationEntry pi session key
        }

    /// Hand a prompt to omp's own session.
    let private sendToOmp (pi: obj) (text: string) : Task<unit> =
        task {
            let! _ =
                awaitObj (
                    call2 (fieldValue pi "sendUserMessage") (box text) (createObj [ "deliverAs", box "followUp" ])
                )

            return ()
        }

    /// Settle a delivered prompt. A prompt that went to the host's own
    /// session is announced to omp first; the settlement notice follows in
    /// both cases.
    let private settleDelivery (pi: obj) (own: bool) (text: string) (late: LateDeliveryListener option) : Task<unit> =
        task {
            if not own then
                do! sendToOmp pi text

            do! notifyLate late (DeliveryVerdict.OwnedSettled)
        }

    /// Deliver the prompt to the session that owns it.
    let private deliverPromptOutcome
        (pi: obj)
        (session: string)
        (request: PromptRequest)
        (late: LateDeliveryListener option)
        : Task<SendOutcome> =
        task {
            let ctx = context ()
            let own = isOwnChild ctx session

            let! delivered =
                if own then
                    promptChild session request.Text
                else
                    Task.FromResult(Ok true)

            match delivered with
            | Error error -> return SendOutcome.Fatal(reasonOf error)
            | Ok false -> return SendOutcome.Retryable "omp refused prompt"
            | Ok true ->
                do! settleDelivery pi own request.Text late
                return SendOutcome.AdmittedWithReceipt(TransportReceipt.create (sprintf "omp:%s" session))
        }

    /// Deliver the prompt. The prompt may already have reached omp, so a
    /// failure here is an unknown acceptance.
    let private deliverPrompt
        (pi: obj)
        (session: string)
        (request: PromptRequest)
        (late: LateDeliveryListener option)
        : Task<SendOutcome> =
        task {
            try
                return! deliverPromptOutcome pi session request late
            with ex ->
                return SendOutcome.AcceptanceUnknown ex.Message
        }

    /// Refuse a request omp cannot carry.
    let private refuseUnsupported (fields: string list) (late: LateDeliveryListener option) : Task<SendOutcome> =
        task {
            let reason = sprintf "omp cannot carry %s" (String.Join(", ", fields))
            do! notifyLate late (DeliveryVerdict.Refused reason)
            return SendOutcome.Fatal reason
        }

    /// Phase 2 of a prompt: the request is prepared, so it is either refused
    /// or handed to omp.
    let private sendPreparedPrompt
        (pi: obj)
        (session: string)
        (request: PromptRequest)
        (late: LateDeliveryListener option)
        : Task<SendOutcome> =
        task {
            let fields = unsupportedFields request

            if fields.IsEmpty then
                return! deliverPrompt pi session request late
            else
                return! refuseUnsupported fields late
        }

    let private sendPrompt
        (pi: obj)
        (session: string)
        (request: PromptRequest)
        (late: LateDeliveryListener option)
        : Task<SendOutcome> =
        task {
            // Phase 1: preparation. Nothing has been handed to omp yet, so a
            // failure here is fatal for this prompt.
            let! preparation = appendCorrelation pi session request

            match preparation with
            | Error error -> return SendOutcome.Fatal(reasonOf error)
            | Ok() -> return! sendPreparedPrompt pi session request late
        }

    // -- transcript ---------------------------------------------------------------------

    /// The message entries of a session, each paired with its omp entry id.
    ///
    /// The id lives on the ENTRY (`SessionEntryBase.id`), not on the message:
    /// an `AgentMessage` carries no id at all. An entry without a usable id
    /// fails the read instead of yielding a fabricated address.
    let private entriesWithIds (entries: obj array) : Result<(string * obj) array, HostError> =
        let messages =
            entries
            |> Array.filter (fun entry -> stringOf (fieldValue entry "type") = "message")

        let missing =
            messages
            |> Array.mapi (fun position entry -> (optionalString (fieldValue entry "id"), position))
            |> Array.tryFind (fun (id, _) -> Option.isNone id)

        match missing with
        | Some(_, position) -> Error(HostError.Transport(sprintf "omp session entry at position %d has no id" position))
        | None ->
            Ok(
                messages
                |> Array.map (fun entry -> (stringOf (fieldValue entry "id"), fieldValue entry "message"))
            )

    /// Decode the message entries into neutral transcript messages. The
    /// address of each message is the id of the entry that carried it.
    let private decodeTranscript (entries: (string * obj) array) : TranscriptMessage list =
        entries
        |> Array.map (fun (id, message) -> decodeMessage message id)
        |> Array.choose id
        |> Array.toList

    /// The transcript of a child session the adapter created, read from the
    /// CHILD's own session manager entries: the same entry ids, the same
    /// address space as the host path.
    let private childTranscript (id: string) : Result<TranscriptMessage list, HostError> =
        match liveChildren.TryGetValue id with
        | false, _ -> Error(HostError.NotFound id)
        | true, child ->
            let manager = fieldValue child.Session "sessionManager"
            let entries = tryArray (callMethod manager "getEntries" null)

            entriesWithIds entries |> Result.map decodeTranscript

    /// The transcript of the host's own session, read from the persisted
    /// session entries.
    let private hostTranscript (ctx: obj) : Result<TranscriptMessage list, HostError> =
        entriesWithIds (ctxEntries ctx) |> Result.map decodeTranscript

    /// The transcript of a session: the child's own entries when the
    /// adapter created it, the host session's entries otherwise.
    let private transcriptFor (ctx: obj) (id: string) : Result<TranscriptMessage list, HostError> =
        if isOwnChild ctx id then
            childTranscript id
        else
            hostTranscript ctx

    let private transcriptOf (ctx: obj) (id: string) : Task<Result<TranscriptMessage list, HostError>> =
        task {
            try
                return transcriptFor ctx id
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    // -- models ------------------------------------------------------------------------

    let private availableModels (ctx: obj) : Task<Result<ModelRoute list, HostError>> =
        task {
            try
                let models = tryArray (callMethod (ctxModelRegistry ctx) "getAvailable" null)

                let routes =
                    models
                    |> Array.map (fun model ->
                        { Provider = stringOf (fieldValue model "provider")
                          Model = stringOf (fieldValue model "id")
                          Variant = None })
                    |> Array.toList

                return Ok routes
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    /// Ask omp to switch the active session to the requested model.
    let private setRequestedModel (pi: obj) (ctx: obj) (route: ModelRoute) : Task<Result<unit, HostError>> =
        task {
            let registry = ctxModelRegistry ctx

            let model =
                callOptionalMethod2 registry "find" (box route.Provider) (box route.Model)

            if isNull model then
                return Error(HostError.NotFound(sprintf "%s/%s" route.Provider route.Model))

            let! outcome = awaitObj (call1 (fieldValue pi "setModel") (box model))

            if isTrue outcome then
                return Ok()
            else
                return Error(HostError.Transport "omp refused the requested model")
        }

    /// Change the model of the active session to the requested one.
    let private applyModelInner (pi: obj) (ctx: obj) (id: string) (route: ModelRoute) : Task<Result<unit, HostError>> =
        task {
            if id <> ctxSessionId ctx then
                return Error(HostError.Refused "omp can only change the model of the active session")
            else
                return! setRequestedModel pi ctx route
        }

    let private applyModel (pi: obj) (ctx: obj) (id: string) (route: ModelRoute) : Task<Result<unit, HostError>> =
        task {
            try
                return! applyModelInner pi ctx id route
            with ex ->
                return Error(HostError.Transport ex.Message)
        }

    // -- tools -------------------------------------------------------------------------

    /// Encode a product tool outcome as an omp tool result. omp expects the
    /// content to be an ARRAY of content parts (`[{ type: "text", text }]`),
    /// not a bare object.
    let private toolOutcome (outcome: ToolOutcome) : obj =
        createObj
            [ "content", box [| createObj [ "type", box "text"; "text", box outcome.Text ] |]
              "details",
              box (
                  match outcome.Details with
                  | None -> null
                  | Some value -> parseJson (canonicalOf value)
              )
              "isError", box outcome.IsError ]

    /// The omp tool definition for one product tool spec.
    ///
    /// `loadMode: "essential"` is required: omp defaults every tool that
    /// does not declare it to `"discoverable"`
    /// (`src/tools/essential-tools.ts:45-48`), and a discoverable tool is
    /// mounted as an `xd://` device instead of being advertised to the
    /// provider (`src/tools/xdev.ts:85-88`), which is the host default
    /// (`src/tools/settings.ts:961-964`).
    let private toolDefinition (spec: ToolSpec) : obj =
        createObj
            [ "name", box spec.Name
              "label", box spec.Name
              "description", box spec.Description
              "loadMode", box "essential"
              "parameters", box (parseJson (jsonSchemaOf spec.Schema))
              "execute",
              box (fun (callId: obj) (arguments: obj) (signal: obj) (onUpdate: obj) (toolCtx: obj) ->
                  let ctx = orNull toolCtx
                  let sid = ctxSessionId ctx
                  let workspace = WorkspacePath(ctxCwd ctx)
                  let args = CanonicalJsonText(if isNull arguments then "{}" else stringify arguments)

                  let progress =
                      match orNull onUpdate with
                      | null -> None
                      | update ->
                          Some(fun line ->
                              call1
                                  update
                                  (createObj
                                      [ "content", box [| createObj [ "type", box "text"; "text", box line ] |]
                                        "details", box null ]))

                  let invocation =
                      { Session = SessionId.create sid
                        CallId = ToolCallId.create (stringOf callId)
                        Workspace = workspace
                        Args = args
                        Cancellation = cancellationOf (orNull signal)
                        Progress = progress }

                  task {
                      let! outcome = spec.Execute invocation
                      return toolOutcome outcome
                  }
                  |> unbox<obj>) ]

    /// Register product tools with omp. The port is synchronous: the omp
    /// registration itself never awaits, so the outcome is known inline.
    let private registerTools (pi: obj) (specs: ToolSpec list) : Result<unit, HostError> =
        try
            specs |> List.iter (fun spec -> registerTool pi (toolDefinition spec))
            Ok()
        with ex ->
            Error(HostError.Transport ex.Message)

    // ------------------------------------------------------------------
    // Host ports over omp
    // ------------------------------------------------------------------

    /// A live subscription to the host signal stream.
    type private SignalSubscription(listeners: ResizeArray<HostSignal -> Task>, listener: HostSignal -> Task) =
        interface IDisposable with
            member _.Dispose() = listeners.Remove listener |> ignore

    [<Emit("Date.now()")>]
    let private nowStamp () : float = jsNative

    [<Emit("Object.fromEntries($0)")>]
    let private entriesOf (fields: (string * string) list) : obj = jsNative

    /// The neutral route of a model object, when it names one.
    let private modelRouteOf (model: obj) : ModelRoute option =
        let provider = optionalString (fieldValue model "provider")
        let id = optionalString (fieldValue model "id")

        match provider, id with
        | Some p, Some m ->
            Some
                { Provider = p
                  Model = m
                  Variant = None }
        | _ -> None

    /// The neutral route of the model the session currently uses.
    let private ctxRoute (ctx: obj) : ModelRoute option =
        let model = ctxModel ctx
        if isNull model then None else modelRouteOf model

    /// Build the host-neutral ports over the omp extension API, together with
    /// the signal emitter the product hooks publish through.
    let private createOmpHostPorts (pi: obj) (agents: AgentRoleSpec list) : HostPorts * (HostSignal -> Task<unit>) =
        let listeners = ResizeArray<HostSignal -> Task>()

        let emit (signal: HostSignal) : Task<unit> =
            task {
                for listener in listeners do
                    do! listener signal
            }

        let sessions: ISessionPort =
            { new ISessionPort with
                member _.Create(parent, spec) =
                    guardTask (fun () -> createSession pi parent spec)

                member _.Parent(session) =
                    guardTask (fun () -> parentSession pi (context ()) (SessionId.value session))

                member _.Children(parent) =
                    guardTask (fun () -> Task.FromResult(Ok(childSessionInfos (SessionId.value parent))))

                member _.Abort(session) =
                    guardTask (fun () -> abortSession (context ()) (SessionId.value session))

                member _.Interrupt(session) =
                    guardTask (fun () -> abortSession (context ()) (SessionId.value session))

                member _.Close(session) =
                    guardTask (fun () -> closeSession (context ()) (SessionId.value session))

                member _.FindByTitle(title) =
                    guardTask (fun () -> findByTitle pi title) }

        let prompts: IPromptPort =
            { new IPromptPort with
                member _.Send(session, request, late) =
                    sendPrompt pi (SessionId.value session) request late }

        let transcript: ITranscriptPort =
            { new ITranscriptPort with
                member _.Messages(session) =
                    guardTask (fun () -> transcriptOf (context ()) (SessionId.value session)) }

        let models: IModelCatalog =
            { new IModelCatalog with
                member _.Available() =
                    guardTask (fun () -> availableModels (context ()))

                member _.Apply(session, route) =
                    guardTask (fun () -> applyModel pi (context ()) (SessionId.value session) route) }

        let tools: IToolRegistrar =
            { new IToolRegistrar with
                member _.Register(specs) = registerTools pi specs }

        let signals: ISignalSource =
            { new ISignalSource with
                member _.Subscribe(listener) =
                    listeners.Add listener
                    SignalSubscription listeners listener :> IDisposable }

        let paths: IHostPaths =
            // The workspace is read once, so the three members cannot
            // disagree if the context changes between reads.
            let workspace = ctxCwd (context ())

            { new IHostPaths with
                // omp exposes no host data directory; the product resolves
                // its own state root from the workspace
                // (`RuntimePath.forWorkspace`), so this member is not a
                // journal root and must not be treated as one. The adapter
                // must not invent a product-private directory name here.
                member _.StateRoot = WorkspacePath(workspace)
                member _.ProjectRoot = WorkspacePath(workspace)
                member _.ProjectId = workspace }

        let diagnostics: IDiagnosticSink =
            { new IDiagnosticSink with
                member _.Emit(operation, fields) =
                    call2 (fieldValue (fieldValue pi "logger") "debug") (box operation) (box (entriesOf fields))
                    |> ignore }

        let catalog: IAgentCatalog =
            { new IAgentCatalog with
                member _.Roles() = agents }

        let ports: HostPorts =
            { Sessions = sessions
              Prompts = prompts
              Transcript = transcript
              Models = models
              Signals = signals
              Tools = tools
              Paths = paths
              Diagnostics = diagnostics
              Agents = catalog
              Capabilities =
                { HostName = "omp"
                  Supports =
                    Set.empty
                    |> Set.add HostCapability.ChildSessions
                    |> Set.add HostCapability.SessionDiscovery
                    |> Set.add HostCapability.Interrupt
                    |> Set.add HostCapability.SubagentRouting } }

        ports, emit

    // ------------------------------------------------------------------
    // Product hooks -> omp registrations
    // ------------------------------------------------------------------

    /// The neutral turn context of an omp session.
    let private turnContext (ctx: obj) : TurnContext =
        { Session = SessionId.create (ctxSessionId ctx)
          Role = None
          Route = ctxRoute ctx
          Workspace = Some(WorkspacePath(ctxCwd ctx)) }

    /// The neutral view of one tool call.
    let private toolCallView (ctx: obj) (callId: obj) (tool: obj) (input: obj) : ToolCallView =
        { Session = SessionId.create (ctxSessionId ctx)
          CallId = ToolCallId.create (stringOf callId)
          Tool = stringOf tool
          Args = CanonicalJsonText(if isNull input then "{}" else stringify input)
          Role = None }

    /// The candidate routes carried by omp spawn patterns (`provider/model`).
    let private spawnCandidates (patterns: obj) : ModelRoute list =
        tryArray patterns
        |> Array.choose (fun pattern ->
            let text = stringOf pattern
            let parts = text.Split('/')

            if parts.Length > 1 then
                Some
                    { Provider = parts.[0]
                      Model = String.Join("/", parts.[1..])
                      Variant = None }
            else
                None)
        |> Array.toList

    /// The neutral spawn request an omp spawn event carries.
    let private spawnRequest (event: obj) (ctx: obj) : SpawnRequest =
        { Parent = SessionId.create (ctxSessionId ctx)
          Role = AgentRole(stringOf (fieldValue event "agent"))
          Candidates = spawnCandidates (fieldValue event "patterns")
          SpawnKey = optionalString (fieldValue event "spawnKey") }

    /// The omp reply to a spawn routing decision.
    let private spawnReply (route: SpawnRoute) : obj =
        match route with
        | SpawnRoute.Keep -> jsUndefined
        | SpawnRoute.Refuse reason -> createObj [ "block", box true; "reason", box reason ]
        | SpawnRoute.Route routes ->
            let models =
                routes |> List.map (fun route -> sprintf "%s/%s" route.Provider route.Model)

            createObj [ "model", box (List.toArray models) ]

    /// Ask the product how an omp spawn should be routed.
    let private routeSpawn (hooks: ProductHooks) (event: obj) (ctx: obj) : Task<obj> =
        task {
            match hooks.Spawn with
            | None -> return jsUndefined
            | Some spawn ->
                let! route = spawn (spawnRequest event ctx)
                return spawnReply route
        }

    /// The omp role name of a neutral transcript role.
    let private roleName (role: TranscriptRole) : string =
        match role with
        | TranscriptRole.User -> "user"
        | TranscriptRole.Assistant -> "assistant"
        | TranscriptRole.System -> "system"

    /// Encode the replacement messages for omp. A message that omp cannot
    /// represent makes the whole edit unusable, and is reported as such.
    let private encodeContextMessages (replacements: ContextMessage list) : obj option =
        // One stamp for the whole edit: every message produced here belongs
        // to the same replacement, so mixed provenance would make the
        // transcript order ambiguous.
        let stamp = nowStamp ()

        let encoded =
            replacements |> List.map (fun message -> message, encodeParts message.Parts)

        if encoded |> List.exists (fun (_, parts) -> parts.IsNone) then
            None
        else
            let payload =
                encoded
                |> List.map (fun (message, parts) ->
                    let content = parts |> Option.defaultValue [] |> List.map (fun part -> box part)

                    createObj
                        [ "role", box (roleName message.Role)
                          "content", box (List.toArray content)
                          "timestamp", box stamp ])

            Some(createObj [ "messages", box (List.toArray payload) ])

    /// Apply the route the product admitted for a turn to omp's active
    /// session. A model omp does not know, or one omp refuses, ends the
    /// turn.
    let private applyAdmittedRoute (pi: obj) (ctx: obj) (chosen: ModelRoute) : Task<obj> =
        task {
            let model =
                callOptionalMethod2 (ctxModelRegistry ctx) "find" (box chosen.Provider) (box chosen.Model)

            let known = not (isNull model)

            let! accepted =
                if known then
                    awaitObj (call1 (fieldValue pi "setModel") (box model))
                else
                    Task.FromResult<obj>(box false)

            let handled = not known || not (isTrue accepted)

            if handled then
                return createObj [ "handled", box true ]
            else
                return jsUndefined
        }

    /// The omp reply to an admitted turn: nothing when the turn runs with
    /// the current model, an object that ends the turn otherwise.
    let private admittedReply (pi: obj) (ctx: obj) (route: ModelRoute option) : Task<obj> =
        task {
            match route with
            | None -> return jsUndefined
            | Some chosen -> return! applyAdmittedRoute pi ctx chosen
        }

    /// The omp reply to a turn admission: nothing when the turn runs with
    /// the current model, an object that ends the turn otherwise.
    let private admissionReply (pi: obj) (ctx: obj) (verdict: TurnAdmission) : Task<obj> =
        task {
            match verdict with
            | TurnAdmission.Refuse _ -> return createObj [ "handled", box true ]
            | TurnAdmission.Admit route -> return! admittedReply pi ctx route
        }

    /// The omp reply to a context edit: nothing when the context stays, the
    /// replacement messages otherwise.
    let private contextEditReply (edit: ContextEdit) : obj option =
        match edit with
        | ContextEdit.Keep -> None
        | ContextEdit.Replace replacements -> encodeContextMessages replacements

    /// The neutral view of one omp context message.
    let private contextMessageView (message: obj) : ContextMessage option =
        match decodeRole message with
        | None -> None
        | Some role ->
            Some
                { Address = None
                  Role = role
                  Parts = decodeParts message }

    /// Register the product hooks on the omp event bus and return the
    /// combined signal emitter: every signal reaches the host subscribers
    /// and the product at the same time.
    let private registerProductHooks
        (pi: obj)
        (hooks: ProductHooks)
        (emit: HostSignal -> Task<unit>)
        : (HostSignal -> Task<unit>) =

        let signal (value: HostSignal) : Task<unit> =
            task {
                let! _ = emit value
                do! hooks.OnSignal value
            }

        // -- turn admission ---------------------------------------------------------------
        registerEvent pi "input" (fun (event: obj) (ctx: obj) ->
            task {
                let! verdict =
                    hooks.Turn.AdmitTurn
                        { Context = turnContext ctx
                          Message = None
                          Text = stringOf (fieldValue event "text")
                          Correlation = None }

                return! admissionReply pi ctx verdict
            }
            |> unbox<obj>)

        // -- system prompt ----------------------------------------------------------------
        registerEvent pi "before_agent_start" (fun (event: obj) (ctx: obj) ->
            task {
                let system = stringList (fieldValue event "systemPrompt")
                let! prompt = hooks.Turn.SystemPrompt (turnContext ctx) system
                return createObj [ "systemPrompt", box (List.toArray prompt) ]
            }
            |> unbox<obj>)

        // -- context messages -------------------------------------------------------------
        registerEvent pi "context" (fun (event: obj) (ctx: obj) ->
            task {
                let messages =
                    tryArray (fieldValue event "messages")
                    |> Array.choose contextMessageView
                    |> Array.toList

                let! edit = hooks.Turn.ContextMessages (turnContext ctx) messages

                match contextEditReply edit with
                | None -> return jsUndefined
                | Some payload -> return payload
            }
            |> unbox<obj>)

        // -- tool gate --------------------------------------------------------------------
        registerEvent pi "tool_call" (fun (event: obj) (ctx: obj) ->
            task {
                let! verdict =
                    hooks.ToolGate.BeforeTool(
                        toolCallView
                            ctx
                            (fieldValue event "toolCallId")
                            (fieldValue event "toolName")
                            (fieldValue event "input")
                    )

                match verdict with
                | ToolCallVerdict.Allow -> return jsUndefined
                | ToolCallVerdict.Refuse reason -> return createObj [ "block", box true; "reason", box reason ]
                | ToolCallVerdict.Rewrite args -> return createObj [ "input", box (parseJson (canonicalOf args)) ]
            }
            |> unbox<obj>)

        registerEvent pi "tool_result" (fun (event: obj) (ctx: obj) ->
            task {
                let text =
                    tryArray (fieldValue event "content")
                    |> Array.choose (fun part ->
                        match optionalString (fieldValue part "type") with
                        | Some "text" -> Some(stringOf (fieldValue part "text"))
                        | _ -> None)
                    |> String.Join "\n"

                let isError = isTrue (fieldValue event "isError")

                let! edit =
                    hooks.ToolGate.AfterTool(
                        toolCallView
                            ctx
                            (fieldValue event "toolCallId")
                            (fieldValue event "toolName")
                            (fieldValue event "input")
                    ),
                    { Text = text
                      Truncated = false
                      IsError = isError
                      Details = None }

                match edit with
                | ToolResultEdit.Keep -> return jsUndefined
                | ToolResultEdit.Replace result ->
                    return
                        createObj
                            [ "content", box [| createObj [ "type", box "text"; "text", box result.Text ] |]
                              "isError", box result.IsError ]
            }
            |> unbox<obj>)

        // -- compaction -------------------------------------------------------------------
        registerEvent pi "session.compacting" (fun (_event: obj) (ctx: obj) ->
            task {
                // The event's session id is omp's PROVIDER session id; the
                // neutral id is always the persisted one.
                let! contribution = hooks.Compaction.OnCompacting(SessionId.create (ctxSessionId ctx))

                match contribution with
                | CompactionContribution.Nothing -> return jsUndefined
                | CompactionContribution.Context lines -> return createObj [ "context", box (List.toArray lines) ]
                | CompactionContribution.ReplacePrompt prompt -> return createObj [ "prompt", box prompt ]
            }
            |> unbox<obj>)

        registerEvent pi "session_stop" (fun (_event: obj) (ctx: obj) ->
            task {
                let! verdict = hooks.Compaction.AutoContinue(SessionId.create (ctxSessionId ctx))

                match verdict with
                | ContinueVerdict.Allow -> return jsUndefined
                | ContinueVerdict.Deny reason -> return createObj [ "continue", box false; "reason", box reason ]
            }
            |> unbox<obj>)

        // -- subagent routing --------------------------------------------------------------
        // `before_subagent_spawn` is ALWAYS registered: the adapter advertises
        // the `SubagentRouting` capability, so the host must be able to reach
        // the product's routing decision even when the product drops the
        // hook. Without a hook there is nothing to route, so the spawn is
        // allowed through unchanged.
        registerEvent pi "before_subagent_spawn" (fun (event: obj) (ctx: obj) ->
            task { return! routeSpawn hooks event ctx } |> unbox<obj>)

        // -- host facts ---------------------------------------------------------------------
        registerEvent pi "turn_start" (fun (_event: obj) (ctx: obj) ->
            task {
                do!
                    signal (
                        HostSignal.AttemptStarted
                            { Session = SessionId.create (ctxSessionId ctx)
                              Message = None }
                    )
            }
            |> unbox<obj>)

        registerEvent pi "turn_end" (fun (event: obj) (ctx: obj) ->
            task {
                match decodeTerminal (fieldValue event "message") with
                | None -> return jsUndefined
                | Some terminal ->
                    do!
                        signal (
                            HostSignal.AttemptFinished(
                                { Session = SessionId.create (ctxSessionId ctx)
                                  Message = None },
                                terminal
                            )
                        )

                    return jsUndefined
            }
            |> unbox<obj>)

        registerEvent pi "agent_end" (fun (event: obj) (ctx: obj) ->
            task {
                if isTrue (fieldValue event "willContinue") || not (ctxIsIdle ctx) then
                    return jsUndefined
                else
                    do! signal (HostSignal.SessionIdle(SessionId.create (ctxSessionId ctx)))
                    return jsUndefined
            }
            |> unbox<obj>)

        registerEvent pi "auto_retry_start" (fun (event: obj) (ctx: obj) ->
            task {
                let attempt =
                    match optionalInt (fieldValue event "attempt") with
                    | Some value -> value
                    | None -> 0

                do!
                    signal (
                        HostSignal.ProviderRetry
                            { Session = SessionId.create (ctxSessionId ctx)
                              Attempt = attempt
                              Failure = decodeFailure (fieldValue event "errorMessage") false }
                    )
            }
            |> unbox<obj>)

        registerEvent pi "tool_execution_start" (fun (event: obj) (ctx: obj) ->
            task {
                do!
                    signal (
                        HostSignal.ToolStarted
                            { Session = SessionId.create (ctxSessionId ctx)
                              CallId = ToolCallId.create (stringOf (fieldValue event "toolCallId"))
                              Tool = stringOf (fieldValue event "toolName") }
                    )
            }
            |> unbox<obj>)

        registerEvent pi "session_shutdown" (fun (_event: obj) (_ctx: obj) ->
            task { do! hooks.Dispose() } |> unbox<obj>)

        signal

    // ------------------------------------------------------------------
    // omp extension entry
    // ------------------------------------------------------------------

    /// Activate the product on an omp extension. The plugin is asked for its
    /// hooks when the first session starts, and the `SessionCreated` signal
    /// is emitted HERE: omp snapshots the listener list when it starts
    /// emitting `session_start`, so a listener registered during that
    /// emission is never called for this session.
    let private startOnOmp (pi: obj) (plugin: IHostPlugin) (agents: AgentRoleSpec list) : Task<unit> =
        let ports, emit = createOmpHostPorts pi agents

        registerEvent pi "session_start" (fun (_event: obj) (ctx: obj) ->
            task {
                latestContext := ctx
                let! outcome = plugin.Activate ports

                match outcome with
                | Error error ->
                    call2
                        (fieldValue (fieldValue pi "logger") "error")
                        (box "wanxiangshu activation failed")
                        (box (createObj [ "error", box (reasonOf error) ]))
                    |> ignore
                | Ok hooks ->
                    let signal = registerProductHooks pi hooks emit

                    do!
                        signal (
                            HostSignal.SessionCreated
                                { Id = SessionId.create (ctxSessionId ctx)
                                  Parent = None
                                  Role = None
                                  Title = None }
                        )
            }
            |> unbox<obj>)

    /// Activate the product on an omp extension.
    let activateOnOmp (pi: obj) (plugin: IHostPlugin) (agents: AgentRoleSpec list) : Task<unit> =
        startOnOmp pi plugin agents
