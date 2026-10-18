namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// Process-live delegation seam for the programming surface: the wiring layer
/// (which owns the production fork/join tool specs) installs one host per
/// session; the generated `js.fork`/`js.join` members call it. Fork returns the
/// real fork receipt text; join waits for this session's children.
type JsDelegationHost =
    { Fork: obj -> Task<string>
      Join: unit -> Task<string> }

module JsDelegationBridge =
    val install: sessionId: string -> host: JsDelegationHost -> unit
    val hostFor: sessionId: string -> JsDelegationHost option
    val clear: sessionId: string -> unit
    val clearAll: unit -> unit
