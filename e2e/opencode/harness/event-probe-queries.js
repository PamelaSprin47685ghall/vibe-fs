/**
 * event-probe-queries.js — Public read-only queries on EventProbe state.
 * Attached to the EventProbe prototype via mixin so the main class file
 * stays within the 200-line Kolmogorov line budget.
 */

export function attachEventProbeQueries(proto) {
  Object.defineProperty(proto, 'lastSeq', {
    get() { return this._seq; },
  });

  proto.bySession = function bySession(sessionID) {
    return this._events.filter(e => e.sessionID === sessionID);
  };

  proto.bySessionAfter = function bySessionAfter(sessionID, minSeq) {
    return this._events.filter(e => e.sessionID === sessionID && e.seq > minSeq);
  };

  proto.count = function count(type, sessionID) {
    return this._events.filter(e => {
      if (e.type !== type) return false;
      if (sessionID !== undefined && e.sessionID !== sessionID) return false;
      return true;
    }).length;
  };

  proto.countAfter = function countAfter(type, sessionID, minSeq) {
    return this._events.filter(e => {
      if (e.type !== type) return false;
      if (sessionID !== undefined && e.sessionID !== sessionID) return false;
      if (minSeq !== undefined && e.seq <= minSeq) return false;
      return true;
    }).length;
  };
}
