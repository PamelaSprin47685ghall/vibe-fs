namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// Process-live delegation seam for the programming surface: the wiring layer
/// (which owns the production fork/join/fission entries) installs one host per
/// session; the generated delegation members call it. Every call returns the
/// structured envelope object — data, never rendered prose.
type JsDelegationHost =
    { // Delegate (name, payload, reportMapping) — the single delegation entry
      // every `this.fork` / `this.resume` / `this.commission` / `this.devops`
      // member travels through. The reportMapping is the parent → child
      // context the host projects; a delegate never carries it implicitly.
      Delegate: string -> obj -> obj -> Task<obj>
      Join: unit -> Task<obj>
      Fission: obj -> Task<obj>
      Horizon: unit -> Task<obj>
      Advisor: obj -> obj -> Task<obj> } // (intent, reportMapping)

module JsDelegationBridge =
    val install: sessionId: string -> host: JsDelegationHost -> unit
    val hostFor: sessionId: string -> JsDelegationHost option
    val clear: sessionId: string -> unit
    val clearAll: unit -> unit
    val installAsk: sessionId: string -> ask: (string -> Task<Result<unit, string>>) -> unit
    val askFor: sessionId: string -> (string -> Task<Result<unit, string>>) option
