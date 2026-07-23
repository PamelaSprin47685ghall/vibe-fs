namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session

type OpenCodePromptOptions = PromptOptions

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

    type HttpPort(baseUrl: string) =
        let postJson (endpoint: string) (body: obj) : Task<Result<obj, string>> =
            task {
                try
                    let init =
                        {| method = "POST"
                           headers = {| ``Content-Type`` = "application/json" |}
                           body = Fable.Core.JS.JSON.stringify body |}
                    let! response = jsFetch (baseUrl + endpoint) init
                    let status = unbox<int> response?status
                    if status >= 200 && status < 300 then
                        let! json = unbox<Task<obj>> (response?json())
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
                        {| sessionID = sId
                           parts = [| {| ``type`` = "text"; text = text |} |]
                           model = opts.Model
                           agent = opts.Agent |}
                    let! res = postJson "/api/prompt" payload
                    match res with
                    | Ok data ->
                        if not (isNull data) && not (isNull data?id) then
                            return Delivered(MessageId.create (unbox<string> data?id))
                        else
                            return AcceptanceUnknown("Missing message id in response", None)
                    | Error err ->
                        return Retryable err
                }

            member _.AbortSession (sessionId: SessionId) =
                task {
                    let sId = SessionId.value sessionId
                    let! res = postJson $"/api/session/{sId}/abort" {| |}
                    match res with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
                }

            member _.CreateChildSession (parentId: SessionId) opts =
                task {
                    let pId = SessionId.value parentId
                    let payload =
                        {| parentID = pId
                           title = opts.Title
                           agent = opts.Agent |}
                    let! res = postJson "/api/session" payload
                    match res with
                    | Ok data ->
                        if not (isNull data) && not (isNull data?id) then
                            return Ok(SessionId.create (unbox<string> data?id))
                        else
                            return Error "Missing session id in response"
                    | Error err -> return Error err
                }

            member _.CloseChildSession (childId: SessionId) =
                task {
                    let cId = SessionId.value childId
                    let! res = postJson $"/api/session/{cId}/close" {| |}
                    match res with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
                }
