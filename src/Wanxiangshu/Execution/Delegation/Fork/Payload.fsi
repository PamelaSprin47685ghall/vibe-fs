namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Foundation

type ForkChildAssignment =
    { Assignment: string
      CommissionerRecord: string option
      Attachment: string option
      RootRequirements: string list
      Payload: string option }

type ForkChildInstructions =
    { Base: string list
      CommissionerRecord: string
      Attachment: string
      Requirements: string }

[<RequireQualifiedAccess>]
module ForkChildPayload =
    val BasePath: string
    val CommissionerRecordPath: string
    val AttachmentPath: string
    val RequirementsPath: string
    val document: prose: ForkChildInstructions -> input: ForkChildAssignment -> LlmFacing.Document
    val render: prose: ForkChildInstructions -> input: ForkChildAssignment -> string

    val relay:
        prose: ForkChildInstructions ->
        assignment: string ->
        commissionerRecord: string option ->
        attachment: string option ->
        requirements: string list ->
        payload: string option ->
            string

    val relayDocument:
        prose: ForkChildInstructions ->
        assignment: string ->
        commissionerRecord: string option ->
        attachment: string option ->
        requirements: string list ->
        payload: string option ->
            LlmFacing.Document
