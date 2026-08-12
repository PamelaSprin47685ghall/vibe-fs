namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Session

/// EXEC-016: join-capable roles must join outstanding work before terminal idle.
module HostJoinGuard =

    [<RequireQualifiedAccess>]
    type JoinGuardNudgeOutcome =
        | Sent of PromptKey
        | AlreadyOutstanding
        | Failed of reason: string

    let private processNudgeKeys = HashSet<string>()

    let private hasOutstandingJoinClaim (journal: AgentJournal) (targetSessionId: SessionId) =
        AgentProjection.tryFind targetSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.map (fun authority ->
            authority.PendingClaims
            |> Map.exists (fun _ claim ->
                claim.Origin = PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.JoinGuard))
        |> Option.defaultValue false

    let private nudgeKey (runtimeId: RuntimeId) (targetSessionId: SessionId) =
        sprintf "join-guard:%s:%s" (RuntimeId.value runtimeId) (SessionId.value targetSessionId)

    /// Send JoinGuard Continuation. Dedupes on durable PendingClaims + process key.
    let nudge
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (nudgeKeys: HashSet<string>)
        (sessionId: SessionId)
        (directory: string option)
        : Task<JoinGuardNudgeOutcome> =
        task {
            match journal with
            | None -> return JoinGuardNudgeOutcome.Failed "Join guard nudge requires an AgentJournal"
            | Some durable ->
                let key = nudgeKey (AgentJournal.runtimeId durable) sessionId

                let reserved =
                    lock processNudgeKeys (fun () ->
                        if
                            hasOutstandingJoinClaim durable sessionId
                            || nudgeKeys.Contains key
                            || processNudgeKeys.Contains key
                        then
                            false
                        else
                            nudgeKeys.Add key |> ignore
                            processNudgeKeys.Add key |> ignore
                            true)

                if not reserved then
                    return JoinGuardNudgeOutcome.AlreadyOutstanding
                else
                    let releaseKey () =
                        lock processNudgeKeys (fun () ->
                            nudgeKeys.Remove key |> ignore
                            processNudgeKeys.Remove key |> ignore)

                    let! sent =
                        HostSessionNudge.sendContinuation
                            sessionPort
                            sessionId
                            (ProviderProse.documentFor sessionId RuntimeNudge.BackgroundJoin Map.empty)
                            PromptAuthority.ContinuationKind.JoinGuard
                            directory
                            (Some durable)

                    match sent with
                    | Ok promptKey -> return JoinGuardNudgeOutcome.Sent promptKey
                    | Error error ->
                        releaseKey ()
                        return JoinGuardNudgeOutcome.Failed error
        }
