namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

/// What a newly forked child is told, before it is rendered (ARCH-010, N3).
///
/// Typed rather than pre-composed text, because ARCH-010 requires the instruction/data split to be
/// decided by the producer and not inferred by a renderer: `Assignment` is the task, the other two
/// are context the child may read but must not mistake for the task.
/// DSL-state-combination: domain — assignment and optional context/payload are
/// one immutable fork request; absence means no supplied evidence, never a next
/// action or lifecycle latch.
type ForkChildAssignment =
    {
        /// The manager's request. Always present: a fork with no assignment is not a fork.
        Assignment: string
        /// Commissioner background for the child. Absent for a commissioner that has produced none yet.
        CommissionerRecord: string option
        /// Another person's bounded work statement, visible only as background context.
        Attachment: string option
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
      Attachment: string
      Requirements: string }

/// The first prompt of a forked child, as one ARCH-010 payload.
[<RequireQualifiedAccess>]
module ForkChildPayload =

    let BasePath = "delegation/fork-child-base"
    let CommissionerRecordPath = "delegation/fork-child-commissioner-record"
    let AttachmentPath = "delegation/fork-child-attachment"
    let RequirementsPath = "delegation/fork-child-requirements"

    let render (prose: ForkChildInstructions) (input: ForkChildAssignment) : string =
        let requirements =
            input.RootRequirements
            |> List.filter (fun text -> not (System.String.IsNullOrWhiteSpace text))

        let commissionerRecord =
            input.CommissionerRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (fun record -> record <> "")

        let attachment =
            input.Attachment
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
               | Some _ -> [ prose.CommissionerRecord ]
               | None -> [])
            @ (match attachment with
               | Some _ -> [ prose.Attachment ]
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
            // Commissioner / attachment LWR as ARCH-010 data fields (DELEG-019/021).
            // Parent → child only: instruction header names the field; the record
            // itself is a TOML string value — never Split into `# Opening` /
            // `# Chronicle` comments, never dumped as bare prose outside a field.
            // Child → parent join is the opposite plane (`# LWR` via
            // JoinResultRenderer / SyntheticToml.comment) — do not conflate.
            @ (match commissionerRecord with
               | Some record -> [ SyntheticToml.field "commissioner_record" (SyntheticToml.renderString record) ]
               | None -> [])
            @ (match attachment with
               | Some record -> [ SyntheticToml.field "attached_work_record" (SyntheticToml.renderString record) ]
               | None -> [])
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
