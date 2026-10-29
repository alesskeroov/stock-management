import assert from 'node:assert/strict'
import test from 'node:test'
import { submitOnEnter } from '../src/formKeyboard.ts'
import { maximumImportBytes, validateImportFiles } from '../src/importFileValidation.ts'

function keyboardEvent(overrides = {}) {
  const calls = { prevented: 0, submitted: 0, clicked: 0 }
  const button = { disabled: false, click: () => calls.clicked++ }
  const input = { tagName: 'INPUT', type: 'text', isContentEditable: false, closest: () => null }
  const form = {
    querySelector: () => button,
    requestSubmit: (submitter) => { assert.equal(submitter, button); calls.submitted++ },
  }
  const event = {
    key: 'Enter', defaultPrevented: false, nativeEvent: {}, repeat: false,
    shiftKey: false, ctrlKey: false, altKey: false, metaKey: false,
    target: input, currentTarget: form, preventDefault: () => calls.prevented++, ...overrides,
  }
  return { event, calls, button, input, form }
}

for (const type of ['text', 'password', 'email', 'number', 'search', 'tel', 'url']) {
  test(`Enter in ${type} delegates to the current form's validating requestSubmit`, () => {
    const { event, input, calls } = keyboardEvent()
    input.type = type
    submitOnEnter(event)
    assert.equal(calls.submitted, 1)
    assert.equal(calls.prevented, 1)
  })
}

for (const tagName of ['TEXTAREA', 'SELECT', 'BUTTON']) {
  test(`Enter preserves native behavior in ${tagName}`, () => {
    const { event, input, calls } = keyboardEvent()
    input.tagName = tagName
    submitOnEnter(event)
    assert.equal(calls.submitted, 0)
    assert.equal(calls.prevented, 0)
  })
}

for (const type of ['file', 'checkbox', 'radio', 'date']) {
  test(`Enter does not hijack ${type} controls`, () => {
    const { event, input, calls } = keyboardEvent()
    input.type = type
    submitOnEnter(event)
    assert.equal(calls.prevented, 0)
    assert.equal(calls.submitted, 0)
  })
}

test('Holding Enter does not repeatedly submit', () => {
  const { event, calls } = keyboardEvent({ repeat: true })
  submitOnEnter(event)
  assert.equal(calls.submitted, 0)
  assert.equal(calls.prevented, 1)
})

test('IME composition, handled events and modifier keys do not submit', () => {
  for (const overrides of [{ nativeEvent: { isComposing: true } }, { nativeEvent: { keyCode: 229 } },
    { defaultPrevented: true }, { shiftKey: true }, { ctrlKey: true }, { altKey: true }, { metaKey: true }]) {
    const { event, calls } = keyboardEvent(overrides)
    submitOnEnter(event)
    assert.equal(calls.submitted, 0)
    assert.equal(calls.prevented, 0)
  }
})

test('A disabled submit button blocks keyboard submission', () => {
  const { event, button, calls } = keyboardEvent()
  button.disabled = true
  submitOnEnter(event)
  assert.equal(calls.submitted, 0)
  assert.equal(calls.prevented, 1)
})

test('A form without a submit action does not submit implicitly', () => {
  const { event, form, calls } = keyboardEvent()
  form.querySelector = () => null
  submitOnEnter(event)
  assert.equal(calls.submitted, 0)
  assert.equal(calls.prevented, 1)
})

test('Enter in the PO line-adder adds one line instead of saving the order', () => {
  const { event, button, input, calls } = keyboardEvent()
  input.closest = () => ({ querySelector: () => button })
  submitOnEnter(event)
  assert.equal(calls.clicked, 1)
  assert.equal(calls.submitted, 0)
})

test('A disabled line-adder never falls back to submitting its parent form', () => {
  const { event, button, input, calls } = keyboardEvent()
  input.closest = () => ({ querySelector: () => button })
  button.disabled = true
  submitOnEnter(event)
  assert.equal(calls.clicked, 0)
  assert.equal(calls.submitted, 0)
})

test('Dropped and picked XLSX files share the same size and extension rules', () => {
  assert.equal(validateImportFiles([{ name: 'Azərbaycan.XLSX', size: 100 }]), '')
  assert.equal(validateImportFiles([{ name: 'mal.xlsx', size: maximumImportBytes }]), '')
  assert.match(validateImportFiles([{ name: 'mal.xlsx', size: maximumImportBytes + 1 }]), /5 MB/)
  assert.match(validateImportFiles([{ name: 'mal.xlsx', size: 0 }]), /boşdur/)
  assert.match(validateImportFiles([{ name: 'mal.csv', size: 10 }]), /\.xlsx/)
})

test('Dropping multiple files or a non-file item is rejected', () => {
  assert.match(validateImportFiles([]), /Qovluq və ya link/)
  assert.match(validateImportFiles([{ name: 'a.xlsx', size: 10 }, { name: 'b.xlsx', size: 10 }]), /yalnız bir/)
})
