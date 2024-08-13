const Default = REST.Default;
const End = REST.End;
const Restart = REST.Restart;

class EventContext {
  /** @type {number} */ nextSkipId;
  /** @type {number[]} */ skips;
  /** @type {number[]} */ labels;
  /** @type {EVENT} */ event;

  /** @param {EVENT} event  */
  constructor(event) {
    this.nextSkipId = 0;
    this.skips = [];
    this.labels = [];
    this.event = event;
  }
}
/** @type {EventContext[]} */
let _eventStack = [];
/** @returns {EventContext} */
function _Event() {
  return _eventStack.at(-1);
}

/** @type {(id: number, restBehavior: number, instructions: any) => EVENT} */
function Event(id, restBehavior, instructions) {
  let evt = new EVENT();
  evt.ID = id;
  evt.RestBehavior = restBehavior;
  _eventStack.push(new EventContext(evt));

  instructions.apply(this, _GetArgs(instructions));

  let skips = _Event().skips;
  if (skips.length > 0) {
    let unfilledSkips = JSON.stringify(skips);
    throw new Error(
      `Reserved skips in Event ${id} have not been filled. Unfilled skips: ${unfilledSkips}`
    );
  }

  EVD.Events.Add(evt);
  return evt;
}

/** @type {(func: (...any: any[]) => any) => string[]} */
function _GetArgs(func) {
  var start = func.toString().indexOf("(");
  var end = func.toString().indexOf(")");
  var args = func
    .toString()
    .substring(start, end)
    .replace("(", "")
    .replace(")", "");
  return args.split(/\s*,\s*/).map((arg) => arg);
}

/** @type {(bank: number, index: number, args: any[]) => unknown} */
function _Instruction(bank, index, args) {
  if (_Event()) {
    let layer = undefined;
    if (args.length) {
      var lastArg = args.pop();
      if (lastArg.layerValue) {
        layer = lastArg.layerValue;
      } else {
        args.push(lastArg);
      }
    }

    if (layer) {
      return Scripter.MakeInstruction(
        _Event().event,
        bank,
        index,
        layer,
        hostArray(args)
      );
    } else {
      return Scripter.MakeInstruction(
        _Event().event,
        bank,
        index,
        hostArray(args)
      );
    }
  }
}

function _ReserveSkip() {
  var id = _Event().nextSkipId++;
  _Event().skips[id] = _Event().event.Instructions.Count;
  // Arbitrary, but checked later as a loose failsafe
  return id;
}

/** @type {(id: number) => void} */
function _FillSkip(id) {
  var skips = _Event().skips;
  var index = id in skips ? skips[id] : -1;
  delete _Event().skips[id];
  Scripter.FillSkipPlaceholder(_Event().event, index);
}

/** @type {(args: any[]) => any[]} */
function hostArray(args) {
  var argOut = $$$_host.newArr(args.length);
  for (var i = 0; i < args.length; i++) {
    argOut[i] = args[i];
  }
  return argOut;
}

// function $LAYERS(...args) {
//   var layer = 0;
//   for (var i = 0; i < args.length; i++) layer |= 1 << args[i];
//   return { layerValue: layer };
// }

/** @type {(num: number) => number} */
function floatArg(num) {
  return Scripter.ConvertFloatToIntBytes(num);
}

/** @type {(...nums: number[]) => number} */
function bytesArg(...nums) {
  return nums[0] + (nums[1] << 8) + (nums[2] << 16) + (nums[3] << 24);
}

/** @type {EventC[]} */
var eventCs = [];

class EventC {
  /**
   * @param {number} id
   * @param {number} restBehavior
   * @param {(...args: number[]) => void} body
   */
  constructor(id, restBehavior, body) {
    this.id = id;
    this.behavior = restBehavior;
    this.body = body;
    eventCs.push(this);
  }

  Event() {
    Event(this.id, this.behavior, this.body);
  }

  /**
   * @param {number} slot
   * @param {number[]} args;
   */
  Initialize(slot, ...args) {
    InitializeEvent(slot, this.id, ...args);
  }
}

function LoadAllEvents() {
  for (let eventC of eventCs) {
    eventC.Event();
  }
}

class ConditionType {
  /**
   * @param {IfFunc} cond
   * @param {SkipFunc} skip
   * @param {Endfunc} end
   * @param {GotoFunc} goto
   * @param {Wait} wait
   */
  constructor(cond, skip, end, goto, wait) {
    /** @type {IfFunc} */
    this.If = cond;
    /** @type {SkipFunc} */
    this.Skip = skip;
    /** @type {Endfunc} */
    this.End = end;
    /** @type {GotoFunc} */
    this.Goto = goto;
    /** @type {Wait} */
    this.Wait = wait;
  }
}

class Condition {
  /**
   * @param {ConditionType} type
   * @param  {...any} args
   */
  constructor(type, ...args) {
    /** @type {ConditionType} */
    this.type = type;
    /** @type {any[]} */
    this.args = args;
  }

  name() {
    return this.type.constructor?.name;
  }
}

/** @param {Condition} cond */
function WaitFor(cond) {
  if (cond.type.Wait) cond.type.Wait(...cond.args);
  else if (cond.type.If) cond.type.If(MAIN, ...cond.args);
  else throw new Error(`No Wait function ${cond.name()}`);
}

/** @param {Condition} cond */
function EndIf(cond) {
  if (cond.type.End) cond.type.End(EventEndType.End);
  else throw new Error(`No End function for condition`);
}
[];
/** @param {Condition} cond */
function RestartIf(cond) {
  if (cond.type.End) cond.type.End(EventEndType.Restart);
  else throw new Error(`No End function for condition`);
}

function RestartEvent() {
  RestartIf(Always());
}

/** @typedef {() => void} Body */
/** @type {(...args: [Condition, Body, ...any[]]) => void} */
function If(...args) {
  /** @type {Condition} */
  let condition;
  /** @type {() => void} */
  let body;
  /** @type {number[]} */
  let skipIds = [];
  /** @type {any[]} */
  let rest = args;

  while (args.length > 2) {
    [condition, body, ...rest] = rest;
    let skipFunc = condition.type.Skip;
    if (skipFunc === null)
      throw new Error(`No if function for condition ${condition}`);

    if (condition) {
      let id = _ReserveSkip();
      skipFunc(0, ...condition.args);
      body();
      _FillSkip(id);
    } else {
      body();
    }
  }
}
