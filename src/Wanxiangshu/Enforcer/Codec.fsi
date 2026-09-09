namespace Wanxiangshu.Enforcer

module EnforcerCodec =

    type CanonicalBlogCall =
        { Text: string option
          Evidence: string option
          Tip: EnforcerTip }

    [<Literal>]
    val MissingTipError: string = "missing required argument: tip"

    val decodeCall: Wanxiangshu.Enforcer.EnforcerRule list -> Map<string, obj> -> Result<CanonicalBlogCall, string>
    val hasValidText: CanonicalBlogCall -> bool
