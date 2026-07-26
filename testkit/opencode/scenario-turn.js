/**
 * scenario-turn.js — Strictly event-driven turn-scoped oracle.
 *
 * Uses push event notifications via `events.awaitEvent(...)`.
 * 100% Event-driven; ZERO polling loops.
 */

import { WATCHDOG_TIMEOUT_MS } from './watchdog-constants.js';

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
    start: (sessionID, options = {}) => new Turn(scenario, sessionID, options),
  };
}

class Turn {
  constructor(scenario, sessionID, options) {
    this._scenario = scenario;
    this._sessionID = sessionID || null;
    this._startedAt = Date.now();
    this._eventSeqBefore = options.afterSeq ?? scenario.events.lastSeq;
    this._activitySeq = null;
    this._terminalSeq = null;
  }

  _matchesSession(e) {
    if (!this._sessionID) return true;
    const es = e.sessionID ?? e.properties?.sessionID;
    return es === this._sessionID;
  }

  get eventSeqBefore() { return this._eventSeqBefore; }
  get activitySeq() { return this._activitySeq; }
  get terminalSeq() { return this._terminalSeq; }

  async awaitTerminal(opts = {}) {
    const o = {
      timeoutMs: opts.timeoutMs || WATCHDOG_TIMEOUT_MS,
      requireActivity: opts.requireActivity !== false,
      requireAssistantTerminal: opts.requireAssistantTerminal !== false,
      requireIdleAfterActivity: opts.requireIdleAfterActivity !== false,
    };
    if (o.requireActivity) {
      const activityEvent = await this._awaitActivity(o.timeoutMs);
      this._activitySeq = activityEvent.seq;
      this._recordProgress('turn-activity');
    } else {
      this._activitySeq = this._eventSeqBefore;
    }
    if (o.requireAssistantTerminal) {
      const terminalEvent = await this._awaitAssistantTerminal(o.timeoutMs);
      this._terminalSeq = terminalEvent.seq;
      this._recordProgress('turn-assistant-terminal');
    }
    if (o.requireIdleAfterActivity) {
      await this._awaitIdleAfterActivity(o.timeoutMs);
      this._recordProgress('turn-idle-after-activity');
    }
  }

  _recordProgress(reason) {
    this._scenario.watchdog?.advance({
      reason,
      lane: `session:${this._sessionID || 'any'}`,
      blocking: true,
    });
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
      return await this._scenario.events.awaitEvent(
        (e) => e.type === 'message.updated' && Boolean(e.finishReason) && e.seq > this._eventSeqBefore && this._matchesSession(e),
        timeoutMs,
      );
    } catch (err) {
      throw new Error(`turn-assistant-terminal: ${err.message}`);
    }
  }
}
