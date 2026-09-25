namespace Wanxiangshu.Participant.Cognition

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module RuntimeSurface =
    val CognitiveOwner_key: owner: obj -> string
    val CognitiveRuntime_create: port: obj -> obj
    val CognitiveRuntime_commit: runtime: obj -> owner: obj -> call: obj -> Task<obj>
    val CognitiveRuntime_currentCanvas: runtime: obj -> owner: obj -> Task<obj>
