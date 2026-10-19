namespace Wanxiangshu.Repository.Programming.Js

open System.Collections.Generic
open System.Threading.Tasks

type JsDelegationHost =
    { Fork: obj -> Task<obj>
      Join: unit -> Task<obj>
      Fission: obj -> Task<obj>
      Horizon: unit -> Task<obj>
      Advisor: obj -> obj -> Task<obj> } // (intent, view)

/// Process-live delegation seam. One host per session; the wiring installs it
/// around a generated tool execution and clears it afterwards. Single-threaded
/// host ordering means one session runs one programming call at a time.
module JsDelegationBridge =

    // DSL-MUTABLE: resource — sessionId → delegation host
    let private hosts = Dictionary<string, JsDelegationHost>()

    let install (sessionId: string) (host: JsDelegationHost) : unit = hosts.[sessionId] <- host

    let hostFor (sessionId: string) : JsDelegationHost option =
        match hosts.TryGetValue sessionId with
        | true, host -> Some host
        | false, _ -> None

    let clear (sessionId: string) : unit = hosts.Remove sessionId |> ignore

    let clearAll () : unit = hosts.Clear()
