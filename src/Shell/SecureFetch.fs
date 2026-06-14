module VibeFs.Shell.SecureFetch

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.IpAllowlist
open VibeFs.Kernel

[<Emit("require('node:dns/promises').resolve4($0)")>]
let private resolve4 (hostname: string) : JS.Promise<string array> = jsNative
[<Emit("require('node:dns/promises').resolve6($0)")>]
let private resolve6 (hostname: string) : JS.Promise<string array> = jsNative
[<Emit("require('node:net').isIP($0)")>]
let private isIpFamily (ip: string) : int = jsNative
[<Emit("new URL($0)")>]
let private newUrl (url: string) : obj = jsNative
[<Emit("globalThis.fetch($0, $1)")>]
let private fetchRaw (url: string) (init: obj) : JS.Promise<obj> = jsNative

/// Resolve a hostname to a pinned IP, refusing any private/blocked address.
let resolveDnsPinned (hostname: string) : JS.Promise<string> =
    async {
        if isIpFamily hostname <> 0 then
            match checkIpAllowlist hostname with
            | BlockedIp(ip, reason) -> return raise (exn $"SSRF protection: direct IP {ip} is blocked ({reason})")
            | AllowlistedIp ip -> return ip
        else
            let mutable addresses: string list = []
            try let! v4 = resolve4 hostname |> Async.AwaitPromise in addresses <- addresses @ List.ofArray v4 with _ -> ()
            try let! v6 = resolve6 hostname |> Async.AwaitPromise in addresses <- addresses @ List.ofArray v6 with _ -> ()
            if List.isEmpty addresses then return raise (exn $"DNS resolution failed for {hostname}")
            else
                for addr in addresses do
                    match checkIpAllowlist addr with
                    | BlockedIp(ip, _) -> return raise (exn $"SSRF protection: resolved IP {ip} for {hostname} is blocked")
                    | AllowlistedIp _ -> ()
                return List.head addresses
    }
    |> Async.StartAsPromise

/// HTTP/HTTPS agents that pin the SSRF-checked resolved IP at connect time.
[<Emit("(() => { const http = require('node:http'); const https = require('node:https'); const { resolve4, resolve6 } = require('node:dns/promises'); const net = require('node:net'); function blocked(ip){ const f = net.isIP(ip); if (f === 4) { const p = ip.split('.'); const n = ((+p[0]<<24)>>>0)|(+p[1]<<16)|(+p[2]<<8)|+p[3]; const ranges = [[0x7F000000,0x7FFFFFFF],[0x0A000000,0x0AFFFFFF],[0xAC100000,0xAC1FFFFF],[0xC0A80000,0xC0A8FFFF],[0xA9FE0000,0xA9FEFFFF],[0x64400000,0x647FFFFF],[0x00000000,0x00FFFFFF],[0xE0000000,0xFFFFFFFF]]; for (const [lo,hi] of ranges) if ((n>>>0)>=(lo>>>0) && (n>>>0)<=(hi>>>0)) return true; } if (f === 6) { let lower = ip.toLowerCase(); if (lower.startsWith('::ffff:')) { const v4p = lower.slice(7); if (net.isIP(v4p) === 4) return blocked(v4p); } if (lower === '::' || lower === '::1') return true; if (lower.startsWith('fc') || lower.startsWith('fd')) return true; if (lower.startsWith('fe8') || lower.startsWith('fe9') || lower.startsWith('fea') || lower.startsWith('feb')) return true; } return false; } async function pin(host){ if (net.isIP(host)) { if (blocked(host)) throw new Error('SSRF: '+host+' blocked'); return host; } let addrs=[]; try{ addrs.push(...await resolve4(host)); }catch{} try{ addrs.push(...await resolve6(host)); }catch{} if(!addrs.length) throw new Error('DNS resolution failed for '+host); for(const a of addrs){ if(blocked(a)) throw new Error('SSRF: '+a+' blocked for '+host); } return addrs[0]; } const mk = (Base, isHttps) => class extends Base { createConnection(opts, cb){ pin(opts.host||'localhost').then(ip=>{ const o = isHttps ? {...opts, host:ip, servername:opts.servername||opts.host} : {...opts, host:ip}; super.createConnection(o, cb); }).catch(e=>cb(e)); } }; return { httpAgent: new (mk(http.Agent,false))({ keepAlive:true }), httpsAgent: new (mk(https.Agent,true))({ keepAlive:true, rejectUnauthorized:false }) }; })()")>]
let private dnsPinningAgents : obj = jsNative

/// Fetch with DNS pinning and SSRF checks.
let secureFetch (url: string) (init: obj) : JS.Promise<obj> =
    async {
        let parsed = newUrl url
        let isHttps = Dyn.str parsed "protocol" = "https:"
        do! resolveDnsPinned (Dyn.str parsed "hostname") |> Async.AwaitPromise |> Async.Ignore
        let agentKey = if isHttps then "httpsAgent" else "httpAgent"
        let agent = Dyn.get dnsPinningAgents agentKey
        let merged =
            if isNull init then box {| agent = agent |}
            else Dyn.withKey init "agent" agent
        return! fetchRaw url merged |> Async.AwaitPromise
    }
    |> Async.StartAsPromise
