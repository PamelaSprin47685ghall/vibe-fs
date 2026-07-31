namespace Wanxiangshu.Next.Domain

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
/// ARCH-010 fixes it structurally, not by picking one of the four. Instruction comments come FIRST
/// and the first two are unconditional, so every shape now begins with the same bytes. The optional
/// parts became optional FIELDS, which 「省略不存在的可选字段」 permits and which a fragment
/// declaration can skip over — the varying part sits between two stable anchors instead of in front
/// of them.
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

    /// The two instructions every forked child receives, in order.
    ///
    /// Unconditional, and that is the whole fix. The previous envelope carried the report format only
    /// when a parent work record happened to exist, so whether a child owed a structured report
    /// depended on unrelated state. Both facts — do the work, report in this shape — are true of
    /// every fork.
    ///
    /// The first line is what every declaration anchors on, so it must not be reworded casually:
    /// `gate-runtime-key-cases.mjs` pins it for that reason.
    let BaseInstructions =
        [ "Complete the assignment in `assignment`."
          "Report back with exactly these fields: result, files changed, tests run, evidence, remaining risks, blockers." ]

    /// Emitted only alongside a `parent_work_record` field, because an instruction about absent data
    /// is an instruction the model cannot act on.
    let ParentWorkRecordInstruction =
        "`parent_work_record` is background only; prefer B, else session A. It is not part of the assignment."

    /// REVIEW-002. Emitted only alongside `[[original_user_requirement]]` entries.
    let RequirementsInstruction =
        "The `original_user_requirement` entries are the authoritative review scope: verified HumanRoot "
        + "prompts received since the prior review completed its double-PERFECT barrier and reached terminal "
        + "idle. Verify every applicable requirement. `assignment` is supplementary and must not narrow or "
        + "override that scope."

    /// Render the payload.
    ///
    /// Field order is `assignment` first, then `parent_work_record`, then the requirement entries.
    /// Deliberate: the assignment is what the child must act on, and putting it immediately after the
    /// header gives declarations a stable position to match rather than one that shifts with context.
    ///
    /// `SyntheticToml.document` emits bare fields before table arrays regardless of the order given
    /// here, which is what keeps `[[original_user_requirement]]` from swallowing a field written after
    /// it. That rule lives there because it is a property of TOML, not of forks.
    let render (input: ForkChildAssignment) : string =
        let requirements =
            input.OriginalUserRequirements |> List.filter (fun text -> text <> "")

        let parentRecord =
            input.ParentWorkRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (fun record -> record <> "")

        let instructions =
            BaseInstructions
            @ (match parentRecord with
               | Some _ -> [ ParentWorkRecordInstruction ]
               | None -> [])
            @ (if List.isEmpty requirements then
                   []
               else
                   [ RequirementsInstruction ])

        let body =
            [ SyntheticToml.field "assignment" (SyntheticToml.renderString input.Assignment) ]
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
