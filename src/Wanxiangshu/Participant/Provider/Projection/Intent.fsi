namespace Wanxiangshu.Participant.Provider.Projection

type ProjectionMessageRow =
    { Message: ProviderProjection.WireMessage
      HostMessageId: string option
      HostIsPhysical: bool }

type ProjectionMessageBase =
    { Key: string
      Rows: ProjectionMessageRow list }

[<RequireQualifiedAccess>]
type ProjectionMessageAnchor =
    | Append
    | BeforeMessageIndex of index: int

type ProjectionMessageInsertion =
    { Key: string
      Anchor: ProjectionMessageAnchor
      Rows: ProjectionMessageRow list }

type ProjectionSnapshot =
    { CurrentProjection: ProviderProjection.ProviderSemanticProjection }

[<RequireQualifiedAccess>]
type ProjectionIntent =
    | ReplaceMessageBase of ProjectionMessageBase
    | InsertMessageRows of ProjectionMessageInsertion

[<RequireQualifiedAccess>]
module ProjectionIntent =
    val replaceMessageBase: key: string -> rows: ProjectionMessageRow list -> ProjectionIntent

    val insertMessageRows:
        key: string -> anchor: ProjectionMessageAnchor -> rows: ProjectionMessageRow list -> ProjectionIntent

[<RequireQualifiedAccess>]
type ProjectionConflict =
    | ConflictingMessageBase
    | ConflictingMessageRows of key: string
