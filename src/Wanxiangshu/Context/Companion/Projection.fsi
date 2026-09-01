namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Foundation.Identity

type CompanionProjection =
    { BloggerSessionId: SessionId option }

module CompanionProjection =
    val empty: CompanionProjection
    val linkBlogger: bloggerSessionId: SessionId -> current: CompanionProjection -> CompanionProjection
    val closeBlogger: current: CompanionProjection -> CompanionProjection
