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
        /// COMPANION background for the child. Absent for a parent that has produced none yet.
        ParentWorkRecord: string option
        /// REVIEW-002 authoritative scope: the HumanRoot prompts received since the previous review
        /// reached its double-PERFECT barrier. Empty for every non-Reviewer fork.
        OriginalUserRequirements: string list
        /// ARCH-010: machine-readable data that the child may read but must not mistake for the task.
        Payload: string option
        /// PENDING 7: Coder TDD phase when Manager `fork` / named coder supplied `tdd`.
        /// Absent for non-Coder forks and for callers that never set a phase.
        TddPhase: TddPhase option
    }

/// The first prompt of a forked child, as one ARCH-010 payload.
///
/// ── why this exists at all ──────────────────────────────────────────────────
///
/// It replaces two independently-composed, independently-CONDITIONAL envelopes:
///
///   `HostForkRuntimeFork.fs:196`  wrapped the assignment when a parent work record existed, and
///                                sent the bare assignment when it did not
///   `HostForkRuntimeFork.fs:98`   wrapped it again when review requirements existed
///
/// Both were `sprintf` templates whose presence depended on runtime state, so the child's first
/// prompt had FOUR possible shapes with no common prefix. That is measurable damage rather than an
/// aesthetic complaint: a canary declares the text a lane expects, and a declaration cannot match a
/// prefix that is sometimes absent. It is the shared root cause of the currently red canaries, and
/// the reason the reviewer half already needed ordered-fragment declarations in seven scenarios.
///
/// ARCH-010 fixes it structurally, not by picking one of the four. Instruction comments come FIRST:
/// the `Assignment` block leads the header, then the unconditional report format, then the
/// interpretive context lines when present. The optional parts became optional FIELDS, which
/// 「省略不存在的可选字段」 permits and which a fragment declaration can skip over.
///
/// ── one payload, not two nested ones ────────────────────────────────────────
///
/// Composing them separately would put a rendered TOML document inside the `assignment` value of
/// another. Legal under data containment, and wrong: the notation would appear twice, the model
/// would have to unwrap it, and the inner document's own instructions would sit below the outer
/// document's data — the ordering ARCH-010 exists to forbid. One payload with a minimal local schema
/// is what 「不引入统一 envelope」 asks for.
[<RequireQualifiedAccess>]
module ForkChildPayload =

    /// The one unconditional instruction every forked child receives: the report shape.
    ///
    /// Unconditional, and that is the whole fix. The previous envelope carried the report format only
    /// when a parent work record happened to exist, so whether a child owed a structured report
    /// depended on unrelated state. The task itself is the `Assignment` comment block, which precedes
    /// this line in the header; the report shape is true of every fork.
    let BaseInstructions =
        [ "Report back with exactly these fields: result, files changed, tests run, evidence, remaining risks, blockers." ]

    /// Emitted only alongside a `parent_work_record` field, because an instruction about absent data
    /// is an instruction the model cannot act on.
    let ParentWorkRecordInstruction =
        "`parent_work_record` is the parent's lifecycle work record, background only. It is not part of the assignment."

    /// REVIEW-002. Emitted only alongside `[[original_user_requirement]]` entries.
    let RequirementsInstruction =
        "The `original_user_requirement` entries are the authoritative review scope: verified HumanRoot "
        + "prompts received since the prior review completed its double-PERFECT barrier and reached terminal "
        + "idle. Verify every applicable requirement. `assignment` is supplementary and must not narrow or "
        + "override that scope."

    /// Render the payload.
    ///
    /// Instructions are emitted as the leading `#` comment block; data is emitted as bare fields and
    /// table arrays. `Assignment` is the instruction text and is always rendered as comments, so the
    /// model cannot mistake the task for a field value. `Payload` is runtime data and is rendered as a
    /// `content` field when present.
    ///
    /// Body list order is optional `[tdd]` (PENDING 7), then `content`, then `parent_work_record`,
    /// then requirement entries. `SyntheticToml.document` emits bare fields before tables regardless
    /// of the order given here, which keeps `[[original_user_requirement]]` from swallowing a field
    /// written after it; `[tdd]` is itself a table, so in the final document it follows the bare
    /// `content`/`parent_work_record` fields and precedes the requirement table array.
    let render (input: ForkChildAssignment) : string =
        let requirements =
            input.OriginalUserRequirements
            |> List.filter (fun text -> not (System.String.IsNullOrWhiteSpace text))

        let parentRecord =
            input.ParentWorkRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (fun record -> record <> "")

        let assignmentText = input.Assignment.Trim()

        let instructions =
            (if System.String.IsNullOrWhiteSpace assignmentText then
                 []
             else
                 [ assignmentText ])
            @ BaseInstructions
            @ (match parentRecord with
               | Some _ -> [ ParentWorkRecordInstruction ]
               | None -> [])
            @ (if List.isEmpty requirements then
                   []
               else
                   [ RequirementsInstruction ])

        let tddSection =
            match input.TddPhase with
            | Some phase ->
                [ String.concat
                      "\n"
                      [ "[tdd]"
                        SyntheticToml.field "phase" (SyntheticToml.renderString (TddPhase.wireName phase)) ] ]
            | None -> []

        let body =
            tddSection
            @ (match input.Payload with
               | Some payload when not (System.String.IsNullOrWhiteSpace payload) ->
                   [ SyntheticToml.field "content" (SyntheticToml.renderString payload) ]
               | _ -> [])
            @ (match parentRecord with
               | Some record -> [ SyntheticToml.field "parent_work_record" (SyntheticToml.renderString record) ]
               | None -> [])
            @ (requirements
               |> List.mapi (fun index text ->
                   SyntheticToml.tableArrayEntry
                       "original_user_requirement"
                       [ SyntheticToml.field "ordinal" (string (index + 1))
                         SyntheticToml.field "text" (SyntheticToml.renderString text) ]))

        SyntheticToml.document instructions body

    /// The positional form, for a call site that reads better without a record literal.
    let relay
        (assignment: string)
        (parentWorkRecord: string option)
        (requirements: string list)
        (payload: string option)
        : string =
        render
            { Assignment = assignment
              ParentWorkRecord = parentWorkRecord
              OriginalUserRequirements = requirements
              Payload = payload
              TddPhase = None }
