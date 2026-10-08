const defaults: WhaleDeckRuntimeConfig = {
  apiBaseUrl: '/api/v1',
  appTitle: 'WhaleDeck 协同平台',
  authentikUrl: 'http://192.168.22.19:8081',
  gitlabUrl: 'http://192.168.22.19:8082',
}

export const runtimeConfig: WhaleDeckRuntimeConfig = {
  ...defaults,
  ...window.__WHALEDECK_RUNTIME_CONFIG__,
}
