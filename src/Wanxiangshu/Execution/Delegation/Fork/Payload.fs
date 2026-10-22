namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Foundation

type ForkChildAssignment =
    { Assignment: string
      CommissionerRecord: string list
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

    let BasePath = "delegation/fork-child-base"
    let CommissionerRecordPath = "delegation/fork-child-commissioner-record"
    let AttachmentPath = "delegation/fork-child-attachment"
    let RequirementsPath = "delegation/fork-child-requirements"

    let document (prose: ForkChildInstructions) (input: ForkChildAssignment) : LlmFacing.Document =
        let requirements =
            input.RootRequirements
            |> List.filter (fun text -> not (System.String.IsNullOrWhiteSpace text))

        let attachment =
            input.Attachment |> Option.filter (System.String.IsNullOrWhiteSpace >> not)

        let assignmentText = input.Assignment.Trim()

        let instructions =
            (if System.String.IsNullOrWhiteSpace assignmentText then
                 []
             else
                 [ assignmentText ])
            @ prose.Base
            @ (if List.isEmpty input.CommissionerRecord then
                   []
               else
                   [ prose.CommissionerRecord ])
            @ (match attachment with
               | Some _ -> [ prose.Attachment ]
               | None -> [])
            @ (if List.isEmpty requirements then
                   []
               else
                   prose.Requirements :: requirements)

        let body =
            (match input.Payload with
             | Some payload when not (System.String.IsNullOrWhiteSpace payload) ->
                 [ LlmFacing.Data.stringField "content" payload ]
             | _ -> [])
            @ (match attachment with
               | Some record -> [ LlmFacing.Data.stringField "attached_view" record ]
               | None -> [])
            @ LlmFacing.Data.textTables "context" input.CommissionerRecord

        LlmFacing.instructions instructions |> LlmFacing.withData body

    let render (prose: ForkChildInstructions) (input: ForkChildAssignment) : string =
        document prose input |> LlmFacing.render

    let relay
        (prose: ForkChildInstructions)
        (assignment: string)
        (commissionerRecord: string list)
        (attachment: string option)
        (requirements: string list)
        (payload: string option)
        : string =
        render
            prose
            { Assignment = assignment
              CommissionerRecord = commissionerRecord
              Attachment = attachment
              RootRequirements = requirements
              Payload = payload }

    let relayDocument
        (prose: ForkChildInstructions)
        (assignment: string)
        (commissionerRecord: string list)
        (attachment: string option)
        (requirements: string list)
        (payload: string option)
        : LlmFacing.Document =
        document
            prose
            { Assignment = assignment
              CommissionerRecord = commissionerRecord
              Attachment = attachment
              RootRequirements = requirements
              Payload = payload }
