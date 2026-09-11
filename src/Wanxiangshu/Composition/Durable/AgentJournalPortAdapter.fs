namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Host

module AgentJournalPortAdapter =
    /// DELEG-029: durable composition is the only place that wraps delegation fact
    /// cases into the outer routing union and adapts the journal handle.
    let fromAgentJournal (journal: AgentJournal) : AgentJournalPort =
        { AppendExecutionFact =
            fun sessionId fact ->
                task {
                    match!
                        AgentJournal.appendAgent (StreamId.Session sessionId) None (AgentFact.Execution fact) journal
                    with
                    | Ok _ -> return Ok()
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                }
          HandleProjection = fun sessionId -> AgentJournal.handleProjection journal sessionId
          ReadBlob = fun blobRef -> journal.Writer.BlobWriter.Read blobRef
          WriteBlob =
            fun content ->
                task {
                    match! journal.WriteBlob content with
                    | Ok receipt -> return Ok(receipt.BlobRef, receipt.BlobDigest)
                    | Error err -> return Error err
                }
          Sha256 = HostDigest.sha256Hex }
