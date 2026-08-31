module DeclaredRetry

/// semantic-decorator-owner: structured-workflow
/// semantic-decorator-WHAT: STRUCTURED-WORKFLOW-008
/// semantic-decorator-trace-relation: R_bounded_retry=at-most-two-attempts-one-outcome
/// semantic-decorator-proof: requirements/structured-workflow/tests/workflow-constitution.test.mjs::WHAT[STRUCTURED-WORKFLOW-008] anonymous_retry_is_RED_but_declared_bounded_retry_is_GREEN
/// semantic-decorator-failure-policy: retry-only-the-declared-transient-failure-then-return-last-failure
/// semantic-decorator-cancel-policy: cancellation-propagates-without-retry
/// semantic-decorator-deadline-policy: caller-deadline-is-shared-and-never-extended
/// semantic-decorator-retry-bound: 1
let withBoundedRetry operation input =
    task {
        try
            return! operation input
        with :? System.TimeoutException ->
            return! operation input
    }
