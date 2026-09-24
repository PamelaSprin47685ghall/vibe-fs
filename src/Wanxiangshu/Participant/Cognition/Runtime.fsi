namespace Wanxiangshu.Participant.Cognition

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type CommitOutcome =
    | Committed of ordinal: int64
    | Replayed of ordinal: int64
    | Rejected of reason: string

type CognitiveRuntime =
    new: port: CognitiveJournalPort -> CognitiveRuntime
    member CurrentCanvas: AssumeSnapshot
    member Serialized: ownerKey: string -> work: (unit -> Task<CommitOutcome>) -> Task<CommitOutcome>

    member Commit:
        owner: CognitiveOwner.T ->
        toolCallId: ToolCallId ->
        inputDigest: string ->
        canvasJson: string ->
        todos: (string * TodoStatus * TodoPriority) list ->
            Task<CommitOutcome>
