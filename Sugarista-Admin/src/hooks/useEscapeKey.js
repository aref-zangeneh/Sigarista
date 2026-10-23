import { useEffect } from 'react'

/** Closes the active dialog when the user presses Escape. */
export function useEscapeKey(active, onEscape) {
  useEffect(() => {
    if (!active) {
      return undefined
    }

    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        event.stopPropagation()
        onEscape()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [active, onEscape])
}
