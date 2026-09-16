import assert from 'node:assert/strict'
import test from 'node:test'
import * as cleanBreak from '../../../dist/Context/Companion/BloggerBoundaryCleanBreakSurface.js'
import * as convergence from '../../../dist/Context/Companion/BloggerConvergenceGapsSurface.js'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as seal from '../../../dist/Context/Companion/BloggerSealReactivateSurface.js'
import * as ordinary from '../../../dist/Context/Companion/CompanionOrdinaryMaterialSurface.js'
import * as companion from '../../../dist/Context/Companion/CompanionSurface.js'
import * as commitConv from '../../../dist/Context/Companion/EnforcerCycleCommitConvergenceSurface.js'
import * as cycleConv from '../../../dist/Context/Companion/EnforcerCycleConvergenceSurface.js'
import * as frameLoader from '../../../dist/Context/Companion/EnforcerFrameLoaderSurface.js'
import * as openReload from '../../../dist/Context/Companion/EnforcerOpenReloadSurface.js'
import * as parked from '../../../dist/Context/Companion/ParkedTransformSurface.js'

const KEY = 'ses-blog'

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_sealed_stays_sealed_cancel_wakes_waiter_and_release_clears_flight', () => {
  assert.ok(cleanBreak.sealedStaysSealed)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_material_mailbox_waiter_first_delivers_directly', () => {
  assert.ok(cleanBreak.waiterFirstDeliversDirectly)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_material_mailbox_material_first_stages_for_next_park', () => {
  assert.ok(cleanBreak.materialFirstStages)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_material_mailbox_newest_covers_supersedes_older_offer', () => {
  assert.ok(cleanBreak.newestCoversSupersedes)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_flight_lease_dispose_clears_exact_flight', () => {
  assert.ok(cleanBreak.disposeClearsFlight)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_blogger_lifecycle_authority_is_physical_ownership', () => {
  assert.ok(convergence.lifecycleAuthorityIsPhysical)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_exact_flight_lease_is_the_only_busy_definition', () => {
  assert.ok(convergence.flightLeaseIsOnlyBusy)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_material_mailbox_and_flight_lease_are_separate_resources', () => {
  assert.ok(convergence.mailboxAndLeaseSeparate)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_commit_uses_live_InFlight_only_not_open_heal', () => {
  assert.ok(convergence.commitUsesLiveInFlight)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_single_main_material_coordinator_entry', () => {
  assert.ok(convergence.singleMainCoordinatorEntry)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_no_BloggerNeedsReset_full_X_replay', () => {
  assert.ok(convergence.noNeedsResetFullReplay)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_first_request_does_not_extract_raw_user_toml', () => {
  assert.ok(convergence.firstRequestNoRawUserToml)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_squash_path_does_not_SubscribeTerminal', () => {
  assert.ok(convergence.squashNoSubscribeTerminal)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_squash_constructs_typed_BloggerRequestContext_Squash_in_production', () => {
  assert.ok(convergence.squashConstructsTypedContext)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_park_only_after_KnownCommitted', () => {
  assert.ok(convergence.parkOnlyAfterCommitted)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_commit_refreshes_durable_coverage_before_park', () => {
  assert.ok(convergence.commitRefreshesDurableCoverage)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_caught_up_is_parked_not_completed_and_wake_rechecks_live_Current', () => {
  assert.ok(convergence.caughtUpIsParkedNotCompleted)
})

test('WHAT[CONTEXT-COMPRESSION-018] C0_adopted_blogger_motion_is_not_active_PENDING', () => {
  assert.ok(convergence.adoptedMotionNotPending)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_idle_plus_material_starts', () => {
  const scope = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(scope, KEY, runtime.main({ toml: 'work' })), 'Claimed')
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_inflight_plus_material_skips_without_queue', () => {
  const scope = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'req-first', toml: 'work' })), 'Claimed')
  assert.equal(runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'req-second', toml: 'more' })), 'Conflict:req-first')
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_open_producer_between_steps_stages_material', () => {
  const scope = runtime.scope()
  assert.equal(runtime.offerParked(scope, KEY, runtime.main({ toml: 'more' })), 'Staged')
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_cycle_commit_clears_flight', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'request-main', toml: 'work' }))
  runtime.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(runtime.tryGetFlight(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_idle_plus_parked_waiter_offers', async () => {
  const scope = runtime.scope()
  const waiter = runtime.park(scope, KEY)
  assert.equal(runtime.offerParked(scope, KEY, runtime.main({ toml: 'more' })), 'Delivered')
  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_clear_flight_is_idempotent', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'request-main', toml: 'work' }))
  runtime.releaseCurrentRequest(scope, KEY, 'request-main')
  runtime.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(runtime.tryGetFlight(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_clear_without_flight_is_idempotent', () => {
  const scope = runtime.scope()
  runtime.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(runtime.tryGetFlight(scope, KEY), null)
})

test('WHAT[PAR-017] Blogger retry replaces exact physical ownership before the next binding', () => {
  const scope = runtime.scope()
  const failed = runtime.main({ requestId: 'request-failed', toml: 'failed' })
  const replacement = runtime.main({ requestId: 'request-replacement', toml: 'replacement' })
  assert.equal(runtime.claimCurrentRequest(scope, KEY, failed), 'Claimed')
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'request-failed'), 'Released')
  assert.equal(runtime.claimCurrentRequest(scope, KEY, replacement), 'Claimed')
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'request-failed'), 'Conflict:request-replacement')
  assert.equal(runtime.peekCurrentRequest(scope, KEY)?.toml, 'replacement')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_squash_commit_clears_flight', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'request-main', toml: 'work' }))
  runtime.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(runtime.tryGetFlight(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_session_delete_is_registry_removal_not_a_cell_state', () => {
  const scope = runtime.scope()
  runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'request-main', toml: 'work' }))
  runtime.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(runtime.tryGetFlight(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_two_inflight_contexts_cannot_coexist', () => {
  const scope = runtime.scope()
  assert.equal(runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'req-first', toml: 'work' })), 'Claimed')
  assert.equal(runtime.claimCurrentRequest(scope, KEY, runtime.main({ requestId: 'req-second', toml: 'more' })), 'Conflict:req-first')
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_waiter_offer_does_not_register_flight', () => {
  const scope = runtime.scope()
  assert.equal(runtime.offerParked(scope, KEY, runtime.main({ toml: 'more' })), 'Staged')
  assert.equal(runtime.tryGetFlight(scope, KEY), null)
  runtime.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] HANDLE_lifecycle_CompletedAwaitingJoin_and_Retired_seal_blogger', () => {
  assert.ok(seal.completedAndRetiredSealBlogger)
})

test('WHAT[CONTEXT-COMPRESSION-018] HANDLE_lifecycle_Abandoned_seals_blogger', () => {
  assert.ok(seal.abandonedSealsBlogger)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_cell_has_no_sealed_mirror_durable_is_truth', () => {
  assert.ok(seal.durableIsTrue)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_park_and_offer_material_mailbox', () => {
  assert.ok(seal.parkAndOfferMailbox)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_staged_offer_delivers_to_next_park', () => {
  assert.ok(seal.stagedOfferDelivers)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_flight_lease_claim_and_release', () => {
  assert.ok(seal.flightLeaseClaimAndRelease)
})

test('WHAT[CONTEXT-COMPRESSION-018] CompanionTransform owns ordinary-material entry and consumes Host suppression as a capability', () => {
  assert.ok(ordinary.companionTransformOwnsOrdinary)
})

test('WHAT[CONTEXT-COMPRESSION-018] explicit-resume nudge path with marked suppression prevents companion double-send', () => {
  assert.ok(ordinary.explicitResumePreventsDoubleSend)
})

test('WHAT[CONTEXT-COMPRESSION-018] fresh binding without suppression history admits ordinary material', () => {
  assert.ok(ordinary.freshBindingAdmitsOrdinary)
})

test('WHAT[CONTEXT-COMPRESSION-018] COMPANION_018_first_turn_shape_is_false_when_historic_frames_or_tips_present', () => {
  assert.ok(companion.firstTurnShapeIsFalse)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_same_run_after_squash_rejected_as_known_not_committed', () => {
  assert.ok(commitConv.sameRunAfterSquashRejected)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_open_without_promptkey_binding_is_unexpected_end', () => {
  assert.ok(commitConv.openWithoutPromptKeyBinding)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_open_bound_promptkey_commits_and_clears_open', () => {
  assert.ok(commitConv.openBoundCommitsAndClears)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_catchup_drains_next_window_after_idempotent_receipt', () => {
  assert.ok(commitConv.catchupDrainsNextWindow)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_park_cancel_is_the_only_non_material_wake', () => {
  assert.ok(commitConv.parkCancelIsOnlyNonMaterialWake)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_caught_up_park_absorbs_future_material_beyond_previous_head_without_frozen_frontier', () => {
  assert.ok(commitConv.caughtUpParkAbsorbs)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_park_resumed_with_flight_projects_directly', () => {
  assert.ok(commitConv.parkResumedProjectsDirectly)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_no_journal_projects_raw_messages', () => {
  assert.ok(commitConv.noJournalProjectsRaw)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_no_journal_empty_messages_is_empty_projection_fatal', () => {
  assert.ok(commitConv.noJournalEmptyMessagesFatal)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_first_request_rebuilds_from_typed_context', () => {
  assert.ok(commitConv.firstRequestRebuilds)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_blog_tool_without_CurrentRequest_rejects_not_ok', () => {
  assert.ok(cycleConv.blogToolWithoutCurrentRequest)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_live_blog_without_CurrentRequest_and_without_open_is_fatal', () => {
  assert.ok(cycleConv.liveBlogWithoutCurrentRequest)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_empty_delta_terminal_is_fatal', () => {
  assert.ok(cycleConv.emptyDeltaTerminalFatal)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_with_live_request_commits_and_advances_coverage', () => {
  assert.ok(cycleConv.hostCompletedBlogCommits)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_without_live_request_is_noop_not_commit', () => {
  assert.ok(cycleConv.hostCompletedBlogWithoutLiveRequest)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_second_pass_same_run_is_idempotent', () => {
  assert.ok(cycleConv.hostCompletedBlogSecondPassIdempotent)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_host_completed_blog_second_window_advances_coverage_not_resend', () => {
  assert.ok(cycleConv.hostCompletedBlogSecondWindow)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_resolveCycleContext_prefers_live_inflight_request', () => {
  assert.ok(cycleConv.resolveCycleContextPrefersLive)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_missing_association', () => {
  assert.ok(frameLoader.loadFramesMissingAssociation)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_empty_ok', () => {
  assert.ok(frameLoader.loadFramesEmptyOk)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_resolves_committed_frame', () => {
  assert.ok(frameLoader.loadFramesResolvesCommitted)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_missing_blob_fails_closed', () => {
  assert.ok(frameLoader.loadFramesMissingBlob)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_load_effective_frames_digest_mismatch_fails_closed', () => {
  assert.ok(frameLoader.loadFramesDigestMismatch)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_rebuild_falls_back_to_raw_when_frame_blob_lost', () => {
  assert.ok(frameLoader.rebuildFallsBackToRaw)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_contribution_preserves_raw_identity', () => {
  assert.ok(frameLoader.contributionPreservesRawIdentity)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_main_context_from_open_materialization', () => {
  assert.ok(openReload.reloadMainContext)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_squash_context_from_open_materialization', () => {
  assert.ok(openReload.reloadSquashContext)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_defaults_when_blob_is_sparse', () => {
  assert.ok(openReload.reloadDefaultsWhenSparse)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_parses_string_numbers_and_derives_delta_digest', () => {
  assert.ok(openReload.reloadParsesStringNumbers)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_reload_derives_delta_digest_from_context_digest_when_toml_empty', () => {
  assert.ok(openReload.reloadDerivesDigest)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_resolve_cycle_prefers_live_request_over_open', () => {
  assert.ok(openReload.resolveCyclePrefersLive)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_frame_count_beyond_existing_frames_abandons', () => {
  assert.ok(openReload.squashFrameCountBeyondAbandons)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_frame_epoch_mismatch_abandons', () => {
  assert.ok(openReload.squashFrameEpochMismatchAbandons)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_frame_digests_mismatch_abandons', () => {
  assert.ok(openReload.squashFrameDigestsMismatchAbandons)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_squash_other_blogger_session_abandons', () => {
  assert.ok(openReload.squashOtherSessionAbandons)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_160_material_event_resumes_park_with_typed_context', () => {
  assert.ok(parked.materialEventResumesPark)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_162_cancel_is_an_explicit_event', () => {
  assert.ok(parked.cancelIsExplicitEvent)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_050_offer_first_is_delivered_by_the_next_await', () => {
  assert.ok(parked.offerFirstIsDelivered)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_160_two_awaits_share_one_material_event', () => {
  assert.ok(parked.twoAwaitsShareEvent)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_162_dispose_cancels_every_material_wait', () => {
  assert.ok(parked.disposeCancelsWait)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_162_cancel_drops_staged_material_without_touching_flight', () => {
  assert.ok(parked.cancelDropsStagedMaterial)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_seal_cancels_wait_without_revoking_existing_flight', () => {
  assert.ok(parked.sealCancelsWait)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_161_sessions_are_independent', () => {
  assert.ok(parked.sessionsIndependent)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_CurrentRequest_is_physical_flight_ownership', () => {
  assert.ok(parked.currentRequestIsPhysicalFlight)
})
