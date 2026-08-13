namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Outcome

[<RequireQualifiedAccess>]
type SessionBindingIntent =
    | Preserve
    | ExplicitExecutionOverride

type OpenCodePromptOptions =
    {
        Model: OpencodeModel option
        Agent: string option
        Directory: string option
        Metadata: obj option
        /// PROMPT-012: complete request-local provider tool surface.
        Tools: Map<string, bool> option
        BindingIntent: SessionBindingIntent
    }

type IPromptPort =
    abstract SendPrompt:
        sessionId: SessionId -> promptText: string -> options: OpenCodePromptOptions -> Task<SendOutcome>

type OpenCodeChildOptions =
    { Title: string option
      Agent: string option
      Directory: string option }

type OpenCodeChildInfo =
    { SessionId: SessionId
      ParentSessionId: SessionId option
      Agent: string option
      Title: string option }

type IOpenCodePort =
    inherit IPromptPort
    abstract AbortSession: sessionId: SessionId -> Task<Result<unit, string>>
    abstract CreateChildSession: parentId: SessionId -> options: OpenCodeChildOptions -> Task<Result<SessionId, string>>
    abstract ListChildren: parentId: SessionId -> Task<Result<OpenCodeChildInfo list, string>>
    abstract CloseChildSession: childId: SessionId -> Task<Result<unit, string>>

module OpenCodePort =

    let private boundModel (opts: OpenCodePromptOptions) =
        match opts.Model with
        | Some model -> Some model
        | None -> opts.Agent |> Option.bind ManagedAgentConfig.tryBoundModel

    [<Emit("fetch($0, $1)")>]
    let private jsFetch (url: string) (init: obj) : Task<obj> = jsNative

    type SdkClientPort(client: obj, workspaceDirectory: string option) =
        let headersObj (directory: string option) =
            match directory |> Option.orElse workspaceDirectory with
            | Some dir -> createObj [ "x-opencode-directory", box dir ]
            | None -> createObj []

        interface IOpenCodePort with
            member _.SendPrompt (sessionId: SessionId) text opts =
                task {
                    let sId = SessionId.value sessionId
                    // Host PromptInput has no top-level correlation field. Put
                    // wanxiangshu keys on TextPart.metadata so chat.message
                    // can recover them from output.parts even when body.metadata
                    // is stripped.
                    let parts =
                        match opts.Metadata with
                        | Some metadata ->
                            [| createObj [ "type", box "text"; "text", box text; "metadata", metadata ] |]
                        | None -> [| createObj [ "type", box "text"; "text", box text ] |]

                    let model = boundModel opts

                    let bodyFields =
                        [ "parts", box parts ]
                        @ (model
                           |> Option.map (fun bound -> [ "model", box bound ])
                           |> Option.defaultValue [])
                        @ (opts.Agent
                           |> Option.map (fun agent -> [ "agent", box agent ])
                           |> Option.defaultValue [])
                        @ (opts.Metadata
                           |> Option.map (fun metadata -> [ "metadata", metadata ])
                           |> Option.defaultValue [])
                        @ (opts.Tools
                           |> Option.map (fun tools ->
                               [ "tools",
                                 box (
                                     tools
                                     |> Map.toList
                                     |> List.map (fun (name, enabled) -> name, box enabled)
                                     |> createObj
                                 ) ])
                           |> Option.defaultValue [])

                    // v1 SDK: { path.id, body.agent }. v2 SDK: top-level sessionID/agent/model/parts.
                    // A nested-only payload drops agent on v2, and OpenCode then
                    // defaultInfo()s a Deep child onto Fast.
                    let payload =
                        createObj (
                            [ "path", box (createObj [ "id", box sId ])
                              "body", box (createObj bodyFields)
                              "headers", box (headersObj opts.Directory)
                              "sessionID", box sId
                              "parts", box parts ]
                            @ (model
                               |> Option.map (fun bound -> [ "model", box bound ])
                               |> Option.defaultValue [])
                            @ (opts.Agent
                               |> Option.map (fun agent -> [ "agent", box agent ])
                               |> Option.defaultValue [])
                        )

                    try
                        let sessObj = client?session
                        let promptFn = sessObj?promptAsync
                        let! _ = unbox<Task<obj>> (promptFn?call (sessObj, payload))
                        // PROMPT-005: this endpoint admits the request and returns no
                        // message identity. The receipt is a transport token, and its
                        // own type is what stops it becoming an Authority Root.
                        return AdmittedWithReceipt(TransportReceipt.create (sprintf "accepted-%s" sId))
                    with ex ->
                        return Retryable ex.Message
                }

            member _.AbortSession(sessionId: SessionId) =
                task {
                    let sId = SessionId.value sessionId

                    try
                        let sessObj = client?session
                        let abortFn = sessObj?abort

                        let payload =
                            createObj [ "path", box (createObj [ "id", box sId ]); "headers", box (headersObj None) ]

                        let! _ = unbox<Task<obj>> (abortFn?call (sessObj, payload))
                        return Ok()
                    with ex ->
                        return Error ex.Message
                }

            member _.CreateChildSession (parentId: SessionId) opts =
                task {
                    let pId = SessionId.value parentId

                    let payload =
                        createObj
                            [ "body",
                              box
                                  {| parentID = pId
                                     title = opts.Title
                                     agent = opts.Agent |}
                              "headers", box (headersObj opts.Directory)
                              "parentID", box pId
                              "title", box opts.Title
                              "agent", box opts.Agent ]

                    try
                        let sessObj = client?session
                        let createFn = sessObj?create
                        let! res = unbox<Task<obj>> (createFn?call (sessObj, payload))

                        let body =
                            if not (isNull res) && not (isNull res?data) then
                                res?data
                            else
                                res

                        if not (isNull body) && not (isNull body?id) then
                            return Ok(SessionId.create (unbox<string> body?id))
                        else
                            return Error "Missing session id in response"
                    with ex ->
                        return Error ex.Message
                }

            member _.ListChildren(parentId: SessionId) =
                task {
                    let pId = SessionId.value parentId

                    try
                        let sessObj = client?session
                        let childrenFn = sessObj?children

                        let payload =
                            createObj [ "path", box (createObj [ "id", box pId ]); "headers", box (headersObj None) ]

                        let! res = unbox<Task<obj>> (childrenFn?call (sessObj, payload))

                        let body =
                            if not (isNull res) && not (isNull res?data) then
                                res?data
                            else
                                res

                        let items: obj array = if isNull body then [||] else unbox body

                        return
                            Ok(
                                items
                                |> Array.choose (fun item ->
                                    if isNull item || isNull item?id then
                                        None
                                    else
                                        Some
                                            { SessionId = SessionId.create (unbox<string> item?id)
                                              ParentSessionId =
                                                if isNull item?parentID then
                                                    None
                                                else
                                                    Some(SessionId.create (unbox<string> item?parentID))
                                              Agent =
                                                if isNull item?agent then
                                                    None
                                                else
                                                    Some(unbox<string> item?agent)
                                              Title =
                                                if isNull item?title then
                                                    None
                                                else
                                                    Some(unbox<string> item?title) })
                                |> Array.toList
                            )
                    with ex ->
                        return Error ex.Message
                }

            member _.CloseChildSession(childId: SessionId) =
                task {
                    let cId = SessionId.value childId

                    try
                        let sessObj = client?session

                        let closeFn =
                            if not (isNull sessObj?delete) then sessObj?delete
                            elif not (isNull sessObj?close) then sessObj?close
                            else null

                        if not (isNull closeFn) then
                            let! _ = unbox<Task<obj>> (closeFn?call (sessObj, {| sessionID = cId |}))
                            return Ok()
                        else
                            return Error "No close/delete session method on SDK client"
                    with ex ->
                        return Error ex.Message
                }

    type HttpPort(baseUrl: string) =
        let cleanBaseUrl =
            if baseUrl.EndsWith("/") then
                baseUrl.Substring(0, baseUrl.Length - 1)
            else
                baseUrl

        let postJson (endpoint: string) (body: obj) : Task<Result<obj, string>> =
            task {
                try
                    let init =
                        {| method = "POST"
                           headers = {| ``Content-Type`` = "application/json" |}
                           body = Fable.Core.JS.JSON.stringify body |}

                    let! response = jsFetch (cleanBaseUrl + endpoint) init
                    let status = unbox<int> response?status

                    if status >= 200 && status < 300 then
                        // Some Host endpoints answer 2xx with an empty body. Never
                        // treat a successful empty response as an error.
                        try
                            let! text = unbox<Task<string>> (response?text ())

                            if String.IsNullOrWhiteSpace text then
                                return Ok(createObj [])
                            else
                                return Ok(Fable.Core.JS.JSON.parse text)
                        with _ ->
                            return Ok(createObj [])
                    else
                        return Error $"HTTP {status}"
                with ex ->
                    return Error ex.Message
            }

        let getJson (endpoint: string) : Task<Result<obj, string>> =
            task {
                try
                    let! response = jsFetch (cleanBaseUrl + endpoint) {| method = "GET" |}
                    let status = unbox<int> response?status

                    if status >= 200 && status < 300 then
                        let! body = unbox<Task<string>> (response?text ())

                        return
                            Ok(
                                if String.IsNullOrWhiteSpace body then
                                    box [||]
                                else
                                    Fable.Core.JS.JSON.parse body
                            )
                    else
                        return Error $"HTTP {status}"
                with ex ->
                    return Error ex.Message
            }

        interface IOpenCodePort with
            member _.SendPrompt (sessionId: SessionId) text opts =
                task {
                    let sId = SessionId.value sessionId
                    // Omit optional fields when absent: host rejects model:null.
                    // Correlation metadata lives on the text part (host-stable).
                    let parts =
                        match opts.Metadata with
                        | Some metadata ->
                            [| createObj [ "type", box "text"; "text", box text; "metadata", metadata ] |]
                        | None -> [| createObj [ "type", box "text"; "text", box text ] |]

                    let model = boundModel opts

                    let bodyFields =
                        [ "parts", box parts ]
                        @ (model
                           |> Option.map (fun bound -> [ "model", box bound ])
                           |> Option.defaultValue [])
                        @ (opts.Agent
                           |> Option.map (fun agent -> [ "agent", box agent ])
                           |> Option.defaultValue [])
                        @ (opts.Metadata
                           |> Option.map (fun metadata -> [ "metadata", metadata ])
                           |> Option.defaultValue [])
                        @ (opts.Tools
                           |> Option.map (fun tools ->
                               [ "tools",
                                 box (
                                     tools
                                     |> Map.toList
                                     |> List.map (fun (name, enabled) -> name, box enabled)
                                     |> createObj
                                 ) ])
                           |> Option.defaultValue [])

                    let! res = postJson $"/session/{sId}/prompt_async" (createObj bodyFields)

                    match res with
                    | Ok data ->
                        // Many Host versions answer 2xx with an empty body. That is
                        // admission, not a message identity — PROMPT-005 keeps the two
                        // apart, so an empty body cannot be reported as delivery.
                        if not (isNull data) && not (isNull data?id) then
                            return AdmittedWithPhysicalMessage(PhysicalUserMessageId.create (unbox<string> data?id))
                        else
                            return AdmittedWithReceipt(TransportReceipt.create (sprintf "accepted-%s" sId))
                    | Error err -> return Retryable err
                }

            member _.AbortSession(sessionId: SessionId) =
                task {
                    let sId = SessionId.value sessionId
                    let! res = postJson $"/session/{sId}/abort" {| |}

                    match res with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
                }

            member _.CreateChildSession (parentId: SessionId) opts =
                task {
                    let pId = SessionId.value parentId

                    let bodyFields =
                        [ "parentID", box pId ]
                        @ (opts.Title
                           |> Option.map (fun title -> [ "title", box title ])
                           |> Option.defaultValue [])
                        @ (opts.Agent
                           |> Option.map (fun agent -> [ "agent", box agent ])
                           |> Option.defaultValue [])

                    let payload = createObj bodyFields

                    let! res = postJson "/session" payload

                    match res with
                    | Ok data ->
                        if not (isNull data) && not (isNull data?id) then
                            return Ok(SessionId.create (unbox<string> data?id))
                        else
                            return Error "Missing session id in response"
                    | Error err -> return Error err
                }

            member _.ListChildren(parentId: SessionId) =
                task {
                    let! res = getJson $"/session/{SessionId.value parentId}/children"

                    match res with
                    | Error err -> return Error err
                    | Ok data ->
                        try
                            let items: obj array = if isNull data then [||] else unbox data

                            return
                                Ok(
                                    items
                                    |> Array.choose (fun item ->
                                        if isNull item || isNull item?id then
                                            None
                                        else
                                            Some
                                                { SessionId = SessionId.create (unbox<string> item?id)
                                                  ParentSessionId =
                                                    if isNull item?parentID then
                                                        None
                                                    else
                                                        Some(SessionId.create (unbox<string> item?parentID))
                                                  Agent =
                                                    if isNull item?agent then
                                                        None
                                                    else
                                                        Some(unbox<string> item?agent)
                                                  Title =
                                                    if isNull item?title then
                                                        None
                                                    else
                                                        Some(unbox<string> item?title) })
                                    |> Array.toList
                                )
                        with ex ->
                            return Error ex.Message
                }

            member _.CloseChildSession(childId: SessionId) =
                task {
                    let cId = SessionId.value childId
                    let! res = postJson $"/session/{cId}/abort" {| |}

                    match res with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
                }

    let create (input: obj) : IOpenCodePort option =
        let workDir =
            if not (isNull input) && not (isNull input?directory) then
                let d = unbox<string> input?directory
                if String.IsNullOrWhiteSpace d then None else Some d
            else
                None

        if isNull input then
            None
        elif not (isNull input?client) && not (isNull input?client?session) then
            Some(SdkClientPort(input?client, workDir) :> IOpenCodePort)
        elif not (isNull input?serverUrl) then
            Some(HttpPort(unbox<string> input?serverUrl) :> IOpenCodePort)
        elif not (isNull input?baseUrl) then
            Some(HttpPort(unbox<string> input?baseUrl) :> IOpenCodePort)
        elif not (isNull input?port) then
            let portNum = unbox<int> input?port
            Some(HttpPort($"http://127.0.0.1:{portNum}") :> IOpenCodePort)
        else
            None
