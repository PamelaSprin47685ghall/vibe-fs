module VibeFs.Tests.OmpWebFetchTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.WebFetchGuard

let blocksLocalhostAndPrivateRanges () =
    let blocked =
        [| "http://localhost/"
           "http://127.0.0.1/"
           "http://0.0.0.0/"
           "http://[::1]/"
           "http://10.0.0.1/"
           "http://192.168.1.1/"
           "http://169.254.169.254/" |]
    for url in blocked do
        match validateFetchUrl url with
        | Error msg ->
            check ("blocked " + url) (msg.Contains "not allowed" || msg.Contains "invalid" || msg.Contains "scheme")
        | Ok () -> check ("expected block " + url) false

let rejectsUnsupportedScheme () =
    match validateFetchUrl "file:///etc/passwd" with
    | Error msg -> check "file scheme rejected" (msg.Contains "scheme")
    | Ok () -> check "file scheme must not pass" false