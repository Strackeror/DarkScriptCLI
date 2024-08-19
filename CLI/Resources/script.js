const Default = REST.Default;
const End = REST.End;
const Restart = REST.Restart;

class EventContext {
  /** @type {number} */ nextSkipId = 0;
  /** @type {{[id: number | string]: number[]}} */ skips = {};
  /** @type {number[]} */ usedLabels = [];
  /** @type {{[target: number]: {sources: number[]} | undefined}} */
  virtualLabels = {};
  /** @type {EVENT} */ event;
  /** @type {number} */ orIndex = 1;
  /** @type {number} */ andIndex = 1;
  /** @type {number | null} */ replaceInstruction = null;

  /** @param {EVENT} event  */
  constructor(event) {
    this.event = event;
  }

  get instructionIndex() {
    return this.event.Instructions.Count;
  }
}
/** @type {EventContext[]} */
let _eventStack = [];
function _Event() {
  let _event = _eventStack.at(-1);
  if (!_event) throw new Error("Not in event");
  return _event;
}

/** @type {(id: number, restBehavior: number, instructions: () => void) => EVENT} */
function CreateEvent(id, restBehavior, instructions) {
  let evt = new EVENT();
  evt.ID = id;
  evt.RestBehavior = restBehavior;
  _eventStack.push(new EventContext(evt));

  instructions();

  let skips = _Event().skips;
  if (Object.keys(skips).length > 0) {
    let unfilledSkips = JSON.stringify(skips);
    throw new Error(
      `Reserved skips in Event ${id} have not been filled. Unfilled skips: ${unfilledSkips}`
    );
  }

  for (let targetIndex in _Event().virtualLabels) {
    let label = 0;
    for (label = 0; _Event().usedLabels.includes(label); ++label);
    if (+targetIndex < _Event().instructionIndex)
      _Event().replaceInstruction = +targetIndex;
    L(label);

    for (let source of _Event().virtualLabels[targetIndex]?.sources ?? []) {
      Scripter.FillGotoPlaceholder(_Event().event, source, label);
      break;
    }
  }
  _eventStack.pop();

  EVD.Events.Add(evt);
  return evt;
}

/** @type {(id: number, restBehavior: number, instructions: (...args: any[]) => void) => EVENT} */
function Event(id, restBehavior, body) {
  var bodyWithArgs = () => body(..._GetArgs(body));
  return CreateEvent(id, restBehavior, bodyWithArgs);
}

/** @type {(id: number, restBehavior: number, instructions: () => void) => EVENT} */
function JsEvent(id, restBehavior, body) {
  return CreateEvent(id, restBehavior, body);
}

/** @type {(func: (...any: any[]) => any) => string[]} */
function _GetArgs(func) {
  var start = func.toString().indexOf("(");
  var end = func.toString().indexOf(")");
  var args = func.toString().substring(start + 1, end);
  return args.split(/\s*,\s*/).map((arg) => arg);
}

function _PlaceHolderInstruction() {
  _Event().event.Instructions.Add(new INSTRUCTION());
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

    let replace = _Event().replaceInstruction;
    _Event().replaceInstruction = null;
    if (replace === null && _Event().virtualLabels[_Event().instructionIndex]) {
      _PlaceHolderInstruction();
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
        replace,
        hostArray(args)
      );
    }
  }
}

/** @type {() => number} */
function _ReserveNewSkip() {
  let id = _Event().nextSkipId++;
  _ReserveSkip(id);
  return id;
}
/** @type {(id: string | number) => void} */
function _ReserveSkip(id) {
  id = id ?? _Event().nextSkipId++;
  if (!_Event().skips[id]) _Event().skips[id] = [];
  _Event().skips[id].push(_Event().instructionIndex - 1);
}

/** @type {(id: string | number) => void} */
function _FillSkip(id) {
  var skips = _Event().skips;
  for (let index of skips[id] ?? []) {
    Scripter.FillSkipPlaceholder(_Event().event, index);
  }

  delete _Event().skips[id];
}

/** @type {(args: any[]) => any[]} */
function hostArray(args) {
  var argOut = $$$_host.newArr(args.length);
  for (var i = 0; i < args.length; i++) {
    argOut[i] = args[i];
  }
  return argOut;
}

/** @type {(...layers: number[]) => {layerValue: number}} */
function $LAYERS(...args) {
  var layer = 0;
  for (var i = 0; i < args.length; i++) layer |= 1 << args[i];
  return { layerValue: layer };
}

/** @type {(num: number) => number} */
function floatArg(num) {
  return Scripter.ConvertFloatToIntBytes(num);
}

/** @type {(...nums: number[]) => number} */
function bytesArg(...nums) {
  return nums[0] + (nums[1] << 8) + (nums[2] << 16) + (nums[3] << 24);
}

/** @type {(start: number, count: number) => string} */
function X(start, count) {
  return `X${start}_${count}`;
}

/** @param {number} id */
function LabelCall(id) {
  [
    Label0,
    Label1,
    Label2,
    Label3,
    Label4,
    Label5,
    Label6,
    Label7,
    Label8,
    Label9,
    Label10,
    Label11,
    Label12,
    Label13,
    Label14,
    Label15,
    Label16,
    Label17,
    Label18,
    Label19,
    Label20,
  ][id]();
}

/** @param {number} id */
function L(id) {
  let index = _Event().event.Instructions.Count;
  let virtualLabels = _Event().virtualLabels[index];
  if (virtualLabels) {
    for (let source of virtualLabels.sources) {
      Scripter.FillGotoPlaceholder(_Event().event, source, id);
    }
    delete _Event().virtualLabels[index];
  }
  _Event().usedLabels.push(id);
  LabelCall(id);
}
