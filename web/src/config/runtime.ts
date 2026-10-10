const defaults: WhaleDeckRuntimeConfig = {
  apiBaseUrl: '/api/v1',
  appTitle: 'WhaleDeck 协同平台',
  backendVersion: '0.1.0',
  authentikUrl: `http://${window.location.hostname}:8081`,
  gitlabUrl: 'http://192.168.22.19:8082',
}

const configured: WhaleDeckRuntimeConfig = {
  ...defaults,
  ...window.__WHALEDECK_RUNTIME_CONFIG__,
}

const approvedWorkstationHosts = new Set(['192.168.22.19', '192.168.100.13'])
function useCurrentWorkstationAddress(value: string) {
  try {
    const url = new URL(value)
    if (
      approvedWorkstationHosts.has(url.hostname) &&
      approvedWorkstationHosts.has(window.location.hostname)
    ) {
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
