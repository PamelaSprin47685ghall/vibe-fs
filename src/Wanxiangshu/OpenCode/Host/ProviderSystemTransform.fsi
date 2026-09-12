namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HOST-026 / PROMPT-017: project the session-bound ProviderLanguage onto the
/// Wanxiangshu-owned system-prompt segment without disturbing Host/AGENTS text.
module ProviderSystemTransform =
    val createWith: role: (SessionId -> Role option) -> (obj -> obj -> Task<unit>)
