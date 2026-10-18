namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// Process-live delegation seam for the programming surface: the wiring layer
/// (which owns the production fork/join/fission entries) installs one host per
/// session; the generated delegation members call it. Every call returns the
/// structured envelope object — data, never rendered prose.
type JsDelegationHost =
    { Fork: obj -> Task<obj>
      Join: unit -> Task<obj>
      Fission: obj -> Task<obj> }

module JsDelegationBridge =
    val install: sessionId: string -> host: JsDelegationHost -> unit
    val hostFor: sessionId: string -> JsDelegationHost option
    val clear: sessionId: string -> unit
    val clearAll: unit -> unit
