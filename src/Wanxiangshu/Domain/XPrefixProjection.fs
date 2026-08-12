namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// COMPANION-009 / CTX-010: which prefix X sends, as a `ProjectionIntent` (PROJ-005).
///
/// Pure and index-based. The Host message objects live at the adapter boundary; what
/// is decided here is the intent: whether the companion memory replaces the physical
/// prefix, and what that synthetic message contains (PROJ-001 — the caller declares
/// the intent, the renderer applies it).
[<RequireQualifiedAccess>]
module XPrefixProjection =

    /// COMPANION-009: the intent for one request.
    ///
    /// `frozenRecordPrefixBody` is the FrozenRecordPrefix text, already read from the blob the snapshot
    /// references. The snapshot carries a `BlobRef` plus digest and never the body —
    /// PERSIST-007 keeps large bodies out of the journal line — so resolving it is the
    /// adapter's job and this module takes the result.
    ///
    /// `memoryPreamble` is already-localized companion memory preamble (PROMPT-019).
    ///
    /// That is the same split `ResolvedPrefixMemory` makes on the Session side: the
    /// journal records WHERE the body is, and only a resolved copy can be handed to the
    /// transform boundary.
    ///
    /// The snapshot's own `SyntheticMessageId` is used, not a freshly derived one. That
    /// id was fixed when the candidate was built and is what the provider has already
    /// seen for this epoch; deriving it again here would be a second construction site
    /// for one identity, and any drift becomes a cold boundary on every later request.
    let forSnapshot
        (snapshot: PrefixSnapshot option)
        (memoryPreamble: string)
        (frozenRecordPrefixBody: string)
        : ProjectionIntent =
        match snapshot with
        | None -> ProjectionIntent.KeepPhysicalPrefix
        | Some value ->
            ProjectionIntent.ActivatePrefixEpoch
                { SyntheticMessageId = value.SyntheticMessageId
                  Memory = CompanionPrompt.companionMemoryBlock memoryPreamble frozenRecordPrefixBody
                  DropLeading = value.CutoffExclusive }

    /// CTX-010: the intent this attempt's profile calls for.
    ///
    /// One function for both cases on purpose. A probe is not a different kind of
    /// request — it is the same request with a candidate prefix — so building it through
    /// a separate path would let the two drift, and CTX-012 requires a promoted probe to
    /// be byte-identical to what the successful attempt sent.
    let forChoice
        (choice: XProjectionChoice)
        (committed: PrefixSnapshot option)
        (memoryPreamble: string)
        (frozenRecordPrefixBody: string)
        : ProjectionIntent =
        match choice with
        | XProjectionChoice.UseCommittedEpoch -> forSnapshot committed memoryPreamble frozenRecordPrefixBody
        | XProjectionChoice.UsePrefixProbe probe ->
            forSnapshot (Some probe.Candidate) memoryPreamble frozenRecordPrefixBody

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
