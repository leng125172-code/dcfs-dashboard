/// <reference types="vite/client" />

interface DcfsRuntimeConfig {
  apiBaseUrl: string
  appTitle: string
  authentikUrl: string
  gitlabUrl: string
}

interface Window {
  __DCFS_RUNTIME_CONFIG__?: Partial<DcfsRuntimeConfig>
}
