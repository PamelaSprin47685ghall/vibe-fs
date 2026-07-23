/**
 * scenario-turn.js — Turn-scoped terminal oracle.
 *
 * The previous `waitForSessionIdle` had three false-green paths:
 *   1. Any historical session.idle event satisfied the next wait.
 *   2. HTTP /session/status returning 'idle' immediately satisfied
 *      without proving the turn ever entered running/busy.
 *   3. The session disappearing from the status map was treated as
 *      idle without proving activity.
 *
 * `Turn.start` records the event watermark before sending a prompt.
 * `awaitTerminal` then requires two pieces of evidence:
 *   - requireActivity: at least one event with seq > eventSeqBefore,
 *     proving the turn entered non-idle territory.
 *   - requireIdleAfterActivity: a session.idle / session.status: idle
 *     event with seq > the activity watermark. Historical idle
 *     events from prior turns do NOT satisfy this — that's the
 *     PR1 fix for the original false-green path #1.
 *
 * For PR1, requireAssistantTerminal defaults to false so existing
 * canary tests (which use t.client.prompt + t.turn.start().awaitTerminal
 * without expecting a typed message) keep running. PR2 will flip
 * the default to true once each canary is rewritten to assert the
 * exact assistant message oracle.
 */

const IDLE_TYPES = ['session.idle'];

function isIdleEvent(e) {
  if (e.type === 'session.idle') return true;
  if (e.type === 'session.status') {
    const s = e.status ?? e.properties?.status;
    if (s === 'idle') return true;
    if (s && typeof s === 'object') {
      return s.type === 'idle' || s.status === 'idle';
    }
  }
  return false;
}

export function createScenarioTurn(scenario) {
  return {
    start: (sessionID) => new Turn(scenario, sessionID),
  };
}

class Turn {
  constructor(scenario, sessionID) {
    this._scenario = scenario;
    this._sessionID = sessionID || null;
    this._startedAt = Date.now();
    this._eventSeqBefore = scenario.events.lastSeq;
    this._activitySeq = null;
  }

  _matchesSession(e) {
    if (!this._sessionID) return true;
    const es = e.sessionID ?? e.properties?.sessionID;
    return es === this._sessionID;
  }

  get eventSeqBefore() { return this._eventSeqBefore; }
  get activitySeq() { return this._activitySeq; }

  /**
   * Await the terminal evidence:
   *   requireActivity           — at least one event with seq > before
   *   requireAssistantTerminal  — at least one assistant message event
   *                              (default false; PR1 keeps legacy
   *                              canary behaviour)
   *   requireIdleAfterActivity  — session.idle with seq > activitySeq
   *                              (default true; this is what blocks
   *                              the historical-idle false-green path)
   *
   * Throws if any required piece is missing by timeoutMs.
   */
  async awaitTerminal(opts = {}) {
    const o = {
      timeoutMs: opts.timeoutMs || 60000,
      requireActivity: opts.requireActivity !== false,
      requireAssistantTerminal: opts.requireAssistantTerminal !== false,
      requireIdleAfterActivity: opts.requireIdleAfterActivity !== false,
    };
    if (o.requireActivity) {
      const activityEvent = await this._awaitActivity(o.timeoutMs);
      this._activitySeq = activityEvent.seq;
    } else {
      this._activitySeq = this._eventSeqBefore;
    }
    if (o.requireAssistantTerminal) {
      await this._awaitAssistantTerminal(o.timeoutMs);
    }
    if (o.requireIdleAfterActivity) {
      await this._awaitIdleAfterActivity(o.timeoutMs);
    }
  }

  async _awaitActivity(timeoutMs) {
    try {
      return await this._scenario.events.awaitEvent(
        (e) => e.seq > this._eventSeqBefore && this._matchesSession(e) && !isIdleEvent(e),
        timeoutMs,
      );
    } catch (err) {
      throw new Error(`turn-activity: ${err.message}`);
    }
  }

  async _awaitIdleAfterActivity(timeoutMs) {
    try {
      await this._scenario.events.awaitEvent(
        (e) => isIdleEvent(e) && e.seq > this._activitySeq && this._matchesSession(e),
        timeoutMs,
      );
    } catch (err) {
      throw new Error(`turn-idle-after-activity: ${err.message}`);
    }
  }

  async _awaitAssistantTerminal(timeoutMs) {
    try {
      await this._scenario.events.awaitEvent(
        (e) => e.type === 'message.updated' && Boolean(e.finishReason) && e.seq > this._eventSeqBefore && this._matchesSession(e),
        timeoutMs,
      );
    } catch (err) {
      throw new Error(`turn-assistant-terminal: ${err.message}`);
    }
  }
}
