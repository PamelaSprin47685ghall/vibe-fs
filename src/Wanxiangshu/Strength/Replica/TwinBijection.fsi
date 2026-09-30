namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.Participant.Provider.Projection

/// The main <-> predictor bijection: the owner transcript is the skeleton, the
/// replica's own speech is restored into the gaps the child recorded.
[<RequireQualifiedAccess>]
module TwinBijection =

    /// Does this message carry at least one tool call?
    val hasCall: message: ProviderProjection.WireMessage -> bool

    /// Speech: text or reasoning with no tool call (replica-only material).
    val isSpeechOnly: message: ProviderProjection.WireMessage -> bool

    /// Restore the replica's speech into the child-recorded gaps. Never refuses,
    /// never drops speech; the owner skeleton keeps its order and its count.
    val restore: child: ProviderProjection.WireMessage list -> owner: ProviderProjection.WireMessage list -> ProviderProjection.WireMessage list

    /// The owner's own call sequence survives restoration unchanged.
    val preservesOwnerOrder: child: ProviderProjection.WireMessage list -> owner: ProviderProjection.WireMessage list -> bool

    /// No message is fabricated: the result is drawn only from owner or child speech.
    val introducesNothing: child: ProviderProjection.WireMessage list -> owner: ProviderProjection.WireMessage list -> bool

    /// Restoration never reduces the number of speech messages.
    val dropsNoSpeech: child: ProviderProjection.WireMessage list -> owner: ProviderProjection.WireMessage list -> bool

    /// Append-only growth on both sides reproduces the earlier request as a prefix.
    val extensionIsPrefix:
        childBefore: ProviderProjection.WireMessage list ->
        ownerBefore: ProviderProjection.WireMessage list ->
        childAfter: ProviderProjection.WireMessage list ->
        ownerAfter: ProviderProjection.WireMessage list ->
            bool

/// JS boundary for the bijection (plain objects in, plain objects out).
[<RequireQualifiedAccess>]
module TwinBijectionSurface =

    val restore: child: obj array -> owner: obj array -> obj array
    val preservesOwnerOrder: child: obj array -> owner: obj array -> bool
    val introducesNothing: child: obj array -> owner: obj array -> bool
    val dropsNoSpeech: child: obj array -> owner: obj array -> bool
    val extensionIsPrefix:
        childBefore: obj array ->
        ownerBefore: obj array ->
        childAfter: obj array ->
        ownerAfter: obj array ->
            bool
