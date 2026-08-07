namespace Wanxiangshu.Domain

/// FALLBACK-012 / CTX-006: is this recovery slot armed.
///
/// Not persistent state, not a field on the cursor, never written to the journal.
/// A local control-flow fact of one automatic recovery sequence.
///
/// The type exists so the answer cannot be produced from a cursor alone. A `bool`
/// parameter would let a caller pass `isOdd cursor.Offset`, which is exactly the
/// parked-cursor bug: FALLBACK-004 does not reset Offset on success, so a run that
/// failed once and then succeeded leaves the cursor on an odd Offset forever. Every
/// later sequence would then arm its FIRST slot, squash half the frames every round,
/// grind history to the output-budget floor, and never recover — while each
/// individual squash looks correct.
[<RequireQualifiedAccess>]
type SlotArming =
    /// The slot begins a sequence, or was reached without an intervening failure.
    | NotArmed
    /// The slot was reached by a real failure advancing the cursor within THIS
    /// sequence.
    | ArmedByAdvance

/// CTX-007: what one provider attempt produced.
///
/// Terminal validity is already resolved into the case: `Completed` means the
/// snapshot said Completed AND `TerminalValidity` accepted the text (CTX-004).
/// `CompletedInvalid` is separate because FALLBACK-008 gives it a repair rather
/// than a cursor advance, and folding it into `Failed` would spend the budget on a
/// response that arrived intact.
///
/// There is no `Overflow` case and no error text. CTX-005: every `Failed` and
/// `Aborted` takes the same recovery path, so a discriminator would only grow a
/// branch that never executes.
[<RequireQualifiedAccess>]
type AttemptOutcome =
    | Completed
    | CompletedInvalid
    | Failed
    | Aborted

/// CTX-007: what the slot does next.
[<RequireQualifiedAccess>]
type SlotDecision =
    /// The squash produced a valid frame. Commit it permanently (CTX-012), leave the
    /// cursor and the failure count alone, and continue to the main request in the
    /// SAME slot.
    | CommitSquashThenMain
    /// The squash was unavailable or produced nothing usable. Skip it and send the
    /// main request against the unchanged frames.
    | MainWithoutSquash
    /// Commit the produced artefact. FALLBACK-011 decides whether this also clears
    /// the consecutive failure count.
    | CommitMain of clearsFailureCount: bool
    /// The generated artefact arrived but is unusable. FALLBACK-008 allows one
    /// repair; the cursor does not move.
    | RepairOnce
    /// The repair also failed to produce a usable terminal. Abandon this round's
    /// product without advancing the cursor — the next offer recomputes the delta
    /// from the unchanged baseline (COMPANION-008).
    | AbandonRoundProduct
    /// This slot terminated in failure. Advance the cursor and increment the count
    /// (FALLBACK-011); the next slot's arming follows from that advance.
    | FailSlot

/// FALLBACK-011 / FALLBACK-012 / CTX-006 / CTX-007: the recovery slot's control flow.
///
/// Pure. The journal writes and the provider calls live in the caller; what is here
/// is the decision, so the parked-cursor rule and the three-outcome dispatch can be
/// tested without a Host (VERIFY-008).
[<RequireQualifiedAccess>]
module RecoverySlot =

    /// FALLBACK-012: a new automatic recovery sequence always starts unarmed.
    ///
    /// Even when the recovered Offset is odd. That is the whole clause: arming is a
    /// property of what happened in THIS sequence, not of where the cursor parked.
    let beginSequence = SlotArming.NotArmed

    /// FALLBACK-012: the arming of the slot reached by a failure advance.
    ///
    /// Takes no cursor and no offset. There is deliberately no way to ask "is offset
    /// N armed", because the question has no answer — arming is not a property of a
    /// position.
    let afterFailureAdvance = SlotArming.ArmedByAdvance

    /// FALLBACK-012: after a crash, arming is lost and the session resumes unarmed.
    ///
    /// The safe side: an unarmed slot sends a normal request, so the worst case is one
    /// missed compression opportunity. Resuming armed would squash on the first slot
    /// after every restart, which is the parked-cursor failure with a different
    /// trigger.
    let afterRestart = SlotArming.NotArmed

    let isArmed (arming: SlotArming) =
        match arming with
        | SlotArming.ArmedByAdvance -> true
        | SlotArming.NotArmed -> false

    /// CTX-006: may this slot attempt a recovery action (X prefix probe or Y frame
    /// squash) before its main request.
    ///
    /// THREE conditions, all required.
    ///
    /// `arming` is the control-flow fact: this slot was reached by a real failure
    /// advancing the cursor within the current sequence.
    ///
    /// `offset` must be odd. This is the A/A′/B/B′ shape itself — the primed slots
    /// are the recovery slots, so `A` and `B` send an ordinary request even when they
    /// were reached by a failure. Dropping this conjunct makes every retry a recovery
    /// slot, which squashes roughly twice as often as the design and reaches the
    /// output-budget floor sooner. FALLBACK-012 forbids arming from parity ALONE, not
    /// parity as one of the conditions.
    ///
    /// `hasMaterial` is whether there is anything to work with — a candidate newer
    /// than the committed epoch (CTX-011), or at least one frame to squash (CTX-012).
    /// An armed odd slot with no material is normal and not an error: CTX-011 says to
    /// send the ordinary main request rather than construct an empty probe. That is
    /// why such a slot is "a slot that MAY recover", not "a slot that compresses".
    let mayRecover (arming: SlotArming) (offset: AgentPairCursor.FallbackOffset) (hasMaterial: bool) =
        isArmed arming && AgentPairCursor.isRecoverySlot offset && hasMaterial

    /// CTX-007 for a `BloggerSquash` sub-request.
    ///
    /// The asymmetry with a main request is the point. A valid squash is committed
    /// permanently and the slot CONTINUES — the squash is independent, reusable
    /// compression work, so a later main failure does not undo it (CTX-012). An
    /// invalid one is skipped rather than repaired: the frames are still there, so
    /// spending a repair on a compression would be spending it on the wrong thing.
    let onSquashOutcome (outcome: AttemptOutcome) : SlotDecision =
        match outcome with
        | AttemptOutcome.Completed -> SlotDecision.CommitSquashThenMain
        | AttemptOutcome.CompletedInvalid -> SlotDecision.MainWithoutSquash
        | AttemptOutcome.Failed
        | AttemptOutcome.Aborted -> SlotDecision.FailSlot

    /// CTX-007 for a main request.
    ///
    /// `aabbConsumed` is FALLBACK-008's one-repair budget for this occasion. Passed in
    /// rather than tracked here because the budget is per unusable terminal and the
    /// Dispatcher already owns it (PROMPT-005); a second counter could disagree.
    let onMainOutcome (kind: ProviderRequestKind) (aabbConsumed: bool) (outcome: AttemptOutcome) : SlotDecision =
        match outcome with
        | AttemptOutcome.Completed -> SlotDecision.CommitMain(ProviderRequestKind.clearsFailureCountOnSuccess kind)
        | AttemptOutcome.CompletedInvalid ->
            if aabbConsumed then
                SlotDecision.AbandonRoundProduct
            else
                SlotDecision.RepairOnce
        | AttemptOutcome.Failed
        | AttemptOutcome.Aborted -> SlotDecision.FailSlot

    /// CTX-008: does this decision advance the fallback cursor.
    ///
    /// Exactly one decision does. In particular `AbandonRoundProduct` does not: an
    /// invalid terminal is not a failed slot (FALLBACK-008 keeps it out of the A/B
    /// count), and `CommitSquashThenMain` does not either, because the slot has not
    /// terminated yet — which is what makes one armed slot produce at most one
    /// `FallbackCursorAdvanced` despite two physical requests (FALLBACK-011).
    let advancesCursor (decision: SlotDecision) =
        match decision with
        | SlotDecision.FailSlot -> true
        | SlotDecision.CommitSquashThenMain
        | SlotDecision.MainWithoutSquash
        | SlotDecision.CommitMain _
        | SlotDecision.RepairOnce
        | SlotDecision.AbandonRoundProduct -> false

    /// The arming of the NEXT slot, given how this one ended.
    ///
    /// The invariant this produces: at least one real failure sits between any two
    /// squashes. Compression is a by-product of recovery, not routine housekeeping —
    /// which is what AA′BB′ meant in the first place.
    let nextArming (decision: SlotDecision) =
        if advancesCursor decision then
            afterFailureAdvance
        else
            beginSequence
