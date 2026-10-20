namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js.OpenCode

/// The durable writer of one delivery settlement.
///
/// The js programming surface owns the narrow capability
/// (`IDeliveryWitnessWriter`) so that it never has to know the durable
/// witness fact. This adapter is where that capability meets the durable
/// side: the fence and the material are carried over as the workflow stated
/// them, the material digest is computed over the very view the witness
/// carries, and the commit is the durable journal's own commit. The journal
/// is injected — this module never reaches for a global one.
[<RequireQualifiedAccess>]
module DeliveryWitnessWriter =

    /// The durable task reference of one delivery: the same three references
    /// the js surface states, in the durable vocabulary.
    let private toTask (task: DeliveryTaskReference) : TaskDeliveryTask =
        match task with
        | DeliveryTaskReference.ChildWork handle -> TaskDeliveryTask.ChildWork handle
        | DeliveryTaskReference.Invocation invocationId -> TaskDeliveryTask.Invocation invocationId
        | DeliveryTaskReference.Lifecycle(workId, incumbencyId) ->
            TaskDeliveryTask.Lifecycle(workId, incumbencyId)

    /// The durable witness of one delivery: the js witness's fence and
    /// material, plus the digest that seals the material it states.
    let toWitness (witness: DeliveryWitness) : TaskDeliveryWitness =
        { Fence =
            { SessionId = witness.SessionId
              LogicalRunId = witness.LogicalRunId
              AuthorityRoot = witness.AuthorityRoot
              PhysicalUserMessageId = witness.PhysicalUserMessageId
              ProviderRun = witness.ProviderRun
              ToolCallId = witness.ToolCallId
              Task = toTask witness.Task }
          FormalText = witness.FormalText
          ViewDeclaration = witness.ViewDeclaration
          View = witness.View
          MaterialDigest = HostDigest.sha256Hex witness.View
          RetirementId = witness.RetirementId }

    /// The writer the settle point uses: it commits the durable witness of
    /// the delivery. A refused commit is reported as a refused delivery — the
    /// caller learns that the delivery is not established.
    let create (journal: AgentJournal) : IDeliveryWitnessWriter =
        { new IDeliveryWitnessWriter with
            member _.CommitDeliveryWitness(witness: DeliveryWitness) : Task<Result<unit, string>> =
                TaskDeliveryJournal.commit journal (toWitness witness) }
