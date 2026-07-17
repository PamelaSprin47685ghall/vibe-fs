module Wanxiangshu.Runtime.WebFetch

open Wanxiangshu.Kernel.WebFetchGuard

/// OMP / Ollama webfetch SSRF gate — pure URL parse, no network.
let validateFetchUrlForOmp (url: string) : string option =
    match validateFetchUrl url with
    | Ok() -> None
    | Error msg -> Some msg
