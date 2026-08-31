namespace Wanxiangshu.Participant.Provider.Projection

/// One provider-visible row and the Host metadata that must travel with it.
type ProjectionMessageRow =
    { Message: ProviderProjection.WireMessage
      HostMessageId: string option
      HostIsPhysical: bool }

/// A deterministic replacement for the current provider-visible message base.
/// `Key` is supplied by the feature owner and is used only for idempotent planning.
type ProjectionMessageBase =
    { Key: string
      Rows: ProjectionMessageRow list }

[<RequireQualifiedAccess>]
type ProjectionMessageAnchor =
    | Append
    | BeforeMessageIndex of index: int

/// Rows inserted at one absolute anchor in the unmodified message base.
/// `Key` is the stable owner-provided identity used for dedupe and ordering.
type ProjectionMessageInsertion =
    { Key: string
      Anchor: ProjectionMessageAnchor
      Rows: ProjectionMessageRow list }

/// The immutable semantic input for one provider projection attempt.
type ProjectionSnapshot =
    { CurrentProjection: ProviderProjection.ProviderSemanticProjection }

/// Feature owners express provider projection changes only as generic rows.
[<RequireQualifiedAccess>]
type ProjectionIntent =
    | ReplaceMessageBase of ProjectionMessageBase
    | InsertMessageRows of ProjectionMessageInsertion

/// Owner factories for the generic message projection intents.
[<RequireQualifiedAccess>]
module ProjectionIntent =

    let replaceMessageBase (key: string) (rows: ProjectionMessageRow list) : ProjectionIntent =
        ProjectionIntent.ReplaceMessageBase { Key = key; Rows = rows }

    let insertMessageRows
        (key: string)
        (anchor: ProjectionMessageAnchor)
        (rows: ProjectionMessageRow list)
        : ProjectionIntent =
        ProjectionIntent.InsertMessageRows
            { Key = key
              Anchor = anchor
              Rows = rows }

/// Planner refusal taxonomy for conflicting generic projection intents.
[<RequireQualifiedAccess>]
type ProjectionConflict =
    | ConflictingMessageBase
    | ConflictingMessageRows of key: string
