namespace Wanxiangshu.Foundation

/// JS-native contract view for Foundation outcome vocabularies. The arrays are
/// stable domain names; Fable union representation and reflection metadata do
/// not cross the boundary.
module OutcomeSurface =

    let sendOutcomeKinds () : string array =
        [| "AdmittedWithReceipt"
           "AdmittedWithPhysicalMessage"
           "Retryable"
           "AcceptanceUnknown"
           "Fatal" |]

    let sessionErrorKinds () : string array =
        [| "NoProgress"
           "SessionCancelled"
           "AutoRecoveryExhausted"
           "ReviewExhausted"
           "PromptUncertain"
           "ProjectionBroken"
           "InboxFull"
           "Protocol" |]
