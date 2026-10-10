const defaults: WhaleDeckRuntimeConfig = {
  apiBaseUrl: '/api/v1',
  appTitle: 'WhaleDeck 协同平台',
  backendVersion: '0.1.0',
  authentikUrl: `http://${window.location.hostname}:8081`,
  gitlabUrl: `http://${window.location.hostname}:8082`,
}

const configured: WhaleDeckRuntimeConfig = {
  ...defaults,
  ...window.__WHALEDECK_RUNTIME_CONFIG__,
}

function useCurrentWorkstationAddress(value: string) {
  try {
    const url = new URL(value)
    if (url.protocol === 'http:' && /^\d{1,3}(?:\.\d{1,3}){3}$/.test(window.location.hostname)) {
      url.hostname = window.location.hostname
      return url.toString().replace(/\/$/, '')
    }
  } catch {
    // Invalid optional portal URLs remain unchanged and are handled by their consumers.
  }
  return value
}

export const runtimeConfig: WhaleDeckRuntimeConfig = {
  ...configured,
  authentikUrl: useCurrentWorkstationAddress(configured.authentikUrl),
  gitlabUrl: useCurrentWorkstationAddress(configured.gitlabUrl),
}
