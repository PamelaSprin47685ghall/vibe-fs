namespace Wanxiangshu.Domain

/// What a newly forked child is told, before it is rendered (ARCH-010, N3).
///
/// Typed rather than pre-composed text, because ARCH-010 requires the instruction/data split to be
/// decided by the producer and not inferred by a renderer: `Assignment` is the task, the other two
/// are context the child may read but must not mistake for the task.
type ForkChildAssignment =
    {
        /// The manager's request. Always present: a fork with no assignment is not a fork.
        Assignment: string
        /// Commissioner background for the child. Absent for a commissioner that has produced none yet.
        CommissionerRecord: string option
        /// REVIEW-002 authoritative scope: the HumanRoot prompts received since the previous review
        /// reached its double-PERFECT barrier. Empty for every non-Reviewer fork.
        RootRequirements: string list
        /// ARCH-010: machine-readable data that the child may read but must not mistake for the task.
        Payload: string option
    }

/// Already-localized fork-child instruction fragments (PROMPT-019).
type ForkChildInstructions =
    { Base: string list
      CommissionerRecord: string
      Requirements: string }

/// The first prompt of a forked child, as one ARCH-010 payload.
[<RequireQualifiedAccess>]
module ForkChildPayload =

    let BasePath = "delegation/fork-child-base"
    let CommissionerRecordPath = "delegation/fork-child-commissioner-record"
    let RequirementsPath = "delegation/fork-child-requirements"

    let render (prose: ForkChildInstructions) (input: ForkChildAssignment) : string =
        let requirements =
            input.RootRequirements
            |> List.filter (fun text -> not (System.String.IsNullOrWhiteSpace text))

        let commissionerRecord =
            input.CommissionerRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (fun record -> record <> "")

        let assignmentText = input.Assignment.Trim()

        let instructions =
            (if System.String.IsNullOrWhiteSpace assignmentText then
                 []
             else
                 [ assignmentText ])
            @ prose.Base
            @ (match commissionerRecord with
               | Some record -> [ prose.CommissionerRecord ] @ (record.Split('\n') |> Array.toList)
               | None -> [])
            @ (if List.isEmpty requirements then
                   []
               else
                   [ prose.Requirements ])

        let body =
            (match input.Payload with
             | Some payload when not (System.String.IsNullOrWhiteSpace payload) ->
                 [ SyntheticToml.field "content" (SyntheticToml.renderString payload) ]
             | _ -> [])
            @ (requirements
               |> List.mapi (fun index text ->
                   SyntheticToml.tableArrayEntry
                       "root_requirement"
                       [ SyntheticToml.field "ordinal" (string (index + 1))
                         SyntheticToml.field "text" (SyntheticToml.renderString text) ]))

        SyntheticToml.document instructions body

    /// The positional form, for a call site that reads better without a record literal.
    let relay
        (prose: ForkChildInstructions)
        (assignment: string)
        (commissionerRecord: string option)
        (requirements: string list)
        (payload: string option)
        : string =
        render
            prose
            { Assignment = assignment
              CommissionerRecord = commissionerRecord
              RootRequirements = requirements
              Payload = payload }
