namespace Wanxiangshu.Persistence.Journal

open Thoth.Json

[<RequireQualifiedAccess>]
module PromptFactCodec =
    val withCoder: baseExtra: ExtraCoders -> ExtraCoders
