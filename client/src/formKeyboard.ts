import type { KeyboardEvent } from 'react'

/** Submit only the current form, preserving browser validation and multiline input. */
export function submitOnEnter(event: KeyboardEvent<HTMLFormElement>) {
  if (event.key !== 'Enter' || event.defaultPrevented || event.nativeEvent.isComposing
    || event.nativeEvent.keyCode === 229 || event.shiftKey || event.ctrlKey || event.altKey || event.metaKey) return
  const input = event.target as HTMLInputElement
  if (input.tagName !== 'INPUT' || input.isContentEditable
    || !['text', 'password', 'email', 'number', 'search', 'tel', 'url'].includes(input.type)) return

  event.preventDefault()
  if (event.repeat) return
  const localAction = input.closest<HTMLElement>('[data-enter-action]')
  if (localAction) {
    const button = localAction.querySelector<HTMLButtonElement>('[data-enter-action-button]')
    if (button && !button.disabled) button.click()
    return
  }
  const form = event.currentTarget
  const button = form.querySelector<HTMLButtonElement | HTMLInputElement>('button[type="submit"], input[type="submit"]')
  if (button && !button.disabled) form.requestSubmit(button)
}
