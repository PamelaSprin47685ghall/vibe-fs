namespace Wanxiangshu.Participant.Provider.Projection

type RenderedMessages =
    { Messages: ProviderProjection.WireMessage list
      HostMessageIds: string option list
      HostIsPhysical: bool list }

[<RequireQualifiedAccess>]
module ProjectionRenderer =
    val renderMessagesWithHostIds:
        _snapshot: ProjectionSnapshot ->
        baseMessages: ProviderProjection.WireMessage list ->
        intents: ProjectionIntent list ->
            RenderedMessages

    val renderMessagesWithIntents:
        snapshot: ProjectionSnapshot ->
        baseMessages: ProviderProjection.WireMessage list ->
        intents: ProjectionIntent list ->
            ProviderProjection.WireMessage list

    val cutoffDigest: sha256: (string -> string) -> snapshot: ProjectionSnapshot -> cutoff: int -> string
