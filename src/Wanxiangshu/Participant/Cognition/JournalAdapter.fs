namespace Wanxiangshu.Participant.Cognition

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// The one place that turns an `AgentJournal` into the narrow cognitive capability.
///
/// Composition owns this adapter so the domain stays ignorant of the journal object:
/// the cognitive owner asks for a blob write, an append and a projection read, and
/// gets nothing else. Keeping the surface this small is what stops the workspace from
/// quietly becoming a second durable subsystem.
[<RequireQualifiedAccess>]
module CognitiveJournalAdapter =

    /// Build the port around a live journal. Returns `empty` when there is none, so
    /// a plugin instance without durability refuses commits honestly instead of
    /// pretending an in-memory canvas is durable state.
    let port (journal: AgentJournal option) : CognitiveJournalPort =
        match journal with
        | None -> CognitiveJournalPort.empty
        | Some durable ->
            /// The pair the commit fact carries, taken from a stored receipt.
            let blobPair (receipt: BlobWriteReceipt) = (receipt.BlobRef, receipt.BlobDigest)

            /// Blob identity as the pair the commit fact carries, or the refusal.
            let writeBlob (content: string) =
                task {
                    let! result = AgentJournal.writeBlob content durable
                    return Result.map blobPair result
                }

            /// The fact for one committed phase, bound to its own stream.
            let factOf (commit: AssumePhaseCommitted) =
                AgentFact.Cognition(AssumeFactCases.T.AssumePhaseCommitted commit)

            /// Append the committed phase. The blob already exists, so a refusal here is
            /// an append failure and never a missing payload.
            let appendCommit (commit: AssumePhaseCommitted) =
                task {
                    let! result =
                        AgentJournal.appendAgent (StreamId.Session commit.SessionId) None (factOf commit) durable

                    return Ok()
                }

            let readProjection (ownerKey: string) =
                let projections = (AgentJournal.snapshot durable).AgentProjections

                Map.tryFind ownerKey projections.Cognition

            /// The snapshot bytes a committed phase points at, read through the
            /// journal's own blob capability so the workspace never holds a second
            /// durable truth and a restart recovers exactly what was committed.
            let readBlob (blobRef: BlobRef) = durable.Writer.BlobWriter.Read blobRef

            { WriteBlob = writeBlob
              AppendCommit = appendCommit
              ReadProjection = readProjection
              ReadBlob = readBlob }
