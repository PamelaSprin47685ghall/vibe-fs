namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// GLORY-029: Manager idle → phase-aware encouragement (§7.4.6).
module ManagerIdle =

    let private idleEncouragement (journal: AgentJournal option) (life: LifeProjection) =
        let preT1 =
            match journal with
            | None -> true
            | Some durable ->
                let snapshot = AgentJournal.snapshot durable

                Map.tryFind (ManagerLifeId.value life.LifeId) snapshot.AgentProjections.MagicTodo.ByLife
                |> Option.map (fun todoLife -> List.isEmpty todoLife.AcceptedOrder)
                |> Option.defaultValue true

        if preT1 then
            ManagerLifecyclePrompt.IdleEncouragementPreT1
        else
            ManagerLifecyclePrompt.IdleEncouragementPostT1

    let encourageLabor
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (life: LifeProjection)
        : Task =
        let turn = context.Turn
        let sessionKey = SessionId.value turn.SessionId

        let encouragementKey =
            sprintf "manager-idle:%s:%s" sessionKey (ProviderRunIdentity.value turn.ProviderRun)

        match context.Quiescence with
        | Some permit when not (nudgeSent.Contains encouragementKey) ->
            let idleAlreadyClaimed =
                match journal, HostSessionNudge.tryActiveProfile journal turn.SessionId with
                | Some durable, Some profile ->
                    PromptDispatcher.forJournal(durable).IdleAlreadyClaimed profile life.LifeId turn.ProviderRun
                | _ -> false

            if idleAlreadyClaimed then
                AsyncSupport.completedTask ()
            else
                nudgeSent.Add encouragementKey |> ignore

                task {
                    match!
                        HostSessionNudge.trySendIdleManagerEncouragement
                            quiescence
                            permit
                            sessionPort
                            turn.SessionId
                            (idleEncouragement journal life)
                            turn.Directory
                            journal
                            life.LifeId
                            turn.ProviderRun
                    with
                    | HostSessionNudge.IdleContinuationOutcome.Sent _
                    | HostSessionNudge.IdleContinuationOutcome.Superseded -> ()
                    | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                        eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
                }
                :> Task
        | _ -> AsyncSupport.completedTask ()
