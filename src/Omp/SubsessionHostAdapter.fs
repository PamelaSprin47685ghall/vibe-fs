module Wanxiangshu.Omp.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionTranscript

/// OMP host adapter.
/// Serial prompt API is the host-guaranteed barrier for turn start receipt.
type OmpSubsessionHost(session: obj, agent: string, pi: obj) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            promise {
                let model = turn.Model

                let modelStr =
                    match model.Variant with
                    | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                    | None -> sprintf "%s/%s" model.ProviderID model.ModelID

                let turnIdStr = TurnId.value turn.TurnId

                let pObj =
                    let p =
                        {| text = turn.Prompt
                           model = modelStr
                           continuationID = turnIdStr |}

                    if agent <> "" then Dyn.withKey p "agent" agent else box p

                let body = box {| prompt = pObj |}

                let arg =
                    box
                        {| sessionId = SessionId.value sessionId
                           body = body |}

                try
                    let! resp = unbox<JS.Promise<obj>> (session?prompt (arg))

                    let msgId =
                        let id1 = Dyn.str resp "id"
                        let id2 = Dyn.str (Dyn.get resp "data") "id"

                        if id1 <> "" then id1
                        elif id2 <> "" then id2
                        else ""

                    if msgId <> "" then
                        return Ok(UserMessageObserved msgId)
                    else
                        return Ok(HostRunAccepted turnIdStr)
                with ex ->
                    return
                        Error
                            { ErrorName = "DispatchFailed"
                              DomainError = None
                              Message = ex.Message
                              StatusCode = None
                              IsRetryable = Some true }
            }

        member _.Abort(sessionId, _turnId) =
            promise {
                try
                    let abortFn = Dyn.get session "abort"

                    if not (Dyn.isNullish abortFn) then
                        do! unbox<JS.Promise<obj>> (session?abort ()) |> Promise.map ignore
                with _ ->
                    ()

                try
                    let sessionApi = Dyn.get pi "session"

                    if not (Dyn.isNullish sessionApi) then
                        let arg = box {| sessionId = SessionId.value sessionId |}
                        do! unbox<JS.Promise<obj>> (sessionApi?sessionAbort (arg)) |> Promise.map ignore
                with _ ->
                    ()
            }

        member _.ReadTranscript(sessionId) =
            promise {
                try
                    let sessionApi = Dyn.get pi "session"

                    if not (Dyn.isNullish sessionApi) then
                        let arg = box {| sessionId = SessionId.value sessionId |}
                        let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionMessages (arg))
                        let data = Dyn.get resp "data"

                        let msgs = if Dyn.isArray data then unbox<obj array> data else [||]

                        return buildTranscriptSnapshot msgs
                    else
                        let sm = Dyn.get session "sessionManager"
                        let msgs = Dyn.get sm "messages"

                        let arr =
                            if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
                                [||]
                            else
                                unbox<obj array> msgs

                        return buildTranscriptSnapshot arr
                with _ ->
                    return
                        { AllTodosCompleted = false
                          ToolCallAsTextRecoveryPrompt = None
                          LastAssistantToolFinish = false
                          HasToolResultAfterLastAssistant = false
                          LastAssistantText = "" }
            }

        member _.AppendEvents(_events) = Promise.lift ()

let createHost (session: obj) (agent: string) (pi: obj) : ISubsessionHost =
    OmpSubsessionHost(session, agent, pi) :> ISubsessionHost
