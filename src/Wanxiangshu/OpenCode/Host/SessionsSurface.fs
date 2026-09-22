namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome

/// JS-native observation surface for session Host adapter contracts.
/// Parent lineage is a durable lookup; the physical parent for every child is
/// the resolved family root, never the immediate logical parent. Adapter probes
/// return JSON observations and keep transport capabilities private.
module SessionsSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private parentsOf (value: obj) : obj array =
        if isNull value then [||] else unbox<obj array> value

    let familyRoot (parents: obj) (session: string) : string =
        let rec resolve current visited =
            if Set.contains current visited then
                current
            else
                parentsOf parents
                |> Array.tryPick (fun pair ->
                    if text pair?child = current then
                        Some(text pair?parent)
                    else
                        None)
                |> Option.map (fun parent -> resolve parent (Set.add current visited))
                |> Option.defaultValue current

        resolve session Set.empty

    let physicalParents (parents: obj) (children: obj) : string array =
        if isNull children then
            [||]
        else
            (unbox<obj array> children)
            |> Array.map (fun child -> familyRoot parents (text child))

    type private ControlledOpenCodePort(childId: SessionId, rejectAbort: bool) =
        let aborts = ResizeArray<string>()
        let abortTimes = ResizeArray<int>()
        let createParents = ResizeArray<string>()

        let rejection =
            TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

        // DSL-MUTABLE: resource — virtual clock for controlled OpenCode port
        let virtualTime = ref 0

        member _.Aborts = aborts.ToArray()
        member _.AbortTimes = abortTimes.ToArray()
        member _.CreateParents = createParents.ToArray()
        member _.VirtualTime = virtualTime.Value
        member _.AdvanceTo(timestamp: int) = virtualTime.Value <- timestamp

        member _.RejectAbort() =
            AsyncSupport.trySetResult rejection (Error "controlled Host rejected AbortSession")
            |> ignore

        interface IOpenCodePort with
            member _.SendPrompt _ _ _ = Task.FromResult(Fatal "unused")

            member _.AbortSession sessionId =
                aborts.Add(SessionId.value sessionId)
                abortTimes.Add virtualTime.Value

                if rejectAbort then
                    rejection.Task
                else
                    Task.FromResult(Ok())

            member _.CreateSession _ _ = Task.FromResult(Error "unused")
            member _.GetSessionParent _ = Task.FromResult(Ok None)
            member _.CreateChildSession parent _ =
                createParents.Add(SessionId.value parent)
                let createdId =
                    if createParents.Count = 1 then childId
                    else SessionId.create (SessionId.value childId + "-" + string createParents.Count)
                Task.FromResult(Ok createdId)
            member _.ListChildren _ = Task.FromResult(Ok [])
            member _.CloseChildSession _ = Task.FromResult(Ok())

    type private ControlledEventPort() =
        let subscription =
            { new IDisposable with
                member _.Dispose() = () }

        interface IEventObservationPort with
            member _.SubscribeTerminalListener _ = subscription
            member _.SubscribeFutureTerminalListener _ = subscription
            member _.NotifyTerminal _ _ = true

    /// Exercise the production flattening membrane, not a duplicated family-root
    /// algorithm or the logical-parent argument above the Host adapter.
    let flattenedChildAdapterProbe () : Task<obj> =
        task {
            let root = SessionId.create "flat-sphinx-root"
            let transport = ControlledOpenCodePort(SessionId.create "flat-sphinx-engineer", false)
            let sessions =
                InjectedSessionPort(Some(transport :> IOpenCodePort), ControlledEventPort() :> IEventObservationPort)
                :> ISessionHostPort
            let options: OpenCodeChildOptions =
                { Title = Some "standard Engineer"; Agent = Some "engineer"; Directory = None }
            let! first = sessions.CreateChildSession(root, options)
            let caller = first |> Result.defaultWith invalidOp
            let! second = sessions.CreateChildSession(caller, options)
            let worker = second |> Result.defaultWith invalidOp
            return
                createObj
                    [ "root" ==> SessionId.value root
                      "caller" ==> SessionId.value caller
                      "worker" ==> SessionId.value worker
                      "physicalParents" ==> transport.CreateParents
                      "workerFamily" ==> SessionId.value (sessions.FamilyRootOf worker) ]
        }

    /// managed-session-lifecycle-016/017: exercise the production session adapter against
    /// a controlled physical Host boundary. The returned view contains values,
    /// never the adapter or its managed-child representation.
    let interruptAttemptAdapterProbe () : Task<obj> =
        task {
            let rootId = SessionId.create "adapter-root"
            let childId = SessionId.create "adapter-child"
            let transport = ControlledOpenCodePort(childId, false)

            let sessions =
                InjectedSessionPort(Some(transport :> IOpenCodePort), ControlledEventPort() :> IEventObservationPort)
                :> ISessionHostPort

            let options: OpenCodeChildOptions =
                { Title = Some "adapter child"
                  Agent = Some "adapter-proof"
                  Directory = None }

            let! created = sessions.CreateChildSession(rootId, options)

            match created with
            | Error error ->
                return
                    createObj
                        [ "created", box false
                          "creationError", box error
                          "rootRejected", box false
                          "rootError", null
                          "transportCallsAfterRoot", box transport.Aborts.Length
                          "childInterrupted", box false
                          "transportCallsAfterChild", box transport.Aborts.Length
                          "abortedSessionIds", box transport.Aborts
                          "childStillManagedAfterInterrupt", box false ]
            | Ok managedChildId ->
                let! rootOutcome = sessions.InterruptAttempt rootId
                let callsAfterRoot = transport.Aborts.Length
                let! childOutcome = sessions.InterruptAttempt managedChildId

                let rootError =
                    match rootOutcome with
                    | Error error -> box error
                    | Ok() -> null

                return
                    createObj
                        [ "created", box true
                          "creationError", null
                          "rootRejected", box (Result.isError rootOutcome)
                          "rootError", rootError
                          "transportCallsAfterRoot", box callsAfterRoot
                          "childInterrupted", box (Result.isOk childOutcome)
                          "transportCallsAfterChild", box transport.Aborts.Length
                          "abortedSessionIds", box transport.Aborts
                          "childStillManagedAfterInterrupt", box (sessions.IsManagedChild managedChildId) ]
        }

    /// managed-session-lifecycle-016: a Host rejection is a typed terminal result for the
    /// single production adapter attempt; the adapter never retries AbortSession.
    let interruptRejectedAdapterProbe () : Task<obj> =
        task {
            let rootId = SessionId.create "adapter-rejected-root"
            let childId = SessionId.create "adapter-rejected-child"
            let transport = ControlledOpenCodePort(childId, true)

            let sessions =
                InjectedSessionPort(Some(transport :> IOpenCodePort), ControlledEventPort() :> IEventObservationPort)
                :> ISessionHostPort

            let options: OpenCodeChildOptions =
                { Title = Some "adapter rejected child"
                  Agent = Some "adapter-rejection-proof"
                  Directory = None }

            match! sessions.CreateChildSession(rootId, options) with
            | Error error ->
                return
                    createObj
                        [ "outcome", box "SetupError"
                          "error", box error
                          "abortAttempts", box transport.Aborts.Length
                          "trace", box [||] ]
            | Ok managedChildId ->
                let pending = sessions.InterruptAttempt managedChildId
                let attemptsBeforeRejection = transport.Aborts.Length
                transport.AdvanceTo 10
                transport.RejectAbort()
                let! outcome = pending
                transport.AdvanceTo 1000

                let outcomeName, error =
                    match outcome with
                    | Ok() -> "Ok", ""
                    | Error rejection -> "Error", rejection

                let attemptsAfterQuiescence = transport.Aborts.Length

                let trace =
                    [| sprintf "t=%d AbortSession(%s)" transport.AbortTimes[0] transport.Aborts[0]
                       sprintf "t=10 %s(%s)" outcomeName error
                       sprintf "t=%d quiescent attempts=%d" transport.VirtualTime attemptsAfterQuiescence |]

                return
                    createObj
                        [ "outcome", box outcomeName
                          "error", box error
                          "attemptsBeforeRejection", box attemptsBeforeRejection
                          "abortAttempts", box attemptsAfterQuiescence
                          "abortedSessionIds", box transport.Aborts
                          "virtualTimes", box [| 0; 10; 1000 |]
                          "trace", box trace ]
        }

    /// managed-session-lifecycle-016 already-terminal: a lifecycle-terminated attempt is
    /// permitted to abort on the Host transport, while a
    /// non-terminal non-managed attempt is still rejected with zero transport
    /// calls. The returned view contains values, never the adapter.
    let interruptTerminatedAdapterProbe () : Task<obj> =
        task {
            let terminalId = SessionId.create "term-child"
            let otherId = SessionId.create "other-root"
            let transport = ControlledOpenCodePort(terminalId, false)

            let sessions =
                InjectedSessionPort(
                    Some(transport :> IOpenCodePort),
                    ControlledEventPort() :> IEventObservationPort,
                    ?isLifecycleTerminated = Some(fun sessionId -> sessionId = terminalId)
                )
                :> ISessionHostPort

            let! terminalOutcome = sessions.InterruptAttempt terminalId
            let abortsAfterTerminal = transport.Aborts.Length
            let! otherOutcome = sessions.InterruptAttempt otherId

            let outcomeName, error =
                match terminalOutcome with
                | Ok() -> "Ok", ""
                | Error rejection -> "Error", rejection

            let otherName, otherError =
                match otherOutcome with
                | Ok() -> "Ok", ""
                | Error rejection -> "Error", rejection

            return
                createObj
                    [ "terminatedOutcome", box outcomeName
                      "terminatedError", box error
                      "abortsAfterTerminal", box abortsAfterTerminal
                      "abortedSessionIds", box transport.Aborts
                      "otherOutcome", box otherName
                      "otherError", box otherError
                      "abortCount", box transport.Aborts.Length ]
        }
