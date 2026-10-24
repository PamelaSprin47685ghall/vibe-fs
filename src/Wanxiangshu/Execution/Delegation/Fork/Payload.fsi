namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Foundation

type ForkChildAssignment =
    { Assignment: string
      RootRequirements: string list }

type ForkChildInstructions =
    { Base: string list
      Requirements: string }

[<RequireQualifiedAccess>]
module ForkChildPayload =
    val BasePath: string
    val RequirementsPath: string
    val document: prose: ForkChildInstructions -> input: ForkChildAssignment -> LlmFacing.Document
    val render: prose: ForkChildInstructions -> input: ForkChildAssignment -> string
    val relay:
        prose: ForkChildInstructions ->
        assignment: string ->
        requirements: string list ->
            string
    val relayDocument:
        prose: ForkChildInstructions ->
        assignment: string ->
        requirements: string list ->
            LlmFacing.Document
