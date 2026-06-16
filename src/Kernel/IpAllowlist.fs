module VibeFs.Kernel.IpAllowlist

open System

let private parseOctet (s: string) : int option =
    match Int32.TryParse s with
    | true, n when n >= 0 && n <= 255 && (s = "0" || not (s.StartsWith("0"))) -> Some n
    | _ -> None

let isValidIPv4 (ip: string) : bool =
    let parts = ip.Split('.')
    parts.Length = 4 && Array.forall (fun p -> parseOctet p |> Option.isSome) parts

let private isHexGroup (g: string) : bool =
    g.Length > 0 && g.Length <= 4 && Seq.forall (fun (c: char) -> "0123456789abcdefABCDEF".IndexOf(c) >= 0) g

let isValidIPv6 (ip: string) : bool =
    if ip = "" then false
    elif not (ip.Contains(":")) then false
    else
        let hasDoubleColon = ip.Contains("::")
        let groups = ip.Split([|"::"|], StringSplitOptions.None)
        if hasDoubleColon && groups.Length > 2 then false
        elif hasDoubleColon then
            let left = if groups.[0] = "" then [||] else groups.[0].Split(':')
            let right = if groups.[1] = "" then [||] else groups.[1].Split(':')
            Array.length left + Array.length right <= 7
            && Array.forall isHexGroup left && Array.forall isHexGroup right
        else
            let parts = ip.Split(':')
            parts.Length = 8 && Array.forall isHexGroup parts

let private ip (a: byte) (b: byte) (c: byte) (d: byte) : uint32 =
    (uint32 a <<< 24) ||| (uint32 b <<< 16) ||| (uint32 c <<< 8) ||| uint32 d

let private ipv4Ranges: (uint32 * uint32) list =
    [ ip 127uy 0uy 0uy 0uy, ip 127uy 255uy 255uy 255uy
      ip 10uy 0uy 0uy 0uy, ip 10uy 255uy 255uy 255uy
      ip 172uy 16uy 0uy 0uy, ip 172uy 31uy 255uy 255uy
      ip 192uy 168uy 0uy 0uy, ip 192uy 168uy 255uy 255uy
      ip 169uy 254uy 0uy 0uy, ip 169uy 254uy 255uy 255uy
      ip 100uy 64uy 0uy 0uy, ip 100uy 127uy 255uy 255uy
      ip 0uy 0uy 0uy 0uy, ip 0uy 255uy 255uy 255uy
      ip 224uy 0uy 0uy 0uy, UInt32.MaxValue ]

let ipv4ToUint32 (ip: string) : uint32 =
    let parts = ip.Split('.')
    (uint32 parts.[0] <<< 24) ||| (uint32 parts.[1] <<< 16)
    ||| (uint32 parts.[2] <<< 8) ||| uint32 parts.[3]

let isPrivateIPv4 (ip: string) : bool =
    let addr = ipv4ToUint32 ip
    ipv4Ranges |> List.exists (fun (lo, hi) -> addr >= lo && addr <= hi)

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

let private classifyIpFamily (ip: string) : int =
    if isValidIPv4 ip then 4
    elif isValidIPv6 ip then 6
    else 0

type SsrfResolution =
    | BlockedIp of ip: string * reason: string
    | AllowlistedIp of ip: string

let checkIpAllowlist (ip: string) : SsrfResolution =
    match classifyIpFamily ip with
    | 4 -> if isPrivateIPv4 ip then BlockedIp(ip, "private or blocked IP range") else AllowlistedIp ip
    | 6 -> if isPrivateIPv6 ip then BlockedIp(ip, "private or blocked IP range") else AllowlistedIp ip
    | _ -> BlockedIp(ip, "unrecognised address family")

let isIpAllowed (ip: string) : bool =
    match checkIpAllowlist ip with
    | AllowlistedIp _ -> true
    | BlockedIp _ -> false

let validateHostname (hostname: string) : bool =
    let stripped = hostname.TrimStart('[').TrimEnd(']').ToLowerInvariant()
    if stripped = "localhost" || stripped = "ip6-localhost" || stripped = "ip6-loopback" then false
    elif isValidIPv4 stripped then not (isPrivateIPv4 stripped)
    elif stripped.Contains(":") then not (isPrivateIPv6 stripped)
    else true
