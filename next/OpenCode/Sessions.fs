namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome

type SessionPromptOptions = OpenCodePromptOptions

type ISessionHostPort =
    abstract SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable

    abstract SendPrompt:
        sessionId: SessionId * text: string * opts: SessionPromptOptions -> Task<Result<MessageId, string>>

    abstract SendChildPromptFireAndForget:
        parentId: SessionId * childId: SessionId * text: string * opts: SessionPromptOptions ->
            Task<Result<unit, string>>

    abstract AbortSession: sessionId: SessionId -> Task<Result<unit, string>>
    abstract AbortChildren: parentId: SessionId -> Task
    abstract CreateChildSession: parentId: SessionId * options: OpenCodeChildOptions -> Task<Result<SessionId, string>>
    abstract GetSessionOutput: sessionId: SessionId -> string list

/// Optional per-session output boundary. Implementations expose only output appended
/// after a caller's watermark; older ISessionHostPort implementations can omit it.
type ISessionOutputBoundaryPort =
    abstract GetSessionOutputWatermark: sessionId: SessionId -> int
    abstract GetSessionOutputSince: sessionId: SessionId * watermark: int -> string list

type InjectedSessionPort(underlyingPort: IOpenCodePort option, eventPort: IEventObservationPort) =
    let activeListeners = HashSet<SessionId>()
    let parentChildMap = Dictionary<SessionId, HashSet<SessionId>>()
    let sessionOutputs = Dictionary<SessionId, List<string>>()
    let lockObj = obj ()

    let recordOutput (sId: SessionId) (text: string) =
        lock lockObj (fun () ->
            if not (sessionOutputs.ContainsKey(sId)) then
                sessionOutputs.[sId] <- List<string>()

            sessionOutputs.[sId].Add(text))

    let registerChild (pId: SessionId) (cId: SessionId) =
        lock lockObj (fun () ->
            if not (parentChildMap.ContainsKey(pId)) then
                parentChildMap.[pId] <- HashSet<SessionId>()

            parentChildMap.[pId].Add(cId) |> ignore)

    let getAndRemoveChildren (pId: SessionId) =
        lock lockObj (fun () ->
            if parentChildMap.ContainsKey(pId) then
                let children = parentChildMap.[pId] |> Seq.toList
                parentChildMap.Remove(pId) |> ignore
                children
            else
                [])

    let abortChildren (parentId: SessionId) =
        task {
            let children = getAndRemoveChildren parentId

            for childId in children do
                recordOutput childId "Aborted"

                match underlyingPort with
                | Some port ->
                    let! _ = port.AbortSession childId
                    ()
                | None -> ()

                eventPort.NotifyTerminal childId (Aborted "Parent session aborted") |> ignore
                ()
        }

    interface ISessionHostPort with
        member _.AbortChildren(parentId) = abortChildren parentId

        member me.SubscribeTerminal(sessionId, listener) =
            lock lockObj (fun () -> activeListeners.Add(sessionId) |> ignore)

            let sub =
                eventPort.SubscribeTerminalListener(fun sId outcome ->
                    if sId = sessionId then
                        listener sId outcome)

            { new IDisposable with
                member _.Dispose() =
                    sub.Dispose()
                    lock lockObj (fun () -> activeListeners.Remove(sessionId) |> ignore) }

        member me.SendPrompt(sessionId, text, opts) =
            task {
                let hasListener = lock lockObj (fun () -> activeListeners.Contains(sessionId))

                if not hasListener then
                    return Error "AG-LISTENER-BEFORE-SEND: Listener must be registered before sending prompt"
                else
                    recordOutput sessionId (sprintf "Prompt: %s" text)

                    match underlyingPort with
                    | Some port ->
                        let! res = port.SendPrompt sessionId text opts

                        match res with
                        | Delivered msgId -> return Ok msgId
                        | AcceptanceUnknown(reason, _) -> return Error reason
                        | Retryable err -> return Error err
                        | Fatal err -> return Error err
                    | None ->
                        let msgId = MessageId.create (Guid.NewGuid().ToString("N"))
                        eventPort.NotifyTerminal sessionId (Completed msgId) |> ignore
                        return Ok msgId
            }

        member me.SendChildPromptFireAndForget(parentId, childId, text, opts) =
            task {
                registerChild parentId childId
                recordOutput childId (sprintf "ChildPrompt: %s" text)
                let! result = (me :> ISessionHostPort).SendPrompt(childId, text, opts)

                match result with
                | Ok _ -> return Ok()
                | Error err -> return Error err
            }

        member me.AbortSession(sessionId) =
            task {
                recordOutput sessionId "Aborted"
                do! abortChildren sessionId

                match underlyingPort with
                | Some port ->
                    let! _ = port.AbortSession(sessionId)
                    ()
                | None -> ()

                eventPort.NotifyTerminal sessionId (Aborted "Session aborted") |> ignore
                return Ok()
            }

        member me.CreateChildSession(parentId, options) =
            task {
                match underlyingPort with
                | Some port ->
                    let! res = port.CreateChildSession parentId options

                    match res with
                    | Ok childId ->
                        registerChild parentId childId
                        return Ok childId
                    | Error err -> return Error err
                | None ->
                    let childId = SessionId.create (Guid.NewGuid().ToString("N"))
                    registerChild parentId childId
                    return Ok childId
            }

        member me.GetSessionOutput(sessionId) =
            lock lockObj (fun () ->
                let localOutput =
                    if sessionOutputs.ContainsKey(sessionId) then
                        sessionOutputs.[sessionId] |> Seq.toList
                    else
                        []

                let capturedOutput = eventPort.GetSessionOutput sessionId
                let existing = localOutput |> Set.ofList

                localOutput
                @ (capturedOutput |> List.filter (fun line -> not (existing.Contains line))))
