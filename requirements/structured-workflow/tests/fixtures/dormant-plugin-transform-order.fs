module PluginTransforms

type private TransformMode = ExplicitResumeDisclosure | StrengthReplica | Ordinary
let private determineTransformMode value = Ordinary

let normalTransform value =
    let nested () =
        caps.BeginPhysicalProviderAttempt value
        caps.BindSessionStartedAt value
        caps.ApplyStrengthReplay value
    let delayed = fun () ->
        caps.ApplyXTracePipeline value
        caps.ApplyCompanion value
        caps.ApplyXWire value
    if false then
        caps.ApplyEnforcerContinuation value
        caps.ApplyStrengthSpeculate value
        caps.InjectPairGuideline value
    let dead =
        caps.ProjectRequirementGrounding value
        caps.InjectBloggerChronicle value
        caps.SanitizeMessages value
        caps.InterruptAfterSubmittedJudgement value
    value

let dispatch value =
    match determineTransformMode value with
    | ExplicitResumeDisclosure -> value
    | StrengthReplica -> value
    | Ordinary -> value
