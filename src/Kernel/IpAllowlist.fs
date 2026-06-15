module VibeFs.Kernel.IpAllowlist

open Fable.Core
open System

/// Strict IP-family classification via node:net.isIP (4, 6, or 0 for invalid).
[<Import("isIP", "node:net")>]
let private netIsIp (ip: string) : int = jsNative

let isValidIPv4 (ip: string) : bool = netIsIp ip = 4
let isValidIPv6 (ip: string) : bool = netIsIp ip = 6

/// Build a uint32 from four octets — each number is a real network octet,
/// not an opaque hex blob.
let private ip (a: byte) (b: byte) (c: byte) (d: byte) : uint32 =
    (uint32 a <<< 24) ||| (uint32 b <<< 16) ||| (uint32 c <<< 8) ||| uint32 d

/// IPv4 private/reserved ranges as (start, end) unsigned 32-bit pairs.
let private ipv4Ranges: (uint32 * uint32) list =
    [ ip 127uy 0uy 0uy 0uy, ip 127uy 255uy 255uy 255uy       // 127.0.0.0/8
      ip 10uy 0uy 0uy 0uy, ip 10uy 255uy 255uy 255uy         // 10.0.0.0/8
      ip 172uy 16uy 0uy 0uy, ip 172uy 31uy 255uy 255uy       // 172.16.0.0/12
      ip 192uy 168uy 0uy 0uy, ip 192uy 168uy 255uy 255uy     // 192.168.0.0/16
      ip 169uy 254uy 0uy 0uy, ip 169uy 254uy 255uy 255uy     // 169.254.0.0/16
      ip 100uy 64uy 0uy 0uy, ip 100uy 127uy 255uy 255uy      // 100.64.0.0/10
      ip 0uy 0uy 0uy 0uy, ip 0uy 255uy 255uy 255uy           // 0.0.0.0/8
      ip 224uy 0uy 0uy 0uy, System.UInt32.MaxValue ]         // 224.0.0.0/4 + 240.0.0.0/4

let ipv4ToUint32 (ip: string) : uint32 =
    let parts = ip.Split('.')
    (uint32 parts.[0] <<< 24) ||| (uint32 parts.[1] <<< 16)
    ||| (uint32 parts.[2] <<< 8) ||| uint32 parts.[3]

let isPrivateIPv4 (ip: string) : bool =
    let addr = ipv4ToUint32 ip
    ipv4Ranges |> List.exists (fun (lo, hi) -> addr >= lo && addr <= hi)

/// Strip an IPv4-mapped IPv6 prefix ("::ffff:1.2.3.4") down to its v4 part.
let normalizeIPv6 (ip: string) : string =
    let lower = ip.ToLowerInvariant()
    if lower.StartsWith("::ffff:") then
        let v4part = lower.[7..]
        if isValidIPv4 v4part then v4part else lower
    else lower

let isPrivateIPv6 (ip: string) : bool =
    let normalized = normalizeIPv6 ip
    if isValidIPv4 normalized then isPrivateIPv4 normalized
    elif normalized = "::" || normalized = "::1" then true
    elif normalized.StartsWith("fc") || normalized.StartsWith("fd") then true
    elif normalized.StartsWith("fe8") || normalized.StartsWith("fe9")
         || normalized.StartsWith("fea") || normalized.StartsWith("feb") then true
    else false

let private classifyIpFamily (ip: string) : int = netIsIp ip

/// Result of classifying one resolved IP against the SSRF allowlist.
type SsrfResolution =
    | BlockedIp of ip: string * reason: string
    | AllowlistedIp of ip: string

let checkIpAllowlist (ip: string) : SsrfResolution =
    match classifyIpFamily ip with
    | 4 -> if isPrivateIPv4 ip then BlockedIp(ip, "private or blocked IP range") else AllowlistedIp ip
    | 6 -> if isPrivateIPv6 ip then BlockedIp(ip, "private or blocked IP range") else AllowlistedIp ip
    | _ -> BlockedIp(ip, "unrecognised address family")

/// Convenience predicate: true when the IP is public/allowlisted.
let isIpAllowed (ip: string) : bool =
    match checkIpAllowlist ip with
    | AllowlistedIp _ -> true
    | BlockedIp _ -> false

/// Validate a hostname: localhost and private IPs are refused; public IPs and
/// domain names are allowed.
let validateHostname (hostname: string) : bool =
    let stripped = hostname.TrimStart('[').TrimEnd(']').ToLowerInvariant()
    if stripped = "localhost" || stripped = "ip6-localhost" || stripped = "ip6-loopback" then false
    elif isValidIPv4 stripped then not (isPrivateIPv4 stripped)
    elif stripped.Contains(":") then not (isPrivateIPv6 stripped)
    else true
