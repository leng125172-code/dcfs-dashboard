/// <reference types="vite/client" />

export {}

declare global {
  interface WhaleDeckRuntimeConfig {
    apiBaseUrl: string
    appTitle: string
    backendVersion: string
    authentikUrl: string
    gitlabUrl: string
  }

  interface Window {
    __WHALEDECK_RUNTIME_CONFIG__?: Partial<WhaleDeckRuntimeConfig>
  }
}

declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean
    administratorOnly?: boolean
    title?: string
    endpoint?: string
  }
}
