namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// COMPANION-009 / CTX-010: which prefix X sends, as a plan over message positions.
///
/// Pure and index-based. The Host message objects live at the adapter boundary; what
/// is decided here is how many leading messages the companion memory replaces and what
/// that synthetic message contains, so both can be tested without a Host (VERIFY-008).
type XPrefixPlan =
    {
        /// `None` means send raw history: no snapshot committed, or a reanchor retired it
        /// (HOST-006). Both are the same instruction, which is why one field carries both.
        CompanionMemory: (string * string) option
        /// How many leading provider-visible messages the memory replaces. Zero when
        /// there is no memory.
        DropLeading: int
    }

[<RequireQualifiedAccess>]
module XPrefixProjection =

    /// COMPANION-009: the plan for one request.
    ///
    /// `frozenRecordPrefixBody` is the FrozenRecordPrefix text, already read from the blob the snapshot
    /// references. The snapshot carries a `BlobRef` plus digest and never the body —
    /// PERSIST-007 keeps large bodies out of the journal line — so resolving it is the
    /// adapter's job and this module takes the result.
    ///
    /// That is the same split `ResolvedPrefixMemory` makes on the Session side: the
    /// journal records WHERE the body is, and only a resolved copy can be handed to the
    /// transform boundary.
    ///
    /// The snapshot's own `SyntheticMessageId` is used, not a freshly derived one. That
    /// id was fixed when the candidate was built and is what the provider has already
    /// seen for this epoch; deriving it again here would be a second construction site
    /// for one identity, and any drift becomes a cold boundary on every later request.
    let forSnapshot (snapshot: PrefixSnapshot option) (frozenRecordPrefixBody: string) : XPrefixPlan =
        match snapshot with
        | None ->
            { CompanionMemory = None
              DropLeading = 0 }
        | Some value ->
            { CompanionMemory =
                Some(value.SyntheticMessageId, CompanionPrompt.companionMemoryBlock frozenRecordPrefixBody)
              DropLeading = value.CutoffExclusive }

    /// CTX-010: the plan this attempt's profile calls for.
    ///
    /// One function for both cases on purpose. A probe is not a different kind of
    /// request — it is the same request with a candidate prefix — so building it through
    /// a separate path would let the two drift, and CTX-012 requires a promoted probe to
    /// be byte-identical to what the successful attempt sent.
    let forChoice
        (choice: XProjectionChoice)
        (committed: PrefixSnapshot option)
        (frozenRecordPrefixBody: string)
        : XPrefixPlan =
        match choice with
        | XProjectionChoice.UseCommittedEpoch -> forSnapshot committed frozenRecordPrefixBody
        | XProjectionChoice.UsePrefixProbe probe -> forSnapshot (Some probe.Candidate) frozenRecordPrefixBody

    /// Which blob this attempt needs read before its plan can be built.
    ///
    /// Exposed so the adapter cannot guess. Reading the COMMITTED snapshot's blob for a
    /// probe attempt would inject the old FrozenRecordPrefix under the candidate's synthetic id —
    /// a pairing the provider sees as a changed prefix and no fold can detect, because
    /// both halves are individually well-formed.
    let requiredBlob (choice: XProjectionChoice) (committed: PrefixSnapshot option) : BlobRef option =
        match choice with
        | XProjectionChoice.UseCommittedEpoch ->
            committed |> Option.map (fun snapshot -> snapshot.FrozenRecordPrefixRef)
        | XProjectionChoice.UsePrefixProbe probe -> Some probe.Candidate.FrozenRecordPrefixRef

    /// Does this plan replace anything.
    let replacesPrefix (plan: XPrefixPlan) = Option.isSome plan.CompanionMemory
