namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Busy-agent nudge via PromptDispatcher (KISS-N12 Continuation).
module HostForkBusyNudge =

    /// Continuation of the child's active Logical Run. Never creates a new
    /// Authority Root / RunId / completion.
    let send
        (sessions: ISessionHostPort)
        (parentId: SessionId)
        (journal: AgentJournal option)
        (childId: SessionId)
        (_role: AgentRole)
        (_agent: string)
        (directory: string option)
        (prompt: string)
        : Task<Result<unit, string>> =
        task {
            match journal with
            | None ->
                return!
                    sessions.SendChildPromptFireAndForget(
                        parentId,
                        childId,
                        prompt,
                        { Model = None
                          Agent = None
                          Directory = directory
                          Metadata = None }
                    )
            | Some j ->
                let snapshot = AgentJournal.snapshot j

                match PromptAuthorityLedger.activeProfile childId snapshot.AgentProjections with
                | None -> return Error "Busy nudge requires ActiveLogicalRun on child session"
                | Some profile ->
                    let cursor = DurableFallback.currentState childId snapshot
                    let effectiveAgent = PromptAuthority.effectiveAgentAt profile cursor.Offset
                    let rt = PromptDispatcher.forJournal j

                    let! sent =
                        rt.SendContinuation
                            sessions
                            childId
                            prompt
                            PromptAuthority.ContinuationKind.BusyAgentNudge
                            profile
                            effectiveAgent
                            directory
                            None

                    match sent with
                    | Ok _ -> return Ok()
                    | Error err -> return Error err
        }

    let sender sessions parentId journal directoryOf =
        fun agentId childId (role: AgentRole) agent prompt ->
            send sessions parentId journal childId role agent (directoryOf agentId) prompt
