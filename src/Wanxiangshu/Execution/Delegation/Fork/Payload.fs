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

    let BasePath = "delegation/fork-child-base"
    let RequirementsPath = "delegation/fork-child-requirements"

    /// Clean break: the child receives ONLY its current charge and current
    /// requirements. There is no data plane to smuggle caller history into.
    let document (prose: ForkChildInstructions) (input: ForkChildAssignment) : LlmFacing.Document =
        let assignment = input.Assignment.Trim()

        let requirements =
            input.RootRequirements |> List.filter (System.String.IsNullOrWhiteSpace >> not)

        let instructions =
            (if System.String.IsNullOrWhiteSpace assignment then
                 []
             else
                 [ assignment ])
            @ prose.Base
            @ (if List.isEmpty requirements then
                   []
               else
                   prose.Requirements :: requirements)

        LlmFacing.instructions instructions

    let render (prose: ForkChildInstructions) (input: ForkChildAssignment) : string =
        document prose input |> LlmFacing.render

    let relay (prose: ForkChildInstructions) (assignment: string) (requirements: string list) : string =
        render
            prose
            { Assignment = assignment
              RootRequirements = requirements }

    let relayDocument
        (prose: ForkChildInstructions)
        (assignment: string)
        (requirements: string list)
        : LlmFacing.Document =
        document
            prose
            { Assignment = assignment
              RootRequirements = requirements }
