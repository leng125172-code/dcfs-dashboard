const defaults: DcfsRuntimeConfig = {
  apiBaseUrl: '/api/v1',
  appTitle: 'DCFS 协同平台',
  authentikUrl: 'http://192.168.22.19:8081',
  gitlabUrl: 'http://192.168.22.19:8082',
}

export const runtimeConfig: DcfsRuntimeConfig = {
  ...defaults,
  ...window.__DCFS_RUNTIME_CONFIG__,
}
