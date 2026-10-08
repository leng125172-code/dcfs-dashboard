import { readonly, ref } from 'vue'

interface AuthenticatedUser {
  name: string | null
  claims: Array<{ type: string; value: string }>
}

const previewStorageKey = 'whaledeck-preview-authenticated'
const authenticated = ref(false)
const initialized = ref(false)
const user = ref<AuthenticatedUser | null>(null)

async function initializeAuthSession() {
  if (initialized.value) return

  if (import.meta.env.DEV && sessionStorage.getItem(previewStorageKey) === 'true') {
    authenticated.value = true
    initialized.value = true
    user.value = { name: 'Preview User', claims: [] }
    return
  }

  // The standalone Vite preview has no API process behind its proxy. Keep it
  // as a guest until the login action deliberately creates a preview session;
  // production always verifies the backend session.
  if (import.meta.env.DEV) {
    initialized.value = true
    return
  }

  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), 2500)

  try {
    const response = await fetch('/api/v1/auth/me', {
      credentials: 'include',
      headers: { Accept: 'application/json' },
      signal: controller.signal,
    })

    if (response.ok) {
      user.value = (await response.json()) as AuthenticatedUser
      authenticated.value = true
    }
  } catch {
    authenticated.value = false
    user.value = null
  } finally {
    window.clearTimeout(timeout)
    initialized.value = true
  }
}

function authenticatePreview() {
  if (!import.meta.env.DEV) return false
  sessionStorage.setItem(previewStorageKey, 'true')
  authenticated.value = true
  initialized.value = true
  user.value = { name: 'Preview User', claims: [] }
  return true
}

function redirectToAuthentik(returnUrl = '/') {
  const safeReturnUrl = returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/'
  window.location.assign(`/api/v1/auth/login?returnUrl=${encodeURIComponent(safeReturnUrl)}`)
}

export const authSession = {
  authenticated: readonly(authenticated),
  initialized: readonly(initialized),
  user: readonly(user),
  initialize: initializeAuthSession,
  authenticatePreview,
  redirectToAuthentik,
}
