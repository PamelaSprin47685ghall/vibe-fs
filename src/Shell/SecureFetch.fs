module VibeFs.Shell.SecureFetch

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.IpAllowlist

[<Import("resolve4", "node:dns/promises")>]
let private resolve4 (hostname: string) : JS.Promise<string array> = jsNative

[<Import("resolve6", "node:dns/promises")>]
let private resolve6 (hostname: string) : JS.Promise<string array> = jsNative

[<Import("isIP", "node:net")>]
let private netIsIP (ip: string) : int = jsNative

[<Emit("new URL($0)")>]
let private parseUrl (url: string) : obj = jsNative

type private LookupCallback = string -> obj -> (obj -> string -> int -> unit) -> unit

type private AgentOptions =
    {| keepAlive: bool
       lookup: LookupCallback |}

[<Import("Agent", "node:http")>]
type private HttpAgent(options: AgentOptions) = class end

[<Import("Agent", "node:https")>]
type private HttpsAgent(options: AgentOptions) = class end

type private AgentPair =
    { http: HttpAgent
      https: HttpsAgent
      hostname: string
      ip: string }

let private makeLookup (hostname: string) (ip: string) : LookupCallback =
    fun h _ cb ->
        if h <> hostname then
            cb (box (exn $"DNS rebinding blocked: {h}")) null 0
        else
            cb null ip (if netIsIP ip = 6 then 6 else 4)

let private makeAgentPair (hostname: string) (ip: string) : AgentPair =
    let lookup = makeLookup hostname ip
    let options = {| keepAlive = true; lookup = lookup |}
    { http = HttpAgent(options)
      https = HttpsAgent(options)
      hostname = hostname
      ip = ip }

let private resolveAll (hostname: string) : Async<string> =
    async {
        let! v4 =
            async {
                try
                    let! arr = resolve4 hostname |> Async.AwaitPromise
                    return arr |> Array.toList
                with _ -> return []
            }
        let! v6 =
            async {
                try
                    let! arr = resolve6 hostname |> Async.AwaitPromise
                    return arr |> Array.toList
                with _ -> return []
            }
        let ips = v4 @ v6
        if List.isEmpty ips then
            return failwith $"DNS resolution failed for {hostname}"
        else
            match ips |> List.tryFind (fun ip -> not (isIpAllowed ip)) with
            | Some blocked -> return failwith $"Blocked private/reserved IP: {blocked}"
            | None -> return List.head ips
    }

let private cache = System.Collections.Generic.Dictionary<string, AgentPair>()

let private getAgentPair (hostname: string) : Async<AgentPair> =
    async {
        match cache.TryGetValue hostname with
        | true, pair -> return pair
        | false, _ ->
            let! ip = resolveAll hostname
            let pair = makeAgentPair hostname ip
            cache.[hostname] <- pair
            return pair
    }

[<Emit("({ ...$0, agent: $1 })")>]
let private withAgent (init: obj) (agent: obj) : obj = jsNative

[<Global>]
let private fetch (url: string) (init: obj) : JS.Promise<obj> = jsNative

let secureFetch (url: string) (init: obj) : JS.Promise<obj> =
    async {
        let u = parseUrl url
        let protocol = u?protocol
        if protocol <> "http:" && protocol <> "https:" then
            failwith $"Unsupported protocol: {protocol}"
        let hostname = u?hostname
        let! pair = getAgentPair hostname
        let agent = if protocol = "https:" then box pair.https else box pair.http
        return! fetch url (withAgent init agent) |> Async.AwaitPromise
    }
    |> Async.StartAsPromise
