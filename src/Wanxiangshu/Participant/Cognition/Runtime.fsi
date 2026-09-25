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

    /// The owner's current canvas, restored from the owner's committed facts when
    /// this process holds none. A projection that claims a snapshot whose blob is
    /// missing or unparsable fails closed instead of answering an empty canvas.
    member CurrentCanvas: owner: CognitiveOwner.T -> Task<Result<AssumeSnapshot, string>>

    member Serialized: ownerKey: string -> work: (unit -> Task<'Result>) -> Task<'Result>

    /// One committed phase for one owner: read the current canvas, run `transform`
    /// over it, and persist — all inside this owner's serial domain, so the next
    /// call's transform sees this call's committed canvas.
    member RunPhase:
        owner: CognitiveOwner.T ->
        toolCallId: ToolCallId ->
        inputDigest: string ->
        todos: (string * TodoStatus * TodoPriority) list ->
        transform: (string -> Task<Result<string, string>>) ->
            Task<CommitOutcome * string>
