namespace Wanxiangshu.Next.Journal

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome

type JournalAppendFailure =
    { EventId: EventId
      Failure: JournalFailure }

type AgentJournal internal (writer: JournalWriter, initialProjection: ProjectionSet) =
    let gate = obj ()
    let mutable proj = initialProjection

    /// Durable dedupe identity for a fallback failure. Matches the identity the
    /// fold stores in the per-session bounded `RecentFailureIds`, so dedupe is
    /// O(sessions), not O(history).
    let fallbackIdentity (logicalRunId: string) (authorityRootUserMessageId: string) (providerAttempt: string) =
        sprintf "%s|%s|%s" logicalRunId authorityRootUserMessageId providerAttempt

    member _.Writer = writer
    member _.RuntimeId = writer.RuntimeId
    member _.IsPoisoned = writer.IsPoisoned

    member _.Snapshot: ProjectionSet = lock gate (fun () -> proj)

    member _.AppendAgent
        (stream: StreamId)
        (turnId: TurnId option)
        (fact: AgentFact)
        : Result<ProjectionSet, JournalAppendFailure> =
        lock gate (fun () ->
            // Fallback append boundary: dedupe only. 0.5.0 never refuses for Dead.
            let refuseFallback =
                match fact with
                | AgentFact.FallbackFailureRecorded p ->
                    match Map.tryFind p.SessionId proj.AgentProjections.Sessions with
                    | Some { Fallback = Some fb } ->
                        List.contains
                            (fallbackIdentity p.LogicalRunId p.AuthorityRootUserMessageId p.ProviderAttempt)
                            fb.RecentFailureIds
                    | _ -> false
                | _ -> false

            if refuseFallback then
                // Duplicate identity: idempotent no-op, no new envelope.
                Ok proj
            else
                match writer.Append stream turnId (Fact.Agent fact) with
                | Committed env ->
                    let updated = Fold.foldEnvelope proj env
                    proj <- updated
                    Ok updated
                | CommitUnknown(eventId, failure) -> Error { EventId = eventId; Failure = failure })

    interface IDisposable with
        member _.Dispose() = (writer :> IDisposable).Dispose()

    interface IAsyncDisposable with
        member _.DisposeAsync() =
            (writer :> IAsyncDisposable).DisposeAsync()

module AgentJournal =

    /// Single durable failure identity format shared by append boundary, fold,
    /// and FallbackDetect in-memory dedupe.
    let fallbackIdentity (logicalRunId: string) (authorityRootUserMessageId: string) (providerAttempt: string) =
        sprintf "%s|%s|%s" logicalRunId authorityRootUserMessageId providerAttempt

    let createFromProjection
        (directory: string)
        (runtimeId: RuntimeId)
        (processId: int)
        (startedAt: DateTimeOffset)
        (projection: ProjectionSet)
        : AgentJournal =
        let writer, initEnv = JournalWriter.create directory runtimeId processId startedAt
        let initialProj = Fold.foldEnvelope projection initEnv
        new AgentJournal(writer, initialProj)

    let createFromBoot
        (directory: string)
        (runtimeId: RuntimeId)
        (processId: int)
        (startedAt: DateTimeOffset)
        (boot: BootSnapshot)
        : AgentJournal =
        let projection = Fold.apply Fold.empty boot.Envelopes
        let writer, initEnv = JournalWriter.create directory runtimeId processId startedAt
        let initialProj = Fold.foldEnvelope projection initEnv
        new AgentJournal(writer, initialProj)

    let create (directory: string) (runtimeId: RuntimeId) (processId: int) (startedAt: DateTimeOffset) : AgentJournal =
        createFromProjection directory runtimeId processId startedAt Fold.empty

    let appendAgent
        (stream: StreamId)
        (turnId: TurnId option)
        (fact: AgentFact)
        (journal: AgentJournal)
        : Result<ProjectionSet, JournalAppendFailure> =
        journal.AppendAgent stream turnId fact

    let snapshot (journal: AgentJournal) : ProjectionSet = journal.Snapshot

    let runtimeId (journal: AgentJournal) : RuntimeId = journal.RuntimeId

    let isPoisoned (journal: AgentJournal) : bool = journal.IsPoisoned

    /// User requirements belong to the logical session family root, not the
    /// reviewer child that happens to consume them.
    let reviewRequirementScope (journal: AgentJournal option) (sessionId: SessionId) : SessionId =
        let parentOf (sessions: Map<SessionId, SessionAgentProjection>) childId =
            let child = ChildId.create (SessionId.value childId)

            sessions
            |> Map.tryPick (fun parentId session ->
                session.Linkage
                |> Option.bind (fun linkage ->
                    if Map.containsKey child linkage.LinkedChildren then Some parentId else None))

        let rec root sessions seen current =
            if Set.contains current seen then
                current
            else
                match parentOf sessions current with
                | Some parent -> root sessions (Set.add current seen) parent
                | None -> current

        match journal with
        | None -> sessionId
        | Some value ->
            let sessions = (snapshot value).AgentProjections.Sessions
            root sessions Set.empty sessionId

    let pendingReviewRequirements
        (journal: AgentJournal option)
        (sessionId: SessionId)
        : ReviewRequirementInput list =
        match journal with
        | None -> []
        | Some value ->
            let scope = reviewRequirementScope journal sessionId

            (snapshot value).AgentProjections.Sessions
            |> Map.tryFind scope
            |> Option.bind (fun session -> session.ReviewRequirements)
            |> Option.map (fun requirements -> requirements.HumanPromptInputs)
            |> Option.defaultValue []


    let recordHumanPromptAccepted
        (journal: AgentJournal)
        (sessionId: SessionId)
        (messageId: MessageId)
        : Result<unit, string> =
        let scope = reviewRequirementScope (Some journal) sessionId

        let input =
            { SourceSessionId = sessionId
              MessageId = messageId }

        if List.contains input (pendingReviewRequirements (Some journal) scope) then
            Ok()
        else
            appendAgent
                (StreamId.Session scope)
                (Some(TurnId.ofMessageId messageId))
                (AgentFact.HumanPromptAccepted
                    {| SessionId = scope
                       SourceSessionId = sessionId
                       MessageId = MessageId.value messageId |})
                journal
            |> Result.map (fun _ -> ())
            |> Result.mapError (fun failure -> sprintf "%A" failure.Failure)

    let recordReviewConfirmedIdle
        (journal: AgentJournal)
        (reviewOwnerSessionId: SessionId)
        (reviewerSessionId: SessionId)
        (assistantMessageId: MessageId)
        : Result<unit, string> =
        let scope = reviewRequirementScope (Some journal) reviewOwnerSessionId

        let alreadyRecorded =
            (snapshot journal).AgentProjections.Sessions
            |> Map.tryFind scope
            |> Option.bind (fun session -> session.ReviewRequirements)
            |> Option.bind (fun requirements -> requirements.LastConfirmedIdleAssistantMessageId)
            |> Option.exists ((=) assistantMessageId)

        if alreadyRecorded then
            Ok()
        else
            appendAgent
                (StreamId.Session scope)
                None
                (AgentFact.ReviewConfirmedIdle
                    {| SessionId = scope
                       ReviewerSessionId = reviewerSessionId
                       AssistantMessageId = MessageId.value assistantMessageId |})
                journal
            |> Result.map (fun _ -> ())
            |> Result.mapError (fun failure -> sprintf "%A" failure.Failure)
