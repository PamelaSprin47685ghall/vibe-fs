namespace Wanxiangshu.Sphinx.V2.Persistence

open System
open Fable.Core.JsInterop

module Surface =
    /// A summary export is redacted by construction.
    val exportSummary: unit -> obj

    /// A full export claims complete replay only when its blobs travel with it.
    val exportFull: bool -> obj
    val exportReplayability: obj -> string
    val exportValidate: obj -> Result<unit, string>

    /// The integration rule this shard contributes, so composition can bind it.
    val ruleKey: unit -> string
