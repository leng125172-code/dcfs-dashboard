export interface CurrentUser {
  subject: string
  name: string | null
  groups: string[]
  isAdministrator: boolean
  permissionExpiresAtUtc: string
}

export interface PortalItem {
  id: string
  scope: 'Personal' | 'Public'
  name: string
  description: string | null
  url: string
  iconKind: string
  iconValue: string
  color: string
  sortOrder: number
  isEnabled: boolean
  version: number
}

export interface AgentHealth {
  available: boolean
  dockerAvailable: boolean
  systemdAvailable: boolean
  status: string
  observedAtUtc: string
}

export interface HostInfo {
  hostName: string
  distribution: string
  kernelVersion: string
  architecture: string
  bootTimeUtc: string
  uptime: string
  logicalProcessorCount: number
  totalMemoryBytes: number
}

export interface ManagedResource {
  id: string
  name: string
  type: string
  state: string
  version: string
  isProtected: boolean
  attributes: Record<string, string>
}

export interface MetricValue {
  kind: string
  deviceId: string
  value: number
  unit: string
  quality: string
  sampledAtUtc: string
}

export interface CatalogApplication {
  id: string
  name: string
  description: string
  image: string
  version: string
  iconUrl: string | null
  installed: boolean
  state: string | null
}

export interface OverviewResponse {
  scope: 'Public' | 'Administrator'
  platformStatus: string
  sampledAtUtc: string
  portals: PortalItem[]
  agent: AgentHealth | null
  host: HostInfo | null
  resources: ManagedResource[] | null
  metrics: MetricValue[] | null
  applications: CatalogApplication[] | null
  catalogIsStale: boolean
}

export interface Job {
  id: string
  jobType: string
  state: string
  phase: string
  progressPercent: number | null
  errorCode: string | null
  createdAtUtc: string
  completedAtUtc: string | null
}
