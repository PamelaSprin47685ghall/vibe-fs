module Wanxiangshu.Kernel.WebFetchGuard

open System

type private Ipv4Range = { start: uint32; end': uint32 }

let private privateIpv4Ranges =
    [| { start = 0x7F000000u; end' = 0x7FFFFFFFu }
       { start = 0x0A000000u; end' = 0x0AFFFFFFu }
       { start = 0xAC100000u; end' = 0xAC1FFFFFu }
       { start = 0xC0A80000u; end' = 0xC0A8FFFFu }
       { start = 0xA9FE0000u; end' = 0xA9FEFFFFu }
       { start = 0x64400000u; end' = 0x647FFFFFu }
       { start = 0x00000000u; end' = 0x00FFFFFFu }
       { start = 0xE0000000u; end' = 0xFFFFFFFFu } |]

let private ipv4ToUint32 (ip: string) =
    let parts = ip.Split('.') |> Array.map UInt32.Parse
    ((parts.[0] <<< 24) ||| (parts.[1] <<< 16) ||| (parts.[2] <<< 8) ||| parts.[3]) >>> 0

let private isPrivateIPv4 (ip: string) =
    let addr = ipv4ToUint32 ip
    privateIpv4Ranges |> Array.exists (fun r -> addr >= r.start && addr <= r.end')

let private normalizeIPv6 (ip: string) =
    let lower = ip.ToLowerInvariant()

    if lower.StartsWith("::ffff:", StringComparison.Ordinal) then
        let v4part = lower.Substring 7

        if v4part.IndexOf '.' >= 0 then
            v4part
        else
            lower
    else
        lower

let private isPrivateIPv6 (ip: string) =
    let normalized = normalizeIPv6 ip

    if normalized.IndexOf '.' >= 0 then
        isPrivateIPv4 normalized
    elif normalized = "::" || normalized = "::1" then
        true
    elif normalized.StartsWith("fc", StringComparison.Ordinal)
         || normalized.StartsWith("fd", StringComparison.Ordinal) then
        true
    elif normalized.StartsWith("fe8", StringComparison.Ordinal)
         || normalized.StartsWith("fe9", StringComparison.Ordinal)
         || normalized.StartsWith("fea", StringComparison.Ordinal)
         || normalized.StartsWith("feb", StringComparison.Ordinal) then
        true
    else
        false

let private looksLikeIPv4 (ip: string) =
    let parts = ip.Split '.'

    parts.Length = 4
    && parts |> Array.forall (fun part ->
        part.Length > 0
        && part.Length <= 3
        && part |> Seq.forall Char.IsDigit)

let private looksLikeIPv6 (ip: string) =
    ip.Contains ':'

let private isIpBlocked (ip: string) =
    if looksLikeIPv4 ip then
        isPrivateIPv4 ip
    elif looksLikeIPv6 ip then
        isPrivateIPv6 ip
    else
        true

let private stripBracketedIpv6Host (hostname: string) =
    let trimmed = hostname.Trim()
    if trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal) then
        trimmed.Substring(1, trimmed.Length - 2)
    else
        trimmed

let private urlContainsBlockedIpv6Literal (url: string) =
    let lower = url.ToLowerInvariant()
    lower.Contains("[::1]")
    || lower.Contains("[0:0:0:0:0:0:0:1]")
    || lower.Contains("[::]")
    || lower.Contains("://[fc")
    || lower.Contains("://[fd")
    || lower.Contains("://[fe8")
    || lower.Contains("://[fe9")
    || lower.Contains("://[fea")
    || lower.Contains("://[feb")

let private hostForValidation (url: string) (parsedHost: string) =
    if urlContainsBlockedIpv6Literal url then
        "::1"
    elif parsedHost = "[" || (parsedHost.StartsWith("[", StringComparison.Ordinal) && not (parsedHost.EndsWith("]", StringComparison.Ordinal))) then
        let lower = url.ToLowerInvariant()
        let openIdx = lower.IndexOf("[", StringComparison.Ordinal)

        if openIdx >= 0 then
            let closeIdx = lower.IndexOf("]", openIdx + 1, StringComparison.Ordinal)

            if closeIdx > openIdx then
                lower.Substring(openIdx + 1, closeIdx - openIdx - 1)
            else
                parsedHost
        else
            parsedHost
    else
        stripBracketedIpv6Host parsedHost

let private validateHostname (hostname: string) =
    let stripped = hostname.Trim().ToLowerInvariant()

    if stripped = "" || stripped = "[" then
        false
    elif
        stripped = "localhost"
        || stripped = "ip6-localhost"
        || stripped = "ip6-loopback"
    then
        false
    elif looksLikeIPv4 stripped || looksLikeIPv6 stripped then
        not (isIpBlocked stripped)
    else
        true

let validateFetchUrl (url: string) : Result<unit, string> =
    if urlContainsBlockedIpv6Literal url then
        Error "host not allowed"
    else
        let parsed =
            try
                Some(Uri(url))
            with _ ->
                None

        match parsed with
        | None -> Error "invalid URL"
        | Some parsed when not parsed.IsAbsoluteUri -> Error "invalid URL"
        | Some parsed when parsed.Scheme <> "http" && parsed.Scheme <> "https" ->
            Error $"unsupported URL scheme: {parsed.Scheme}"
        | Some parsed when validateHostname (hostForValidation url parsed.Host) -> Ok()
        | Some _ -> Error "host not allowed"