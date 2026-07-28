namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module HostSessionNudge =

    let private runtime (journal: AgentJournal option) =
        match journal with
        | Some j -> PromptDispatcher.forJournal j
        | None -> PromptDispatcher.ephemeral ()

    let tryActiveProfile (journal: AgentJournal option) (sessionId: SessionId) =
        match journal with
        | None -> None
        | Some j ->
            let snapshot = AgentJournal.snapshot j
            PromptAuthorityLedger.activeProfile sessionId snapshot.AgentProjections

    let private effectiveAgent
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        =
        match journal with
        | None -> profile.SelectedAgent
        | Some j ->
            let cursor = DurableFallback.currentState sessionId (AgentJournal.snapshot j)
            PromptAuthority.effectiveAgentAt profile cursor.Offset

    /// Reconciled linked children have a host-proven root user message even when
    /// the host omitted agent metadata from `chat.message`. Register that real
    /// AgentOwner authority once; never use this for an unlinked/unknown session.
    /// `agent` must be a Managed Agent name (fast-* / deep-*).
    let ensureAgentOwnerAuthority
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (rootUserMessageId: MessageId)
        (agent: string)
        =
        match tryActiveProfile journal sessionId with
        | Some _ -> ()
        | None ->
            let rt = runtime journal

            match
                PromptAuthorityRun.createAuthorityRoot
                    PromptAuthority.sha256Hex
                    rt.RuntimeId
                    sessionId
                    PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                    rootUserMessageId
                    agent
            with
            | Ok profile -> rt.RegisterAuthority profile
            | Error _ -> ()

    let sendContinuationResult
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (onAccepted: (MessageId -> unit) option)
        =
        task {
            match tryActiveProfile journal sessionId with
            | None -> return Error "No active authority profile"
            | Some profile ->
                let agent = effectiveAgent journal sessionId profile
                let rt = runtime journal

                return! rt.SendContinuation sessionPort sessionId prompt kind profile agent directory onAccepted
        }

    /// Sends a continuation only when a durable Authority Root exists. Unknown
    /// physical user messages fail closed rather than manufacturing a new root.
    let sendContinuation
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (kind: PromptAuthority.ContinuationKind)
        (directory: string option)
        (journal: AgentJournal option)
        (onAccepted: (MessageId -> unit) option)
        =
        sendContinuationResult sessionPort sessionId prompt kind directory journal onAccepted
        |> ignore

    let trySendInteractionRepair
        (sessionPort: ISessionHostPort)
        (sessionId: SessionId)
        (prompt: string)
        (directory: string option)
        (journal: AgentJournal option)
        (terminalAssistantMessageId: MessageId)
        (repairKind: string)
        (onAccepted: (MessageId -> unit) option)
        : bool =
        match tryActiveProfile journal sessionId with
        | None -> false
        | Some profile ->
            let rt = runtime journal

            if not (rt.TryClaimInteractionRepair profile terminalAssistantMessageId repairKind) then
                false
            else
                let agent = effectiveAgent journal sessionId profile

                task {
                    let! _ =
                        rt.SendContinuation
                            sessionPort
                            sessionId
                            prompt
                            PromptAuthority.InteractionRepair
                            profile
                            agent
                            directory
                            onAccepted

                    ()
                }
                |> ignore

                true
