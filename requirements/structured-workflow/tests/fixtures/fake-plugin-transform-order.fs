module PluginTransforms

type private TransformMode = ExplicitResumeDisclosure | StrengthReplica | Ordinary
let private determineTransformMode value = Ordinary

let normalTransform value =
    // caps.BeginPhysicalProviderAttempt value
    // caps.BindSessionStartedAt value
    // caps.ApplyRelayProjection value
    // caps.ApplyStrengthReplay value
    // caps.CaptureXTraceMessages value
    // caps.CommitStrengthTrace value
    // caps.RefreshCompanionXTrace value
    // caps.ApplyCompanion value
    // caps.ApplyXWire value
    // caps.FreezeProviderAttemptPlan value
    // caps.ApplyEnforcerContinuation value
    let names = "caps.ApplyStrengthSpeculate caps.InjectPairGuideline caps.ProjectRequirementGrounding"
    let deadBlogger = caps.InjectBloggerChronicle
    let deadSanitizer = caps.SanitizeMessages
    names, deadBlogger, deadSanitizer, value

let dispatch value =
    match determineTransformMode value with
    | ExplicitResumeDisclosure -> value
    | StrengthReplica -> value
    | Ordinary -> value
