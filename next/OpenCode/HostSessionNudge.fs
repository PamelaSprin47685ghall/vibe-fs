namespace Wanxiangshu.Next.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Host
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

    /// Look up the agent the current cursor selects.
    ///
    /// Named for the lookup, not the algorithm: FALLBACK-002's side selection is
    /// owned by AgentPairCursor, and this only fetches the cursor and asks. A
    /// second `effectiveAgent` here would read as a competing implementation.
    ///
    /// No cursor means no accepted Authority Root (FALLBACK-001), so there is no
    /// fallback state to consult and SelectedAgent is the only defensible answer.
    let private agentForActiveCursor
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (profile: PromptAuthority.AuthorityExecutionProfile)
        =
        journal
        |> Option.bind (fun j -> DurableFallback.tryCurrentCursor sessionId (AgentJournal.snapshot j))
        |> Option.map (PromptAuthority.effectiveAgentFor profile)
        |> Option.defaultValue profile.SelectedAgent

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
                    HostDigest.sha256Hex
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
                let agent = agentForActiveCursor journal sessionId profile
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
                let agent = agentForActiveCursor journal sessionId profile

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
