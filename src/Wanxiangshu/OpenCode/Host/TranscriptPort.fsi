namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

module TranscriptPort =
    /// P-03: projects host session messages into neutral transcript records.
    val project: message: SessionMessage -> Result<TranscriptMessage, HostError>
    val projectAll: messages: SessionMessage list -> Result<TranscriptMessage list, HostError>

    type HostTranscriptPort =
        new: inner: ISessionSnapshotPort -> HostTranscriptPort
        interface ITranscriptPort
