namespace Wanxiangshu.Enforcer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Enforcer.Cycle

module ObservationSurface =

    val pairTipsAndFrames: string array -> obj array -> obj array
    val ofTipsAndFrames: obj array -> string array -> obj array
    val workLogFromUnits: obj array -> obj array -> obj array
    val emptyEnforcement: obj
    val applyEnforcementCycle: obj -> obj -> obj
    val applyEnforcementSquash: int -> obj -> obj
    val recentTips: obj -> obj array
    val enforcementRecordCount: obj -> int
    val emptyBlog: obj
    val blogFrame: obj -> obj
    val applyBlogEntry: obj -> obj -> obj -> obj
    val applyBlogSquash: obj -> obj -> obj -> obj
    val frameCount: obj -> int
    val frameKinds: obj -> string array
    val coverage: obj -> obj
    val observationsOf: obj -> obj -> obj array
    val observationsAfterSquash: int -> obj -> obj -> obj array
