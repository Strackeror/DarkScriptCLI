const Default = REST.Default;
const End = REST.End;
const Restart = REST.Restart;

var _event = void 0;
var _codeblock = void 0;
var _skips = void 0;
var _skipIds = 0;
var _labels;

function Event(id, restBehavior, instructions) {
  var evt = new EVENT();
  evt.ID = id;
  evt.RestBehavior = restBehavior;

  _labels = {};
  _skips = {};
  _skipIds = 0;
  _event = evt;
  instructions.apply(this, _GetArgs(instructions));
  if (_skips.length > 0) {
    throw new Error(
      `Reserved skips in Event ${id} have not been filled. Unfilled skips: ${JSON.stringify(
        _skips
      )}`
    );
  }
  _event = void 0;
  _skips = void 0;
  _labels = void 0;

  EVD.Events.Add(evt);
  return evt;
}

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

function _Instruction(bank, index, args) {
  if (_codeblock) {
    _codeblock.instructions.push(Array.from(arguments));
    return;
  }

  if (_event) {
    var layer = void 0;
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
        _event,
        bank,
        index,
        layer,
        hostArray(args)
      );
    } else {
      return Scripter.MakeInstruction(_event, bank, index, hostArray(args));
    }
  }
}

function _ReserveSkip() {
  var id = _skipIds++;
  _skips[id] = _event.Instructions.Count;
  // Arbitrary, but checked later as a loose failsafe
  return id;
}

function _FillSkip(id) {
  var index = id in _skips ? _skips[id] : -1;
  delete _skips[id];
  Scripter.FillSkipPlaceholder(_event, index);
}

function hostArray(args) {
  var argOut = $$$_host.newArr(args.length);
  for (var i = 0; i < args.length; i++) {
    argOut[i] = args[i];
  }
  return argOut;
}

function $LAYERS(...args) {
  var layer = 0;
  for (var i = 0; i < args.length; i++) layer |= 1 << args[i];
  return { layerValue: layer };
}

// Utility function
function floatArg(num) {
  return Scripter.ConvertFloatToIntBytes(num);
}

function bytesArg(...nums) {
  return nums[0] + (nums[1] << 8) + (nums[2] << 16) + (nums[3] << 24);
}

/** @type {EventC[]} */
var eventCs = [];

class EventC {
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
  constructor(cond, skip, end, goto, wait) {
    /** @type {(cond: number, ...args: any[]) => void} */
    this.If = cond;
    /** @type {(lines: number, ...args: any[]) => void} */
    this.Skip = skip;
    /** @type {(end: EventEndType, ...args: any[]) => void} */
    this.End = end;
    /** @type {(label: Label, ...args: any[]) => void} */
    this.Goto = goto;
    /** @type {(...args: any[]) => void} */
    this.Wait = wait;
  }
}

class Condition {
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

function If(...args) {
  /** @type {Condition} */
  let condition;
  /** @type {() => void} */
  let body;
  while (args.length > 2) {
    [condition, body, ...args] = args;
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
