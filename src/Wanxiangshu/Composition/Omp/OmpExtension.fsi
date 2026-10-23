namespace Wanxiangshu.Composition.Omp

open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Host.SecondHost

/// The omp extension entrypoint.
///
/// omp loads an extension bundle and calls `export default (pi) => …`. The
/// bundle hands the extension API to the host-neutral adapter, which activates
/// the product on the first omp session. This module takes no product
/// decision: it only wires the pieces together.
module OmpExtension =

    /// Activate the product inside an omp extension runtime.
    ///
    /// `pi` is the omp `ExtensionAPI` object an extension factory receives.
    /// The returned task settles once the adapter has been wired; the product
    /// itself is activated on the first `session_start` event.
    val activate: pi: obj -> Task<unit>

    /// Compose the product with caller-owned clock and per-activation timer factories.
    val activateWithTiming: pi: obj -> clock: IClockPort -> createTimer: (unit -> ITimerPort) -> Task<unit>

    /// The omp extension factory: `export default (pi) => …`.
    ///
    /// omp's loader (`getExtensionFactory`) accepts the module itself or
    /// `module.default` and rejects anything that is not callable, so the
    /// default export must BE the function and not the result of calling it.
    /// The factory therefore returns without waiting: the product is
    /// activated later, on the first `session_start` event.
    [<ExportDefault>]
    val defaultExport: pi: obj -> unit
