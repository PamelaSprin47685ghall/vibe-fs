// Trusted realm source for the isolated candidate. Raw values stay in this closure.
export const callableBase = `const JsProgram = (() => {
  const apply = Reflect.apply;
  const construct = Reflect.construct;
  const get = Reflect.get;
  const keys = Reflect.ownKeys;
  const prototype = Object.getPrototypeOf;
  const define = Object.defineProperty;
  const freeze = Object.freeze;
  const hasOwn = Object.prototype.hasOwnProperty;
  const array = Array.isArray;
  const finite = Number.isFinite;
  const integer = Number.isSafeInteger;
  const MapClass = Map;
  const mapGet = Map.prototype.get;
  const mapSet = Map.prototype.set;
  const mapHas = Map.prototype.has;
  const mapDelete = Map.prototype.delete;
  const mapForEach = Map.prototype.forEach;
  const table = () => {
    const values = new MapClass();
    return {
      get: key => apply(mapGet, values, [key]),
      set: (key, value) => apply(mapSet, values, [key, value]),
      has: key => apply(mapHas, values, [key]),
      delete: key => apply(mapDelete, values, [key]),
      each: visit => apply(mapForEach, values, [visit]),
    };
  };
  const entries = table();
  const offers = table();
  const completedOffers = table();
  let serial = 0;
  let api;
  let incarnation;
  const failure = (code, message) => {
    const error = new Error(message);
    error.__jsFailure = { code, reason: message };
    error.code = code;
    return error;
  };
  const ref = (kind, value, offer) => {
    const token = freeze({ runId: incarnation, kind, id: ++serial });
    entries.set(token.id, { token, value, offer });
    return token;
  };
  const known = (token, kind) => {
    if (!token || token.runId !== incarnation || token.kind !== kind) throw failure('CALLABLE_REF_INVALID', 'Wrong reference');
    const entry = entries.get(token.id);
    if (!entry || entry.token.kind !== kind || !offers.has(entry.offer)) throw failure('CALLABLE_REF_INVALID', 'Retired reference');
    return entry;
  };
  const passive = (value, seen = new Set()) => {
    if (value === null || value === undefined || typeof value === 'string' || typeof value === 'boolean') return value;
    if (typeof value === 'number' && finite(value)) return value;
    if (typeof value !== 'object') throw failure('CALL_RESULT_INVALID', 'Result is not passive data');
    if (seen.has(value)) throw failure('CALL_RESULT_INVALID', 'Cyclic result');
    seen.add(value);
    try {
      const toJSON = get(value, 'toJSON');
      if (typeof toJSON === 'function') return passive(apply(toJSON, value, []), seen);
      const result = array(value) ? [] : {};
      if (array(value)) result.length = value.length;
      if (!array(value) && prototype(value) !== Object.prototype && prototype(value) !== null) throw failure('CALL_RESULT_INVALID', 'Result is not a plain object');
      for (const key of keys(value)) {
        if (array(value) && key === 'length') continue;
        if (typeof key !== 'string') throw failure('CALL_RESULT_INVALID', 'Symbol result key');
        define(result, key, { value: passive(get(value, key), seen), enumerable: true, configurable: true, writable: true });
      }
      return result;
    } finally { seen.delete(value); }
  };
  const discard = offer => {
    entries.each((entry, id) => { if (entry.offer === offer) entries.delete(id); });
  };
  const spec = instance => {
    const maxDuration = get(instance, 'maxDuration');
    const next = get(instance, 'next');
    const watch = get(instance, 'watch');
    const interval = get(instance, 'interval');
    const budget = get(instance, 'outputBudgetBytes');
    const worldLock = get(instance, 'worldLock');
    if (typeof maxDuration !== 'number' || !finite(maxDuration) || maxDuration <= 0) throw failure('CALLABLE_SPEC_INCOMPLETE', 'Callable must declare a positive maxDuration');
    if (typeof next !== 'function' || typeof watch !== 'function' || typeof get(instance, 'sanity') !== 'function') throw failure('CALLABLE_SPEC_INCOMPLETE', 'Callable methods are incomplete');
    if (budget != null && (typeof budget !== 'number' || !integer(budget) || budget < 0)) throw failure('CALL_OUTPUT_BUDGET_INVALID', 'Invalid output budget');
    if (worldLock != null && typeof worldLock !== 'boolean') throw failure('CALL_WORLD_LOCK_INVALID', 'Invalid worldLock');
    return { maxDuration, interval: typeof interval === 'number' && finite(interval) ? interval : undefined, outputBudgetBytes: budget == null ? 65536 : budget, worldLock: worldLock === true, hasNext: true, hasWatch: true, hasSanity: true, hasOnExit: typeof get(instance, 'onExit') === 'function' };
  };
  const dispatch = async command => {
    if (command.operation === 'finishOffer') {
      const entry = known(command.ref, 'class');
      const offer = offers.get(entry.offer);
      offers.delete(entry.offer);
      discard(entry.offer);
      completedOffers.set(command.ref.id, { offer, value: command.value });
      return { finished: true };
    }
    if (command.operation === 'completeOffer') {
      const completion = completedOffers.get(command.ref.id);
      if (!completion) throw failure('CALLABLE_OFFER_RETIRED', 'Completion is not pending');
      completedOffers.delete(command.ref.id);
      completion.offer.resolve(completion.value);
      return null;
    }
    if (command.operation === 'abandonOffers') {
      offers.each((offer, id) => {
        offers.delete(id);
        discard(id);
        const error = new Error('ABANDONED');
        error.__jsAbandoned = true;
        offer.reject(error);
      });
      return null;
    }
    const kind = command.operation === 'construct' ? 'class' : 'instance';
    const entry = known(command.ref, kind);
    if (command.operation === 'construct') {
      if (command.role !== 'proof' && command.role !== 'execution') throw failure('CALLABLE_ROLE_INVALID', 'Invalid instance role');
      return ref('instance', { instance: construct(entry.value, []), role: command.role }, entry.offer);
    }
    const { instance, role } = entry.value;
    if (command.operation === 'readSpec') return spec(instance);
    if (command.operation === 'sanity') {
      if (role !== 'proof') throw failure('CALLABLE_ROLE_INVALID', 'Sanity requires proof instance');
      const pair = await apply(get(instance, 'sanity'), instance, [null]);
      const nextDone = get(pair, 'nextDone');
      const watchVeto = get(pair, 'watchVeto');
      if (typeof nextDone !== 'function' || typeof watchVeto !== 'function') throw failure('CALLABLE_SANITY_INVALID', 'Sanity must return two thunks');
      return { nextDone: ref('thunk', { thunk: nextDone, instanceId: entry.token.id, method: 'next' }, entry.offer), watchVeto: ref('thunk', { thunk: watchVeto, instanceId: entry.token.id, method: 'watch' }, entry.offer) };
    }
    if (command.operation === 'invokeThunkAndApplyProof') {
      const thunk = known(command.thunk, 'thunk');
      if (role !== 'proof' || thunk.value.instanceId !== entry.token.id || thunk.value.method !== command.method) throw failure('CALLABLE_PROOF_FENCE', 'Proof reference mismatch');
      const args = await apply(thunk.value.thunk, undefined, []);
      if (!array(args)) return { argumentArray: false, accepted: false };
      const value = await apply(get(instance, command.method), instance, args);
      const accepted = command.method === 'next' ? value == null : value === false || (value != null && apply(hasOwn, value, ['abort']) && get(value, 'abort') != null);
      return { argumentArray: true, accepted };
    }
    if (command.operation === 'callMethod') {
      if (!['next', 'watch', 'onExit'].includes(command.method)) throw failure('CALLABLE_METHOD_INVALID', 'Unsupported method');
      return passive(await apply(get(instance, command.method), instance, [command.context]));
    }
    if (command.operation === 'release') {
      entries.delete(entry.token.id);
      entries.each((child, id) => { if (child.token.kind === 'thunk' && child.value.instanceId === entry.token.id) entries.delete(id); });
      return { released: true };
    }
    throw failure('CALLABLE_OPERATION_INVALID', 'Unsupported operation');
  };
  return class JsProgram {
    constructor(host) {
      api = host;
      incarnation = api.runId;
      api.installCallablePort(dispatch);
    }
    write(...args) { return api.write(...args); }
    async cat(...args) { return await api.cat(...args); }
    rm(...args) { return api.rm(...args); }
    async park(payload) { return await api.park(payload); }
    async offerCallable(callable) {
      if (typeof callable !== 'function') throw failure('CALLABLE_SPEC_INCOMPLETE', 'Expected a class');
      const id = ++serial;
      const promise = new Promise((resolve, reject) => { offers.set(id, { resolve, reject }); });
      api.offerCallable(ref('class', callable, id));
      return await promise;
    }
  };
})();`;
