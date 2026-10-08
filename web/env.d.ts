/// <reference types="vite/client" />

interface WhaleDeckRuntimeConfig {
  apiBaseUrl: string
  appTitle: string
  authentikUrl: string
  gitlabUrl: string
}

interface Window {
  __WHALEDECK_RUNTIME_CONFIG__?: Partial<WhaleDeckRuntimeConfig>
}
