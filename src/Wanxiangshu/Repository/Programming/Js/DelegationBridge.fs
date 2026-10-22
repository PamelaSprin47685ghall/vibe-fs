namespace Wanxiangshu.Repository.Programming.Js

open System.Collections.Generic
open System.Threading.Tasks

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

    // DSL-MUTABLE: resource — sessionId → delegation host
    let private hosts = Dictionary<string, JsDelegationHost>()

    // DSL-MUTABLE: resource — sessionId → ask channel
    let private askChannels = Dictionary<string, string -> Task<Result<unit, string>>>()

    let install (sessionId: string) (host: JsDelegationHost) : unit = hosts.[sessionId] <- host

    let hostFor (sessionId: string) : JsDelegationHost option =
        match hosts.TryGetValue sessionId with
        | true, host -> Some host
        | false, _ -> None

    let installAsk (sessionId: string) (ask: string -> Task<Result<unit, string>>) : unit =
        askChannels.[sessionId] <- ask

    let askFor (sessionId: string) : (string -> Task<Result<unit, string>>) option =
        match askChannels.TryGetValue sessionId with
        | true, ask -> Some ask
        | false, _ -> None

    let clear (sessionId: string) : unit =
        hosts.Remove sessionId |> ignore
        askChannels.Remove sessionId |> ignore

    let clearAll () : unit =
        hosts.Clear()
        askChannels.Clear()
