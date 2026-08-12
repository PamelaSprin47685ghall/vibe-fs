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

/// The first prompt of a forked child, as one ARCH-010 payload.
[<RequireQualifiedAccess>]
module ForkChildPayload =

    /// The one unconditional instruction every forked child receives: how to close.
    ///
    /// GrandRewrite §3.2.2 — constrain the honesty of the content, not the skeleton of the
    /// account. The closing report is prose testimony, never a universal field list; the
    /// retired form (`result, files changed, tests run, evidence, remaining risks, blockers`)
    /// made every child fill a status DTO instead of testifying about its own work.
    let BaseInstructions =
        [ "When your charge is complete, leave an ordinary closing report in natural prose."
          ""
          "Tell your Commissioner what became true, what evidence materially supports that account, and what remains unresolved when something genuinely remains."
          ""
          "Do not force the report into a universal field list."
          "Do not omit an important fact merely because no predefined field asks for it."
          ""
          "The closing report is testimony about the work, not a serialized status object." ]

    /// Emitted only alongside a commissioner record field.
    let CommissionerRecordInstruction =
        "The record below belongs to your Commissioner. It is their history, not yours. Read it for context and evidence. Unfinished work in that record does not become yours merely because you can see it. Your charge tells you what is yours to carry."

    /// REVIEW-002. Emitted only alongside `[[root_requirement]]` entries.
    let RequirementsInstruction =
        "The `root_requirement` entries are the authoritative review scope: verified HumanRoot "
        + "prompts received since the prior review completed its double-PERFECT barrier and reached terminal "
        + "idle. Verify every applicable requirement. The charge is supplementary and must not narrow or "
        + "override that scope."

    let render (input: ForkChildAssignment) : string =
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
            @ BaseInstructions
            @ (match commissionerRecord with
               | Some record -> [ CommissionerRecordInstruction ] @ (record.Split('\n') |> Array.toList)
               | None -> [])
            @ (if List.isEmpty requirements then
                   []
               else
                   [ RequirementsInstruction ])

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
        (assignment: string)
        (commissionerRecord: string option)
        (requirements: string list)
        (payload: string option)
        : string =
        render
            { Assignment = assignment
              CommissionerRecord = commissionerRecord
              RootRequirements = requirements
              Payload = payload }
