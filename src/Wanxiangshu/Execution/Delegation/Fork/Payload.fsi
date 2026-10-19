namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Foundation

type ForkChildAssignment =
    { Assignment: string
      Attachment: string option
      RootRequirements: string list
      Payload: string option }

type ForkChildInstructions =
    { Base: string list
      Attachment: string
      Requirements: string }

[<RequireQualifiedAccess>]
module ForkChildPayload =
    val BasePath: string
    val AttachmentPath: string
    val RequirementsPath: string
    val document: prose: ForkChildInstructions -> input: ForkChildAssignment -> LlmFacing.Document
    val render: prose: ForkChildInstructions -> input: ForkChildAssignment -> string

    val relay:
        prose: ForkChildInstructions ->
        assignment: string ->
        attachment: string option ->
        requirements: string list ->
        payload: string option ->
            string

    val relayDocument:
        prose: ForkChildInstructions ->
        assignment: string ->
        attachment: string option ->
        requirements: string list ->
        payload: string option ->
            LlmFacing.Document
