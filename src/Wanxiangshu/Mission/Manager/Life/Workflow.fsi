namespace Wanxiangshu.Mission.Manager.Life

open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type BlessedLifeCompletion =
    | AlreadyCompleted
    | Completed of authorityRoot: AuthorityRootUserMessageId

/// Durable Manager Life transitions that must not be owned by a tool adapter.
module ManagerLifeWorkflow =

    /// HumanRoot Birth / Reawakening: WriteBlob → LifeOpened.
    val ensureOpening:
        journal: AgentJournal ->
        sessionId: SessionId ->
        lifeId: ManagerLifeId ->
        openingUserMessageId: PhysicalUserMessageId ->
        rawText: string ->
        openingCursor: XTraceCursor ->
            Task<Result<unit, string>>

    /// GLORY-069 HumanRoot upgrade: WriteBlob → LifeOpened.
    /// Opening floor is derived from LifeOpened / XTrace / WorkRecordStart (TODO-001).
    val ensureMigrated:
        journal: AgentJournal ->
        sessionId: SessionId ->
        lifeId: ManagerLifeId ->
        openingUserMessageId: PhysicalUserMessageId ->
        assignmentText: string ->
            Task<Result<unit, string>>

    /// FINALITY-022 admission owner for ending-time Life lookup.
    /// Existing Life wins; an AgentOwnerRoot may materialize exactly one migration
    /// Life before any completed-Life history exists; otherwise no Life is admitted.
    val ensureEndingLife: journal: AgentJournal -> sessionId: SessionId -> Task<Result<LifeProjection option, string>>

    /// GLORY-062 durable half of the second suicide. Physical terminal publish is
    /// deliberately returned to Infrastructure as a capability effect.
    val completeBlessedLife:
        journal: AgentJournal ->
        sessionId: SessionId ->
        life: LifeProjection ->
        blessing: BlessingEvidence ->
        lastWords: string ->
        providerRun: ProviderRunIdentity ->
            Task<Result<BlessedLifeCompletion, string>>
