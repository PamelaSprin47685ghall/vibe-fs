namespace Wanxiangshu.Strength.Replica

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength

[<RequireQualifiedAccess>]
type StrengthReplicaTransformOutcome =
    | NotReplica
    | Ready of completedBatches: StrengthRequestBatch list
    | Retired of reason: string * completedBatches: StrengthRequestBatch list

[<RequireQualifiedAccess>]
module StrengthReplicaTransform =
    val tryApplyRenderedMessages:
        sessionId: string -> sha256: (string -> string) -> rendered: RenderedMessages -> Result<obj list, string>

    val tryEncodeOwnerMessages: sha256: (string -> string) -> rawMessages: obj list -> Result<obj list, string>

    val apply:
        sha256: (string -> string) ->
        runtime: StrengthRuntime ->
        sessions: ISessionHostPort ->
        output: obj ->
        outboundRequest: bool ->
            Task<StrengthReplicaTransformOutcome>
