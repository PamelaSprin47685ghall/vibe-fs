namespace Wanxiangshu.Enforcer.Cycle

module EnforcerCycleDecode =

    [<Literal>]
    val EmptyTextError: string = "blog cycle text is empty after canonicalisation (ENFORCER-043)"

    val lastAssistantStep: obj list -> (string * obj list * bool) option

    val extractCalls:
        emitDiagnostic: (string -> (string * string) list -> unit) ->
        obj list ->
            (string *
            (int * Wanxiangshu.Foundation.Identity.ToolCallId * Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall) list *
            bool) option

    val validateCycle:
        string ->
        (int * Wanxiangshu.Foundation.Identity.ToolCallId * Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall) list ->
            Result<
                (Wanxiangshu.Enforcer.Cycle.EnforcerCycle.CanonicalCycle *
                Wanxiangshu.Foundation.Identity.ToolCallId list),
                string
             >
