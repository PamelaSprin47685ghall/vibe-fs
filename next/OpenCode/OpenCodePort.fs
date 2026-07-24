namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome

type OpenCodePromptOptions =
    { Model: string option
      Agent: string option }

type IPromptPort =
    abstract SendPrompt:
        sessionId: SessionId -> promptText: string -> options: OpenCodePromptOptions -> Task<SendOutcome>

type OpenCodeChildOptions =
    { Title: string option
      Agent: string option }

type IOpenCodePort =
    inherit IPromptPort
    abstract AbortSession: sessionId: SessionId -> Task<Result<unit, string>>
    abstract CreateChildSession: parentId: SessionId -> options: OpenCodeChildOptions -> Task<Result<SessionId, string>>
    abstract CloseChildSession: childId: SessionId -> Task<Result<unit, string>>

module OpenCodePort =

    [<Emit("fetch($0, $1)")>]
    let private jsFetch (url: string) (init: obj) : Task<obj> = jsNative

    type SdkClientPort(client: obj) =
        interface IOpenCodePort with
            member _.SendPrompt (sessionId: SessionId) text opts =
                task {
                    let sId = SessionId.value sessionId
                    let parts = [| {| ``type`` = "text"; text = text |} |]

                    let bodyFields =
                        [ "parts", box parts ]
                        @ (opts.Model
                           |> Option.map (fun model -> [ "model", box model ])
                           |> Option.defaultValue [])
                        @ (opts.Agent
                           |> Option.map (fun agent -> [ "agent", box agent ])
                           |> Option.defaultValue [])

                    let payload =
                        createObj
                            [ "path", box (createObj [ "id", box sId ])
                              "body", box (createObj bodyFields) ]

                    try
                        let sessObj = client?session
                        let promptFn = sessObj?promptAsync
                        let! _ = unbox<Task<obj>> (promptFn?call (sessObj, payload))
                        return Delivered(MessageId.create (sprintf "accepted-%s" sId))
                    with ex ->
                        return Retryable ex.Message
                }

            member _.AbortSession(sessionId: SessionId) =
                task {
                    let sId = SessionId.value sessionId

                    try
                        let sessObj = client?session
                        let abortFn = sessObj?abort
                        let! _ = unbox<Task<obj>> (abortFn?call (sessObj, {| sessionID = sId |}))
                        return Ok()
                    with ex ->
                        return Error ex.Message
                }

            member _.CreateChildSession (parentId: SessionId) opts =
                task {
                    let pId = SessionId.value parentId

                    let payload =
                        {| parentID = pId
                           title = opts.Title
                           agent = opts.Agent |}

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
                        let! json = unbox<Task<obj>> (response?json ())
                        return Ok json
                    else
                        return Error $"HTTP {status}"
                with ex ->
                    return Error ex.Message
            }

        interface IOpenCodePort with
            member _.SendPrompt (sessionId: SessionId) text opts =
                task {
                    let sId = SessionId.value sessionId

                    let payload =
                        {| parts = [| {| ``type`` = "text"; text = text |} |]
                           model = opts.Model
                           agent = opts.Agent |}

                    let! res = postJson $"/session/{sId}/prompt" payload

                    match res with
                    | Ok data ->
                        if not (isNull data) && not (isNull data?id) then
                            return Delivered(MessageId.create (unbox<string> data?id))
                        else
                            return AcceptanceUnknown("Missing message id in response", None)
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

                    let payload = createObj [ "body", box (createObj bodyFields) ]

                    let! res = postJson "/session" payload

                    match res with
                    | Ok data ->
                        if not (isNull data) && not (isNull data?id) then
                            return Ok(SessionId.create (unbox<string> data?id))
                        else
                            return Error "Missing session id in response"
                    | Error err -> return Error err
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
        if isNull input then
            None
        elif not (isNull input?client) && not (isNull input?client?session) then
            Some(SdkClientPort(input?client) :> IOpenCodePort)
        elif not (isNull input?serverUrl) then
            Some(HttpPort(unbox<string> input?serverUrl) :> IOpenCodePort)
        elif not (isNull input?baseUrl) then
            Some(HttpPort(unbox<string> input?baseUrl) :> IOpenCodePort)
        elif not (isNull input?port) then
            let portNum = unbox<int> input?port
            Some(HttpPort($"http://127.0.0.1:{portNum}") :> IOpenCodePort)
        else
            None
