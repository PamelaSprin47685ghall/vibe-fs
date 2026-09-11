namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// DELEG-029: The delegation subsystem declares this capability port while
/// durable composition implements it over the concrete journal. Host runtime,
/// recovery, and fold consume only this port.
type AgentJournalPort =
    {
        /// Append one execution fact case to a parent session stream.
        AppendExecutionFact: SessionId -> ExecutionFactCases -> Task<Result<unit, string>>
        /// Read the session's handle-linkage projection.
        HandleProjection: SessionId -> AgentLinkageProjection
        /// Read a completion blob body.
        ReadBlob: BlobRef -> Task<Result<string, string>>
        /// Write a completion blob body; PERSIST-007 ordering (blob before fact) is preserved by the caller.
        WriteBlob: string -> Task<Result<BlobRef * BlobDigest, string>>
    }
