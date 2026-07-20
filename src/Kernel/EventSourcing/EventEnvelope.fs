module Wanxiangshu.Kernel.EventSourcing.EventEnvelope

/// One persisted line in `[workspace]/.wanxiangshu.ndjson` (schema v1).
type WanEvent =
    { V: int
      Session: string
      Kind: string
      At: string
      Payload: Map<string, string>
      EventId: string option
      WriterId: string option
      Sequence: int option
      Checksum: string option }

type EpisodeStage =
    | NoEpisode
    | Requested
    | DispatchStarted
    | Dispatched
    | Terminal
