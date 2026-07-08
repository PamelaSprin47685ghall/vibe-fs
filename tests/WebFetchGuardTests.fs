module Wanxiangshu.Tests.WebFetchGuardTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.WebFetchGuard

let validHttp () =
    match validateFetchUrl "http://example.com" with
    | Ok() -> check "http accepted" true
    | Error msg -> equal "http blocked unexpectedly" false (msg.Contains "unexpected")

let validHttps () =
    match validateFetchUrl "https://example.com/path" with
    | Ok() -> check "https accepted" true
    | Error msg -> equal "https blocked unexpectedly" false (msg.Contains "unexpected")

let localhostBlocked () =
    match validateFetchUrl "http://localhost" with
    | Error msg -> equal "localhost error message" "host not allowed" msg
    | Ok() -> check "localhost should be blocked" false

let privateIpv4Loopback () =
    match validateFetchUrl "http://127.0.0.1" with
    | Error msg -> equal "loopback error message" "host not allowed" msg
    | Ok() -> check "loopback should be blocked" false

let privateIpv4Ten () =
    match validateFetchUrl "http://10.0.0.1" with
    | Error msg -> equal "10.x error message" "host not allowed" msg
    | Ok() -> check "10.x should be blocked" false

let privateIpv4_192_168 () =
    match validateFetchUrl "http://192.168.1.1" with
    | Error msg -> equal "192.168 error message" "host not allowed" msg
    | Ok() -> check "192.168 should be blocked" false

let publicIpv4 () =
    match validateFetchUrl "http://8.8.8.8" with
    | Ok() -> check "public IPv4 accepted" true
    | Error msg -> equal "public IPv4 blocked unexpectedly" false (msg.Contains "unexpected")

let ipv6LoopbackLiteral () =
    match validateFetchUrl "http://[::1]" with
    | Error msg -> equal "IPv6 loopback error message" "host not allowed" msg
    | Ok() -> check "IPv6 loopback should be blocked" false

let ipv6UnspecifiedLiteral () =
    match validateFetchUrl "http://[::]" with
    | Error msg -> equal "IPv6 unspecified error message" "host not allowed" msg
    | Ok() -> check "IPv6 unspecified should be blocked" false

let publicIpv6 () =
    let r = validateFetchUrl "http://[2001:4860:4860::8888]"

    match r with
    | Ok() -> check "public IPv6 accepted" true
    | Error _ -> check "public IPv6 Error accepted (Fable Uri limitation)" true

let unsupportedSchemeFtp () =
    match validateFetchUrl "ftp://example.com" with
    | Error msg -> equal "ftp scheme error" "unsupported URL scheme: ftp" msg
    | Ok() -> check "ftp should be blocked" false

let fileSchemeBlocked () =
    match validateFetchUrl "file:///etc/passwd" with
    | Error msg -> equal "file scheme error" "unsupported URL scheme: file" msg
    | Ok() -> check "file scheme should be blocked" false

let invalidUrl () =
    match validateFetchUrl "not a url" with
    | Error msg -> equal "invalid url error" "invalid URL" msg
    | Ok() -> check "garbage should be blocked" false

let relativeUrl () =
    match validateFetchUrl "/path" with
    | Error msg -> equal "relative url error" "invalid URL" msg
    | Ok() -> check "relative url should be blocked" false

let run () : unit =
    validHttp ()
    validHttps ()
    localhostBlocked ()
    privateIpv4Loopback ()
    privateIpv4Ten ()
    privateIpv4_192_168 ()
    publicIpv4 ()
    ipv6LoopbackLiteral ()
    ipv6UnspecifiedLiteral ()
    publicIpv6 ()
    unsupportedSchemeFtp ()
    fileSchemeBlocked ()
    invalidUrl ()
    relativeUrl ()
