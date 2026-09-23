namespace Wanxiangshu.Sphinx.V2.Persistence

open System
open Fable.Core.JsInterop

/// The JS-native surface for Sphinx v2 persistence.
///
/// WHAT[sphinx-v2-020]: an export declares its own replayability. A redacted bundle
/// never claims complete replay, and a bundle whose blobs live outside never hides it.
module Surface =

    /// A summary export is redacted by construction.
    let exportSummary () : obj = box Replayability.SummaryOnly

    /// A full export claims complete replay only when its blobs travel with it.
    let exportFull (blobsCollocated: bool) : obj =
        match blobsCollocated with
        | true -> box Replayability.Complete
        | false -> box Replayability.RequiresExternalBlobs

    let exportReplayability (bundle: obj) : string =
        match unbox<Replayability> bundle with
        | Replayability.Complete -> "complete"
        | Replayability.SummaryOnly -> "summary-only"
        | Replayability.RequiresExternalBlobs -> "requires-external-blobs"

    /// Whether the bundle's claim is internally consistent. The contradiction this
    /// refuses is a redacted file that also claims byte-complete replay.
    let exportValidate (bundle: obj) : Result<unit, string> =
        match unbox<Replayability> bundle with
        | Replayability.Complete ->
            let fullBundle () =
                unbox<ExportMode> bundle = ExportMode.Full

            match fullBundle () with
            | true -> Ok()
            | false -> Error "a complete-replay claim requires a full mode bundle"
        | Replayability.SummaryOnly -> Ok()
        | Replayability.RequiresExternalBlobs -> Ok()

    /// The integration rule this shard contributes, so composition can bind it.
    let ruleKey () : string = Integrator.currentKey
